// <copyright file="MaintenanceToolsOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Web.AdminPanel.Auth;
using PersistenceIdentifiable = HNMO.Persistence.IIdentifiable;

/// <summary>
/// Audited server maintenance operations (ROADMAP 14.8): inactive/empty account finder and cleanup,
/// duplicate item GUID scanner/resolver, and character experience fixer, all with dry-run previews.
/// </summary>
/// <param name="persistence">Persistence context provider.</param>
/// <param name="configurations">Game configuration source.</param>
/// <param name="executor">Authorization and audit executor.</param>
/// <param name="timeProvider">Clock provider.</param>
public sealed class MaintenanceToolsOperations(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    AdminOperationExecutor executor,
    TimeProvider timeProvider)
{
    /// <summary>Dry-run scan for inactive (and optionally empty) player accounts.</summary>
    /// <param name="minInactiveDays">Minimum days since registration.</param>
    /// <param name="emptyOnly">When true, match only accounts with zero characters and zero vault items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching candidate accounts.</returns>
    [NotExposedToAgents("Admin panel dry-run maintenance preview only.")]
    public async ValueTask<IReadOnlyList<MaintenanceAccountCandidate>> PreviewInactiveAccountsAsync(
        int minInactiveDays,
        bool emptyOnly,
        CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(config);
        var accounts = await LoadAllAccountsAsync(context, cancellationToken).ConfigureAwait(false);
        return this.FilterInactiveAccounts(accounts, minInactiveDays, emptyOnly)
            .Select(a => new MaintenanceAccountCandidate(
                (a as PersistenceIdentifiable)?.Id ?? Guid.Empty,
                a.LoginName,
                a.RegistrationDate,
                a.Characters.Count,
                a.Vault?.Items.Count ?? 0,
                a.WCoinC + a.WCoinP + a.GoblinPoints))
            .OrderBy(c => c.RegistrationDateUtc)
            .ThenBy(c => c.LoginName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Deletes matching inactive accounts after dry-run preview and records an audit entry.</summary>
    /// <param name="minInactiveDays">Minimum days since registration.</param>
    /// <param name="emptyOnly">When true, delete only empty accounts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of deleted accounts.</returns>
    [NotExposedToAgents("Destructive maintenance cleanup requires interactive dry-run confirmation in admin panel.")]
    public ValueTask<int> PurgeInactiveAccountsAsync(
        int minInactiveDays,
        bool emptyOnly,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            AdminRoles.Administrator,
            "maintenance_purge_inactive_accounts",
            new { minInactiveDays, emptyOnly },
            async _ =>
            {
                var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                using var context = persistence.CreateNewPlayerContext(config);
                var accounts = await LoadAllAccountsAsync(context, cancellationToken).ConfigureAwait(false);
                var targets = this.FilterInactiveAccounts(accounts, minInactiveDays, emptyOnly).ToList();
                foreach (var account in targets)
                {
                    await context.DeleteAsync(account).ConfigureAwait(false);
                }

                if (targets.Count > 0)
                {
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return targets.Count;
            },
            cancellationToken);

    /// <summary>Dry-run scan for item GUIDs/IDs that appear in more than one inventory or vault.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Flattened duplicate occurrences grouped by item GUID.</returns>
    [NotExposedToAgents("Admin panel dry-run maintenance preview only.")]
    public async ValueTask<IReadOnlyList<MaintenanceDuplicateItemOccurrence>> PreviewDuplicateItemIdsAsync(
        CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(config);
        var accounts = await LoadAllAccountsAsync(context, cancellationToken).ConfigureAwait(false);
        var entries = CollectAllItemLocations(accounts);

        return entries
            .GroupBy(e => e.ItemId)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .Select(e => new MaintenanceDuplicateItemOccurrence(
                e.ItemId,
                e.AccountLogin,
                e.LocationLabel,
                AccountSafetyPlugIn.Describe(e.Item),
                e.Item.ItemSlot))
            .OrderBy(d => d.ItemId)
            .ThenBy(d => d.AccountLogin, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Removes extra duplicate item copies (keeping the first occurrence per GUID) and records an audit entry.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of duplicate item copies removed.</returns>
    [NotExposedToAgents("Destructive duplicate item removal requires interactive dry-run confirmation in admin panel.")]
    public ValueTask<int> RemoveDuplicateItemsAsync(CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            AdminRoles.Administrator,
            "maintenance_remove_duplicate_items",
            new { action = "remove_extra_copies" },
            async _ =>
            {
                var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                using var context = persistence.CreateNewPlayerContext(config);
                var accounts = await LoadAllAccountsAsync(context, cancellationToken).ConfigureAwait(false);
                var entries = CollectAllItemLocations(accounts);

                var removed = 0;
                foreach (var group in entries.GroupBy(e => e.ItemId).Where(g => g.Count() > 1))
                {
                    foreach (var extra in group.Skip(1))
                    {
                        extra.Storage.Items.Remove(extra.Item);
                        await context.DeleteAsync(extra.Item).ConfigureAwait(false);
                        removed++;
                    }
                }

                if (removed > 0)
                {
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return removed;
            },
            cancellationToken);

    /// <summary>Dry-run scan for characters whose stored experience is out of bounds for their current level.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detected experience issues and their repaired values.</returns>
    [NotExposedToAgents("Admin panel dry-run maintenance preview only.")]
    public async ValueTask<IReadOnlyList<MaintenanceExperienceIssue>> PreviewExperienceIssuesAsync(
        CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(config);
        var accounts = await LoadAllAccountsAsync(context, cancellationToken).ConfigureAwait(false);
        var expTable = GameContext.BuildExperienceTable(config.ExperienceFormula, config.MaximumLevel);
        return InspectExperienceIssues(accounts, expTable, config.MaximumLevel)
            .Select(x => x.Issue)
            .OrderBy(x => x.AccountLogin, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.CharacterName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Applies the clamped experience repairs to affected characters and records an audit entry.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of characters fixed.</returns>
    [NotExposedToAgents("Character experience repair requires interactive dry-run confirmation in admin panel.")]
    public ValueTask<int> FixExperienceIssuesAsync(CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            AdminRoles.Administrator,
            "maintenance_fix_experience",
            new { action = "clamp_character_experience" },
            async _ =>
            {
                var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                using var context = persistence.CreateNewPlayerContext(config);
                var accounts = await LoadAllAccountsAsync(context, cancellationToken).ConfigureAwait(false);
                var expTable = GameContext.BuildExperienceTable(config.ExperienceFormula, config.MaximumLevel);
                var matches = InspectExperienceIssues(accounts, expTable, config.MaximumLevel).ToList();
                foreach (var item in matches)
                {
                    item.Character.Experience = item.Issue.FixedExperience;
                }

                if (matches.Count > 0)
                {
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return matches.Count;
            },
            cancellationToken);

    private static async ValueTask<List<Account>> LoadAllAccountsAsync(IPlayerContext context, CancellationToken cancellationToken)
    {
        var result = new List<Account>();
        var skip = 0;
        const int batchSize = 500;
        while (true)
        {
            var page = (await context.GetAccountsOrderedByLoginNameAsync(skip, batchSize, cancellationToken).ConfigureAwait(false)).ToList();
            if (page.Count == 0)
            {
                break;
            }

            result.AddRange(page);
            if (page.Count < batchSize)
            {
                break;
            }

            skip += page.Count;
        }

        return result;
    }

    private static List<(Guid ItemId, string AccountLogin, string LocationLabel, ItemStorage Storage, Item Item)> CollectAllItemLocations(
        IReadOnlyList<Account> accounts)
    {
        var list = new List<(Guid, string, string, ItemStorage, Item)>();
        foreach (var acc in accounts)
        {
            if (acc.IsBot || acc.IsTemplate)
            {
                continue;
            }

            if (acc.Vault is { } vault)
            {
                foreach (var item in vault.Items)
                {
                    if (item is PersistenceIdentifiable id && id.Id != Guid.Empty)
                    {
                        list.Add((id.Id, acc.LoginName, "Vault", vault, item));
                    }
                }
            }

            foreach (var ch in acc.Characters)
            {
                if (ch.Inventory is { } inv)
                {
                    foreach (var item in inv.Items)
                    {
                        if (item is PersistenceIdentifiable id && id.Id != Guid.Empty)
                        {
                            list.Add((id.Id, acc.LoginName, $"Character:{ch.Name}", inv, item));
                        }
                    }
                }
            }
        }

        return list;
    }

    private static IEnumerable<(Character Character, MaintenanceExperienceIssue Issue)> InspectExperienceIssues(
        IReadOnlyList<Account> accounts,
        long[] expTable,
        short maximumLevel)
    {
        foreach (var acc in accounts)
        {
            if (acc.IsBot || acc.IsTemplate)
            {
                continue;
            }

            foreach (var ch in acc.Characters)
            {
                var rawLevel = (int)(ch.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.Level.Id)?.Value ?? 1f);
                var level = Math.Clamp(rawLevel, 1, Math.Max(1, (int)maximumLevel));
                var minExp = level < expTable.Length ? expTable[level] : 0;
                var maxExp = level < maximumLevel && level + 1 < expTable.Length
                    ? Math.Max(minExp, expTable[level + 1] - 1)
                    : minExp;

                if (ch.Experience < minExp)
                {
                    yield return (
                        ch,
                        new MaintenanceExperienceIssue(
                            (ch as PersistenceIdentifiable)?.Id ?? Guid.Empty,
                            acc.LoginName,
                            ch.Name,
                            level,
                            ch.Experience,
                            minExp,
                            "Below level floor"));
                }
                else if (ch.Experience > maxExp)
                {
                    yield return (
                        ch,
                        new MaintenanceExperienceIssue(
                            (ch as PersistenceIdentifiable)?.Id ?? Guid.Empty,
                            acc.LoginName,
                            ch.Name,
                            level,
                            ch.Experience,
                            maxExp,
                            "Exceeds level cap"));
                }
            }
        }
    }

    private IEnumerable<Account> FilterInactiveAccounts(IReadOnlyList<Account> accounts, int minInactiveDays, bool emptyOnly)
    {
        var days = Math.Clamp(minInactiveDays, 1, 36500);
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-days);
        return accounts.Where(a =>
            !a.IsBot &&
            !a.IsTemplate &&
            a.State != AccountState.GameMaster &&
            a.State != AccountState.GameMasterInvisible &&
            a.RegistrationDate <= cutoff &&
            (!emptyOnly || (a.Characters.Count == 0 && (a.Vault?.Items.Count ?? 0) == 0)));
    }
}

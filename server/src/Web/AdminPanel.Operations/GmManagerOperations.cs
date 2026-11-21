// <copyright file="GmManagerOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.GmManager;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;

/// <summary>Manages per-GM permissions and in-game GM command audit entries (ROADMAP 14.17).</summary>
/// <param name="persistence">The persistence context provider.</param>
/// <param name="configurations">The game configuration source.</param>
/// <param name="login">The login coordinator.</param>
/// <param name="executor">The admin authorization and audit executor.</param>
/// <param name="settings">The admin write settings.</param>
public sealed class GmManagerOperations(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    ILoginServer login,
    AdminOperationExecutor executor,
    AdminWriteSettings settings)
{
    /// <summary>Gets or sets the store override used in isolated unit tests.</summary>
    internal GmPermissionStore? StoreOverride { get; set; }

    /// <summary>Gets or sets an optional isolated activity log file name used in tests.</summary>
    internal string? ActivityLogFileName { get; set; }

    private GmPermissionStore Store => this.StoreOverride ?? GmPermissionStore.Default;

    /// <summary>Loads current GM assignments, strict listing setting, and recent GM command audit entries.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Assignments, strict flag, and audit entries.</returns>
    [NotExposedToAgents("Admin panel GM Manager UI only.")]
    public async ValueTask<(IReadOnlyList<GmAssignment> Assignments, bool RequireListed, IReadOnlyList<GmCommandAuditEntry> Audits)> LoadAsync(CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        return (this.Store.ListAssignments(), this.Store.RequireListedForGmCommands, this.Store.ListAuditEntries(100));
    }

    /// <summary>Sets whether unlisted GameMaster characters are denied by default.</summary>
    /// <param name="requireListed">True to require an explicit GM Manager assignment for all GM commands.</param>
    /// <param name="reason">The required reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The async task.</returns>
    [NotExposedToAgents("Admin panel GM Manager UI only.")]
    public async ValueTask SetRequireListedAsync(bool requireListed, string reason, CancellationToken cancellationToken)
    {
        await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var cleanReason = ValidateReason(reason);

        await executor.ExecuteAsync(
            AdminRoles.Administrator,
            "gm_manager_mode",
            new { requireListed, reason = cleanReason },
            actor =>
            {
                this.Store.RequireListedForGmCommands = requireListed;
                this.RecordActivity(string.Empty, string.Empty, actor.Name, $"SetRequireListed={requireListed} | Reason: {cleanReason}");
                return ValueTask.FromResult(true);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Assigns or updates per-GM command permissions for a character (optionally promoting offline CharacterStatus to GameMaster).</summary>
    /// <param name="characterName">The exact character name.</param>
    /// <param name="permissions">The granted GM permissions.</param>
    /// <param name="promoteCharacterIfOffline">Whether to set <see cref="CharacterStatus.GameMaster"/> in persistence when offline.</param>
    /// <param name="reason">The required operator reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated assignment.</returns>
    [NotExposedToAgents("Admin panel GM Manager UI only.")]
    public async ValueTask<GmAssignment> AssignGmAsync(
        string characterName,
        GmPermission permissions,
        bool promoteCharacterIfOffline,
        string reason,
        CancellationToken cancellationToken)
    {
        await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var trimmedName = ValidateCharacterName(characterName);
        var cleanReason = ValidateReason(reason);

        var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var ctx = persistence.CreateNewPlayerContext(config);
        var account = await ctx.GetAccountByCharacterNameAsync(trimmedName, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminOperationException("Character not found.", AdminOperationErrorKind.InvalidInput);
        var character = account.Characters.Single(c => string.Equals(c.Name, trimmedName, StringComparison.OrdinalIgnoreCase));

        return await executor.ExecuteAsync(
            AdminRoles.Administrator,
            "gm_manager_assign",
            new { characterName = character.Name, account = account.LoginName, permissions = permissions.ToString(), promoteCharacterIfOffline, reason = cleanReason },
            async actor =>
            {
                if (promoteCharacterIfOffline && character.CharacterStatus != CharacterStatus.GameMaster)
                {
                    await ExecuteWithOfflineReservationAsync(
                        login,
                        account.LoginName,
                        "Character must be offline to change CharacterStatus to GameMaster.",
                        cancellationToken,
                        async () =>
                        {
                            character.CharacterStatus = CharacterStatus.GameMaster;
                            await AdminChangeExecution.SaveAsync(ctx, cancellationToken).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                }

                var assignment = new GmAssignment
                {
                    CharacterName = character.Name,
                    AccountLogin = account.LoginName,
                    Permissions = permissions,
                    UpdatedBy = actor.Name,
                    UpdatedAtUtc = DateTime.UtcNow,
                    Notes = cleanReason,
                };
                this.Store.UpsertAssignment(assignment);
                this.RecordActivity(account.LoginName, character.Name, actor.Name, $"AssignGM [{permissions}] | Reason: {cleanReason}");
                return assignment;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Revokes a character's GM permissions with a required reason.</summary>
    /// <param name="characterName">The exact character name.</param>
    /// <param name="demoteCharacterIfOffline">Whether to reset <see cref="CharacterStatus.Normal"/> when offline.</param>
    /// <param name="reason">The required operator reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if an assignment was removed.</returns>
    [NotExposedToAgents("Admin panel GM Manager UI only.")]
    public async ValueTask<bool> RevokeGmAsync(string characterName, bool demoteCharacterIfOffline, string reason, CancellationToken cancellationToken)
    {
        await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var trimmedName = ValidateCharacterName(characterName);
        var cleanReason = ValidateReason(reason);

        return await executor.ExecuteAsync(
            AdminRoles.Administrator,
            "gm_manager_revoke",
            new { characterName = trimmedName, demoteCharacterIfOffline, reason = cleanReason },
            async actor =>
            {
                var accountLogin = string.Empty;
                if (demoteCharacterIfOffline)
                {
                    var config = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    using var ctx = persistence.CreateNewPlayerContext(config);
                    var account = await ctx.GetAccountByCharacterNameAsync(trimmedName, cancellationToken).ConfigureAwait(false);
                    if (account is not null)
                    {
                        accountLogin = account.LoginName;
                        var ch = account.Characters.FirstOrDefault(c => string.Equals(c.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
                        if (ch is not null && ch.CharacterStatus == CharacterStatus.GameMaster)
                        {
                            await ExecuteWithOfflineReservationAsync(
                                login,
                                account.LoginName,
                                "Character must be offline to demote CharacterStatus.",
                                cancellationToken,
                                async () =>
                                {
                                    ch.CharacterStatus = CharacterStatus.Normal;
                                    await AdminChangeExecution.SaveAsync(ctx, cancellationToken).ConfigureAwait(false);
                                }).ConfigureAwait(false);
                        }
                    }
                }

                var removed = this.Store.RemoveAssignment(trimmedName);
                this.RecordActivity(accountLogin, trimmedName, actor.Name, $"RevokeGM removed={removed} | Reason: {cleanReason}");
                return removed;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ExecuteWithOfflineReservationAsync(
        ILoginServer login,
        string loginName,
        string onlineErrorMessage,
        CancellationToken cancellationToken,
        Func<Task> action)
    {
        var snapshot = await login.GetSnapshotAsync().ConfigureAwait(false);
        if (snapshot.Keys.Any(k => string.Equals(k, loginName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AdminOperationException(onlineErrorMessage, AdminOperationErrorKind.InvalidInput);
        }

        var reservationId = System.Security.Cryptography.RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        var reserved = await login.TryReserveAccountAsync(loginName, reservationId).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!reserved)
        {
            throw new AdminOperationException(onlineErrorMessage, AdminOperationErrorKind.Conflict);
        }

        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            await login.ReleaseAccountReservationAsync(loginName, reservationId).ConfigureAwait(false);
        }
    }

    private static string ValidateCharacterName(string characterName)
    {
        var trimmed = characterName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 10)
        {
            throw new AdminOperationException("A valid character name (1-10 characters) is required.", AdminOperationErrorKind.InvalidInput);
        }

        return trimmed;
    }

    private static string ValidateReason(string reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length < 3)
        {
            throw new AdminOperationException("An operator reason (at least 3 characters) is required.", AdminOperationErrorKind.InvalidInput);
        }

        return trimmed;
    }

    private void RecordActivity(string account, string character, string actorName, string details)
    {
        ActivityLogPlugIn.RecordExternal(
            new ActivityLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Kind = ActivityKind.Administration,
                Account = account,
                Character = character,
                PartnerCharacter = actorName,
                ItemSummary = "GmManager",
                Details = details,
            },
            this.ActivityLogFileName);
    }
}

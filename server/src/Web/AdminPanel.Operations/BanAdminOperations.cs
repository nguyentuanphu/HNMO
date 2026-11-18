// <copyright file="BanAdminOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Moderation;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;

/// <summary>Audited administrator/operator moderation operations (timed/permanent bans, mutes, salted network/device bans).</summary>
/// <param name="persistence">Account persistence provider.</param>
/// <param name="configurations">Canonical game configuration source.</param>
/// <param name="login">Authoritative login server snapshot and lease provider.</param>
/// <param name="executor">Mandatory RBAC and durable admin audit executor.</param>
/// <param name="clock">Authoritative clock.</param>
/// <param name="rows">Optional isolated moderation row store (defaults to shared host row store).</param>
/// <param name="activity">Optional isolated Activity Log callback.</param>
public sealed class BanAdminOperations(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    ILoginServer login,
    AdminOperationExecutor executor,
    TimeProvider clock,
    IHnmoPlayerStateRowBackend? rows = null,
    Action<ActivityLogEntry>? activity = null)
{
    private readonly IHnmoPlayerStateRowBackend? _injectedRows = rows;
    private IHnmoPlayerStateRowBackend? _rows = rows;

    private IHnmoPlayerStateRowBackend Rows => this._rows ??= HnmoPlayerStateRowStoreRegistry.GetBackend(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName));

    private BanManagerService Service => new(this.Rows);

    /// <summary>Searches moderation records with optional text, type, and active filter.</summary>
    /// <param name="query">Search text.</param>
    /// <param name="kindFilter">Optional target kind filter.</param>
    /// <param name="activeOnly">Whether to return only active unexpired entries.</param>
    /// <param name="offset">Zero-based page offset.</param>
    /// <param name="limit">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching records and total count.</returns>
    [AdminOperation(AdminRole.Viewer)]
    public ValueTask<(IReadOnlyList<BanRecord> Items, int Total)> SearchAsync(
        string? query,
        BanTargetKind? kindFilter,
        bool activeOnly,
        int offset,
        int limit,
        CancellationToken cancellationToken)
        => executor.ExecuteAsync(
            AdminRoles.Viewer,
            "moderation_search",
            new { query, kind = kindFilter?.ToString(), activeOnly, offset, limit },
            _ => ValueTask.FromResult(this.Service.Search(query, kindFilter, activeOnly, clock.GetUtcNow().UtcDateTime, offset, limit)),
            cancellationToken);

    /// <summary>Issues a timed or permanent account/character ban, chat mute, or salted network/device ban.</summary>
    /// <param name="kind">Enforcement scope.</param>
    /// <param name="target">Account login, character name, IP address, or device token.</param>
    /// <param name="reasonEn">Player-visible English reason.</param>
    /// <param name="reasonVi">Player-visible Vietnamese reason.</param>
    /// <param name="expiresAtUtc">UTC expiry or <c>null</c> for permanent.</param>
    /// <param name="auditReason">Mandatory private internal reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created moderation record.</returns>
    [AdminOperation(AdminRole.Operator, ReadOnly = false)]
    public ValueTask<BanRecord> IssueAsync(
        BanTargetKind kind,
        string target,
        string reasonEn,
        string reasonVi,
        DateTime? expiresAtUtc,
        string auditReason,
        CancellationToken cancellationToken)
        => executor.ExecuteAsync(
            AdminRoles.Operator,
            "moderation_issue",
            new { kind = kind.ToString(), target = kind is BanTargetKind.NetworkBan or BanTargetKind.DeviceBan ? "<hashed>" : target, expiresAtUtc, reason = auditReason },
            async actor =>
            {
                AccountWriteOperations.ValidateReason(auditReason);
                var now = clock.GetUtcNow().UtcDateTime;
                if (expiresAtUtc.HasValue && expiresAtUtc.Value <= now)
                {
                    throw new AdminOperationException("Expiration time must be in the future (Sydney time).", AdminOperationErrorKind.InvalidInput);
                }

                // For account/character targets, verify entity existence when offline-safe persistence is queried.
                string canonicalTarget = target?.Trim() ?? string.Empty;
                if (kind is BanTargetKind.AccountBan or BanTargetKind.AccountMute or BanTargetKind.CharacterBan or BanTargetKind.CharacterMute)
                {
                    if (string.IsNullOrWhiteSpace(canonicalTarget))
                    {
                        throw new AdminOperationException("Target login or character name is required.", AdminOperationErrorKind.InvalidInput);
                    }

                    var cfg = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    using var context = persistence.CreateNewPlayerContext(cfg);
                    if (kind is BanTargetKind.AccountBan or BanTargetKind.AccountMute)
                    {
                        var account = await context.GetAccountByLoginNameAsync(canonicalTarget, cancellationToken).ConfigureAwait(false)
                            ?? throw new AdminOperationException($"Account '{canonicalTarget}' not found.", AdminOperationErrorKind.InvalidInput);
                        canonicalTarget = account.LoginName;

                        // Synchronize persisted Account.State / ChatBanUntil safely only if offline.
                        var snapshot = await login.GetSnapshotAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
                        if (!snapshot.Keys.Contains(account.LoginName, StringComparer.OrdinalIgnoreCase))
                        {
                            if (kind == BanTargetKind.AccountBan)
                            {
                                account.State = expiresAtUtc.HasValue ? AccountState.TemporarilyBanned : AccountState.Banned;
                            }
                            else
                            {
                                account.ChatBanUntil = expiresAtUtc ?? new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                            }

                            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        var owner = await context.GetAccountByCharacterNameAsync(canonicalTarget, cancellationToken).ConfigureAwait(false);
                        var character = owner?.Characters.FirstOrDefault(c => c.Name.Equals(canonicalTarget, StringComparison.OrdinalIgnoreCase))
                            ?? throw new AdminOperationException($"Character '{canonicalTarget}' not found.", AdminOperationErrorKind.InvalidInput);
                        canonicalTarget = character.Name;
                        if (kind == BanTargetKind.CharacterBan && owner is not null)
                        {
                            var snapshot = await login.GetSnapshotAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
                            if (!snapshot.Keys.Contains(owner.LoginName, StringComparer.OrdinalIgnoreCase))
                            {
                                character.CharacterStatus = CharacterStatus.Banned;
                                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }

                BanRecord created;
                try
                {
                    created = this.Service.Issue(kind, canonicalTarget, reasonEn, reasonVi, actor.Id, now, expiresAtUtc);
                }
                catch (ArgumentException ex)
                {
                    throw new AdminOperationException(ex.Message, AdminOperationErrorKind.InvalidInput);
                }

                this.RecordActivity(now, created, "issue", auditReason);
                return created;
            },
            cancellationToken);

    /// <summary>Lifts an active ban or mute and restores account/character state if offline and no other active ban remains.</summary>
    /// <param name="recordId">The moderation record ID.</param>
    /// <param name="unbanReason">Mandatory internal audit reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated record.</returns>
    [AdminOperation(AdminRole.Operator, ReadOnly = false)]
    public ValueTask<BanRecord> LiftAsync(string recordId, string unbanReason, CancellationToken cancellationToken)
        => executor.ExecuteAsync(
            AdminRoles.Operator,
            "moderation_lift",
            new { recordId, reason = unbanReason },
            async actor =>
            {
                AccountWriteOperations.ValidateReason(unbanReason);
                var now = clock.GetUtcNow().UtcDateTime;
                var updated = this.Service.Lift(recordId, actor.Id, unbanReason, now)
                    ?? throw new AdminOperationException("Moderation record not found.", AdminOperationErrorKind.InvalidInput);

                if (updated.Kind is BanTargetKind.AccountBan or BanTargetKind.AccountMute or BanTargetKind.CharacterBan)
                {
                    var cfg = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    using var context = persistence.CreateNewPlayerContext(cfg);
                    var snapshot = await login.GetSnapshotAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (updated.Kind == BanTargetKind.AccountBan
                        && !this.Service.HasOtherActiveAccountBan(updated.TargetDisplay, now)
                        && !snapshot.Keys.Contains(updated.TargetDisplay, StringComparer.OrdinalIgnoreCase)
                        && await context.GetAccountByLoginNameAsync(updated.TargetDisplay, cancellationToken).ConfigureAwait(false) is { } account
                        && account.State is AccountState.Banned or AccountState.TemporarilyBanned)
                    {
                        account.State = AccountState.Normal;
                        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else if (updated.Kind == BanTargetKind.AccountMute
                        && !snapshot.Keys.Contains(updated.TargetDisplay, StringComparer.OrdinalIgnoreCase)
                        && await context.GetAccountByLoginNameAsync(updated.TargetDisplay, cancellationToken).ConfigureAwait(false) is { } mutedAccount)
                    {
                        mutedAccount.ChatBanUntil = null;
                        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else if (updated.Kind == BanTargetKind.CharacterBan
                        && await context.GetAccountByCharacterNameAsync(updated.TargetDisplay, cancellationToken).ConfigureAwait(false) is { } owner
                        && owner.Characters.FirstOrDefault(c => c.Name.Equals(updated.TargetDisplay, StringComparison.OrdinalIgnoreCase)) is { } character
                        && !snapshot.Keys.Contains(owner.LoginName, StringComparer.OrdinalIgnoreCase)
                        && character.CharacterStatus == CharacterStatus.Banned)
                    {
                        character.CharacterStatus = CharacterStatus.Normal;
                        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                this.RecordActivity(now, updated, "lift", unbanReason);
                return updated;
            },
            cancellationToken);

    private void RecordActivity(DateTime nowUtc, BanRecord record, string action, string auditReason)
    {
        try
        {
            var entry = new ActivityLogEntry
            {
                TimestampUtc = nowUtc,
                Kind = ActivityKind.Administration,
                Account = record.Kind is BanTargetKind.AccountBan or BanTargetKind.AccountMute ? record.TargetDisplay : string.Empty,
                Character = record.Kind is BanTargetKind.CharacterBan or BanTargetKind.CharacterMute ? record.TargetDisplay : string.Empty,
                ItemSummary = $"moderation:{action}:{record.Kind}",
                Details = JsonSerializer.Serialize(new
                {
                    kind = "moderation",
                    action,
                    scope = record.Kind.ToString(),
                    target = record.TargetDisplay,
                    expiresAtUtc = record.ExpiresAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                    reason = auditReason,
                }),
            };

            if (activity is not null)
            {
                activity(entry);
            }
            else if (this._injectedRows is null)
            {
                ActivityLogStore.Open(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName)).Append(entry);
            }
        }
        catch
        {
            // Non-fatal: moderation record is already persisted.
        }
    }
}

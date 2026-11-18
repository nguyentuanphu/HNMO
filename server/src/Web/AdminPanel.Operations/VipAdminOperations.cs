// <copyright file="VipAdminOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.Extensions.Logging;

/// <summary>Authorizes, previews and audits host VIP changes without depending on Razor or browser state.</summary>
/// <param name="persistence">The account persistence provider.</param>
/// <param name="configurations">The canonical game configuration.</param>
/// <param name="login">The global login reservation service.</param>
/// <param name="executor">Role enforcement and durable administration audit.</param>
/// <param name="confirmations">Actor/state-bound, single-use previews.</param>
/// <param name="settings">The host write policy.</param>
/// <param name="logger">The operational logger.</param>
/// <param name="clock">The authoritative clock.</param>
/// <param name="state">An optional isolated state store for tests.</param>
/// <param name="activity">An optional activity sink for isolated tests; production uses the existing activity log.</param>
public sealed class VipAdminOperations(IPersistenceContextProvider persistence, IDataSource<GameConfiguration> configurations, ILoginServer login, AdminOperationExecutor executor, AdminChangeConfirmation confirmations, AdminWriteSettings settings, ILogger<VipAdminOperations> logger, TimeProvider clock, VipStateStore? state = null, Action<ActivityLogEntry>? activity = null)
{
    private VipStateStore? _state = state;

    private VipStateStore State => this._state ??= VipStateStore.OpenHost();

    /// <summary>Reads current editor settings and up to fifty proved VIP subscriptions.</summary>
    /// <param name="loginFilter">An optional exact login for authoritative recovery after restoring the shared index.</param>
    /// <param name="cancellationToken">Cancellation before reading.</param>
    /// <param name="offset">The number of indexed accounts to skip; each page contains at most fifty.</param>
    /// <returns>Host settings and proved active accounts; never a password or private receipt.</returns>
    [AdminOperation(AdminRole.Administrator)]
    public async ValueTask<VipAdminSnapshot> ReadAsync(string? loginFilter, CancellationToken cancellationToken, int offset = 0)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        if (offset is < 0 or > int.MaxValue - 50)
        {
            throw new AdminOperationException("The VIP page offset is invalid.", AdminOperationErrorKind.InvalidInput);
        }

        var saved = await this.ReadSettingsAsync<VipConfiguration>(typeof(VipPlugIn), cancellationToken).ConfigureAwait(false);
        var configuration = saved.Settings ?? new();
        var game = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(game);
        var now = clock.GetUtcNow().UtcDateTime;
        var known = this.State.KnownAccountIds(now);
        var subjects = new List<Account>();
        if (string.IsNullOrWhiteSpace(loginFilter))
        {
            foreach (var id in known.Skip(offset).Take(50))
            {
                if (await context.GetByIdAsync<Account>(id, cancellationToken).ConfigureAwait(false) is { } account)
                {
                    subjects.Add(account);
                }
            }
        }
        else if (await context.GetAccountByLoginNameAsync(NormalizeLogin(loginFilter), cancellationToken).ConfigureAwait(false) is { } found)
        {
            subjects.Add(found);
        }

        var rows = new List<VipAdminAccount>();
        var wallet = new VipWallet(configuration, this.State, game, logger);
        foreach (var account in subjects)
        {
            if (await wallet.ReadActiveAsync(context, account, now, cancellationToken).ConfigureAwait(false) is { } active)
            {
                rows.Add(new(account.LoginName, active.Intent.Plan.LevelId, active.Intent.NameEn, active.Intent.NameVi, active.Intent.Membership.UntilUtc));
            }
        }

        return new(configuration, saved.Active, rows, string.IsNullOrWhiteSpace(loginFilter) && known.Count > offset + 50);
    }

    /// <summary>Previews or applies one host grant/removal with required attribution, account ownership and activity logging.</summary>
    /// <param name="loginName">The exact account login.</param>
    /// <param name="operationId">The stable host retry identifier.</param>
    /// <param name="levelId">The configured level for a grant; zero for removal.</param>
    /// <param name="days">Days to grant; zero for removal.</param>
    /// <param name="remove">Whether to remove the current owned interval.</param>
    /// <param name="reason">The required operational reason.</param>
    /// <param name="apply">Whether to consume the reviewed preview.</param>
    /// <param name="confirmationToken">The caller/state-bound preview token.</param>
    /// <param name="cancellationToken">Cancellation before committing.</param>
    /// <returns>A preview or machine-readable outcome for the localized page.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    [NotExposedToAgents("VIP grants and removals require the interactive host approval page; the agent tool exposes only subscription reads.")]
    public async ValueTask<JsonObject> ChangeAsync(string loginName, Guid operationId, int levelId, int days, bool remove, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
    {
        var actor = await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeLogin(loginName);
        var attribution = new VipAdministrativeGrant(actor.Id, reason.Trim());
        if (!attribution.IsValid || operationId == Guid.Empty || (!remove && (levelId <= 0 || days is <= 0 or > 3650)))
        {
            throw new AdminOperationException("A configured VIP level, valid duration and bounded operational reason are required.", AdminOperationErrorKind.InvalidInput);
        }

        var arguments = new { loginName = normalized, operationId, levelId = remove ? 0 : levelId, days = remove ? 0 : days, remove, reason = attribution.Reason };
        var operation = remove ? "vip_remove" : "vip_grant";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        var reserved = false;
        var attemptedReservation = false;
        string? reservedLogin = null;
        using (await confirmations.Gate.LockAsync(token).ConfigureAwait(false))
        {
            try
            {
                var game = await configurations.GetOwnerAsync(cancellationToken: token).ConfigureAwait(false);
                using var initial = persistence.CreateNewPlayerContext(game);
                var subject = await initial.GetAccountByLoginNameAsync(normalized, token).ConfigureAwait(false)
                    ?? throw new AdminOperationException("Account not found.", AdminOperationErrorKind.InvalidInput);
                if (apply)
                {
                    attemptedReservation = true;
                    reservedLogin = subject.LoginName;
                    reserved = await login.TryReserveAccountAsync(reservedLogin, reservation).AsTask().WaitAsync(token).ConfigureAwait(false);
                    if (!reserved)
                    {
                        return new() { ["status"] = "Online" };
                    }
                }

                var saved = await this.ReadSettingsAsync<VipConfiguration>(typeof(VipPlugIn), token).ConfigureAwait(false);
                var configuration = saved.Settings ?? new();
                using var context = persistence.CreateNewPlayerContext(game);
                var account = await context.GetAccountByLoginNameAsync(subject.LoginName, token).ConfigureAwait(false)
                    ?? throw new AdminOperationException("Account not found.", AdminOperationErrorKind.InvalidInput);
                if (account.GetId() != subject.GetId())
                {
                    throw new AdminOperationException("The target account changed; request a new preview.", AdminOperationErrorKind.Conflict);
                }

                var wallet = new VipWallet(configuration, this.State, game, logger);
                var active = await wallet.ReadActiveAsync(context, account, clock.GetUtcNow().UtcDateTime, token).ConfigureAwait(false);
                var change = new JsonObject
                {
                    ["loginName"] = account.LoginName,
                    ["remove"] = remove,
                    ["currentExpiryUtc"] = active?.Intent.Membership.UntilUtc.ToString("O"),
                    ["grantIds"] = active is null ? string.Empty : string.Join(',', active.GrantIds.Order()),
                    ["level"] = remove ? null : JsonSerializer.SerializeToNode(configuration.Levels.SingleOrDefault(value => value.Id == levelId)),
                };
                if (!apply)
                {
                    return confirmations.Preview(actor, operation, arguments, change);
                }

                confirmations.Consume(actor, operation, arguments, change, confirmationToken);
                return await executor.ExecuteAsync(
                    AdminRoles.Administrator,
                    operation,
                    arguments,
                    async verified =>
                    {
                        if (verified.Id != actor.Id || verified.Frontend != actor.Frontend)
                        {
                            throw new AdminOperationException("The authenticated host changed; request a new preview.", AdminOperationErrorKind.Forbidden);
                        }

                        // Persist an activity attempt before the account changes. The receipt separately keeps durable actor/reason proof.
                        await this.RecordActivityAsync(account.LoginName, verified, operationId, remove, "Requested", attribution.Reason, token).ConfigureAwait(false);
                        try
                        {
                            var administration = new VipAdministration(configuration, this.State, game, logger);
                            var now = clock.GetUtcNow().UtcDateTime;
                            var result = remove
                                ? await administration.RemoveAsync(context, account, operationId, attribution, now, () => context.SaveChangesAsync(token)).ConfigureAwait(false)
                                : await administration.GrantAsync(context, account, operationId, levelId, days, attribution, now, () => context.SaveChangesAsync(token)).ConfigureAwait(false);
                            await this.RecordActivityAsync(account.LoginName, verified, operationId, remove, result.Status, attribution.Reason, token).ConfigureAwait(false);
                            if (result.Status == "Pending")
                            {
                                throw new AdminOperationException("The VIP change needs reconciliation; retry the same operation.", AdminOperationErrorKind.Uncertain);
                            }

                            return new JsonObject { ["status"] = result.Status, ["rule"] = result.Rule?.ToString() };
                        }
                        catch (AdminOperationException exception) when (exception.Kind == AdminOperationErrorKind.Uncertain)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            // A failure after entering the wallet cannot prove rollback; keep the central audit Pending.
                            throw new AdminOperationException("The VIP change or its activity outcome could not be confirmed; retry the same operation.", AdminOperationErrorKind.Uncertain, exception);
                        }
                    },
                    token,
                    result => result["status"]?.GetValue<string>() == "Applied").ConfigureAwait(false);
            }
            finally
            {
                if (attemptedReservation && reservedLogin is not null)
                {
                    try
                    {
                        await login.ReleaseAccountReservationAsync(reservedLogin, reservation).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "The VIP administration reservation needs its normal timeout recovery.");
                    }
                }
            }
        }
    }

    private static string NormalizeLogin(string value)
    {
        var login = value.Trim();
        if (login.Length is < 1 or > 10 || login.Any(char.IsControl))
        {
            throw new AdminOperationException("Use an exact account login of at most ten characters.", AdminOperationErrorKind.InvalidInput);
        }

        return login;
    }

    private async ValueTask<(bool Active, T? Settings)> ReadSettingsAsync<T>(Type plugInType, CancellationToken cancellationToken)
        where T : class
    {
        using var context = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), false);
        var configurations = await context.GetAsync<PlugInConfiguration>(cancellationToken).ConfigureAwait(false);
        var saved = configurations.SingleOrDefault(value => value.TypeId == plugInType.GUID);
        return (saved?.IsActive == true, saved?.GetConfiguration<T>(ReferenceHandler.Preserve));
    }

    private async ValueTask RecordActivityAsync(string account, AdminActor actor, Guid operationId, bool remove, string status, string reason, CancellationToken cancellationToken)
    {
        var saved = await this.ReadSettingsAsync<ActivityLogConfiguration>(typeof(ActivityLogPlugIn), cancellationToken).ConfigureAwait(false);
        var entry = new ActivityLogEntry
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            Kind = ActivityKind.Administration,
            Account = account,
            PartnerAccount = actor.Name,
            Details = JsonSerializer.Serialize(new { operation = remove ? "vip_remove" : "vip_grant", operationId, actor.Id, actor.Name, status, reason }),
        };
        if (activity is not null)
        {
            activity(entry);
        }
        else
        {
            ActivityLogStore.Open(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName)).Append(entry, saved.Settings?.RetentionDays ?? 30);
        }
    }
}

// <copyright file="GiftBoxAdminOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;

/// <summary>Authorizes, reviews and audits immutable mail sends without mutating recipients' live accounts.</summary>
/// <param name="persistence">The account persistence provider.</param>
/// <param name="configurations">The canonical game definitions.</param>
/// <param name="login">The authoritative live-account snapshot.</param>
/// <param name="executor">Mandatory host authorization and durable administration audit.</param>
/// <param name="confirmations">Single-use actor/state-bound confirmations.</param>
/// <param name="settings">The host write policy.</param>
/// <param name="clock">The authoritative clock.</param>
/// <param name="rows">An optional isolated backend for tests; production uses the existing player-state registry.</param>
/// <param name="activity">An optional isolated audit sink; production uses activity log45.</param>
public sealed class GiftBoxAdminOperations(IPersistenceContextProvider persistence, IDataSource<GameConfiguration> configurations, ILoginServer login, AdminOperationExecutor executor, AdminChangeConfirmation confirmations, AdminWriteSettings settings, TimeProvider clock, IHnmoPlayerStateRowBackend? rows = null, Action<ActivityLogEntry>? activity = null)
{
    private IHnmoPlayerStateRowBackend? _rows = rows;

    private IHnmoPlayerStateRowBackend Rows => this._rows ??= HnmoPlayerStateRowStoreRegistry.GetBackend(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName));

    /// <summary>Previews or queues a confirmed bilingual gift for an exact frozen recipient snapshot.</summary>
    /// <param name="draft">The reviewed host input with a stable send identifier.</param>
    /// <param name="reason">The mandatory private operational reason.</param>
    /// <param name="apply">Whether to apply the confirmed preview.</param>
    /// <param name="confirmationToken">The single-use token returned by this operation's preview.</param>
    /// <param name="cancellationToken">Cancellation before queuing; uncertain commits must retain the original send identifier.</param>
    /// <returns>A preview or queued/replayed count, never account credentials.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    [NotExposedToAgents("Gift sends require the interactive host confirmation page.")]
    public async ValueTask<JsonObject> SendAsync(GiftBoxSendDraft draft, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
    {
        var actor = await AdminChangeExecution.RequireAsync(executor, settings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        Validate(draft, reason);
        draft = JsonSerializer.Deserialize<GiftBoxSendDraft>(JsonSerializer.Serialize(draft))!;
        reason = reason.Trim();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        using (await confirmations.Gate.LockAsync(token).ConfigureAwait(false))
        {
            var game = await configurations.GetOwnerAsync(cancellationToken: token).ConfigureAwait(false);
            var dispatch = new GiftBoxDispatch(this.Rows);
            var frozen = dispatch.ReadFrozenAudience(draft.SendId);
            if (frozen is null)
            {
                await ValidateItemsAsync(game, draft).ConfigureAwait(false);
            }

            var now = clock.GetUtcNow().UtcDateTime;
            if (frozen is null && draft.ExpiresAtUtc <= now)
            {
                throw new AdminOperationException("The gift collection deadline must be in the future.", AdminOperationErrorKind.InvalidInput);
            }

            using var context = persistence.CreateNewPlayerContext(game);
            var recipients = frozen ?? await this.ResolveAsync(context, draft, now, token).ConfigureAwait(false);
            if (recipients.Count == 0)
            {
                throw new AdminOperationException("No accounts match the reviewed gift audience.", AdminOperationErrorKind.InvalidInput);
            }

            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { draft, reason, recipients }))));

            // The full message and possibly 10,000 identities would exceed the central audit limit.
            // Hash the complete reviewed input; immutable mail rows keep the actual public contents.
            var arguments = new { draft.SendId, reason, digest, count = recipients.Count };
            var change = new JsonObject { ["count"] = recipients.Count, ["digest"] = digest, ["alreadyQueued"] = frozen is not null };
            const string operation = "gift_box_send";
            token.ThrowIfCancellationRequested();
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
                        throw new AdminOperationException("The authenticated host changed; review the gift again.", AdminOperationErrorKind.Forbidden);
                    }

                    var sendNow = clock.GetUtcNow().UtcDateTime;
                    if (frozen is null && draft.ExpiresAtUtc <= sendNow)
                    {
                        throw new AdminOperationException("The gift collection deadline expired before queuing.", AdminOperationErrorKind.InvalidInput);
                    }

                    this.Record(verified, draft.SendId, reason, recipients.Count, "Requested");
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        var count = dispatch.Send(draft.SendId, verified.Id, reason, recipients, draft.TitleEn, draft.TitleVi, draft.MessageEn, draft.MessageVi, draft.Reward, sendNow, draft.ExpiresAtUtc);
                        this.Record(verified, draft.SendId, reason, count, frozen is null ? "Queued" : "AlreadyQueued");
                        return new JsonObject { ["status"] = frozen is null ? "Queued" : "AlreadyQueued", ["count"] = count, ["sendId"] = draft.SendId.ToString("D") };
                    }
                    catch (Exception exception)
                    {
                        // This includes a lost commit response or failed final activity write. Do not mark
                        // the durable host audit as failed/refused when the immutable send may exist.
                        throw new AdminOperationException("The gift send needs confirmation. Keep its original identifier and retry the same reviewed input.", AdminOperationErrorKind.Uncertain, exception);
                    }
                },
                token).ConfigureAwait(false);
        }
    }

    private static void Validate(GiftBoxSendDraft draft, string reason)
    {
        if (draft is null || draft.SendId == Guid.Empty || !Enum.IsDefined(draft.Audience) || draft.ActiveDays is < 1 or > 3650
            || !Text(reason, 500) || !Text(draft.TitleEn, 100) || !Text(draft.TitleVi, 100)
            || !Text(draft.MessageEn, 2000) || !Text(draft.MessageVi, 2000) || draft.Logins is null || draft.Logins.Length > 200000
            || draft.ExpiresAtUtc is { Kind: not DateTimeKind.Utc })
        {
            throw new AdminOperationException("Supply a bounded bilingual gift, valid audience and operational reason.", AdminOperationErrorKind.InvalidInput);
        }

        var reward = draft.Reward;
        if (reward is null || !Enum.IsDefined(reward.CoinType) || reward.Coins is < 0 or > 1000000 || reward.Zen is < 0 or > 2000000000
            || reward.Items is null || reward.Items.Count > 50 || reward.IsEmpty
            || reward.Items.Any(item => item is null || item.Count is < 1 or > 50 || item.Group is < 0 or > 15 || item.Number is < 0 or > 511 || item.Level > 15 || item.OptionLevel > 7 || item.ExcellentOptions > 63)
            || reward.Items.Sum(item => item.Count) > 50)
        {
            throw new AdminOperationException("The gift reward is invalid or empty.", AdminOperationErrorKind.InvalidInput);
        }
    }

    private static async ValueTask ValidateItemsAsync(GameConfiguration game, GiftBoxSendDraft draft)
    {
        // Every gift must be collectible in an empty base bag; recipients do not need to buy extensions
        // to receive a compensation. Large kits can be split into separate reviewed sends.
        var plan = await GiftBoxPacking.PlanAsync(game, [], 0, draft.Reward).ConfigureAwait(false);
        if (plan.Status != GiftBoxPackingStatus.Ready)
        {
            throw new AdminOperationException("The promised items are unsupported or cannot fit in one empty bag. Split a large gift into separate sends.", AdminOperationErrorKind.InvalidInput);
        }
    }

    private static bool Text(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t');

    private async ValueTask<IReadOnlyList<Guid>> ResolveAsync(IPlayerContext context, GiftBoxSendDraft draft, DateTime now, CancellationToken token)
    {
        var online = draft.ActiveDays is null ? new Dictionary<string, byte>() : await login.GetSnapshotAsync().AsTask().WaitAsync(token).ConfigureAwait(false);
        var observed = draft.ActiveDays is null ? new Dictionary<Guid, DateTime>() : new GiftBoxActivity(this.Rows).Read();
        var accounts = new List<GiftBoxRecipient>();
        void Add(HNMO.DataModel.Entities.Account account) => accounts.Add(new(account.GetId(), account.LoginName, observed.TryGetValue(account.GetId(), out var last) ? last : null, online.ContainsKey(account.LoginName)));
        if (draft.Audience is GiftBoxAudienceKind.Account or GiftBoxAudienceKind.List)
        {
            var requested = draft.Audience == GiftBoxAudienceKind.Account ? new[] { draft.Logins.Trim() }
                : draft.Logins.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
            if (requested.Length is < 1 or > 10000 || requested.Any(name => name.Length is < 1 or > 10 || name.Any(character => character is < '!' or > '~')))
            {
                throw new AdminOperationException("Supply exact account logins, one per line for a list.", AdminOperationErrorKind.InvalidInput);
            }

            foreach (var name in requested)
            {
                var account = await context.GetAccountByLoginNameAsync(name, token).ConfigureAwait(false)
                    ?? throw new AdminOperationException("An account in the reviewed gift audience does not exist.", AdminOperationErrorKind.InvalidInput);
                Add(account);
            }
        }
        else
        {
            for (var offset = 0; ; offset = checked(offset + 100))
            {
                var (page, total) = await context.SearchAccountPageAsync(string.Empty, null, offset, 100, token).ConfigureAwait(false);
                foreach (var account in page)
                {
                    Add(account);
                }

                if (page.Count == 0 || offset + page.Count >= total)
                {
                    break;
                }
            }
        }

        return GiftBoxAudience.Resolve(accounts, null, draft.ActiveDays, now).Select(account => account.AccountId).ToArray();
    }

    private void Record(AdminActor actor, Guid sendId, string reason, int count, string status)
    {
        var entry = new ActivityLogEntry
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            Kind = ActivityKind.Administration,
            Account = actor.Name,
            Details = JsonSerializer.Serialize(new { kind = "gift-box-send", sendId, actor = actor.Name, reason, count, status }),
        };
        if (activity is not null)
        {
            activity(entry);
        }
        else
        {
            ActivityLogStore.Open(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName)).Append(entry);
        }
    }
}

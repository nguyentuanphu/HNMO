// <copyright file="VipWallet.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence;

/// <summary>Commits VIP debit, replay proof and owned timed grants in one existing account save.</summary>
/// <remarks>
/// The trusted caller must authenticate and own the account: hold its player persistence lock online,
/// or an offline login reservation. A failed save retains mutations in that context, like pending coin
/// grants. It must never restore the balance alone. A retry flushes the same tracked receipt, or reads
/// its committed proof after a restart. No public endpoint may accept a quote or grant identifiers.
/// </remarks>
/// <param name="configuration">The host's current offer configuration.</param>
/// <param name="store">The shared vip section, never a separate save file.</param>
/// <param name="gameConfiguration">The canonical persisted attribute definitions.</param>
/// <param name="logger">The operational logger; never logs credentials.</param>
public sealed class VipWallet(VipConfiguration configuration, VipStateStore store, GameConfiguration gameConfiguration, ILogger logger)
{
    private const string ReceiptKind = "vip-wallet-v1";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> AccountGates = new();
    private static readonly ConditionalWeakTable<IPlayerContext, Dictionary<Guid, CashShopCoinGrant>> TrackedReceipts = new();

    /// <summary>Reads only recognized account-bound wallet proof metadata.</summary>
    /// <param name="reason">The receipt's stored reason.</param>
    /// <returns>The proof, or null for an ordinary or malformed reason.</returns>
    public static VipWalletReceipt? ReadReceipt(string? reason)
    {
        if (reason is null || reason.Length > 16384 || !reason.StartsWith('{'))
        {
            return null;
        }

        try
        {
            var proof = JsonSerializer.Deserialize<VipWalletReceipt>(reason);
            return proof is { Kind: ReceiptKind, Intent: { Plan: not null, Membership: not null, PreviousGrantIds: not null }, GrantIds.Count: 3 } && proof.GrantIds.Distinct().Count() == 3 && !proof.GrantIds.Contains(Guid.Empty) ? proof : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Purchases or retries one server-priced offer without charging its identifier twice.</summary>
    /// <param name="context">The exclusively owned account context.</param>
    /// <param name="account">The authenticated account loaded in that context.</param>
    /// <param name="operationId">A stable identifier reused for retries of this exact selection.</param>
    /// <param name="levelId">The selected configured level.</param>
    /// <param name="days">The selected configured duration.</param>
    /// <param name="nowUtc">The current UTC instant.</param>
    /// <param name="save">Saves that same context; never a different unit of work.</param>
    /// <param name="offerToken">The reviewed offer version; network adapters must require it. Null is for trusted internal callers only.</param>
    /// <param name="cancellationToken">Cancellation while waiting for ownership.</param>
    /// <returns>An applied, pending, conflicting or refused outcome.</returns>
    public async ValueTask<VipWalletResult> PurchaseAsync(
        IPlayerContext context,
        Account account,
        Guid operationId,
        int levelId,
        int days,
        DateTime nowUtc,
        Func<ValueTask<bool>> save,
        string? offerToken = null,
        CancellationToken cancellationToken = default)
    {
        var accountId = account.GetId();
        if (accountId == Guid.Empty || operationId == Guid.Empty || string.IsNullOrWhiteSpace(account.LoginName) || nowUtc.Kind != DateTimeKind.Utc)
        {
            return new("Refused", VipRuleResult.Invalid);
        }

        var gate = AccountGates.GetOrAdd(accountId, _ => new(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.PurchaseOwnedAsync(context, account, operationId, levelId, days, nowUtc, save, offerToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Reads the paid, active entitlement from account grants and database proof, rebuilding only its file projection.</summary>
    /// <param name="context">The authenticated account's owned context.</param>
    /// <param name="account">The authenticated account.</param>
    /// <param name="nowUtc">The current UTC instant.</param>
    /// <param name="cancellationToken">Cancellation before reading the account.</param>
    /// <returns>The active paid proof, or null when it cannot be established; never invents a grant from a file record.</returns>
    public async ValueTask<VipWalletReceipt?> ReadActiveAsync(IPlayerContext context, Account account, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (account.GetId() == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc)
        {
            return null;
        }

        var gate = AccountGates.GetOrAdd(account.GetId(), _ => new(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ReadActiveOwnedAsync(context, account, nowUtc).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Checks whether this context still holds an unconfirmed account mutation.</summary>
    /// <param name="context">The owned account context.</param>
    /// <param name="operationId">The purchase identifier.</param>
    /// <returns>Whether only an account save retry can confirm that tracked receipt.</returns>
    internal static bool IsUnconfirmed(IPlayerContext context, Guid operationId)
        => TrackedReceipts.TryGetValue(context, out var tracked) && tracked.ContainsKey(operationId);

    /// <summary>Checks all unconfirmed mutations before live attribute refresh.</summary>
    /// <param name="context">The exclusively owned context.</param>
    /// <returns>Whether any tracked purchase still needs its save confirmed.</returns>
    internal static bool HasUnconfirmed(IPlayerContext context)
        => TrackedReceipts.TryGetValue(context, out var tracked) && tracked.Count > 0;

    private static VipWalletReceipt? MatchActiveProof(CashShopCoinGrant row, Account account, HashSet<Guid> activeIds)
    {
        var proof = ReadReceipt(row.Reason);
        if (proof is null || row.AppliedAt is null || row.AccountId != account.GetId() || proof.Intent.AccountId != account.GetId()
            || proof.Intent.Plan.LoginName != account.LoginName.Trim().ToUpperInvariant() || proof.Intent.Plan.Price < 0
            || row.Amount != -proof.Intent.Plan.Price || row.CoinType != proof.Intent.Plan.CoinType
            || row.Reference != $"vip:{proof.Intent.AccountId:N}:{proof.Intent.OperationId:N}")
        {
            return null;
        }

        var vip = account.AttributeGrants.SingleOrDefault(grant => activeIds.Contains(grant.GetId()) && proof.GrantIds.Contains(grant.GetId()));
        return vip?.ValidUntil == proof.Intent.Membership.UntilUtc && vip.Value == proof.Intent.Plan.LevelId ? proof : null;
    }

    private async ValueTask<VipWalletResult> PurchaseOwnedAsync(IPlayerContext context, Account account, Guid operationId, int levelId, int days, DateTime nowUtc, Func<ValueTask<bool>> save, string? offerToken)
    {
        var tracked = TrackedReceipts.GetOrCreateValue(context);
        var reference = $"vip:{account.GetId():N}:{operationId:N}";
        var isTracked = tracked.TryGetValue(operationId, out var receipt);
        receipt ??= await context.GetCashShopCoinGrantByReferenceAsync(reference).ConfigureAwait(false);
        if (receipt is not null)
        {
            var proof = ReadReceipt(receipt.Reason);
            if (proof is null || proof.Intent.AccountId != account.GetId() || proof.Intent.OperationId != operationId
                || proof.Intent.Plan.LevelId != levelId || proof.Intent.Plan.Days != days
                || receipt.AccountId != account.GetId() || receipt.Reference != reference || receipt.AppliedAt is null
                || proof.Intent.Plan.LoginName != account.LoginName.Trim().ToUpperInvariant() || proof.Intent.Plan.Price < 0
                || receipt.CoinType != proof.Intent.Plan.CoinType || receipt.Amount != -proof.Intent.Plan.Price)
            {
                return new("Conflict");
            }

            if (offerToken is not null && !string.Equals(offerToken, VipOfferToken.FromIntent(proof.Intent), StringComparison.OrdinalIgnoreCase))
            {
                return new("Conflict");
            }

            if (isTracked && !await this.TrySaveAsync(save, operationId).ConfigureAwait(false))
            {
                return new("Pending");
            }

            tracked.Remove(operationId);
            return this.ConfirmProof(account, proof, nowUtc);
        }

        if (tracked.Count > 0)
        {
            return new("Pending");
        }

        // Another host or an older shared-file backup may not know the current database-owned grants.
        await this.ReadActiveOwnedAsync(context, account, nowUtc).ConfigureAwait(false);

        VipWalletIntent intent;
        lock (store.SyncRoot)
        {
            if (store.Data.WalletRefusals.TryGetValue(operationId, out var refusal))
            {
                return new("Refused", refusal);
            }

            if (store.Data.WalletIntents.TryGetValue(operationId, out var previous))
            {
                if (previous.AccountId != account.GetId() || previous.Plan.LevelId != levelId || previous.Plan.Days != days)
                {
                    return new("Conflict");
                }

                if (!configuration.Enabled || previous.Plan.QuotedAtUtc > nowUtc || nowUtc - previous.Plan.QuotedAtUtc > TimeSpan.FromMinutes(5))
                {
                    return this.RefuseIntent(operationId, configuration.Enabled ? VipRuleResult.Invalid : VipRuleResult.Disabled);
                }

                if (offerToken is not null && !string.Equals(offerToken, VipOfferToken.FromIntent(previous), StringComparison.OrdinalIgnoreCase))
                {
                    return new("Conflict");
                }

                intent = previous;
            }
            else
            {
                if (store.Data.Payments.ContainsKey(operationId))
                {
                    return new("Conflict");
                }

                // An unresolved operation must be recovered before a different request can buy more time.
                if (store.Data.WalletIntents.Values.Any(value => value.AccountId == account.GetId() && !store.Data.Payments.ContainsKey(value.OperationId) && !store.Data.WalletRefusals.ContainsKey(value.OperationId)))
                {
                    return new("Pending");
                }

                var name = account.LoginName.Trim().ToUpperInvariant();
                var owned = store.Data.OwnedGrants.TryGetValue(name, out var ids) ? ids.ToList() : [];
                if (account.AttributeGrants.Any(grant => grant.Attribute?.Id == Stats.IsVip.Id && grant.IsValidAt(nowUtc) && !owned.Contains(grant.GetId()))
                    || account.Attributes.Any(attribute => attribute.Definition?.Id == Stats.IsVip.Id && attribute.Value > 0)
                    || account.AttributeGrants.Any(grant => owned.Contains(grant.GetId()) && grant.Attribute?.Id == Stats.IsVip.Id && grant.IsValidAt(nowUtc)
                        && (!store.Data.Memberships.TryGetValue(name, out var current) || grant.ValidUntil != current.UntilUtc || grant.Value != current.LevelId))
                    || (store.Data.Memberships.TryGetValue(name, out var known) && known.UntilUtc > nowUtc && !account.AttributeGrants.Any(grant => owned.Contains(grant.GetId()) && grant.Attribute?.Id == Stats.IsVip.Id && grant.IsValidAt(nowUtc))))
                {
                    return new("Conflict");
                }

                var quote = new VipRules(configuration, store).Quote(account.LoginName, levelId, days, long.MaxValue, nowUtc);
                if (quote.Plan is not { } plan)
                {
                    return new("Refused", quote.Result);
                }

                var level = configuration.Levels.Single(value => value.Id == levelId);
                if (level.NameEn.Length > 120 || level.NameVi.Length > 120)
                {
                    return new("Refused", VipRuleResult.Invalid);
                }

                var from = store.Data.Memberships.TryGetValue(name, out var active) && active.UntilUtc > nowUtc ? active.FromUtc : nowUtc;
                intent = new(account.GetId(), operationId, plan, new(name, levelId, from, plan.UntilUtc), level.ExperiencePercent, level.ZenPercent, level.NameEn, level.NameVi, owned, level.DailyCoins, level.DailyCoinType);
                if (offerToken is not null && !string.Equals(offerToken, VipOfferToken.FromIntent(intent), StringComparison.OrdinalIgnoreCase))
                {
                    return new("PriceChanged");
                }

                if (account.GetCashShopCoins(plan.CoinType) < plan.Price)
                {
                    return new("Refused", VipRuleResult.InsufficientCoins);
                }

                store.Data.WalletIntents.Add(operationId, intent);
            }

            // Always retry the write before touching the wallet, even if an earlier write left a cached intent.
            store.Save();
        }

        var balance = account.GetCashShopCoins(intent.Plan.CoinType);
        if (balance < intent.Plan.Price)
        {
            return this.RefuseIntent(operationId, VipRuleResult.InsufficientCoins);
        }

        var definitions = new[] { Stats.IsVip, Stats.BonusExperienceRate, Stats.MoneyAmountRate }
            .Select(attribute => gameConfiguration.Attributes.SingleOrDefault(value => value.Id == attribute.Id)).ToArray();
        if (definitions.Any(attribute => attribute is null))
        {
            return this.RefuseIntent(operationId, VipRuleResult.Invalid);
        }

        var values = new[] { (float)intent.Plan.LevelId, intent.ExperiencePercent / 100f, intent.ZenPercent / 100f };
        var grantIds = new List<Guid>();
        for (var index = 0; index < definitions.Length; index++)
        {
            var grant = context.CreateNew<AccountAttributeGrant>();
            grant.Attribute = definitions[index];
            grant.Value = values[index];
            grant.ValidUntil = intent.Membership.UntilUtc;
            account.AttributeGrants.Add(grant);
            grantIds.Add(grant.GetId());
        }

        foreach (var grant in account.AttributeGrants.Where(value => intent.PreviousGrantIds.Contains(value.GetId()) && value.IsValidAt(nowUtc)))
        {
            grant.ValidUntil = nowUtc;
        }

        var created = context.CreateNew<CashShopCoinGrant>();
        created.AccountId = account.GetId();
        created.Reference = reference;
        created.CoinType = intent.Plan.CoinType;
        created.Amount = -intent.Plan.Price;
        created.CreatedAt = nowUtc;
        created.AppliedAt = nowUtc;
        var completed = new VipWalletReceipt(ReceiptKind, intent, grantIds);
        created.Reason = JsonSerializer.Serialize(completed);
        account.SetCashShopCoins(intent.Plan.CoinType, balance - intent.Plan.Price);
        tracked.Add(operationId, created);
        if (!await this.TrySaveAsync(save, operationId).ConfigureAwait(false))
        {
            return new("Pending");
        }

        tracked.Remove(operationId);
        return this.ConfirmProof(account, completed, nowUtc);
    }

    private VipWalletResult ConfirmProof(Account account, VipWalletReceipt proof, DateTime nowUtc)
    {
        var intent = proof.Intent;
        lock (store.SyncRoot)
        {
            if (!store.Data.Payments.ContainsKey(intent.OperationId))
            {
                store.Data.Payments[intent.OperationId] = intent.Plan;
                store.Data.Audit.Add(new(nowUtc, intent.Plan.LoginName, "purchase", "wallet", intent.OperationId.ToString("N")));
            }

            // Rebuild a missing file projection only from an actual active account grant and its paid proof.
            // Expired, removed or superseded receipts never overwrite the current entitlement.
            var vipGrant = account.AttributeGrants.SingleOrDefault(grant => proof.GrantIds.Contains(grant.GetId()) && grant.Attribute?.Id == Stats.IsVip.Id);
            if (vipGrant?.ValidUntil == intent.Membership.UntilUtc && vipGrant.Value == intent.Plan.LevelId && vipGrant.IsValidAt(nowUtc))
            {
                store.Data.Memberships[intent.Plan.LoginName] = intent.Membership;
                store.Data.OwnedGrants[intent.Plan.LoginName] = proof.GrantIds.ToList();
                store.Data.WalletReferences[intent.Plan.LoginName] = $"vip:{intent.AccountId:N}:{intent.OperationId:N}";
            }

            try
            {
                store.Save();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "VIP payment {OperationId} is committed but its shared-state confirmation needs retry.", intent.OperationId);
                return new("Pending");
            }
        }

        var active = account.AttributeGrants.Any(grant => proof.GrantIds.Contains(grant.GetId()) && grant.Attribute?.Id == Stats.IsVip.Id && grant.IsValidAt(nowUtc) && grant.ValidUntil == intent.Membership.UntilUtc && grant.Value == intent.Plan.LevelId)
            ? intent.Membership : null;
        return new("Applied", Membership: active);
    }

    private async ValueTask<VipWalletReceipt?> ReadActiveOwnedAsync(IPlayerContext context, Account account, DateTime nowUtc)
    {
        if (TrackedReceipts.TryGetValue(context, out var tracked) && tracked.Count > 0)
        {
            return null;
        }

        var activeIds = account.AttributeGrants.Where(grant => grant.Attribute?.Id == Stats.IsVip.Id && grant.IsValidAt(nowUtc))
            .Select(grant => grant.GetId()).ToHashSet();
        if (activeIds.Count == 0)
        {
            return null;
        }

        string? reference;
        lock (store.SyncRoot)
        {
            store.Data.WalletReferences.TryGetValue(account.LoginName.Trim().ToUpperInvariant(), out reference);
        }

        if (reference is not null && await context.GetCashShopCoinGrantByReferenceAsync(reference).ConfigureAwait(false) is { } cached
            && MatchActiveProof(cached, account, activeIds) is { } known)
        {
            bool current;
            lock (store.SyncRoot)
            {
                var name = known.Intent.Plan.LoginName;
                current = store.Data.Payments.ContainsKey(known.Intent.OperationId)
                    && store.Data.Memberships.TryGetValue(name, out var membership) && membership == known.Intent.Membership
                    && store.Data.OwnedGrants.TryGetValue(name, out var ids) && ids.SequenceEqual(known.GrantIds);
            }

            if (!current)
            {
                this.ConfirmProof(account, known, nowUtc);
            }

            return known;
        }

        // Cold reconstruction is bounded. Unknown legacy grants remain intact and cannot authorize a purchase.
        const int pageSize = 100;
        for (var page = 0; page < 200; page++)
        {
            var rows = await context.GetCashShopCoinHistoryAsync(account.GetId(), page * pageSize, pageSize).ConfigureAwait(false);
            foreach (var row in rows)
            {
                if (MatchActiveProof(row, account, activeIds) is not { } proof)
                {
                    continue;
                }

                this.ConfirmProof(account, proof, nowUtc);
                return proof;
            }

            if (rows.Count < pageSize)
            {
                break;
            }
        }

        return null;
    }

    private VipWalletResult RefuseIntent(Guid operationId, VipRuleResult reason)
    {
        lock (store.SyncRoot)
        {
            store.Data.WalletRefusals[operationId] = reason;
            store.Save();
        }

        return new("Refused", reason);
    }

    private async ValueTask<bool> TrySaveAsync(Func<ValueTask<bool>> save, Guid operationId)
    {
        try
        {
            return await save().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "VIP payment {OperationId} needs recovery; its account context retains the debit and grant proof together.", operationId);
            return false;
        }
    }
}

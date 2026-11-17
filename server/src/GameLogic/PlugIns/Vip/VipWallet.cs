// <copyright file="VipWallet.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence;
using Microsoft.Extensions.Logging;

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
            return await this.PurchaseOwnedAsync(context, account, operationId, levelId, days, nowUtc, save).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<VipWalletResult> PurchaseOwnedAsync(IPlayerContext context, Account account, Guid operationId, int levelId, int days, DateTime nowUtc, Func<ValueTask<bool>> save)
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
                || proof.Intent.Plan.LoginName != account.LoginName.ToLowerInvariant() || proof.Intent.Plan.Price < 0
                || receipt.CoinType != proof.Intent.Plan.CoinType || receipt.Amount != -proof.Intent.Plan.Price)
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

                var name = account.LoginName.ToLowerInvariant();
                var owned = store.Data.OwnedGrants.TryGetValue(name, out var ids) ? ids.ToList() : [];
                if (account.AttributeGrants.Any(grant => grant.Attribute?.Id == Stats.IsVip.Id && grant.IsValidAt(nowUtc) && !owned.Contains(grant.GetId()))
                    || account.Attributes.Any(attribute => attribute.Definition?.Id == Stats.IsVip.Id && attribute.Value > 0)
                    || (store.Data.Memberships.TryGetValue(name, out var known) && known.UntilUtc > nowUtc && !account.AttributeGrants.Any(grant => owned.Contains(grant.GetId()) && grant.Attribute?.Id == Stats.IsVip.Id && grant.IsValidAt(nowUtc))))
                {
                    return new("Conflict");
                }

                var level = configuration.Levels.FirstOrDefault(value => value.Id == levelId);
                var offer = level?.Offers.FirstOrDefault(value => value.Days == days);
                var quote = new VipRules(configuration, store).Quote(account.LoginName, levelId, days, offer is null || !Enum.IsDefined(offer.CoinType) ? 0 : account.GetCashShopCoins(offer.CoinType), nowUtc);
                if (quote.Plan is not { } plan || level is null)
                {
                    return new("Refused", quote.Result);
                }

                if (level.NameEn.Length > 120 || level.NameVi.Length > 120)
                {
                    return new("Refused", VipRuleResult.Invalid);
                }

                var from = store.Data.Memberships.TryGetValue(name, out var active) && active.UntilUtc > nowUtc ? active.FromUtc : nowUtc;
                intent = new(account.GetId(), operationId, plan, new(name, levelId, from, plan.UntilUtc), level.ExperiencePercent, level.ZenPercent, level.NameEn, level.NameVi, owned);
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
                // A removed grant is never recreated by a replay. Actual account grants own the expiry.
                var vipGrant = account.AttributeGrants.SingleOrDefault(grant => proof.GrantIds.Contains(grant.GetId()) && grant.Attribute?.Id == Stats.IsVip.Id);
                if (vipGrant?.ValidUntil == intent.Membership.UntilUtc)
                {
                    store.Data.Memberships[intent.Plan.LoginName] = intent.Membership;
                    store.Data.OwnedGrants[intent.Plan.LoginName] = proof.GrantIds.ToList();
                }

                store.Data.Payments[intent.OperationId] = intent.Plan;
                store.Data.Audit.Add(new(nowUtc, intent.Plan.LoginName, "purchase", "wallet", intent.OperationId.ToString("N")));
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

        var active = account.AttributeGrants.Any(grant => proof.GrantIds.Contains(grant.GetId()) && grant.Attribute?.Id == Stats.IsVip.Id && grant.IsValidAt(nowUtc))
            ? intent.Membership : null;
        return new("Applied", Membership: active);
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

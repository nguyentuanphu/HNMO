// <copyright file="VipStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Text.Json;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence;

/// <summary>Projects only safe account-owned display fields for the website and web-game window.</summary>
public static class VipStateWriter
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Reads saved entitlement proof and current offers without creating a purchase or applying a coin grant.</summary>
    /// <param name="configuration">The active host settings.</param>
    /// <param name="store">The shared VIP journal and projection.</param>
    /// <param name="wallet">The paid entitlement reader.</param>
    /// <param name="context">The authenticated account context.</param>
    /// <param name="account">The authenticated account.</param>
    /// <param name="nowUtc">The server's current UTC instant.</param>
    /// <param name="operationId">An optional account-bound operation being recovered.</param>
    /// <param name="status">The action outcome, if any.</param>
    /// <param name="rule">The quote refusal, if any.</param>
    /// <returns>Safe JSON with no credential, private audit, account identifier or internal grant identifiers.</returns>
    public static async ValueTask<string> WriteAsync(
        VipConfiguration configuration,
        VipStateStore store,
        VipWallet wallet,
        IPlayerContext context,
        Account account,
        DateTime nowUtc,
        Guid? operationId = null,
        string? status = null,
        VipRuleResult? rule = null)
    {
        var active = await wallet.ReadActiveAsync(context, account, nowUtc).ConfigureAwait(false);
        VipWalletIntent? pending;
        lock (store.SyncRoot)
        {
            pending = store.Data.WalletIntents.Values.Where(intent => intent.AccountId == account.GetId() && intent.Administration is null
                && !store.Data.Payments.ContainsKey(intent.OperationId) && !store.Data.WalletRefusals.ContainsKey(intent.OperationId))
                .OrderBy(intent => intent.Plan.QuotedAtUtc).FirstOrDefault();
            if (operationId is { } requested && store.Data.WalletIntents.TryGetValue(requested, out var intent)
                && intent.AccountId == account.GetId() && store.Data.WalletRefusals.TryGetValue(requested, out var refusal))
            {
                status = "Refused";
                rule = refusal;
            }
        }

        if (operationId is { } id && !VipWallet.IsUnconfirmed(context, id)
            && await context.GetCashShopCoinGrantByReferenceAsync($"vip:{account.GetId():N}:{id:N}").ConfigureAwait(false) is { AppliedAt: not null } entry
            && entry.AccountId == account.GetId() && VipWallet.ReadReceipt(entry.Reason) is { } paid
            && paid.Intent.AccountId == account.GetId() && paid.Intent.OperationId == id)
        {
            status = "Applied";
        }

        if (pending is not null && status is null)
        {
            status = "Pending";
        }

        var offers = new List<object>();
        if (configuration.Enabled)
        {
            foreach (var level in configuration.Levels ?? [])
            {
                foreach (var offer in level?.Offers ?? [])
                {
                    var quote = new VipRules(configuration, store).Quote(account.LoginName, level!.Id, offer.Days, long.MaxValue, nowUtc);
                    if (quote.Plan is not { } plan)
                    {
                        continue;
                    }

                    offers.Add(new
                    {
                        levelId = level.Id,
                        level.NameEn,
                        level.NameVi,
                        offer.Days,
                        offer.Price,
                        coinType = offer.CoinType.ToString(),
                        offerToken = VipOfferToken.Create(account.GetId(), level, offer, plan.PreviousUntilUtc),
                        level.ExperiencePercent,
                        level.ZenPercent,
                        level.DailyCoins,
                        dailyCoinType = level.DailyCoinType.ToString(),
                    });
                }
            }
        }

        var daily = await context.GetCashShopCoinGrantByReferenceAsync(VipDailyCoins.ReferenceFor(account.GetId(), nowUtc)).ConfigureAwait(false);
        var claimed = daily?.AccountId == account.GetId() && VipDailyCoins.ReadReceipt(daily.Reason) is { } dailyProof && dailyProof.AccountId == account.GetId();
        var legacy = active is null ? account.GetAccountAttributeValue(Stats.IsVip, nowUtc) : 0;
        var snapshot = new
        {
            enabled = configuration.Enabled,
            accountKey = account.LoginName,
            active = active is null ? null : new
            {
                levelId = active.Intent.Plan.LevelId,
                active.Intent.NameEn,
                active.Intent.NameVi,
                untilUtc = active.Intent.Membership.UntilUtc,
                active.Intent.ExperiencePercent,
                active.Intent.ZenPercent,
                active.Intent.DailyCoins,
                dailyCoinType = active.Intent.DailyCoinType.ToString(),
            },
            legacyLevel = legacy > 0 && pending is null && !VipWallet.HasUnconfirmed(context) ? (float?)legacy : null,
            offers,
            pending = pending is null ? null : new
            {
                pending.OperationId,
                levelId = pending.Plan.LevelId,
                pending.Plan.Days,
                offerToken = VipOfferToken.FromIntent(pending),
            },
            daily = new { available = active?.Intent.DailyCoins > 0 && !claimed, claimed },
            status,
            rule = rule?.ToString(),
        };
        return JsonSerializer.Serialize(snapshot, Options);
    }
}

// <copyright file="VipAdministration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.Persistence;

/// <summary>Applies trusted host grants through the same idempotent, timed entitlement transaction as purchases.</summary>
/// <remarks>The caller must authorize and audit the host, then own an offline account reservation or live persistence lock.</remarks>
/// <param name="configuration">The host's configured bilingual benefits.</param>
/// <param name="store">The shared VIP journal.</param>
/// <param name="gameConfiguration">The canonical persisted attribute definitions.</param>
/// <param name="logger">The operational logger.</param>
public sealed class VipAdministration(VipConfiguration configuration, VipStateStore store, GameConfiguration gameConfiguration, ILogger logger)
{
    /// <summary>Grants a configured level without spending coins; retry binds the same actor, reason, level and duration.</summary>
    /// <param name="context">The exclusively owned account context.</param>
    /// <param name="account">The authorized target account.</param>
    /// <param name="operationId">Stable host operation identifier.</param>
    /// <param name="levelId">The configured level.</param>
    /// <param name="days">Days to add, bounded to the same supported duration as purchases.</param>
    /// <param name="attribution">Authenticated host and required reason.</param>
    /// <param name="nowUtc">The server's current UTC instant.</param>
    /// <param name="save">Commits this same account context.</param>
    /// <returns>The confirmed, pending or refused outcome.</returns>
    public ValueTask<VipWalletResult> GrantAsync(IPlayerContext context, Account account, Guid operationId, int levelId, int days, VipAdministrativeGrant attribution, DateTime nowUtc, Func<ValueTask<bool>> save)
    {
        if (!attribution.IsValid || days is <= 0 or > 3650)
        {
            return ValueTask.FromResult(new VipWalletResult("Refused", VipRuleResult.Invalid));
        }

        var level = configuration.Levels?.SingleOrDefault(value => value.Id == levelId);
        var hostOffer = new VipConfiguration
        {
            // Turning off purchases does not prevent an explicitly authorized host from granting time.
            Enabled = true,
            Levels = level is null ? [] :
            [
                new VipLevel
                {
                    Id = level.Id,
                    NameEn = level.NameEn,
                    NameVi = level.NameVi,
                    ExperiencePercent = level.ExperiencePercent,
                    ZenPercent = level.ZenPercent,
                    DailyCoins = level.DailyCoins,
                    DailyCoinType = level.DailyCoinType,
                    Offers = [new(days, 0, CashShopCoinType.WCoinC)],
                },
            ],
        };
        var wallet = new VipWallet(hostOffer, store, gameConfiguration, logger);
        return wallet.PurchaseFromHostAsync(context, account, operationId, levelId, days, nowUtc, save, attribution);
    }
}

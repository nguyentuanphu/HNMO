// <copyright file="VipOfferToken.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Detects changed offers or subscription bases between review and purchase; this public hash is not authorization.</summary>
public static class VipOfferToken
{
    /// <summary>Fingerprints the values displayed to one authenticated account.</summary>
    /// <param name="accountId">The persistent account.</param>
    /// <param name="level">The reviewed level and benefits.</param>
    /// <param name="offer">The reviewed duration and price.</param>
    /// <param name="previousUntilUtc">The active subscription expiry, or null for a new subscription.</param>
    /// <returns>The public SHA-256 offer version.</returns>
    public static string Create(Guid accountId, VipLevel level, VipOffer offer, DateTime? previousUntilUtc)
        => Hash([accountId, level.Id, offer.Days, offer.Price, offer.CoinType, previousUntilUtc, level.NameEn, level.NameVi, level.ExperiencePercent, level.ZenPercent, level.DailyCoins, level.DailyCoinType]);

    /// <summary>Reads the same fingerprint from the durable server-priced intent, including its original benefits.</summary>
    /// <param name="intent">The saved purchase selection.</param>
    /// <returns>The original reviewed offer version.</returns>
    public static string FromIntent(VipWalletIntent intent)
        => Hash([intent.AccountId, intent.Plan.LevelId, intent.Plan.Days, intent.Plan.Price, intent.Plan.CoinType, intent.Plan.PreviousUntilUtc, intent.NameEn, intent.NameVi, intent.ExperiencePercent, intent.ZenPercent, intent.DailyCoins, intent.DailyCoinType]);

    private static string Hash(object?[] values) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values)));
}

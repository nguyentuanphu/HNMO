// <copyright file="OfflineTradeEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.OfflineTrade;

/// <summary>
/// Pure validation and expiry rules for <see cref="OfflineTradePlugIn"/>.
/// </summary>
public static class OfflineTradeEngine
{
    /// <summary>
    /// Evaluates whether a player can transition into <c>/offtrade</c>.
    /// </summary>
    /// <param name="config">The active plug-in configuration.</param>
    /// <param name="isPlugInActive">Whether <see cref="OfflineTradePlugIn"/> is enabled.</param>
    /// <param name="hasCharacter">Whether a character and map are present.</param>
    /// <param name="isAlive">Whether the character is alive.</param>
    /// <param name="isInMiniGame">Whether the character is currently in a mini-game.</param>
    /// <param name="alreadyOffline">Whether the account already has an active offline session.</param>
    /// <param name="isStoreOpen">Whether the personal store is currently open.</param>
    /// <param name="storeItemCount">The number of items currently inside the store.</param>
    /// <param name="isSafeZone">Whether the character is currently inside a safe zone tile.</param>
    /// <param name="mapNumber">Current map number.</param>
    /// <param name="level">Character level.</param>
    /// <param name="zen">Character inventory Zen.</param>
    /// <param name="usesNonZenCurrency">Whether any store item is priced in coins or jewels rather than Zen.</param>
    /// <returns>The validation decision.</returns>
    public static OfflineTradeDecision EvaluateStart(
        OfflineTradeConfiguration config,
        bool isPlugInActive,
        bool hasCharacter,
        bool isAlive,
        bool isInMiniGame,
        bool alreadyOffline,
        bool isStoreOpen,
        int storeItemCount,
        bool isSafeZone,
        short mapNumber,
        int level,
        long zen,
        bool usesNonZenCurrency)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!isPlugInActive)
        {
            return Deny(OfflineTradeDenialReason.Disabled);
        }

        if (!hasCharacter)
        {
            return Deny(OfflineTradeDenialReason.NoCharacterSelected);
        }

        if (!isAlive)
        {
            return Deny(OfflineTradeDenialReason.NotAlive);
        }

        if (isInMiniGame)
        {
            return Deny(OfflineTradeDenialReason.InMiniGame);
        }

        if (alreadyOffline)
        {
            return Deny(OfflineTradeDenialReason.AlreadyOffline);
        }

        if (!isStoreOpen)
        {
            return Deny(OfflineTradeDenialReason.StoreNotOpen);
        }

        if (storeItemCount <= 0)
        {
            return Deny(OfflineTradeDenialReason.StoreEmpty);
        }

        if (config.SafeZoneOnly && !isSafeZone)
        {
            return Deny(OfflineTradeDenialReason.NotInSafeZone);
        }

        if (config.AllowedMapNumbers is { Count: > 0 } && !config.AllowedMapNumbers.Contains(mapNumber))
        {
            return Deny(OfflineTradeDenialReason.MapNotAllowed);
        }

        if (level < Math.Max(1, config.MinimumLevel))
        {
            return Deny(OfflineTradeDenialReason.LevelTooLow);
        }

        if (!config.AllowCoinCurrencies && usesNonZenCurrency)
        {
            return Deny(OfflineTradeDenialReason.CoinCurrenciesNotAllowed);
        }

        var fee = Math.Max(0, config.OpenFeeZen);
        if (fee > 0 && zen < fee)
        {
            return Deny(OfflineTradeDenialReason.NotEnoughZen, fee);
        }

        return new OfflineTradeDecision(
            true,
            OfflineTradeDenialReason.None,
            fee,
            Math.Max(0, config.MaxDurationHours),
            config.StopWhenSoldOut);
    }

    /// <summary>
    /// Determines whether an offline shop started at <paramref name="startedAtUtc"/> has expired.
    /// </summary>
    /// <param name="startedAtUtc">The UTC start timestamp.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <param name="maxDurationHours">Configured lifetime in hours (<c>0</c> = never expires).</param>
    /// <returns><c>true</c> if expired.</returns>
    public static bool IsExpired(DateTime startedAtUtc, DateTime nowUtc, int maxDurationHours) =>
        maxDurationHours > 0 && nowUtc - startedAtUtc >= TimeSpan.FromHours(maxDurationHours);

    private static OfflineTradeDecision Deny(OfflineTradeDenialReason reason, int fee = 0) =>
        new(false, reason, fee, 0, true);
}

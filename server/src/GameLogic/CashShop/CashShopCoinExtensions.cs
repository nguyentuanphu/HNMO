// <copyright file="CashShopCoinExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.CashShop;

using HNMO.Persistence;

/// <summary>
/// Extensions for the cash shop coins of an account.
/// </summary>
public static class CashShopCoinExtensions
{
    /// <summary>
    /// Gets the balance of a coin type of the account.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="coinType">The coin type.</param>
    /// <returns>The balance.</returns>
    public static int GetCashShopCoins(this Account account, CashShopCoinType coinType)
    {
        return coinType switch
        {
            CashShopCoinType.WCoinC => account.WCoinC,
            CashShopCoinType.WCoinP => account.WCoinP,
            CashShopCoinType.GoblinPoints => account.GoblinPoints,
            _ => throw new ArgumentOutOfRangeException(nameof(coinType), coinType, null),
        };
    }

    /// <summary>
    /// Sets the balance of a coin type of the account.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="coinType">The coin type.</param>
    /// <param name="value">The new balance.</param>
    public static void SetCashShopCoins(this Account account, CashShopCoinType coinType, int value)
    {
        switch (coinType)
        {
            case CashShopCoinType.WCoinC:
                account.WCoinC = value;
                break;
            case CashShopCoinType.WCoinP:
                account.WCoinP = value;
                break;
            case CashShopCoinType.GoblinPoints:
                account.GoblinPoints = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(coinType), coinType, null);
        }
    }

    /// <summary>Applies one owned grant once in a tracked context, including a retry after an uncertain save.</summary>
    /// <param name="account">The owning account.</param>
    /// <param name="grant">The grant returned by persistence, which may already be marked in its change tracker.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>
    /// Whether this call applied the balance change; the caller still saves account and marker together. A grant
    /// which would take the balance above <see cref="int.MaxValue"/> isn't applied and stays waiting.
    /// </returns>
    public static bool TryApplyCashShopCoinGrant(this Account account, CashShopCoinGrant grant, DateTime nowUtc)
    {
        if (grant.AppliedAt is not null || grant.AccountId != account.GetId() || !Enum.IsDefined(grant.CoinType))
        {
            return false;
        }

        var balance = (long)account.GetCashShopCoins(grant.CoinType) + grant.Amount;
        if (balance > int.MaxValue)
        {
            // A balance holds at most int.MaxValue coins. The grant isn't cut to fit: it waits, whole, until the
            // player has spent enough for it to fit.
            return false;
        }

        account.SetCashShopCoins(grant.CoinType, (int)Math.Max(balance, 0));
        grant.AppliedAt = nowUtc;
        return true;
    }

    /// <summary>
    /// Applies the pending coin grants to the balances of the account of the player,
    /// and marks them as applied in the same save.
    /// </summary>
    /// <remarks>
    /// A negative grant takes coins, but the balance doesn't go below zero.
    /// If the save fails, the changes stay in the context of the player and are committed by its next save.
    /// </remarks>
    /// <param name="player">The player.</param>
    /// <returns>A value task which completes when the grants are applied.</returns>
    public static async ValueTask ApplyPendingCashShopCoinGrantsAsync(this Player player)
    {
        if (player.Account is not { } account)
        {
            return;
        }

        await player.RunPersistenceExclusiveAsync(async () =>
        {
            var grants = await player.PersistenceContext.GetPendingCashShopCoinGrantsAsync(account.GetId()).ConfigureAwait(false);
            if (grants.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var grant in grants)
            {
                if (!Enum.IsDefined(grant.CoinType))
                {
                    // It stays pending, so it doesn't block the other grants and can be corrected in the database.
                    player.Logger.LogWarning("The coin grant {grant} of player {player} has the unknown coin type {coinType}; it's skipped.", grant.GetId(), player, grant.CoinType);
                    continue;
                }

                if (!account.TryApplyCashShopCoinGrant(grant, now))
                {
                    continue;
                }

                player.Logger.LogInformation(
                    "Applied the grant of {amount} {coinType} to the account of player {player}, reason: {reason}.",
                    grant.Amount,
                    grant.CoinType,
                    player,
                    grant.Reason);
            }

            await player.SaveProgressAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}

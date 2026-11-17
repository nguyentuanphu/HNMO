// <copyright file="VipPendingCoins.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.GameLogic.CashShop;
using HNMO.Persistence;

/// <summary>Settles earlier coin changes before a VIP charge, retaining the original account/grant save boundary.</summary>
public static class VipPendingCoins
{
    /// <summary>Applies pending grants without repeating a change already marked in the same tracked context.</summary>
    /// <param name="context">The exclusively owned account context.</param>
    /// <param name="account">The authenticated account.</param>
    /// <param name="nowUtc">The server's current UTC instant.</param>
    /// <param name="save">Saves this same context; false leaves its mutations for the next retry.</param>
    /// <returns>Whether the prior coin changes are confirmed before a new purchase may begin.</returns>
    public static async ValueTask<bool> ApplyAsync(IPlayerContext context, Account account, DateTime nowUtc, Func<ValueTask<bool>> save)
    {
        var rows = await context.GetPendingCashShopCoinGrantsAsync(account.GetId()).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return true;
        }

        foreach (var grant in rows)
        {
            account.TryApplyCashShopCoinGrant(grant, nowUtc);
        }

        // Even if every row was already marked locally, an earlier failed save still needs flushing.
        return await save().ConfigureAwait(false);
    }
}

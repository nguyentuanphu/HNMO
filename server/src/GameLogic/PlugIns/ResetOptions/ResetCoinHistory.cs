// <copyright file="ResetCoinHistory.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.GameLogic.CashShop;
using HNMO.Persistence;

/// <summary>Wires already-paid reset costs to the same account save.</summary>
public static class ResetCoinHistory
{
    /// <summary>Records each actual coin cost; this never takes coins or saves the context.</summary>
    /// <param name="context">The existing payment context.</param>
    /// <param name="account">The account which paid.</param>
    /// <param name="plan">The paid plan.</param>
    /// <param name="reason">The payment source.</param>
    public static void Record(IContext context, Account account, ResetPlan plan, CashShopBalanceReason reason)
    {
        foreach (var coin in plan.Coins)
        {
            CashShopBalanceReceipt.Record(context, account, coin.CoinType, -coin.Amount, reason);
        }
    }
}

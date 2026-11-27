// <copyright file="ResetTexts.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Globalization;
using HNMO.DataModel.Configuration;

/// <summary>
/// The players' texts of the reset options, in English and Vietnamese.
/// </summary>
public static class ResetTexts
{
    /// <summary>
    /// The name of a coin type.
    /// </summary>
    /// <param name="coinType">The coin type.</param>
    /// <returns>The name.</returns>
    public static string CoinName(CashShopCoinType coinType) => coinType switch
    {
        CashShopCoinType.WCoinC => "W Coin (C)",
        CashShopCoinType.WCoinP => "W Coin (P)",
        _ => "Goblin Points",
    };

    /// <summary>
    /// The costs of a plan, e.g. "5,000,000 zen + 10 Goblin Points + Jewel of Chaos x2".
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The text.</returns>
    public static string CostText(ResetPlan plan, bool vietnamese, Func<short, short, string?>? itemName)
    {
        var parts = new List<string>();
        if (plan.Zen > 0)
        {
            parts.Add($"{plan.Zen.ToString("N0", CultureInfo.InvariantCulture)} zen");
        }

        parts.AddRange(plan.Coins.Select(c => $"{c.Amount.ToString("N0", CultureInfo.InvariantCulture)} {CoinName(c.CoinType)}"));
        parts.AddRange(plan.Items.Select(i => $"{ItemText(i, itemName)} x{i.Count}"));
        return parts.Count == 0 ? (vietnamese ? "miễn phí" : "free") : string.Join(" + ", parts);
    }

    /// <summary>
    /// The text of an item cost without the count.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The text.</returns>
    public static string ItemText(ResetItemCost item, Func<short, short, string?>? itemName) =>
        (itemName?.Invoke(item.Group, item.Number) ?? $"{item.Group}/{item.Number}") + (item.MinimumLevel > 0 ? $" +{item.MinimumLevel}" : string.Empty);

    /// <summary>
    /// The text of a plan which can't be done, or of a done reset or relife.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <param name="done">Whether it was done (else the result says why not).</param>
    /// <returns>The text.</returns>
    public static string ResultText(ResetPlan plan, bool vietnamese, Func<short, short, string?>? itemName, bool done = false)
    {
        var cost = CostText(plan, vietnamese, itemName);
        if (done || plan.Result == ResetResult.Ok)
        {
            var points = plan.PointsMode switch
            {
                ResetPointsMode.Total => vietnamese ? $" Điểm tự do: {plan.Points.ToString("N0", CultureInfo.InvariantCulture)}." : $" Free points: {plan.Points.ToString("N0", CultureInfo.InvariantCulture)}.",
                ResetPointsMode.Add => vietnamese ? $" +{plan.Points.ToString("N0", CultureInfo.InvariantCulture)} điểm." : $" +{plan.Points.ToString("N0", CultureInfo.InvariantCulture)} points.",
                _ => string.Empty,
            };
            var vip = plan.Vip ? " (VIP)" : string.Empty;
            if (!done)
            {
                return plan.IsRelife
                    ? (vietnamese ? $"Có thể relife {plan.RelifesAfter}{vip}: {cost}." : $"Relife {plan.RelifesAfter} is ready{vip}: {cost}.")
                    : (vietnamese ? $"Có thể reset lần {plan.ResetsAfter}{vip}: {cost}." : $"Reset {plan.ResetsAfter} is ready{vip}: {cost}.");
            }

            return plan.IsRelife
                ? (vietnamese ? $"Relife thành công! Relife {plan.RelifesAfter}, reset còn {plan.ResetsAfter}{vip}.{points}" : $"Relife done! Relife {plan.RelifesAfter}, resets now {plan.ResetsAfter}{vip}.{points}")
                : (vietnamese ? $"Reset thành công! Lần reset thứ {plan.ResetsAfter}{vip}.{points}" : $"Reset done! This is reset {plan.ResetsAfter}{vip}.{points}");
        }

        var limit = plan.DailyLimit.ToString(CultureInfo.InvariantCulture);
        return plan.Result switch
        {
            ResetResult.Off => vietnamese ? "Hệ thống reset đang tắt." : "Resets are switched off.",
            ResetResult.NoTier => vietnamese ? "Không có mức reset nào cho số lần reset của bạn." : "There is no reset tier for your reset count.",
            ResetResult.MaximumResets => vietnamese ? $"Bạn đã đạt số lần reset tối đa ({plan.Maximum})." : $"You reached the most resets ({plan.Maximum}).",
            ResetResult.LevelTooLow => plan.IsRelife
                ? (vietnamese ? $"Relife cần cấp {plan.RequiredLevel}." : $"A relife needs level {plan.RequiredLevel}.")
                : (vietnamese ? $"Reset cần cấp {plan.RequiredLevel}." : $"A reset needs level {plan.RequiredLevel}."),
            ResetResult.DailyLimit => vietnamese ? $"Hôm nay bạn đã reset {plan.Today}/{limit} lần. Hãy quay lại vào ngày mai." : $"You did {plan.Today}/{limit} resets today. Come back tomorrow.",
            ResetResult.DelegatedDailyLimit => vietnamese ? $"Ủy thác reset hôm nay đã đủ {plan.TodayDelegated}/{plan.DelegatedDailyLimit} lần." : $"The delegated resets of today are done ({plan.TodayDelegated}/{plan.DelegatedDailyLimit}).",
            ResetResult.NotEnoughZen => vietnamese ? $"Không đủ zen. Cần: {cost}." : $"Not enough zen. It costs {cost}.",
            ResetResult.NotEnoughCoins => vietnamese ? $"Không đủ coin. Cần: {cost}." : $"Not enough coins. It costs {cost}.",
            ResetResult.NotEnoughItems => vietnamese
                ? $"Thiếu vật phẩm: {(plan.MissingItem is { } m ? $"{ItemText(m, itemName)} x{m.Count}" : "-")}."
                : $"Items missing: {(plan.MissingItem is { } m2 ? $"{ItemText(m2, itemName)} x{m2.Count}" : "-")}.",
            ResetResult.RelifeOff => vietnamese ? "Relife đang tắt." : "Relifes are switched off.",
            ResetResult.ResetsTooLow => vietnamese ? $"Relife cần {plan.ResetsNeeded} lần reset." : $"A relife needs {plan.ResetsNeeded} resets.",
            ResetResult.MaximumRelifes => vietnamese ? $"Bạn đã đạt số relife tối đa ({plan.Maximum})." : $"You reached the most relifes ({plan.Maximum}).",
            ResetResult.RelifeDailyLimit => vietnamese ? $"Hôm nay bạn đã relife {plan.Today}/{limit} lần." : $"You did {plan.Today}/{limit} relifes today.",
            ResetResult.Online => vietnamese ? "Hãy thoát game (và tắt ủy thác/offline) trước." : "Leave the game (and stop offline leveling) first.",
            _ => vietnamese ? "Không được phép." : "Not allowed.",
        };
    }

    /// <summary>
    /// The text about delegated resets done while the player was away.
    /// </summary>
    /// <param name="count">The number of delegated resets.</param>
    /// <param name="resets">The resets now.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <returns>The text.</returns>
    public static string AwayText(int count, int resets, bool vietnamese) => vietnamese
        ? $"Ủy thác reset: máy chủ đã reset {count} lần khi bạn treo máy. Số lần reset hiện tại: {resets}."
        : $"Delegated reset: the server did {count} resets while you were offline leveling. Resets now: {resets}.";

    /// <summary>
    /// The text of the delegated reset switch.
    /// </summary>
    /// <param name="on">On or off.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <returns>The text.</returns>
    public static string DelegatedText(bool on, bool vietnamese) => on
        ? (vietnamese ? "Đã bật ủy thác reset: khi treo máy (/offlevel) máy chủ sẽ tự reset khi đủ cấp." : "Delegated reset is on: while offline leveling (/offlevel), the server resets you when you reach the level.")
        : (vietnamese ? "Đã tắt ủy thác reset." : "Delegated reset is off.");
}

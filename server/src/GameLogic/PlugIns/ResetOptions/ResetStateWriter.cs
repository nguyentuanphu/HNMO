// <copyright file="ResetStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes the reset options as JSON: a character's state for the game client (HnmoResetsState) and the account page,
/// and the public rankings for the website. Non-ASCII text is escaped, so the JSON is plain ASCII.
/// </summary>
public static class ResetStateWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The state of a character: resets, relifes, today's counts and limits, the delegated reset and the next reset and
    /// relife with their needs and costs.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="subject">The character.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The object to serialize.</returns>
    public static object State(ResetEngine engine, ResetSubject subject, DateTime nowUtc, Func<short, short, string?>? itemName)
    {
        var record = engine.RecordOf(subject.CharacterId, nowUtc);
        var next = engine.PlanReset(subject, nowUtc, ResetSource.Game);
        var relife = engine.PlanRelife(subject, nowUtc);
        var configuration = engine.Configuration;
        return new
        {
            name = subject.Name,
            level = subject.Level,
            resets = subject.Resets,
            relifes = record.Relifes,
            vip = engine.IsVip(subject.VipLevel),
            today = record.DayResets,
            dailyLimit = next.DailyLimit,
            todayRelifes = record.DayRelifes,
            relifeDailyLimit = relife.DailyLimit,
            month = engine.MonthlyRanking(engine.Clock.MonthKey(nowUtc), int.MaxValue).FirstOrDefault(e => e.CharacterId == subject.CharacterId)?.Value ?? 0,
            delegated = new
            {
                allowed = configuration.Delegated.Enabled,
                on = record.Delegated,
                today = record.DayDelegated,
                dailyLimit = configuration.Delegated.DailyLimit,
                blocked = record.DelegatedBlocked.Length > 0 ? record.DelegatedBlocked : null,
            },
            next = Plan(next, itemName),
            relife = configuration.Relife.Enabled ? Plan(relife, itemName) : null,
        };
    }

    /// <summary>
    /// Writes the state of a character for the game client, with what happened.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="subject">The character.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <param name="eventType">What happened: "reset", "relife", "delegated" (a delegated reset while offline leveling), "away" (delegated resets done while away), "switch" (the delegated reset was switched), "info"; <c>null</c> for nothing.</param>
    /// <param name="result">The result of the event (a <see cref="ResetResult"/> name).</param>
    /// <param name="count">A number of the event (the delegated resets while away).</param>
    /// <returns>The JSON.</returns>
    public static string WriteState(ResetEngine engine, ResetSubject subject, DateTime nowUtc, Func<short, short, string?>? itemName, string? eventType, ResetResult? result = null, int count = 0)
    {
        var state = new
        {
            v = 1,
            on = true,
            character = State(engine, subject, nowUtc, itemName),
            @event = eventType is null ? null : new { type = eventType, result = result?.ToString(), count },
        };
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>
    /// Writes the public rankings for the website: the resets of this month (and the last one) and the relifes.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The JSON.</returns>
    public static string WritePublic(ResetEngine engine, DateTime nowUtc)
    {
        var size = Math.Clamp(engine.Configuration.RankingSize, 5, 500);
        var month = engine.Clock.MonthKey(nowUtc);
        var day = engine.Clock.Day(nowUtc);
        var previous = new DateOnly(day.Year, day.Month, 1).AddMonths(-1).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var configuration = engine.Configuration;
        var state = new
        {
            v = 1,
            enabled = true,
            month,
            previousMonth = previous,
            monthly = engine.MonthlyRanking(month, size).Select(Entry),
            previous = engine.MonthlyRanking(previous, size).Select(Entry),
            relifes = engine.RelifeRanking(size).Select(Entry),
            rules = new
            {
                maximumResets = configuration.MaximumResets,
                tiers = configuration.Tiers.Select(t => new
                {
                    from = t.FromResets,
                    to = t.ToResets,
                    level = t.RequiredLevel,
                    levelAfter = t.LevelAfterReset,
                    zen = t.Zen,
                    zenTimesReset = t.ZenTimesResetNumber,
                    coins = t.Coins,
                    coinType = t.CoinType,
                    points = t.Points,
                    mode = t.PointsMode,
                    clearStats = t.ClearStats,
                    dailyLimit = t.DailyLimit,
                    items = t.Items.Count,
                }),
                vip = configuration.Vip.Enabled ? new
                {
                    levelReduction = configuration.Vip.LevelReduction,
                    zenDiscount = configuration.Vip.ZenDiscountPercent,
                    coinsDiscount = configuration.Vip.CoinsDiscountPercent,
                    extraDaily = configuration.Vip.ExtraDailyResets,
                    extraPoints = configuration.Vip.ExtraPointsPercent,
                } : null,
                relife = configuration.Relife.Enabled ? new
                {
                    resetsNeeded = configuration.Relife.ResetsNeeded,
                    level = configuration.Relife.RequiredLevel,
                    zen = configuration.Relife.Zen,
                    coins = configuration.Relife.Coins,
                    coinType = configuration.Relife.CoinType,
                    keepPercent = configuration.Relife.KeepResetsPercent,
                    pointsPerRelife = configuration.Relife.PointsPerRelife,
                    maximum = configuration.Relife.MaximumRelifes,
                    dailyLimit = configuration.Relife.DailyLimit,
                } : null,
                delegated = configuration.Delegated.Enabled,
            },
        };
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>
    /// Serializes an object like the other JSON of this writer.
    /// </summary>
    /// <param name="value">The object.</param>
    /// <returns>The JSON.</returns>
    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);

    /// <summary>
    /// A plan as JSON object.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The object.</returns>
    public static object Plan(ResetPlan plan, Func<short, short, string?>? itemName) => new
    {
        result = plan.Result,
        level = plan.RequiredLevel,
        levelAfter = plan.LevelAfter,
        resetsAfter = plan.ResetsAfter,
        relifesAfter = plan.RelifesAfter,
        resetsNeeded = plan.IsRelife ? plan.ResetsNeeded : (int?)null,
        zen = plan.Zen,
        coins = plan.Coins.Select(c => new { type = c.CoinType, amount = c.Amount }),
        items = plan.Items.Select(i => new { group = i.Group, number = i.Number, level = i.MinimumLevel, count = i.Count, name = itemName?.Invoke(i.Group, i.Number) }),
        mode = plan.PointsMode,
        points = plan.Points,
        clearStats = plan.ClearStats,
        vip = plan.Vip,
        maximum = plan.Maximum,
    };

    private static object Entry(ResetRankingEntry entry) =>
        new { rank = entry.Rank, name = entry.Name, cls = entry.Class, value = entry.Value, resets = entry.Resets, relifes = entry.Relifes };
}

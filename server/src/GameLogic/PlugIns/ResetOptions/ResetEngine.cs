// <copyright file="ResetEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The result of a reset or relife check.
/// </summary>
public enum ResetResult
{
    /// <summary>It can be done.</summary>
    Ok,

    /// <summary>Resets are switched off (no tiers).</summary>
    Off,

    /// <summary>No tier covers the reset count.</summary>
    NoTier,

    /// <summary>The character has the most resets.</summary>
    MaximumResets,

    /// <summary>The level is too low.</summary>
    LevelTooLow,

    /// <summary>The character did its resets of today.</summary>
    DailyLimit,

    /// <summary>The character did its delegated resets of today.</summary>
    DelegatedDailyLimit,

    /// <summary>Not enough zen.</summary>
    NotEnoughZen,

    /// <summary>Not enough coins.</summary>
    NotEnoughCoins,

    /// <summary>The items are missing.</summary>
    NotEnoughItems,

    /// <summary>Relifes are switched off.</summary>
    RelifeOff,

    /// <summary>Not enough resets for a relife.</summary>
    ResetsTooLow,

    /// <summary>The character has the most relifes.</summary>
    MaximumRelifes,

    /// <summary>The character did its relifes of today.</summary>
    RelifeDailyLimit,

    /// <summary>The account is in the game (website changes need it offline).</summary>
    Online,

    /// <summary>The website may not do this.</summary>
    NotAllowed,
}

/// <summary>
/// HNMO's reset rules (pure, no game objects): the tier of a reset count, VIP advantages, daily limits, the relife and
/// what each costs and gives; the store keeps relifes, daily counts, the monthly ranking and the log.
/// </summary>
public sealed class ResetEngine
{
    private const int MonthsKept = 24;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public ResetEngine(ResetOptionsConfiguration configuration, ResetStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Clock = new MissionClock(configuration.TimeZone, configuration.DayStartHour);
    }

    /// <summary>Gets the configuration.</summary>
    public ResetOptionsConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public ResetStore Store { get; }

    /// <summary>Gets the clock (Sydney days and months by default).</summary>
    public MissionClock Clock { get; }

    /// <summary>
    /// Gets the tier of a reset count (before the reset): the first which covers it.
    /// </summary>
    /// <param name="resets">The reset count.</param>
    /// <returns>The tier; <c>null</c>, if none covers it.</returns>
    public ResetTier? TierFor(int resets) => this.Configuration.Tiers.FirstOrDefault(t => t.Covers(resets));

    /// <summary>
    /// Whether the VIP advantages count for a VIP value.
    /// </summary>
    /// <param name="vipLevel">The account's VIP value.</param>
    /// <returns><c>true</c>, if they do.</returns>
    public bool IsVip(int vipLevel) => this.Configuration.Vip is { Enabled: true } vip && vipLevel >= Math.Max(1, vip.MinimumVipLevel);

    /// <summary>
    /// Gets the relifes of a character.
    /// </summary>
    /// <param name="characterId">The character's id.</param>
    /// <returns>The relifes.</returns>
    public int Relifes(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Characters.TryGetValue(characterId, out var record) ? record.Relifes : 0;
        }
    }

    /// <summary>
    /// Gets a copy of what's remembered of a character, with today's counts (zero when the day changed).
    /// </summary>
    /// <param name="characterId">The character's id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The copy.</returns>
    public ResetCharacterRecord RecordOf(string characterId, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var copy = new ResetCharacterRecord();
            if (this.Store.Data.Characters.TryGetValue(characterId, out var record))
            {
                copy.Name = record.Name;
                copy.Login = record.Login;
                copy.Class = record.Class;
                copy.Relifes = record.Relifes;
                copy.Resets = record.Resets;
                copy.Delegated = record.Delegated;
                copy.DelegatedUntold = record.DelegatedUntold;
                copy.DelegatedBlocked = record.DelegatedBlocked;
                copy.LastAtUtc = record.LastAtUtc;
                copy.DayKey = this.Clock.DayKey(nowUtc);
                if (record.DayKey == copy.DayKey)
                {
                    copy.DayResets = record.DayResets;
                    copy.DayRelifes = record.DayRelifes;
                    copy.DayDelegated = record.DayDelegated;
                }
            }

            return copy;
        }
    }

    /// <summary>
    /// Whether the delegated reset is on for a character.
    /// </summary>
    /// <param name="characterId">The character's id.</param>
    /// <returns><c>true</c>, if it is.</returns>
    public bool IsDelegated(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Characters.TryGetValue(characterId, out var record) && record.Delegated;
        }
    }

    /// <summary>
    /// Switches the delegated reset of a character on or off.
    /// </summary>
    /// <param name="subject">The character.</param>
    /// <param name="on">On or off.</param>
    public void SetDelegated(ResetSubject subject, bool on)
    {
        lock (this.Store.Lock)
        {
            var record = this.RecordLocked(subject);
            if (record.Delegated != on)
            {
                record.Delegated = on;
                record.DelegatedBlocked = string.Empty;
                this.Store.Changed();
            }
        }
    }

    /// <summary>
    /// Remembers why a delegated reset couldn't be done (shown on the website and when the player comes back).
    /// </summary>
    /// <param name="subject">The character.</param>
    /// <param name="result">The result.</param>
    public void SetDelegatedBlocked(ResetSubject subject, ResetResult result)
    {
        lock (this.Store.Lock)
        {
            var record = this.RecordLocked(subject);
            var text = result == ResetResult.Ok ? string.Empty : result.ToString();
            if (record.DelegatedBlocked != text)
            {
                record.DelegatedBlocked = text;
                this.Store.Changed();
            }
        }
    }

    /// <summary>
    /// Takes the number of delegated resets the player wasn't told about yet.
    /// </summary>
    /// <param name="characterId">The character's id.</param>
    /// <returns>The number.</returns>
    public int TakeDelegatedUntold(string characterId)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Characters.TryGetValue(characterId, out var record) || record.DelegatedUntold == 0)
            {
                return 0;
            }

            var count = record.DelegatedUntold;
            record.DelegatedUntold = 0;
            this.Store.Changed();
            return count;
        }
    }

    /// <summary>
    /// Checks the next reset of a character and works out what it costs and gives.
    /// </summary>
    /// <param name="subject">The character.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="source">Where it would be done.</param>
    /// <param name="catchupDailyCap">Optional server-wide catch-up daily reset cap.</param>
    /// <returns>The plan; <see cref="ResetPlan.Result"/> says whether it can be done.</returns>
    public ResetPlan PlanReset(ResetSubject subject, DateTime nowUtc, ResetSource source, int? catchupDailyCap = null)
    {
        var configuration = this.Configuration;
        var record = this.RecordOf(subject.CharacterId, nowUtc);
        var vip = this.IsVip(subject.VipLevel);
        var plan = new ResetPlan
        {
            Vip = vip,
            ResetsAfter = subject.Resets + 1,
            RelifesAfter = record.Relifes,
            Today = record.DayResets,
            TodayDelegated = record.DayDelegated,
            DelegatedDailyLimit = configuration.Delegated.DailyLimit,
            Maximum = configuration.MaximumResets,
        };

        if (configuration.Tiers.Count == 0)
        {
            plan.Result = ResetResult.Off;
            return plan;
        }

        var tier = this.TierFor(subject.Resets);
        if (configuration.MaximumResets > 0 && subject.Resets >= configuration.MaximumResets)
        {
            plan.Result = ResetResult.MaximumResets;
            return plan;
        }

        if (tier is null)
        {
            plan.Result = ResetResult.NoTier;
            return plan;
        }

        plan.RequiredLevel = Math.Max(1, tier.RequiredLevel - (vip ? configuration.Vip.LevelReduction : 0));
        plan.LevelAfter = Math.Max(1, tier.LevelAfterReset);
        plan.ClearStats = tier.ClearStats;
        plan.PointsMode = tier.PointsMode;
        plan.DailyLimit = tier.DailyLimit > 0 ? tier.DailyLimit + (vip ? configuration.Vip.ExtraDailyResets : 0) : 0;
        if (catchupDailyCap is { } cap && cap >= 0)
        {
            plan.DailyLimit = plan.DailyLimit > 0 ? Math.Min(plan.DailyLimit, cap) : cap;
        }

        var zen = (long)Math.Max(0, tier.Zen) * (tier.ZenTimesResetNumber ? plan.ResetsAfter : 1);
        plan.Zen = vip ? Discount(zen, configuration.Vip.ZenDiscountPercent) : zen;
        var coins = new List<ResetCoinCost>();
        AddCoins(coins, tier.CoinType, vip ? (int)Discount(tier.Coins, configuration.Vip.CoinsDiscountPercent) : tier.Coins);
        if (source == ResetSource.Delegated)
        {
            plan.Zen += Math.Max(0, configuration.Delegated.ExtraZen);
            AddCoins(coins, configuration.Delegated.ExtraCoinType, configuration.Delegated.ExtraCoins);
        }

        plan.Coins = coins;
        plan.Items = tier.Items.Where(i => i.Count > 0).ToList();
        plan.Points = this.PointsAfter(tier, subject, plan, vip);

        plan.Result = subject.Level < plan.RequiredLevel ? ResetResult.LevelTooLow
            : plan.DailyLimit > 0 && plan.Today >= plan.DailyLimit ? ResetResult.DailyLimit
            : source == ResetSource.Delegated && plan.DelegatedDailyLimit > 0 && plan.TodayDelegated >= plan.DelegatedDailyLimit ? ResetResult.DelegatedDailyLimit
            : this.CostResult(subject, plan);
        return plan;
    }

    /// <summary>
    /// Checks the relife of a character and works out what it costs and gives.
    /// </summary>
    /// <param name="subject">The character.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The plan; <see cref="ResetPlan.Result"/> says whether it can be done.</returns>
    public ResetPlan PlanRelife(ResetSubject subject, DateTime nowUtc)
    {
        var options = this.Configuration.Relife;
        var record = this.RecordOf(subject.CharacterId, nowUtc);
        var vip = this.IsVip(subject.VipLevel) && this.Configuration.Vip.AppliesToRelife;
        var kept = (int)((long)subject.Resets * Math.Clamp(options.KeepResetsPercent, 0, 100) / 100);
        var plan = new ResetPlan
        {
            IsRelife = true,
            Vip = vip,
            ResetsAfter = kept,
            RelifesAfter = record.Relifes + 1,
            Today = record.DayRelifes,
            DailyLimit = Math.Max(0, options.DailyLimit),
            Maximum = Math.Max(0, options.MaximumRelifes),
            ResetsNeeded = Math.Max(1, options.ResetsNeeded),
            RequiredLevel = Math.Max(1, options.RequiredLevel - (vip ? this.Configuration.Vip.LevelReduction : 0)),
            LevelAfter = Math.Max(1, options.LevelAfterRelife),
            ClearStats = true,
            PointsMode = ResetPointsMode.Total,
            Zen = vip ? Discount(options.Zen, this.Configuration.Vip.ZenDiscountPercent) : Math.Max(0, options.Zen),
            Items = options.Items.Where(i => i.Count > 0).ToList(),
        };
        AddCoins(plan.Coins, options.CoinType, vip ? (int)Discount(options.Coins, this.Configuration.Vip.CoinsDiscountPercent) : options.Coins);
        plan.Points = this.TotalPoints(kept, plan.RelifesAfter, this.IsVip(subject.VipLevel));

        plan.Result = !options.Enabled ? ResetResult.RelifeOff
            : plan.Maximum > 0 && record.Relifes >= plan.Maximum ? ResetResult.MaximumRelifes
            : subject.Resets < plan.ResetsNeeded ? ResetResult.ResetsTooLow
            : subject.Level < plan.RequiredLevel ? ResetResult.LevelTooLow
            : plan.DailyLimit > 0 && plan.Today >= plan.DailyLimit ? ResetResult.RelifeDailyLimit
            : this.CostResult(subject, plan);
        return plan;
    }

    /// <summary>
    /// The free points of a character with a number of resets and relifes when every reset gave the tiers' points
    /// (<see cref="ResetPointsMode.Total"/>): the points of each reset, the relifes' permanent bonus and the VIP bonus.
    /// </summary>
    /// <param name="resets">The resets.</param>
    /// <param name="relifes">The relifes.</param>
    /// <param name="vip">Whether the VIP bonus counts.</param>
    /// <returns>The points.</returns>
    public int TotalPoints(int resets, int relifes, bool vip)
    {
        long total = 0;
        for (var r = 0; r < resets && total < int.MaxValue; r++)
        {
            total += Math.Max(0, this.TierFor(r)?.Points ?? 0);
        }

        total += (long)Math.Max(0, relifes) * Math.Max(0, this.Configuration.Relife.PointsPerRelife);
        if (vip)
        {
            total += total * Math.Max(0, this.Configuration.Vip.ExtraPointsPercent) / 100;
        }

        return (int)Math.Min(total, int.MaxValue);
    }

    /// <summary>
    /// Remembers a reset or relife which was done: today's counts, the month's resets, the relifes and the log.
    /// </summary>
    /// <param name="subject">The character (before).</param>
    /// <param name="plan">The plan which was done.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="source">Where it was done.</param>
    public void Record(ResetSubject subject, ResetPlan plan, DateTime nowUtc, ResetSource source)
    {
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            var record = this.RecordLocked(subject);
            var day = this.Clock.DayKey(nowUtc);
            if (record.DayKey != day)
            {
                record.DayKey = day;
                record.DayResets = 0;
                record.DayRelifes = 0;
                record.DayDelegated = 0;
            }

            if (plan.IsRelife)
            {
                record.DayRelifes++;
            }
            else
            {
                record.DayResets++;
                var month = this.Clock.MonthKey(nowUtc);
                if (!data.Months.TryGetValue(month, out var counts))
                {
                    counts = new Dictionary<string, int>(StringComparer.Ordinal);
                    data.Months[month] = counts;
                    foreach (var old in data.Months.Keys.OrderByDescending(k => k, StringComparer.Ordinal).Skip(MonthsKept).ToList())
                    {
                        data.Months.Remove(old);
                    }
                }

                counts[subject.CharacterId] = counts.GetValueOrDefault(subject.CharacterId) + 1;
            }

            if (source == ResetSource.Delegated)
            {
                record.DayDelegated++;
                record.DelegatedUntold++;
                record.DelegatedBlocked = string.Empty;
            }

            record.Relifes = plan.RelifesAfter;
            record.Resets = plan.ResetsAfter;
            record.LastAtUtc = nowUtc;
            data.Log.Insert(0, new ResetLogEntry
            {
                AtUtc = nowUtc,
                CharacterId = subject.CharacterId,
                Name = subject.Name,
                Login = subject.Login,
                Kind = plan.IsRelife ? "relife" : "reset",
                Source = source,
                LevelBefore = subject.Level,
                ResetsAfter = plan.ResetsAfter,
                RelifesAfter = plan.RelifesAfter,
                Zen = plan.Zen,
                Coins = plan.Coins.Sum(c => c.Amount),
                CoinsText = string.Join(", ", plan.Coins.Select(c => $"{c.Amount.ToString(CultureInfo.InvariantCulture)} {c.CoinType}")),
                Vip = plan.Vip,
            });
            var kept = Math.Max(10, this.Configuration.LogKept);
            if (data.Log.Count > kept)
            {
                data.Log.RemoveRange(kept, data.Log.Count - kept);
            }

            this.Store.Changed();
        }
    }

    /// <summary>
    /// Gets the reset ranking of a month.
    /// </summary>
    /// <param name="monthKey">The month (yyyy-MM).</param>
    /// <param name="size">The most places.</param>
    /// <returns>The ranking.</returns>
    public IReadOnlyList<ResetRankingEntry> MonthlyRanking(string monthKey, int size)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Months.TryGetValue(monthKey, out var counts))
            {
                return Array.Empty<ResetRankingEntry>();
            }

            var characters = this.Store.Data.Characters;
            return counts
                .Where(p => p.Value > 0)
                .Select(p => (Id: p.Key, Count: p.Value, Record: characters.GetValueOrDefault(p.Key)))
                .OrderByDescending(p => p.Count)
                .ThenBy(p => p.Record?.LastAtUtc ?? DateTime.MaxValue)
                .ThenBy(p => p.Record?.Name ?? p.Id, StringComparer.Ordinal)
                .Take(Math.Max(1, size))
                .Select((p, index) => new ResetRankingEntry(index + 1, p.Id, p.Record?.Name ?? string.Empty, p.Record?.Class ?? 0, p.Count, p.Record?.Resets ?? 0, p.Record?.Relifes ?? 0))
                .ToList();
        }
    }

    /// <summary>
    /// Gets the relife ranking: most relifes, then most resets.
    /// </summary>
    /// <param name="size">The most places.</param>
    /// <returns>The ranking.</returns>
    public IReadOnlyList<ResetRankingEntry> RelifeRanking(int size)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Characters
                .Where(p => p.Value.Relifes > 0)
                .OrderByDescending(p => p.Value.Relifes)
                .ThenByDescending(p => p.Value.Resets)
                .ThenBy(p => p.Value.LastAtUtc ?? DateTime.MaxValue)
                .Take(Math.Max(1, size))
                .Select((p, index) => new ResetRankingEntry(index + 1, p.Key, p.Value.Name, p.Value.Class, p.Value.Relifes, p.Value.Resets, p.Value.Relifes))
                .ToList();
        }
    }

    /// <summary>
    /// Gets the log, newest first.
    /// </summary>
    /// <param name="count">The most lines.</param>
    /// <param name="filter">Only lines with this in the name or login; empty for all.</param>
    /// <param name="source">Only lines of this source; <c>null</c> for all.</param>
    /// <returns>The lines.</returns>
    public IReadOnlyList<ResetLogEntry> Log(int count, string? filter = null, ResetSource? source = null)
    {
        var text = filter?.Trim() ?? string.Empty;
        lock (this.Store.Lock)
        {
            return this.Store.Data.Log
                .Where(e => (source is null || e.Source == source)
                            && (text.Length == 0 || e.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || e.Login.Contains(text, StringComparison.OrdinalIgnoreCase)))
                .Take(Math.Max(1, count))
                .ToList();
        }
    }

    private static long Discount(long amount, int percent) => Math.Max(0, amount) * (100 - Math.Clamp(percent, 0, 100)) / 100;

    private static void AddCoins(List<ResetCoinCost> coins, CashShopCoinType coinType, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var index = coins.FindIndex(c => c.CoinType == coinType);
        if (index < 0)
        {
            coins.Add(new ResetCoinCost(coinType, amount));
        }
        else
        {
            coins[index] = new ResetCoinCost(coinType, (int)Math.Min((long)coins[index].Amount + amount, int.MaxValue));
        }
    }

    private int PointsAfter(ResetTier tier, ResetSubject subject, ResetPlan plan, bool vip)
    {
        switch (tier.PointsMode)
        {
            case ResetPointsMode.Total:
                return this.TotalPoints(plan.ResetsAfter, plan.RelifesAfter, vip);
            case ResetPointsMode.Add:
                var points = (long)Math.Max(0, tier.Points);
                if (vip)
                {
                    points += points * Math.Max(0, this.Configuration.Vip.ExtraPointsPercent) / 100;
                }

                return (int)Math.Min(points, int.MaxValue);
            default:
                return 0;
        }
    }

    private ResetResult CostResult(ResetSubject subject, ResetPlan plan)
    {
        if (subject.Zen < plan.Zen)
        {
            return ResetResult.NotEnoughZen;
        }

        if (plan.Coins.Any(c => subject.Coins.GetValueOrDefault(c.CoinType) < c.Amount))
        {
            return ResetResult.NotEnoughCoins;
        }

        foreach (var item in plan.Items)
        {
            if ((subject.CountItems?.Invoke(item) ?? 0) < item.Count)
            {
                plan.MissingItem = item;
                return ResetResult.NotEnoughItems;
            }
        }

        return ResetResult.Ok;
    }

    private ResetCharacterRecord RecordLocked(ResetSubject subject)
    {
        var characters = this.Store.Data.Characters;
        if (!characters.TryGetValue(subject.CharacterId, out var record))
        {
            record = new ResetCharacterRecord { Resets = subject.Resets };
            characters[subject.CharacterId] = record;
            this.Store.Changed();
        }

        if (record.Name != subject.Name || record.Login != subject.Login || record.Class != subject.Class)
        {
            record.Name = subject.Name;
            record.Login = subject.Login;
            record.Class = subject.Class;
            this.Store.Changed();
        }

        return record;
    }
}

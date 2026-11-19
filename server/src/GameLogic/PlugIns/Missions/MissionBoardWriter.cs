// <copyright file="MissionBoardWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes the missions as JSON: the whole board of a character for the game client (HnmoMissionsState),
/// and the public list of the missions for the website. Non-ASCII text (the Vietnamese titles) is escaped, so the JSON is plain ASCII.
/// </summary>
public static class MissionBoardWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes the board of a character.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="characterId">The character id.</param>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="boardEvent">What happened, if anything.</param>
    /// <returns>The JSON.</returns>
    public static string WriteBoard(MissionEngine engine, string characterId, string loginName, DateTime nowUtc, MissionBoardEvent? boardEvent)
    {
        var configuration = engine.Configuration;
        var clock = engine.Clock;
        var missions = configuration.Missions
            .Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.Id))
            .OrderBy(m => m.Period)
            .Select(m =>
            {
                var (value, claimed) = engine.GetProgress(characterId, m, nowUtc);
                return new
                {
                    m.Id,
                    Period = m.Period == MissionPeriod.Daily ? "daily" : "weekly",
                    m.Kind,
                    m.TitleEn,
                    m.TitleVi,
                    Value = value,
                    Target = Math.Max(1, m.Target),
                    Claimed = claimed,
                    Reward = Reward(m.Reward),
                };
            })
            .ToList();
        var streak = engine.GetStreak(characterId, nowUtc);
        var calendar = engine.GetCalendar(loginName, nowUtc);
        var earned = engine.GetEarnedToday(loginName, nowUtc);
        var daysInMonth = clock.DaysInMonth(nowUtc);
        var invite = configuration.Invite;
        MissionAccountState account;
        lock (engine.Store.Lock)
        {
            account = engine.Store.Account(loginName);
        }

        var board = new
        {
            V = 1,
            Now = ToMs(nowUtc),
            Day = clock.DayKey(nowUtc),
            Week = clock.WeekKey(nowUtc),
            NextDaily = ToMs(clock.NextDayStart(nowUtc)),
            NextWeekly = ToMs(clock.NextWeekStart(nowUtc)),
            Missions = missions,
            Streak = new
            {
                streak.Days,
                streak.TodayCounted,
                Required = engine.StreakRequired,
                BonusDay = Math.Max(2, configuration.StreakBonusDay),
                streak.BonusClaimable,
                Bonus = Reward(configuration.StreakBonus),
            },
            Calendar = new
            {
                configuration.LoginCalendar.Enabled,
                Month = clock.MonthKey(nowUtc),
                calendar.ClaimedDays,
                calendar.ClaimedToday,
                DaysInMonth = daysInMonth,
                configuration.LoginCalendar.CoinType,
                Rewards = Enumerable.Range(1, daysInMonth).Select(configuration.LoginCalendar.CoinsOf).ToList(),
            },
            Online = new
            {
                configuration.OnlineTime.Enabled,
                CoinsToday = earned.Online,
                Cap = configuration.OnlineTime.DailyCap,
                MinutesToday = earned.OnlineMinutes,
                configuration.OnlineTime.CoinsPerHour,
                configuration.OnlineTime.CoinType,
                AutoPlayHalf = configuration.OnlineTime.AutoPlayCountsHalf,
            },
            Earned = new { Today = earned.Earned, Cap = configuration.EarnedDailyCap, Owed = earned.Owed },
            Milestones = new
            {
                configuration.Milestones.Enabled,
                configuration.Milestones.CoinType,
                Levels = configuration.Milestones.Levels.OrderBy(l => l.Level).Select(l => new { l.Level, l.Coins }).ToList(),
                configuration.Milestones.CoinsPerReset,
            },
            Invite = new
            {
                invite.Enabled,
                Code = invite.Enabled ? engine.InviteCodeOf(loginName) : null,
                account.InvitedBy,
                Rewarded = account.InviteRewarded,
                invite.RequiredLevel,
                invite.InviterCoins,
                invite.InvitedCoins,
                invite.CoinType,
            },
            Event = boardEvent,
        };
        return JsonSerializer.Serialize(board, Options);
    }

    /// <summary>
    /// Writes the public list of the missions (no player data), for the website.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="itemName">Gives the name of a reward item, if known.</param>
    /// <returns>The JSON.</returns>
    public static string WritePublic(MissionsConfiguration configuration, DateTime nowUtc, Func<short, short, string?>? itemName = null)
    {
        var clock = new MissionClock(configuration.TimeZone, configuration.ResetHour);
        var value = new
        {
            Day = clock.DayKey(nowUtc),
            Week = clock.WeekKey(nowUtc),
            NextDaily = ToMs(clock.NextDayStart(nowUtc)),
            NextWeekly = ToMs(clock.NextWeekStart(nowUtc)),
            configuration.ResetHour,
            configuration.TimeZone,
            Missions = configuration.Missions
                .Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.Id))
                .OrderBy(m => m.Period)
                .Select(m => new { m.Id, Period = m.Period == MissionPeriod.Daily ? "daily" : "weekly", m.Kind, m.TitleEn, m.TitleVi, Target = Math.Max(1, m.Target), Reward = Reward(m.Reward, itemName) })
                .ToList(),
            Streak = new { BonusDay = Math.Max(2, configuration.StreakBonusDay), Bonus = Reward(configuration.StreakBonus, itemName) },
            EarnedDailyCap = configuration.EarnedDailyCap,
        };
        return JsonSerializer.Serialize(value, Options);
    }

    private static object Reward(MissionReward reward, Func<short, short, string?>? itemName = null) => new
    {
        reward.Zen,
        reward.Coins,
        reward.CoinType,
        Items = reward.Items.Where(i => i.Count > 0).Select(i => new { i.Group, i.Number, i.Level, i.Count, Name = itemName?.Invoke(i.Group, i.Number) }).ToList(),
    };

    private static long ToMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}

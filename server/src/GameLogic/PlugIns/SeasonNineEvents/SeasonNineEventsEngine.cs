// <copyright file="SeasonNineEventsEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Persistence;

/// <summary>
/// Pure helper logic and canonical Season 9 event schedules/specs for Acheron Guardian, Arca Battle,
/// Chaos Castle Survival, Tormented Square, Invasion Manager, and Uruk Mountain encounters.
/// </summary>
public static class SeasonNineEventsEngine
{
    /// <summary>Tournament stage names shared by Chaos Castle Survival and Tormented Square.</summary>
    public static class TournamentStage
    {
        /// <summary>Outside active tournament days (practice / standard mode).</summary>
        public const string Practice = "Practice";

        /// <summary>Elimination qualifier stage (Stage 1).</summary>
        public const string Elimination = "Elimination";

        /// <summary>Semi-final stage (Stage 2).</summary>
        public const string SemiFinal = "SemiFinal";

        /// <summary>Championship final stage (Stage 3).</summary>
        public const string Final = "Final";
    }

    /// <summary>Describes a fixed obelisk / guardian tower location from the Season 9 server suite XMLs.</summary>
    /// <param name="Index">Obelisk index (1..3).</param>
    /// <param name="MapNumber">Target map number (92 or 96).</param>
    /// <param name="MonsterNumber">Monster definition number (<c>598</c> Fire Tower / obelisk).</param>
    /// <param name="X">Spawn X coordinate.</param>
    /// <param name="Y">Spawn Y coordinate.</param>
    public readonly record struct ObeliskSpec(byte Index, short MapNumber, short MonsterNumber, byte X, byte Y);

    /// <summary>Describes one monster wave round in Tormented Square.</summary>
    /// <param name="RoundNumber">1-based round number.</param>
    /// <param name="MonsterNumber">Monster definition number.</param>
    /// <param name="Count">Configured wave monster count.</param>
    /// <param name="IsBossRound">Whether this round spawns an event boss.</param>
    /// <param name="PointReward">Point value awarded on completion.</param>
    public readonly record struct TormentedRoundSpec(int RoundNumber, short MonsterNumber, int Count, bool IsBossRound, int PointReward);

    /// <summary>Obelisk locations shared by Acheron Guardian and Arca Battle (Alkmaar, Ubaid, Debenter).</summary>
    public static IReadOnlyList<ObeliskSpec> AcheronObelisks { get; } =
    [
        new(1, 92, 598, 89, 41),
        new(2, 92, 598, 188, 101),
        new(3, 96, 598, 162, 54),
    ];

    /// <summary>Elemental defender monster IDs spawned around Acheron Guardian obelisks (632..641).</summary>
    public static IReadOnlyList<short> AcheronGuardianMonsterIds { get; } =
    [
        632, 633, 634, 635, 636, 637, 638, 639, 640, 641,
    ];

    /// <summary>Elemental monsters defending Alkmaar/Ubaid/Debenter obelisks during Arca Battle.</summary>
    public static IReadOnlyList<short> ArcaBattleMonsterIds { get; } =
    [
        592, 593, 594, 595, 596, 611, 612, 613, 614, 615,
    ];

    /// <summary>Monsters spawned on Chaos Castle Survival map 97 (644, 645).</summary>
    public static IReadOnlyList<short> ChaosCastleSurvivalMonsterIds { get; } = [644, 645];

    /// <summary>Uruk Mountain elemental boss guard IDs defending Lord Silvester (673).</summary>
    public static IReadOnlyList<short> UrukMountainGuardMonsterIds { get; } = [678, 679, 680];

    /// <summary>The 15 Tormented Square wave rounds from the Season 9 suite configuration.</summary>
    public static IReadOnlyList<TormentedRoundSpec> TormentedSquareRounds { get; } =
    [
        new(1, 572, 130, false, 300),
        new(2, 344, 130, false, 600),
        new(3, 549, 130, false, 900),
        new(4, 575, 130, false, 1200),
        new(5, 315, 50, true, 3000),
        new(6, 550, 130, false, 1500),
        new(7, 552, 130, false, 1800),
        new(8, 314, 130, false, 2100),
        new(9, 335, 130, false, 2400),
        new(10, 557, 50, true, 10000),
        new(11, 554, 130, false, 2700),
        new(12, 336, 130, false, 3000),
        new(13, 337, 130, false, 3300),
        new(14, 556, 130, false, 5000),
        new(15, 340, 50, true, 200000),
    ];

    /// <summary>Parses a comma-separated weekday list (e.g. <c>Sun,Mon,Wed</c>) and checks whether <paramref name="day"/> matches.</summary>
    /// <param name="allowedDaysCsv">Comma-separated day names; empty means all days allowed.</param>
    /// <param name="day">Local weekday.</param>
    /// <returns><see langword="true"/> if active on <paramref name="day"/>.</returns>
    public static bool IsDayAllowed(string? allowedDaysCsv, DayOfWeek day)
    {
        if (string.IsNullOrWhiteSpace(allowedDaysCsv))
        {
            return true;
        }

        var shortName = day.ToString()[..3];
        var fullName = day.ToString();
        var dayNum = ((int)day).ToString(CultureInfo.InvariantCulture);
        foreach (var token in allowedDaysCsv.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Equals(shortName, StringComparison.OrdinalIgnoreCase)
                || token.Equals(fullName, StringComparison.OrdinalIgnoreCase)
                || token.Equals(dayNum, StringComparison.OrdinalIgnoreCase)
                || (day == DayOfWeek.Thursday && token.Equals("Thur", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses a comma-separated day-of-month list into a set of valid integers (1..31).</summary>
    /// <param name="csv">Comma-separated numbers.</param>
    /// <returns>Parsed day set.</returns>
    public static HashSet<int> ParseDayNumbers(string? csv)
    {
        var result = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            return result;
        }

        foreach (var token in csv.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) && d is >= 1 and <= 31)
            {
                result.Add(d);
            }
        }

        return result;
    }

    /// <summary>Resolves current tournament stage for <paramref name="dayOfMonth"/>.</summary>
    /// <param name="dayOfMonth">Local day of month (1..31).</param>
    /// <param name="eliminationDaysCsv">Comma-separated elimination days.</param>
    /// <param name="semiFinalDaysCsv">Comma-separated semi-final days.</param>
    /// <param name="finalDay">Championship final day.</param>
    /// <returns>Canonical <see cref="TournamentStage"/> constant.</returns>
    public static string ResolveTournamentStage(
        int dayOfMonth,
        string? eliminationDaysCsv,
        string? semiFinalDaysCsv,
        int finalDay)
    {
        if (dayOfMonth == finalDay)
        {
            return TournamentStage.Final;
        }

        if (ParseDayNumbers(semiFinalDaysCsv).Contains(dayOfMonth))
        {
            return TournamentStage.SemiFinal;
        }

        if (ParseDayNumbers(eliminationDaysCsv).Contains(dayOfMonth))
        {
            return TournamentStage.Elimination;
        }

        return TournamentStage.Practice;
    }

    /// <summary>Checks whether <paramref name="dayOfMonth"/> lies in Tormented Square's winner reward claim window.</summary>
    /// <param name="dayOfMonth">Local day of month (1..31).</param>
    /// <param name="fromDay">Inclusive start day.</param>
    /// <param name="toDay">Inclusive end day.</param>
    /// <returns><see langword="true"/> when within claim window.</returns>
    public static bool IsInRewardDayRange(int dayOfMonth, int fromDay, int toDay) =>
        fromDay > 0 && toDay >= fromDay && dayOfMonth >= fromDay && dayOfMonth <= toDay;

    /// <summary>Computes Trophy of Battle Booty Mix success % using the Season 9 server suite formula.</summary>
    /// <param name="trophyCount">Number of Trophies of Battle submitted (1..100).</param>
    /// <param name="baseRateLow">Base rate for tier 1..4 (1..40 trophies).</param>
    /// <param name="baseRateMid">Base rate for tier 5..7 (41..70 trophies).</param>
    /// <param name="baseRateHigh">Base rate for tier 8..10 (71..100 trophies).</param>
    /// <param name="addRate">Bonus % per Trophy of Battle.</param>
    /// <returns>Clamped mix success rate in <c>[1, 100]</c>.</returns>
    public static int CalculateTrophyOfBattleMixRate(
        int trophyCount,
        int baseRateLow,
        int baseRateMid,
        int baseRateHigh,
        int addRate)
    {
        var count = Math.Clamp(trophyCount, 1, 100);
        var level = ((count - 1) / 10) + 1;
        var baseRate = level <= 4
            ? baseRateLow
            : level <= 7
                ? baseRateMid
                : baseRateHigh;
        var computed = baseRate + (count * addRate) - level;
        return Math.Clamp(computed, 1, 100);
    }

    /// <summary>Returns the Chaos Castle Survival stage gift item number (<c>14:254/256/257</c>).</summary>
    /// <param name="stage">Tournament stage string.</param>
    /// <returns>Group 14 gift item number.</returns>
    public static short ResolveChaosCastleSurvivalGiftNumber(string stage) => stage switch
    {
        TournamentStage.Final => 257,
        TournamentStage.SemiFinal => 256,
        _ => 254,
    };

    /// <summary>Checks whether any configured <c>HH:mm</c> daily slot matches the local minute in <paramref name="timeZoneId"/> and optional weekday filters.</summary>
    /// <param name="dailyTimes">Configured <c>HH:mm</c> slots.</param>
    /// <param name="timeZoneId">Timezone identifier.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="allowedDaysCsv">Optional weekday CSV filter.</param>
    /// <param name="dayOfWeekFilter">Optional integer DayOfWeek (<c>-1</c> = all).</param>
    /// <returns>Canonical <c>yyyy-MM-dd HH:mm</c> slot key if due; otherwise <c>null</c>.</returns>
    public static string? ResolveDueScheduleSlot(
        IEnumerable<string>? dailyTimes,
        string? timeZoneId,
        DateTime utcNow,
        string? allowedDaysCsv = null,
        int dayOfWeekFilter = -1)
    {
        if (dailyTimes is null)
        {
            return null;
        }

        var tz = TimetableEngine.ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        if (dayOfWeekFilter >= 0 && (int)local.DayOfWeek != dayOfWeekFilter)
        {
            return null;
        }

        if (!IsDayAllowed(allowedDaysCsv, local.DayOfWeek))
        {
            return null;
        }

        var hm = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        foreach (var raw in dailyTimes)
        {
            if (TimeSpan.TryParseExact(raw?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var ts)
                && string.Equals($"{ts.Hours:D2}:{ts.Minutes:D2}", hm, StringComparison.Ordinal))
            {
                return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    /// <summary>Delivers item and Zen from <paramref name="reward"/> to <paramref name="player"/> with full rollback if space is insufficient.</summary>
    /// <param name="player">Recipient player.</param>
    /// <param name="reward">Reward payload.</param>
    /// <returns><see langword="true"/> if delivered cleanly; <see langword="false"/> if bag/Zen full.</returns>
    public static async ValueTask<bool> TryDeliverGoodsAsync(Player player, S9EventOwedReward reward)
    {
        if (player.Inventory is not { } inventory)
        {
            return false;
        }

        var added = new List<Item>();
        async ValueTask RollbackAsync()
        {
            foreach (var back in added)
            {
                await inventory.RemoveItemAsync(back).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(back).ConfigureAwait(false);
            }
        }

        if (reward.ItemGroup >= 0 && reward.ItemCount > 0)
        {
            var definition = player.GameContext.Configuration.Items.FirstOrDefault(
                d => d.Group == reward.ItemGroup && d.Number == reward.ItemNumber);
            if (definition is not null)
            {
                var level = Math.Min(reward.ItemLevel, definition.MaximumItemLevel);
                var count = Math.Clamp(reward.ItemCount, 1, 20);
                for (var i = 0; i < count; i++)
                {
                    var persisted = player.PersistenceContext.CreateNew<Item>();
                    persisted.Definition = definition;
                    persisted.Level = level;
                    persisted.Durability = persisted.IsStackable() ? 1 : persisted.GetMaximumDurabilityOfOnePiece();
                    if (inventory.CheckInvSpace(persisted) is not { } slot
                        || !await inventory.AddItemAsync(slot, persisted).ConfigureAwait(false))
                    {
                        player.PersistenceContext.Detach(persisted);
                        await RollbackAsync().ConfigureAwait(false);
                        return false;
                    }

                    added.Add(persisted);
                }
            }
        }

        if (reward.Zen > 0 && !player.TryAddMoney((int)Math.Min(int.MaxValue, reward.Zen)))
        {
            await RollbackAsync().ConfigureAwait(false);
            return false;
        }

        foreach (var item in added)
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        if (reward.Zen > 0)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        return true;
    }
}

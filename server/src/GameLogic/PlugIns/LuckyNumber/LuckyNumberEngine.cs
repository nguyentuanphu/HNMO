// <copyright file="LuckyNumberEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>Outcome of picking a daily lucky number.</summary>
public enum LuckyNumberPickResult
{
    /// <summary>The pick succeeded.</summary>
    Success,

    /// <summary>The feature is disabled.</summary>
    Disabled,

    /// <summary>The number was outside the configured min..max range.</summary>
    OutOfRange,

    /// <summary>The account already reached the daily pick cap.</summary>
    DailyLimitReached,

    /// <summary>The account already picked that exact number for this round.</summary>
    DuplicateNumber,
}

/// <summary>Summary returned after running any due daily draw.</summary>
/// <param name="DateKey">The drawn date key (<c>yyyy-MM-dd</c>).</param>
/// <param name="DrawnNumber">The winning number.</param>
/// <param name="Winners">The winning picks matched for that round.</param>
public sealed record LuckyNumberDrawOutcome(string DateKey, int DrawnNumber, IReadOnlyList<LuckyNumberWinnerEntry> Winners);

/// <summary>
/// Pure, deterministic rule engine for Daily Lucky Number rounds, odds, picking, and drawing.
/// </summary>
public sealed class LuckyNumberEngine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LuckyNumberEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    /// <param name="enabled">Whether the feature is currently active.</param>
    public LuckyNumberEngine(LuckyNumberConfiguration configuration, LuckyNumberStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets the configuration.</summary>
    public LuckyNumberConfiguration Configuration { get; }

    /// <summary>Gets the backing store.</summary>
    public LuckyNumberStore Store { get; }

    /// <summary>Gets a value indicating whether the feature is active.</summary>
    public bool Enabled { get; }

    /// <summary>Resolves the configured wall-clock time zone (Sydney default, UTC fallback).</summary>
    /// <param name="timeZoneId">The configured time zone identifier.</param>
    /// <returns>The resolved time zone.</returns>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        foreach (var id in new[] { timeZoneId, "Australia/Sydney", "AUS Eastern Standard Time" }.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id!.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    /// <summary>Parses <see cref="LuckyNumberConfiguration.DrawTime"/> or returns 20:00.</summary>
    /// <param name="drawTime">The HH:mm text.</param>
    /// <returns>The parsed wall-clock <see cref="TimeOnly"/>.</returns>
    public static TimeOnly ParseDrawTime(string? drawTime)
    {
        return TimeOnly.TryParseExact(drawTime?.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : new TimeOnly(20, 0);
    }

    /// <summary>Computes the active pick round key (<c>yyyy-MM-dd</c>) and its draw timestamp in UTC.</summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>The active date key and next draw timestamp in UTC.</returns>
    public (string DateKey, DateTime NextDrawUtc) ResolveActiveRound(DateTime utcNow)
    {
        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var tz = ResolveTimeZone(this.Configuration.TimeZoneId);
        var drawWallTime = ParseDrawTime(this.Configuration.DrawTime);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        var localDate = DateOnly.FromDateTime(localNow);
        var todayDrawUtc = ToUtc(localDate, drawWallTime, tz);

        if (utcNow < todayDrawUtc)
        {
            return (localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), todayDrawUtc);
        }

        var tomorrow = localDate.AddDays(1);
        return (tomorrow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ToUtc(tomorrow, drawWallTime, tz));
    }

    /// <summary>Computes exact-match odds and optional last-digit odds in percent.</summary>
    /// <returns>Exact win percentage and last-digit win percentage.</returns>
    public (double ExactPercent, double LastDigitPercent) ComputeOddsPercent()
    {
        var min = Math.Min(this.Configuration.MinNumber, this.Configuration.MaxNumber);
        var max = Math.Max(this.Configuration.MinNumber, this.Configuration.MaxNumber);
        var count = Math.Max(1, max - min + 1);
        var exact = Math.Round(100.0 / count, 2);

        if (!this.Configuration.EnableLastDigitPrize)
        {
            return (exact, 0.0);
        }

        var lastDigitMatches = 0;
        for (var n = min; n <= max; n++)
        {
            if (Math.Abs(n % 10) == 0)
            {
                lastDigitMatches++;
            }
        }

        var avgMatchCount = Math.Max(0, count / 10.0 - 1.0);
        var lastDigitPct = count <= 10 ? 0.0 : Math.Round(avgMatchCount * 100.0 / count, 2);
        _ = lastDigitMatches;
        return (exact, lastDigitPct);
    }

    /// <summary>Submits a player pick for the active daily round.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="characterName">Character name (or login name on web).</param>
    /// <param name="number">Chosen lucky number.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="source">Origin (<c>game</c> or <c>website</c>).</param>
    /// <returns>The outcome.</returns>
    public LuckyNumberPickResult Pick(string loginName, string characterName, int number, DateTime utcNow, string source)
    {
        if (!this.Enabled)
        {
            return LuckyNumberPickResult.Disabled;
        }

        var min = Math.Min(this.Configuration.MinNumber, this.Configuration.MaxNumber);
        var max = Math.Max(this.Configuration.MinNumber, this.Configuration.MaxNumber);
        if (number < min || number > max)
        {
            return LuckyNumberPickResult.OutOfRange;
        }

        var (dateKey, _) = this.ResolveActiveRound(utcNow);
        var normalizedLogin = loginName.Trim();
        var maxPicks = Math.Max(1, this.Configuration.MaxPicksPerAccountPerDay);

        return this.Store.Mutate(data =>
        {
            var round = data.Rounds.FirstOrDefault(r => r.DateKey == dateKey);
            if (round is null)
            {
                round = new LuckyNumberRound { DateKey = dateKey };
                data.Rounds.Add(round);
            }

            if (round.DrawnNumber.HasValue)
            {
                return (false, LuckyNumberPickResult.DailyLimitReached);
            }

            var myPicks = round.Picks.Where(p => string.Equals(p.LoginName, normalizedLogin, StringComparison.OrdinalIgnoreCase)).ToList();
            if (myPicks.Count >= maxPicks)
            {
                return (false, LuckyNumberPickResult.DailyLimitReached);
            }

            if (myPicks.Any(p => p.Number == number))
            {
                return (false, LuckyNumberPickResult.DuplicateNumber);
            }

            round.Picks.Add(new LuckyNumberPickEntry
            {
                LoginName = normalizedLogin,
                CharacterName = string.IsNullOrWhiteSpace(characterName) ? normalizedLogin : characterName.Trim(),
                Number = number,
                PickedAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
                Source = source,
            });

            return (true, LuckyNumberPickResult.Success);
        });
    }

    /// <summary>Executes any pending daily draw whose scheduled wall-clock time has elapsed.</summary>
    /// <param name="utcNow">Current UTC time.</param>
    /// <param name="overrideWinningNumber">Optional fixed number (for deterministic unit tests); uses <see cref="RandomNumberGenerator"/> when null.</param>
    /// <returns>List of newly completed draw outcomes.</returns>
    public IReadOnlyList<LuckyNumberDrawOutcome> DrawDueRounds(DateTime utcNow, int? overrideWinningNumber = null) =>
        this.DrawDueRoundsInternal(utcNow, forceCurrentRound: false, overrideWinningNumber);

    /// <summary>Draws the active round immediately (for admin test/trigger).</summary>
    /// <param name="utcNow">Current UTC time.</param>
    /// <param name="overrideWinningNumber">Optional fixed winning number.</param>
    /// <returns>The draw outcome, if not already drawn.</returns>
    public LuckyNumberDrawOutcome? DrawNow(DateTime utcNow, int? overrideWinningNumber = null) =>
        this.DrawDueRoundsInternal(utcNow, forceCurrentRound: true, overrideWinningNumber).FirstOrDefault();

    private IReadOnlyList<LuckyNumberDrawOutcome> DrawDueRoundsInternal(DateTime utcNow, bool forceCurrentRound, int? overrideWinningNumber)
    {
        if (!this.Enabled)
        {
            return [];
        }

        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var tz = ResolveTimeZone(this.Configuration.TimeZoneId);
        var drawWallTime = ParseDrawTime(this.Configuration.DrawTime);
        var min = Math.Min(this.Configuration.MinNumber, this.Configuration.MaxNumber);
        var max = Math.Max(this.Configuration.MinNumber, this.Configuration.MaxNumber);

        return this.Store.Mutate(data =>
        {
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz));
            var todayKey = localToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Ensure today's round exists if its draw time has already arrived (or forced) so players see a result even when 0 picks exist.
            var todayDrawUtc = ToUtc(localToday, drawWallTime, tz);
            if ((forceCurrentRound || utcNow >= todayDrawUtc) && data.Rounds.All(r => r.DateKey != todayKey))
            {
                data.Rounds.Add(new LuckyNumberRound { DateKey = todayKey });
            }

            var outcomes = new List<LuckyNumberDrawOutcome>();
            foreach (var round in data.Rounds.Where(r => !r.DrawnNumber.HasValue).OrderBy(r => r.DateKey, StringComparer.Ordinal))
            {
                if (!DateOnly.TryParseExact(round.DateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var roundDate))
                {
                    continue;
                }

                var dueUtc = ToUtc(roundDate, drawWallTime, tz);
                if (!forceCurrentRound && utcNow < dueUtc)
                {
                    continue;
                }

                var winning = overrideWinningNumber.HasValue
                    ? Math.Clamp(overrideWinningNumber.Value, min, max)
                    : RandomNumberGenerator.GetInt32(min, max + 1);
                round.DrawnNumber = winning;
                round.DrawnAtUtc = utcNow;

                foreach (var pick in round.Picks)
                {
                    string? tier = null;
                    var coins = 0;
                    var coinType = this.Configuration.ExactMatchCoinType;
                    var jewelGroup = 0;
                    var jewelNumber = 0;
                    var jewelCount = 0;

                    if (pick.Number == winning)
                    {
                        tier = "exact";
                        coins = this.Configuration.ExactMatchCoins;
                        coinType = this.Configuration.ExactMatchCoinType;
                        jewelGroup = this.Configuration.ExactMatchJewelGroup;
                        jewelNumber = this.Configuration.ExactMatchJewelNumber;
                        jewelCount = this.Configuration.ExactMatchJewelCount;
                    }
                    else if (this.Configuration.EnableLastDigitPrize && Math.Abs(pick.Number % 10) == Math.Abs(winning % 10))
                    {
                        tier = "lastDigit";
                        coins = this.Configuration.LastDigitCoins;
                        coinType = this.Configuration.LastDigitCoinType;
                        jewelGroup = this.Configuration.LastDigitJewelGroup;
                        jewelNumber = this.Configuration.LastDigitJewelNumber;
                        jewelCount = this.Configuration.LastDigitJewelCount;
                    }

                    if (tier is null)
                    {
                        continue;
                    }

                    var winner = new LuckyNumberWinnerEntry
                    {
                        LoginName = pick.LoginName,
                        CharacterName = pick.CharacterName,
                        Number = pick.Number,
                        Tier = tier,
                        Coins = coins,
                        CoinType = coinType,
                        JewelCount = jewelCount,
                        JewelGroup = jewelGroup,
                        JewelNumber = jewelNumber,
                        CoinsGranted = false,
                    };
                    round.Winners.Add(winner);

                    if (jewelCount > 0)
                    {
                        if (!data.OwedByAccount.TryGetValue(pick.LoginName, out var owedList))
                        {
                            owedList = [];
                            data.OwedByAccount[pick.LoginName] = owedList;
                        }

                        owedList.Add(new LuckyNumberOwedReward
                        {
                            Id = $"{round.DateKey}:{pick.Number}:{Guid.NewGuid():N}"[..24],
                            DateKey = round.DateKey,
                            Tier = tier,
                            Group = jewelGroup,
                            Number = jewelNumber,
                            Count = jewelCount,
                        });
                    }
                }

                outcomes.Add(new LuckyNumberDrawOutcome(round.DateKey, winning, round.Winners.ToList()));
            }

            return (outcomes.Count > 0, outcomes);
        });
    }

    /// <summary>Marks a winner's coin grant as paid once saved to the database.</summary>
    /// <param name="dateKey">The round date key.</param>
    /// <param name="loginName">The winner login name.</param>
    /// <param name="number">The picked number.</param>
    public void MarkCoinsGranted(string dateKey, string loginName, int number)
    {
        this.Store.Mutate(data =>
        {
            var round = data.Rounds.FirstOrDefault(r => r.DateKey == dateKey);
            if (round is null)
            {
                return (false, false);
            }

            var updated = false;
            foreach (var w in round.Winners.Where(w => string.Equals(w.LoginName, loginName, StringComparison.OrdinalIgnoreCase) && w.Number == number))
            {
                if (!w.CoinsGranted)
                {
                    w.CoinsGranted = true;
                    updated = true;
                }
            }

            return (updated, updated);
        });
    }

    /// <summary>Gets and removes an account's owed jewel rewards after claiming (or peeks when inspecting).</summary>
    /// <param name="loginName">Account login name.</param>
    /// <returns>Current owed rewards list.</returns>
    public IReadOnlyList<LuckyNumberOwedReward> PeekOwed(string loginName)
    {
        return this.Store.Read(data =>
            data.OwedByAccount.TryGetValue(loginName, out var list)
                ? list.ToList()
                : []);
    }

    /// <summary>Removes one owed jewel reward after successful bag delivery.</summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="owedId">The owed reward ID.</param>
    public void RemoveOwed(string loginName, string owedId)
    {
        this.Store.Mutate(data =>
        {
            if (!data.OwedByAccount.TryGetValue(loginName, out var list))
            {
                return (false, false);
            }

            var removed = list.RemoveAll(r => r.Id == owedId) > 0;
            if (list.Count == 0)
            {
                data.OwedByAccount.Remove(loginName);
            }

            return (removed, removed);
        });
    }

    private static DateTime ToUtc(DateOnly day, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}

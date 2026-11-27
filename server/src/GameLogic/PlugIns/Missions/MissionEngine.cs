// <copyright file="MissionEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Security.Cryptography;
using HNMO.DataModel.Configuration;

/// <summary>
/// What a claim found.
/// </summary>
public enum MissionClaimResult
{
    /// <summary>The reward can be given.</summary>
    Ok,

    /// <summary>There is no such mission (or it's off).</summary>
    Unknown,

    /// <summary>The mission isn't finished yet.</summary>
    NotFinished,

    /// <summary>The reward was claimed already.</summary>
    AlreadyClaimed,

    /// <summary>The bag has no room for the items; the reward stays claimable.</summary>
    BagFull,

    /// <summary>The zen doesn't fit; the reward stays claimable.</summary>
    ZenFull,

    /// <summary>The source is off.</summary>
    Disabled,
}

/// <summary>
/// What entering an invite code found.
/// </summary>
public enum InviteResult
{
    /// <summary>The invite was saved.</summary>
    Ok,

    /// <summary>Invites are off.</summary>
    Disabled,

    /// <summary>No account has that code or character.</summary>
    NotFound,

    /// <summary>The account can't invite itself.</summary>
    Self,

    /// <summary>An invite was entered already.</summary>
    AlreadyInvited,

    /// <summary>Both accounts play from the same network address.</summary>
    SameAddress,

    /// <summary>The account is already past the required level, so it can't be invited anymore.</summary>
    TooLate,
}

/// <summary>
/// The rules of the missions, the streak and the earned coins, without the game: it works on the
/// <see cref="MissionStore"/> with given times, so it's tested on its own. Every method takes the store's lock.
/// </summary>
public sealed class MissionEngine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissionEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public MissionEngine(MissionsConfiguration configuration, MissionStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Clock = new MissionClock(configuration.TimeZone, configuration.ResetHour);
    }

    /// <summary>Gets the configuration.</summary>
    public MissionsConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public MissionStore Store { get; }

    /// <summary>Gets the clock.</summary>
    public MissionClock Clock { get; }

    /// <summary>
    /// Gets the number of daily missions which finish a streak day.
    /// </summary>
    public int StreakRequired
    {
        get
        {
            var dailies = this.Configuration.Enabled(MissionPeriod.Daily).Count();
            var required = this.Configuration.StreakMissionsRequired;
            return required <= 0 ? dailies : Math.Min(required, dailies);
        }
    }

    /// <summary>
    /// Counts progress of a character.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="kind">The kind of progress.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="matches">Whether a mission of the kind counts it (e.g. the right map); <c>null</c> counts it for all.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The missions which were finished by it.</returns>
    public IReadOnlyList<MissionDefinition> AddProgress(string characterId, MissionKind kind, long amount, Func<MissionDefinition, bool>? matches, DateTime nowUtc)
    {
        if (amount <= 0)
        {
            return [];
        }

        List<MissionDefinition>? finished = null;
        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            this.RollPeriods(state, nowUtc);
            foreach (var mission in this.Configuration.Missions)
            {
                if (!mission.Enabled || mission.Kind != kind || string.IsNullOrWhiteSpace(mission.Id) || (matches is not null && !matches(mission)))
                {
                    continue;
                }

                var progress = this.ProgressOf(state, mission, true)!;
                var target = Math.Max(1, mission.Target);
                if (progress.Value >= target)
                {
                    continue;
                }

                progress.Value = (int)Math.Min(target, progress.Value + amount);
                this.Store.Changed();
                if (progress.Value >= target)
                {
                    (finished ??= new()).Add(mission);
                }
            }

            if (finished?.Any(m => m.Period == MissionPeriod.Daily) is true)
            {
                this.UpdateStreak(state, nowUtc);
            }
        }

        return (IReadOnlyList<MissionDefinition>?)finished ?? [];
    }

    /// <summary>
    /// Checks whether a mission's reward can be claimed.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="missionId">The mission id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="mission">The mission.</param>
    /// <returns>The result.</returns>
    public MissionClaimResult CheckClaim(string characterId, string missionId, DateTime nowUtc, out MissionDefinition? mission)
    {
        mission = this.Configuration.Missions.FirstOrDefault(m => m.Enabled && string.Equals(m.Id, missionId, StringComparison.Ordinal));
        if (mission is null)
        {
            return MissionClaimResult.Unknown;
        }

        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            this.RollPeriods(state, nowUtc);
            var progress = this.ProgressOf(state, mission, false);
            if (progress?.Claimed is true)
            {
                return MissionClaimResult.AlreadyClaimed;
            }

            return progress is not null && progress.Value >= Math.Max(1, mission.Target) ? MissionClaimResult.Ok : MissionClaimResult.NotFinished;
        }
    }

    /// <summary>
    /// Marks a mission's reward as claimed, if it can be claimed.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="missionId">The mission id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="reference">The reference of the claim (character, period and mission), so its coins count once.</param>
    /// <returns><c>true</c>, if it was marked now; <c>false</c>, if it can't be claimed (anymore).</returns>
    public bool MarkClaimed(string characterId, string missionId, DateTime nowUtc, out string reference)
    {
        reference = string.Empty;
        if (this.CheckClaim(characterId, missionId, nowUtc, out var mission) != MissionClaimResult.Ok)
        {
            return false;
        }

        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            var progress = this.ProgressOf(state, mission!, false)!;
            if (progress.Claimed)
            {
                return false;
            }

            progress.Claimed = true;
            var period = mission!.Period == MissionPeriod.Daily ? state.DailyKey : state.WeeklyKey;
            reference = $"mission:{characterId}:{period}:{mission.Id}";
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>
    /// Gets the progress of a mission in the current period.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="mission">The mission.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The count and whether it's claimed.</returns>
    public (int Value, bool Claimed) GetProgress(string characterId, MissionDefinition mission, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            this.RollPeriods(state, nowUtc);
            var progress = this.ProgressOf(state, mission, false);
            return (progress?.Value ?? 0, progress?.Claimed ?? false);
        }
    }

    /// <summary>
    /// Counts the claimed daily/weekly missions and level milestones for a character.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The achievement count.</returns>
    public int GetAchievementCount(string characterId)
    {
        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            return state.Daily.Values.Count(p => p.Claimed)
                + state.Weekly.Values.Count(p => p.Claimed)
                + state.MilestonesRewarded.Count;
        }
    }

    /// <summary>
    /// Gets the streak as it counts now: a missed day shows 0.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The days, whether today counted already, and whether a bonus waits.</returns>
    public (int Days, bool TodayCounted, bool BonusClaimable) GetStreak(string characterId, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            var today = this.Clock.Day(nowUtc);
            var last = MissionClock.Parse(state.StreakLastDay);
            var counted = last == today;
            var days = last is { } l && (l == today || l == today.AddDays(-1)) ? state.StreakCount : 0;
            return (days, counted, state.StreakBonusDay is not null);
        }
    }

    /// <summary>
    /// Takes the waiting streak bonus.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="reference">The reference of the bonus.</param>
    /// <returns><c>true</c>, if a bonus was waiting.</returns>
    public bool TakeStreakBonus(string characterId, out string reference)
    {
        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            reference = $"streak:{characterId}:{state.StreakBonusDay}";
            if (state.StreakBonusDay is null)
            {
                return false;
            }

            state.StreakBonusDay = null;
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>
    /// Puts a taken streak bonus back, when it couldn't be given (bag full).
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="reference">The reference which <see cref="TakeStreakBonus"/> gave.</param>
    public void ReturnStreakBonus(string characterId, string reference)
    {
        lock (this.Store.Lock)
        {
            this.Store.Character(characterId).StreakBonusDay = reference[(reference.LastIndexOf(':') + 1)..];
            this.Store.Changed();
        }
    }

    /// <summary>
    /// Puts a claimed mission back to unclaimed, when its reward couldn't be given.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="missionId">The mission id.</param>
    /// <param name="nowUtc">The time.</param>
    public void Unclaim(string characterId, string missionId, DateTime nowUtc)
    {
        if (this.Configuration.Missions.FirstOrDefault(m => m.Id == missionId) is not { } mission)
        {
            return;
        }

        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            this.RollPeriods(state, nowUtc);
            if (this.ProgressOf(state, mission, false) is { } progress)
            {
                progress.Claimed = false;
                this.Store.Changed();
            }
        }
    }

    /// <summary>
    /// Counts time in the game of an account and gives the coins of the full hours, under the source's and the overall daily cap.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="seconds">The seconds in the game since the last call.</param>
    /// <param name="autoPlay">Whether auto play was running (counts half, if configured).</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The grant, if a full hour was reached.</returns>
    public CoinGrant? AddOnlineTime(string loginName, int seconds, bool autoPlay, DateTime nowUtc)
    {
        var source = this.Configuration.OnlineTime;
        if (!source.Enabled || seconds <= 0)
        {
            return null;
        }

        lock (this.Store.Lock)
        {
            var account = this.AccountToday(loginName, nowUtc);
            account.OnlineSecondsToday += autoPlay && source.AutoPlayCountsHalf ? seconds / 2 : seconds;
            this.Store.Changed();
            var hours = account.OnlineSecondsToday / 3600;
            if (hours <= account.OnlineHoursPaidToday)
            {
                return null;
            }

            var newHours = hours - account.OnlineHoursPaidToday;
            account.OnlineHoursPaidToday = hours;
            var coins = (int)Math.Min((long)newHours * source.CoinsPerHour, int.MaxValue);
            coins = Math.Min(coins, Math.Max(0, source.DailyCap - account.OnlineCoinsToday));
            coins = Math.Min(coins, this.RoomToday(account));
            if (coins <= 0)
            {
                return null;
            }

            account.OnlineCoinsToday += coins;
            account.EarnedToday += coins;
            return new CoinGrant(loginName, source.CoinType, coins, $"Online time: hour {hours} of {this.Clock.DayKey(nowUtc)}", $"online:{loginName}:{this.Clock.DayKey(nowUtc)}:{hours}");
        }
    }

    /// <summary>
    /// Gives a one-time earned reward under the daily cap: it's given now when it fits (or when nothing was earned
    /// today yet, so a reward bigger than the cap isn't stuck), else it waits for a later day.
    /// </summary>
    /// <param name="grant">The grant.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c>, if it's given now; <c>false</c>, if it waits.</returns>
    public bool ReserveOrOwe(CoinGrant grant, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var account = this.AccountToday(grant.LoginName, nowUtc);
            this.Store.Changed();
            if (this.Fits(account, grant.Amount))
            {
                account.EarnedToday += grant.Amount;
                return true;
            }

            if (account.Owed.All(o => o.Reference != grant.Reference))
            {
                account.Owed.Add(new OwedCoinsState { Reference = grant.Reference, Reason = grant.Reason, Amount = grant.Amount, CoinType = grant.CoinType });
            }

            return false;
        }
    }

    /// <summary>
    /// Takes the waiting rewards which fit under today's cap.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The grants to give now.</returns>
    public IReadOnlyList<CoinGrant> TakeOwed(string loginName, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var account = this.AccountToday(loginName, nowUtc);
            if (account.Owed.Count == 0)
            {
                return [];
            }

            var result = new List<CoinGrant>();
            foreach (var owed in account.Owed.ToList())
            {
                if (!this.Fits(account, owed.Amount))
                {
                    break;
                }

                account.EarnedToday += owed.Amount;
                account.Owed.Remove(owed);
                result.Add(new CoinGrant(loginName, owed.CoinType, owed.Amount, owed.Reason, owed.Reference));
            }

            if (result.Count > 0)
            {
                this.Store.Changed();
            }

            return result;
        }
    }

    /// <summary>
    /// Claims today's day of the login calendar.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <param name="grant">The grant of the day (its coins may be 0).</param>
    /// <returns><see cref="MissionClaimResult.Ok"/>, <see cref="MissionClaimResult.AlreadyClaimed"/> or <see cref="MissionClaimResult.Disabled"/>.</returns>
    public MissionClaimResult ClaimCalendar(string loginName, DateTime nowUtc, out CoinGrant? grant)
    {
        grant = null;
        var source = this.Configuration.LoginCalendar;
        if (!source.Enabled)
        {
            return MissionClaimResult.Disabled;
        }

        lock (this.Store.Lock)
        {
            var account = this.AccountToday(loginName, nowUtc);
            var today = this.Clock.DayKey(nowUtc);
            if (account.CalendarLastDay == today)
            {
                return MissionClaimResult.AlreadyClaimed;
            }

            var month = this.Clock.MonthKey(nowUtc);
            if (account.CalendarMonth != month)
            {
                account.CalendarMonth = month;
                account.CalendarDays = 0;
            }

            account.CalendarDays++;
            account.CalendarLastDay = today;
            this.Store.Changed();
            var coins = source.CoinsOf(account.CalendarDays);
            grant = new CoinGrant(loginName, source.CoinType, coins, $"Login calendar: day {account.CalendarDays} of {month}", $"calendar:{loginName}:{month}:{account.CalendarDays}");
            return MissionClaimResult.Ok;
        }
    }

    /// <summary>
    /// Gets the login calendar of an account as it is now.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The claimed days of the month and whether today is claimed.</returns>
    public (int ClaimedDays, bool ClaimedToday) GetCalendar(string loginName, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var account = this.Store.Account(loginName);
            var claimed = account.CalendarMonth == this.Clock.MonthKey(nowUtc) ? account.CalendarDays : 0;
            return (claimed, account.CalendarLastDay == this.Clock.DayKey(nowUtc));
        }
    }

    /// <summary>
    /// Gets the earned coins of an account today.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The coins of all capped sources, of the online time, the counted minutes, and the waiting coins.</returns>
    public (int Earned, int Online, int OnlineMinutes, int Owed) GetEarnedToday(string loginName, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var account = this.AccountToday(loginName, nowUtc);
            return (account.EarnedToday, account.OnlineCoinsToday, account.OnlineSecondsToday / 60, account.Owed.Sum(o => o.Amount));
        }
    }

    /// <summary>
    /// Gets the milestone and reset rewards a character has reached and not gotten yet, and marks them as given.
    /// The first time a character is seen, its resets so far count as given (only new resets pay); its levels pay once each.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="loginName">The login name of its account.</param>
    /// <param name="characterName">The character name.</param>
    /// <param name="level">The level.</param>
    /// <param name="resets">The resets.</param>
    /// <returns>The grants.</returns>
    public IReadOnlyList<CoinGrant> TakeMilestones(string characterId, string loginName, string characterName, int level, int resets)
    {
        var source = this.Configuration.Milestones;
        if (!source.Enabled)
        {
            return [];
        }

        lock (this.Store.Lock)
        {
            var state = this.Store.Character(characterId);
            var result = new List<CoinGrant>();
            foreach (var milestone in source.Levels.Where(m => m.Level <= level && m.Coins > 0).OrderBy(m => m.Level))
            {
                if (state.MilestonesRewarded.Contains(milestone.Level))
                {
                    continue;
                }

                state.MilestonesRewarded.Add(milestone.Level);
                result.Add(new CoinGrant(loginName, source.CoinType, milestone.Coins, $"Level {milestone.Level} reached by {characterName}", $"milestone:{characterId}:level:{milestone.Level}"));
            }

            if (state.ResetsRewarded < 0)
            {
                state.ResetsRewarded = resets;
            }

            for (var reset = state.ResetsRewarded + 1; reset <= resets; reset++)
            {
                if (source.CoinsPerReset > 0)
                {
                    result.Add(new CoinGrant(loginName, source.CoinType, source.CoinsPerReset, $"Reset {reset} of {characterName}", $"milestone:{characterId}:reset:{reset}"));
                }
            }

            if (resets > state.ResetsRewarded)
            {
                state.ResetsRewarded = resets;
            }

            this.Store.Changed();
            return result;
        }
    }

    /// <summary>
    /// Gets (and creates) the invite code of an account.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <returns>The code.</returns>
    public string InviteCodeOf(string loginName)
    {
        lock (this.Store.Lock)
        {
            var account = this.Store.Account(loginName);
            if (account.InviteCode is null)
            {
                const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
                string code;
                do
                {
                    code = string.Create(6, 0, (span, _) =>
                    {
                        for (var i = 0; i < span.Length; i++)
                        {
                            span[i] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
                        }
                    });
                }
                while (this.Store.FindByInviteCode(code) is not null);
                account.InviteCode = code;
                this.Store.Changed();
            }

            return account.InviteCode;
        }
    }

    /// <summary>
    /// Saves who invited an account: once per account.
    /// </summary>
    /// <param name="loginName">The invited account.</param>
    /// <param name="inviterLoginName">The inviting account (found by its code or a character name).</param>
    /// <param name="address">The network address of the invited account, if known.</param>
    /// <param name="highestLevel">The highest level of the invited account's characters.</param>
    /// <returns>The result.</returns>
    public InviteResult RegisterInvite(string loginName, string? inviterLoginName, string? address, int highestLevel)
    {
        var source = this.Configuration.Invite;
        if (!source.Enabled)
        {
            return InviteResult.Disabled;
        }

        if (inviterLoginName is null)
        {
            return InviteResult.NotFound;
        }

        if (string.Equals(loginName, inviterLoginName, StringComparison.OrdinalIgnoreCase))
        {
            return InviteResult.Self;
        }

        lock (this.Store.Lock)
        {
            var account = this.Store.Account(loginName);
            if (account.InvitedBy is not null)
            {
                return InviteResult.AlreadyInvited;
            }

            if (highestLevel >= source.RequiredLevel)
            {
                return InviteResult.TooLate;
            }

            var inviter = this.Store.Account(inviterLoginName);
            if (source.RefuseSameAddress && IsSameAddress(address ?? account.LastAddress, inviter.LastAddress))
            {
                return InviteResult.SameAddress;
            }

            account.InvitedBy = inviterLoginName;
            this.Store.Changed();
            return InviteResult.Ok;
        }
    }

    /// <summary>
    /// Takes the invite rewards when a character of an invited account reached the required level: once per invited account.
    /// </summary>
    /// <param name="loginName">The invited account.</param>
    /// <param name="level">The level of its character.</param>
    /// <returns>The grants for the inviting (and the invited) account; none when it's not due, or both play from the same address.</returns>
    public IReadOnlyList<CoinGrant> TakeInviteRewards(string loginName, int level)
    {
        var source = this.Configuration.Invite;
        if (!source.Enabled || level < source.RequiredLevel)
        {
            return [];
        }

        lock (this.Store.Lock)
        {
            var account = this.Store.Account(loginName);
            if (account.InvitedBy is not { } inviterName || account.InviteRewarded)
            {
                return [];
            }

            account.InviteRewarded = true;
            this.Store.Changed();
            var inviter = this.Store.Account(inviterName);
            if (source.RefuseSameAddress && IsSameAddress(account.LastAddress, inviter.LastAddress))
            {
                return [];
            }

            var result = new List<CoinGrant>();
            if (source.InviterCoins > 0)
            {
                result.Add(new CoinGrant(inviterName, source.CoinType, source.InviterCoins, $"Invited {loginName}, who reached level {source.RequiredLevel}", $"invite:{inviterName}:{loginName}"));
            }

            if (source.InvitedCoins > 0)
            {
                result.Add(new CoinGrant(loginName, source.CoinType, source.InvitedCoins, $"Invited by {inviterName}, reached level {source.RequiredLevel}", $"invited:{loginName}"));
            }

            return result;
        }
    }

    /// <summary>
    /// Remembers the network address of an account's login.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="address">The address.</param>
    public void SetAddress(string loginName, string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        lock (this.Store.Lock)
        {
            var account = this.Store.Account(loginName);
            if (account.LastAddress != address)
            {
                account.LastAddress = address;
                this.Store.Changed();
            }
        }
    }

    /// <summary>
    /// Whether two addresses are the same known address. Loopback addresses don't count: the web game's players all
    /// come through the local relay, so their address is unknown.
    /// </summary>
    /// <param name="a">The one address.</param>
    /// <param name="b">The other address.</param>
    /// <returns><c>true</c>, if they're the same.</returns>
    internal static bool IsSameAddress(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || !string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !(System.Net.IPAddress.TryParse(a, out var ip) && System.Net.IPAddress.IsLoopback(ip));
    }

    private bool Fits(MissionAccountState account, int amount) =>
        account.EarnedToday == 0 || account.EarnedToday + (long)amount <= this.Configuration.EarnedDailyCap;

    private int RoomToday(MissionAccountState account) => Math.Max(0, this.Configuration.EarnedDailyCap - account.EarnedToday);

    private MissionAccountState AccountToday(string loginName, DateTime nowUtc)
    {
        var account = this.Store.Account(loginName);
        var today = this.Clock.DayKey(nowUtc);
        if (account.DayKey != today)
        {
            account.DayKey = today;
            account.EarnedToday = 0;
            account.OnlineSecondsToday = 0;
            account.OnlineHoursPaidToday = 0;
            account.OnlineCoinsToday = 0;
            this.Store.Changed();
        }

        return account;
    }

    private void RollPeriods(MissionCharacterState state, DateTime nowUtc)
    {
        var day = this.Clock.DayKey(nowUtc);
        if (state.DailyKey != day)
        {
            state.DailyKey = day;
            state.Daily.Clear();
            this.Store.Changed();
        }

        var week = this.Clock.WeekKey(nowUtc);
        if (state.WeeklyKey != week)
        {
            state.WeeklyKey = week;
            state.Weekly.Clear();
            this.Store.Changed();
        }
    }

    private MissionProgressState? ProgressOf(MissionCharacterState state, MissionDefinition mission, bool create)
    {
        var map = mission.Period == MissionPeriod.Daily ? state.Daily : state.Weekly;
        if (!map.TryGetValue(mission.Id, out var progress) && create)
        {
            progress = new MissionProgressState();
            map[mission.Id] = progress;
        }

        return progress;
    }

    private void UpdateStreak(MissionCharacterState state, DateTime nowUtc)
    {
        var required = this.StreakRequired;
        if (required <= 0)
        {
            return;
        }

        var finished = this.Configuration.Enabled(MissionPeriod.Daily)
            .Count(m => state.Daily.TryGetValue(m.Id, out var p) && p.Value >= Math.Max(1, m.Target));
        var today = this.Clock.Day(nowUtc);
        var last = MissionClock.Parse(state.StreakLastDay);
        if (finished < required || last == today)
        {
            return;
        }

        state.StreakCount = last == today.AddDays(-1) ? state.StreakCount + 1 : 1;
        state.StreakLastDay = MissionClock.Key(today);
        var bonusDay = Math.Max(2, this.Configuration.StreakBonusDay);
        if (state.StreakCount % bonusDay == 0 && !this.Configuration.StreakBonus.IsEmpty)
        {
            state.StreakBonusDay = state.StreakLastDay;
        }

        this.Store.Changed();
    }
}

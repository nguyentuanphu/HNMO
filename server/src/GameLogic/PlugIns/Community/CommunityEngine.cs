// <copyright file="CommunityEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The result of a one-time community reward claim.
/// </summary>
public enum CommunityClaimResult
{
    /// <summary>The reward is turned off.</summary>
    Off,

    /// <summary>The account isn't linked to Discord.</summary>
    NotLinked,

    /// <summary>The account got the reward already.</summary>
    AlreadyPaid,

    /// <summary>The Discord user's reward was paid to another account already.</summary>
    DiscordUserUsed,

    /// <summary>The Zalo claim waits for the admin.</summary>
    Pending,

    /// <summary>Done: the reward was given, or the claim was made.</summary>
    Ok,
}

/// <summary>
/// The result of entering an event code.
/// </summary>
public enum CommunityCodeResult
{
    /// <summary>No open event has this code.</summary>
    Unknown,

    /// <summary>The event hasn't started yet.</summary>
    NotStarted,

    /// <summary>The event is over.</summary>
    Ended,

    /// <summary>The account entered the code already.</summary>
    AlreadyEntered,

    /// <summary>The reward was given.</summary>
    Ok,
}

/// <summary>
/// Where an event is in time.
/// </summary>
public enum CommunityEventPhase
{
    /// <summary>It starts later.</summary>
    Upcoming,

    /// <summary>It runs now.</summary>
    Open,

    /// <summary>It's over.</summary>
    Ended,
}

/// <summary>
/// The rules of HNMO's community rewards (pure, without the game): the one-time coins for linking Discord and joining
/// the Zalo group (once per account; Discord also once per Discord user), the community events (a code entered once per
/// account while the event runs, or listed winners, each paid once), and the automatic posts (which drops are rare, the
/// texts, the website's recent highlights). Event dates are wall-clock times of the configured time zone (Sydney).
/// </summary>
public sealed class CommunityEngine
{
    /// <summary>The most highlights kept.</summary>
    public const int HighlightsKept = 100;

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly string[] LocalFormats = ["yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd"];

    private readonly ConcurrentDictionary<string, DateTime> _lastDropPost = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunityEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public CommunityEngine(CommunityConfiguration configuration, CommunityStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.TimeZone = new MissionClock(configuration.TimeZone, 0).TimeZone;
    }

    /// <summary>Gets the configuration.</summary>
    public CommunityConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public CommunityStore Store { get; }

    /// <summary>Gets the time zone of the event dates.</summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>Gets the reference of the Discord coins of an account.</summary>
    /// <param name="login">The login name.</param>
    /// <returns>The reference.</returns>
    public static string DiscordReference(string login) => $"hnmo-community:discord:{Key(login)}";

    /// <summary>Gets the reference of the Zalo coins of an account.</summary>
    /// <param name="login">The login name.</param>
    /// <returns>The reference.</returns>
    public static string ZaloReference(string login) => $"hnmo-community:zalo:{Key(login)}";

    /// <summary>Gets the reference of an event's reward.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The reference.</returns>
    public static string EventReference(CommunityEventEntry entry) => entry.Winner
        ? $"hnmo-community:event:{entry.EventId}:winner:{Key(entry.Character)}"
        : $"hnmo-community:event:{entry.EventId}:code:{Key(entry.Login)}";

    /// <summary>Normalizes a code: letters and digits only, upper case.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The normalized code.</returns>
    public static string NormalizeCode(string? code) =>
        new((code ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Fills the placeholders of a text, e.g. {player}.</summary>
    /// <param name="template">The text.</param>
    /// <param name="values">The values by placeholder name (without braces).</param>
    /// <returns>The filled text.</returns>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        var builder = new StringBuilder(template ?? string.Empty);
        foreach (var (name, value) in values)
        {
            builder.Replace("{" + name + "}", value);
        }

        return builder.ToString().Trim();
    }

    /// <summary>Parses an item of the drop filter: "group number".</summary>
    /// <param name="text">The text.</param>
    /// <returns>The item; <c>null</c> when it isn't one.</returns>
    public static (int Group, int Number)? ParseItem(string? text)
    {
        var parts = (text ?? string.Empty).Split([' ', ',', '/', ':'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var group) && group is >= 0 and <= 15
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 0 and <= 511
            ? (group, number)
            : null;
    }

    /// <summary>
    /// Converts a wall-clock time of the time zone to UTC. A time which doesn't exist on the day the clocks go forward
    /// moves to the first minute after the gap; a time which happens twice when they go back is its first occurrence.
    /// </summary>
    /// <param name="local">The time, e.g. "2026-11-20 18:00"; empty for none.</param>
    /// <returns>The UTC time; <c>null</c> when it's empty or not a time.</returns>
    public DateTime? ToUtc(string? local)
    {
        if (string.IsNullOrWhiteSpace(local)
            || !DateTime.TryParseExact(local.Trim(), LocalFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return null;
        }

        time = DateTime.SpecifyKind(time, DateTimeKind.Unspecified);
        for (var i = 0; i < 180 && this.TimeZone.IsInvalidTime(time); i++)
        {
            time = time.AddMinutes(1);
        }

        if (this.TimeZone.IsAmbiguousTime(time))
        {
            var offset = this.TimeZone.GetAmbiguousTimeOffsets(time).Max();
            return DateTime.SpecifyKind(time - offset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(time, this.TimeZone);
    }

    /// <summary>Converts a UTC time to the wall-clock time of the time zone.</summary>
    /// <param name="utc">The time.</param>
    /// <returns>The local time.</returns>
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), this.TimeZone);

    /// <summary>Gets where an event is in time.</summary>
    /// <param name="definition">The event.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The phase.</returns>
    public CommunityEventPhase Phase(CommunityEventDefinition definition, DateTime nowUtc)
    {
        if (this.ToUtc(definition.Start) is { } start && nowUtc < start)
        {
            return CommunityEventPhase.Upcoming;
        }

        return this.ToUtc(definition.End) is { } end && nowUtc >= end ? CommunityEventPhase.Ended : CommunityEventPhase.Open;
    }

    /// <summary>Gets the events which are on (in their order), with their phase.</summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The events.</returns>
    public IReadOnlyList<(CommunityEventDefinition Definition, CommunityEventPhase Phase)> Events(DateTime nowUtc) =>
        this.Configuration.Events.Where(e => e.Enabled && !string.IsNullOrWhiteSpace(e.Id)).Select(e => (e, this.Phase(e, nowUtc))).ToList();

    /// <summary>Gets the state of an account.</summary>
    /// <param name="login">The login name.</param>
    /// <returns>A copy of the state; <c>null</c> when there's none.</returns>
    public CommunityAccountState? Account(string login)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Accounts.TryGetValue(Key(login), out var state) ? Copy(state) : null;
        }
    }

    /// <summary>
    /// Gives the Discord coins of an account which is linked to a Discord user, once per account and Discord user.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="discordUserId">The linked Discord user; <c>null</c> when the account isn't linked.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The result and, when it's <see cref="CommunityClaimResult.Ok"/>, the grant to give.</returns>
    public (CommunityClaimResult Result, CoinGrant? Grant) ClaimDiscord(string login, string? discordUserId, DateTime nowUtc)
    {
        var reward = this.Configuration.Discord;
        if (!reward.Enabled || reward.Coins <= 0 || string.IsNullOrWhiteSpace(login))
        {
            return (CommunityClaimResult.Off, null);
        }

        lock (this.Store.Lock)
        {
            var state = this.State(login);
            if (state.DiscordPaidAtUtc is not null)
            {
                return (CommunityClaimResult.AlreadyPaid, null);
            }

            if (string.IsNullOrWhiteSpace(discordUserId))
            {
                return (CommunityClaimResult.NotLinked, null);
            }

            if (this.Store.Data.PaidDiscordUsers.TryGetValue(discordUserId, out var paidLogin) && paidLogin != Key(login))
            {
                return (CommunityClaimResult.DiscordUserUsed, null);
            }

            state.DiscordPaidAtUtc = nowUtc;
            state.DiscordUserId = discordUserId;
            this.Store.Data.PaidDiscordUsers[discordUserId] = Key(login);
            this.Store.Changed();
            return (CommunityClaimResult.Ok, new CoinGrant(login, reward.CoinType, reward.Coins, "Community: Discord account linked", DiscordReference(login)));
        }
    }

    /// <summary>
    /// Makes a Zalo claim: the player gets a code to post in the Zalo group (or send to the admin), and the admin
    /// approves it in the admin panel. Asking again keeps the waiting claim (and its code).
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="character">The character which asked.</param>
    /// <param name="zaloName">The player's name in Zalo, if given.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The result (<see cref="CommunityClaimResult.Ok"/> for a new claim) and the code.</returns>
    public (CommunityClaimResult Result, string? Code) RequestZalo(string login, string character, string? zaloName, DateTime nowUtc)
    {
        var reward = this.Configuration.Zalo;
        if (!reward.Enabled || reward.Coins <= 0 || string.IsNullOrWhiteSpace(login))
        {
            return (CommunityClaimResult.Off, null);
        }

        lock (this.Store.Lock)
        {
            var state = this.State(login);
            var name = (zaloName ?? string.Empty).Trim();
            if (name.Length > 40)
            {
                name = name[..40];
            }

            switch (state.ZaloStatus)
            {
                case ZaloClaimStatus.Approved:
                    return (CommunityClaimResult.AlreadyPaid, state.ZaloCode);
                case ZaloClaimStatus.Pending:
                    if (name.Length > 0 && name != state.ZaloName)
                    {
                        state.ZaloName = name;
                        this.Store.Changed();
                    }

                    return (CommunityClaimResult.Pending, state.ZaloCode);
            }

            state.ZaloStatus = ZaloClaimStatus.Pending;
            state.ZaloCode = "Z-" + RandomCode(6);
            state.ZaloName = name.Length > 0 ? name : null;
            state.ZaloCharacter = character;
            state.ZaloRequestedAtUtc = nowUtc;
            state.ZaloDecidedAtUtc = null;
            this.Store.Changed();
            return (CommunityClaimResult.Ok, state.ZaloCode);
        }
    }

    /// <summary>Gets the waiting Zalo claims, oldest first.</summary>
    /// <returns>Copies of the accounts' states.</returns>
    public IReadOnlyList<CommunityAccountState> PendingZalo()
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Accounts.Values.Where(a => a.ZaloStatus == ZaloClaimStatus.Pending)
                .OrderBy(a => a.ZaloRequestedAtUtc).Select(Copy).ToList();
        }
    }

    /// <summary>Gets the decided Zalo claims, newest first.</summary>
    /// <param name="count">How many.</param>
    /// <returns>Copies of the accounts' states.</returns>
    public IReadOnlyList<CommunityAccountState> DecidedZalo(int count)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Accounts.Values.Where(a => a.ZaloStatus is ZaloClaimStatus.Approved or ZaloClaimStatus.Rejected)
                .OrderByDescending(a => a.ZaloDecidedAtUtc).Take(count).Select(Copy).ToList();
        }
    }

    /// <summary>
    /// The admin approves or rejects a waiting Zalo claim. An approved claim is paid by the game server (once).
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="approve">Whether it's approved.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c>, if a waiting claim was decided.</returns>
    public bool DecideZalo(string login, bool approve, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Accounts.TryGetValue(Key(login), out var state) || state.ZaloStatus != ZaloClaimStatus.Pending)
            {
                return false;
            }

            state.ZaloStatus = approve ? ZaloClaimStatus.Approved : ZaloClaimStatus.Rejected;
            state.ZaloDecidedAtUtc = nowUtc;
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>Gets the grants of approved Zalo claims which weren't paid yet.</summary>
    /// <returns>The grants.</returns>
    public IReadOnlyList<CoinGrant> ZaloToPay()
    {
        var reward = this.Configuration.Zalo;
        if (!reward.Enabled || reward.Coins <= 0)
        {
            return [];
        }

        lock (this.Store.Lock)
        {
            return this.Store.Data.Accounts.Values.Where(a => a.ZaloStatus == ZaloClaimStatus.Approved && a.ZaloPaidAtUtc is null)
                .Select(a => new CoinGrant(a.Login, reward.CoinType, reward.Coins, "Community: joined the Zalo group", ZaloReference(a.Login)))
                .ToList();
        }
    }

    /// <summary>Notes that the Zalo coins of an account were paid.</summary>
    /// <param name="login">The login name.</param>
    /// <param name="nowUtc">The time.</param>
    public void MarkZaloPaid(string login, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            if (this.Store.Data.Accounts.TryGetValue(Key(login), out var state))
            {
                state.ZaloPaidAtUtc = nowUtc;
                this.Store.Changed();
            }
        }
    }

    /// <summary>
    /// Enters an event's code: the reward once per account, while the event runs.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="character">The character name.</param>
    /// <param name="code">The entered code.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The result, and the event.</returns>
    public (CommunityCodeResult Result, CommunityEventDefinition? Event) EnterCode(string login, string character, string? code, DateTime nowUtc)
    {
        var normalized = NormalizeCode(code);
        if (normalized.Length == 0 || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(character))
        {
            return (CommunityCodeResult.Unknown, null);
        }

        var matches = this.Events(nowUtc).Where(e => NormalizeCode(e.Definition.Code) == normalized).ToList();
        if (matches.Count == 0)
        {
            return (CommunityCodeResult.Unknown, null);
        }

        var open = matches.FirstOrDefault(m => m.Phase == CommunityEventPhase.Open);
        if (open.Definition is not { } definition)
        {
            var first = matches[0];
            return (first.Phase == CommunityEventPhase.Upcoming ? CommunityCodeResult.NotStarted : CommunityCodeResult.Ended, first.Definition);
        }

        lock (this.Store.Lock)
        {
            var accountKey = definition.Id + "|" + Key(login);
            if (this.Store.Data.CodeAccounts.ContainsKey(accountKey))
            {
                return (CommunityCodeResult.AlreadyEntered, definition);
            }

            this.Store.Data.CodeAccounts[accountKey] = character;
            this.AddEntry(definition, character, login, false, nowUtc);
            return (CommunityCodeResult.Ok, definition);
        }
    }

    /// <summary>Gets the listed winners of started events who weren't recorded yet.</summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The events and character names.</returns>
    public IReadOnlyList<(CommunityEventDefinition Event, string Character)> WinnersToRecord(DateTime nowUtc)
    {
        var result = new List<(CommunityEventDefinition, string)>();
        lock (this.Store.Lock)
        {
            foreach (var (definition, phase) in this.Events(nowUtc))
            {
                if (phase == CommunityEventPhase.Upcoming)
                {
                    continue;
                }

                foreach (var name in definition.Winners.Select(w => w.Trim()).Where(w => w.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!this.Store.Data.Entries.ContainsKey(EntryKey(definition.Id, name, true)))
                    {
                        result.Add((definition, name));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Records a listed winner (once): the coins wait to be paid, the zen and items for the character.</summary>
    /// <param name="definition">The event.</param>
    /// <param name="character">The character name.</param>
    /// <param name="login">The character's login name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c>, if it was new.</returns>
    public bool RecordWinner(CommunityEventDefinition definition, string character, string login, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            if (this.Store.Data.Entries.ContainsKey(EntryKey(definition.Id, character, true)))
            {
                return false;
            }

            this.AddEntry(definition, character, login, true, nowUtc);
            return true;
        }
    }

    /// <summary>Gets the entries whose coins weren't paid yet, with their grants.</summary>
    /// <returns>The entries and grants.</returns>
    public IReadOnlyList<(CommunityEventEntry Entry, CoinGrant Grant)> CoinsToPay()
    {
        var result = new List<(CommunityEventEntry, CoinGrant)>();
        lock (this.Store.Lock)
        {
            foreach (var entry in this.Store.Data.Entries.Values.Where(e => !e.CoinsPaid))
            {
                var definition = this.Configuration.Events.FirstOrDefault(e => e.Id == entry.EventId);
                if (definition is null || definition.Reward.Coins <= 0 || string.IsNullOrEmpty(entry.Login))
                {
                    entry.CoinsPaid = true;
                    this.Store.Changed();
                    continue;
                }

                var reason = entry.Winner ? $"Community event {definition.Id}: winner {entry.Character}" : $"Community event {definition.Id}: code entered by {entry.Character}";
                result.Add((entry, new CoinGrant(entry.Login, definition.Reward.CoinType, definition.Reward.Coins, reason, EventReference(entry))));
            }
        }

        return result;
    }

    /// <summary>Notes that an entry's coins were paid.</summary>
    /// <param name="entry">The entry.</param>
    public void MarkCoinsPaid(CommunityEventEntry entry)
    {
        lock (this.Store.Lock)
        {
            entry.CoinsPaid = true;
            this.Store.Changed();
        }
    }

    /// <summary>Gets the entries of an event.</summary>
    /// <param name="eventId">The event id.</param>
    /// <returns>Copies of the entries, newest first.</returns>
    public IReadOnlyList<CommunityEventEntry> EntriesOf(string eventId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Entries.Values.Where(e => e.EventId == eventId).OrderByDescending(e => e.AtUtc)
                .Select(e => new CommunityEventEntry { EventId = e.EventId, Character = e.Character, Login = e.Login, Winner = e.Winner, AtUtc = e.AtUtc, CoinsPaid = e.CoinsPaid })
                .ToList();
        }
    }

    /// <summary>Gets whether an account entered an event's code, or a character is a recorded winner.</summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="login">The login name.</param>
    /// <param name="character">The character name.</param>
    /// <returns>(entered the code, recorded as a winner).</returns>
    public (bool Entered, bool Winner) Took(string eventId, string login, string character)
    {
        lock (this.Store.Lock)
        {
            return (this.Store.Data.CodeAccounts.ContainsKey(eventId + "|" + Key(login)), this.Store.Data.Entries.ContainsKey(EntryKey(eventId, character, true)));
        }
    }

    /// <summary>Gets the rewards which wait for a character.</summary>
    /// <param name="character">The character name.</param>
    /// <returns>A copy of the list.</returns>
    public IReadOnlyList<CommunityOwedReward> Owed(string character)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Owed.TryGetValue(Key(character), out var list) ? list.ToList() : [];
        }
    }

    /// <summary>Takes the rewards which wait for a character (give back the ones which didn't fit with <see cref="ReturnOwed"/>).</summary>
    /// <param name="character">The character name.</param>
    /// <returns>The rewards.</returns>
    public IReadOnlyList<CommunityOwedReward> TakeOwed(string character)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Owed.Remove(Key(character), out var list))
            {
                return [];
            }

            this.Store.Changed();
            return list;
        }
    }

    /// <summary>Gives back rewards which didn't fit.</summary>
    /// <param name="character">The character name.</param>
    /// <param name="rewards">The rewards.</param>
    public void ReturnOwed(string character, IEnumerable<CommunityOwedReward> rewards)
    {
        lock (this.Store.Lock)
        {
            var list = rewards.ToList();
            if (list.Count == 0)
            {
                return;
            }

            if (this.Store.Data.Owed.TryGetValue(Key(character), out var existing))
            {
                existing.AddRange(list.Where(r => existing.All(e => e.Reference != r.Reference)));
            }
            else
            {
                this.Store.Data.Owed[Key(character)] = list;
            }

            this.Store.Changed();
        }
    }

    /// <summary>Gets whether a dropped item counts as rare (by the drop filter and the map).</summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="level">The item level.</param>
    /// <param name="excellent">Whether it's excellent.</param>
    /// <param name="ancient">Whether it's ancient.</param>
    /// <param name="map">The map number.</param>
    /// <returns><c>true</c>, if it's posted.</returns>
    public bool IsRareDrop(int group, int number, int level, bool excellent, bool ancient, int map)
    {
        var filter = this.Configuration.Drops;
        if (filter.Maps.Count > 0 && !filter.Maps.Contains((short)map))
        {
            return false;
        }

        return (excellent && filter.Excellent)
            || (ancient && filter.Ancient)
            || (filter.MinimumLevel > 0 && level >= filter.MinimumLevel)
            || filter.Items.Any(i => ParseItem(i) is { } item && item.Group == group && item.Number == number);
    }

    /// <summary>Takes the drop post slot of a player (one post per player within the cooldown).</summary>
    /// <param name="player">The player's name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c>, if a post may go out.</returns>
    public bool TryTakeDropSlot(string player, DateTime nowUtc)
    {
        var cooldown = TimeSpan.FromSeconds(Math.Max(0, this.Configuration.DropCooldownSeconds));
        var key = Key(player);
        if (this._lastDropPost.TryGetValue(key, out var last) && nowUtc - last < cooldown)
        {
            return false;
        }

        this._lastDropPost[key] = nowUtc;
        return true;
    }

    /// <summary>
    /// Makes a post of a kind: its texts with the values filled in; it goes into the website's highlights when that's on.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="valuesEn">The English values of the placeholders.</param>
    /// <param name="valuesVi">The Vietnamese values; the English ones when <c>null</c>.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The post; <c>null</c> when the kind is off.</returns>
    public CommunityHighlight? Post(CommunityPostKind kind, IReadOnlyDictionary<string, string> valuesEn, IReadOnlyDictionary<string, string>? valuesVi, DateTime nowUtc)
    {
        var setting = this.Configuration.Post(kind);
        if (!setting.Enabled)
        {
            return null;
        }

        var english = Fill(setting.TextEn, valuesEn);
        var vietnamese = Fill(string.IsNullOrWhiteSpace(setting.TextVi) ? setting.TextEn : setting.TextVi, valuesVi ?? valuesEn);
        var highlight = new CommunityHighlight { AtUtc = nowUtc, Kind = kind, TextEn = english, TextVi = vietnamese.Length > 0 ? vietnamese : english };
        if (setting.ToWebsite && english.Length > 0)
        {
            lock (this.Store.Lock)
            {
                var list = this.Store.Data.Highlights;
                list.Insert(0, highlight);
                if (list.Count > HighlightsKept)
                {
                    list.RemoveRange(HighlightsKept, list.Count - HighlightsKept);
                }

                this.Store.Changed();
            }
        }

        return highlight;
    }

    /// <summary>Gets the newest highlights.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The highlights, newest first.</returns>
    public IReadOnlyList<CommunityHighlight> Highlights(int count)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Highlights.Take(Math.Max(0, count)).ToList();
        }
    }

    /// <summary>Removes a highlight (the admin panel).</summary>
    /// <param name="atUtc">Its time.</param>
    /// <param name="textEn">Its English text.</param>
    /// <returns><c>true</c>, if it was removed.</returns>
    public bool RemoveHighlight(DateTime atUtc, string textEn)
    {
        lock (this.Store.Lock)
        {
            var removed = this.Store.Data.Highlights.RemoveAll(h => h.AtUtc == atUtc && h.TextEn == textEn) > 0;
            if (removed)
            {
                this.Store.Changed();
            }

            return removed;
        }
    }

    private static string Key(string text) => (text ?? string.Empty).Trim().ToLowerInvariant();

    private static string EntryKey(string eventId, string character, bool winner) => $"{eventId}|{(winner ? "w" : "c")}|{Key(character)}";

    private static string RandomCode(int length) => string.Create(length, 0, static (span, _) =>
    {
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }
    });

    private static CommunityAccountState Copy(CommunityAccountState s) => new()
    {
        Login = s.Login,
        DiscordPaidAtUtc = s.DiscordPaidAtUtc,
        DiscordUserId = s.DiscordUserId,
        ZaloStatus = s.ZaloStatus,
        ZaloCode = s.ZaloCode,
        ZaloName = s.ZaloName,
        ZaloCharacter = s.ZaloCharacter,
        ZaloRequestedAtUtc = s.ZaloRequestedAtUtc,
        ZaloDecidedAtUtc = s.ZaloDecidedAtUtc,
        ZaloPaidAtUtc = s.ZaloPaidAtUtc,
    };

    private CommunityAccountState State(string login)
    {
        var key = Key(login);
        if (!this.Store.Data.Accounts.TryGetValue(key, out var state))
        {
            state = new CommunityAccountState { Login = login.Trim() };
            this.Store.Data.Accounts[key] = state;
        }

        return state;
    }

    private void AddEntry(CommunityEventDefinition definition, string character, string login, bool winner, DateTime nowUtc)
    {
        var entry = new CommunityEventEntry { EventId = definition.Id, Character = character, Login = login, Winner = winner, AtUtc = nowUtc };
        this.Store.Data.Entries[EntryKey(definition.Id, character, winner)] = entry;
        var reward = definition.Reward;
        if (reward.Zen > 0 || reward.Items.Any(i => i.Count > 0))
        {
            var owed = new CommunityOwedReward
            {
                Reference = EventReference(entry) + ":goods",
                EventId = definition.Id,
                TitleEn = definition.TitleEn,
                TitleVi = definition.TitleVi,
                Reward = new MissionReward { Zen = reward.Zen, Items = reward.Items.Where(i => i.Count > 0).ToList() },
            };
            if (this.Store.Data.Owed.TryGetValue(Key(character), out var list))
            {
                list.Add(owed);
            }
            else
            {
                this.Store.Data.Owed[Key(character)] = [owed];
            }
        }

        this.Store.Changed();
    }
}

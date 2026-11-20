// <copyright file="ArenaGuildWar.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// The HNMO Arena's guild war on one game server (owner's choices, 2026-11-20): on Saturdays and Sundays at 21:00 Sydney
/// two guilds fight on the Valley of Loren. Members of a Season 6.3 guild sign up in the Arena window until ten minutes
/// before; at the start every guild with at least five members in the Arena takes part (ten at most), paired by their
/// guild war record, one match after the other. A kill is a point; a capture point (flag) held alone for a few seconds
/// becomes the guild's and gives it a point every ten seconds. After fifteen minutes the guild with more points wins:
/// +15 arena points for each of its fighters, +4 for the others (and both in a draw). The fallen come back at their
/// side's gate. The week's guild with the most wins is the "Guild war champion".
/// </summary>
public sealed class ArenaGuildWar
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Signup> _signups = [];
    private readonly List<PendingMatch> _pending = [];
    private readonly ArenaConfiguration _configuration;
    private readonly ArenaStore _store;
    private DateTime? _startUtc;
    private bool _noticeSent;
    private bool _closedNoticeSent;
    private bool _forced;
    private Match? _match;
    private int _ticking;

    /// <summary>Initializes a new instance of the <see cref="ArenaGuildWar"/> class.</summary>
    /// <param name="configuration">The Arena's configuration, or the defaults.</param>
    /// <param name="store">The store of the records (the guild war records live there too).</param>
    public ArenaGuildWar(ArenaConfiguration? configuration, ArenaStore store)
    {
        this._configuration = configuration ?? new ArenaConfiguration();
        this._store = store;
    }

    /// <summary>Gets or sets how long the players stay on the battle map after a match, to see the result.</summary>
    public TimeSpan AfterMatch { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets how the guild of a login is found (default: the shared Season 6.3 database).</summary>
    public Func<string, (Guid Id, string Name)?> GuildOf { get; set; } = login => Persistence.ArenaShared.Current?.GuildOf(login);

    /// <summary>Gets a value indicating whether the guild war holds the battle map (a match runs or waits to run).</summary>
    public bool Busy
    {
        get
        {
            lock (this._gate)
            {
                return this._match is not null || this._pending.Count > 0;
            }
        }
    }

    /// <summary>Gets a value indicating whether a match is being fought (or just ended).</summary>
    public bool Running
    {
        get
        {
            lock (this._gate)
            {
                return this._match is not null;
            }
        }
    }

    /// <summary>The side of a player in the running match: 1 or 2, or 0.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The side.</returns>
    public int SideOf(Player player)
    {
        lock (this._gate)
        {
            return this._match is { } match && match.Sides.TryGetValue(player, out var side) ? side : 0;
        }
    }

    /// <summary>Starts the sign-up now for a start in the given time (a game master's command; also for tests).</summary>
    /// <param name="startIn">The time until the start.</param>
    /// <param name="nowUtc">The moment.</param>
    public void ForceStart(TimeSpan startIn, DateTime nowUtc)
    {
        lock (this._gate)
        {
            if (this._match is not null || this._pending.Count > 0)
            {
                return;
            }

            this._startUtc = nowUtc + startIn;
            this._noticeSent = true;
            this._closedNoticeSent = true;
            this._forced = true;
        }
    }

    /// <summary>Signs a player up to fight for their guild (until the sign-up closes).</summary>
    /// <param name="player">The player.</param>
    /// <returns>The EN and VI message.</returns>
    public (string En, string Vi) Join(Player player)
    {
        var now = DateTime.UtcNow;
        lock (this._gate)
        {
            if (!this.SignupOpenLocked(now))
            {
                return ("The guild war sign-up is closed now.", "Đăng ký bang chiến đang đóng.");
            }
        }

        (Guid Id, string Name)? guild;
        try
        {
            guild = player.Account?.LoginName is { Length: > 0 } login ? this.GuildOf(login) : null;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Arena: the guild of {Player} couldn't be read.", player.Name);
            return ("Your guild couldn't be read right now; try again later.", "Chưa đọc được bang của bạn; hãy thử lại sau.");
        }

        if (guild is not { } g)
        {
            return ("Only members of a guild (on Season 6.3) can fight in the guild war.", "Chỉ thành viên bang hội (ở Season 6.3) mới tham gia bang chiến được.");
        }

        lock (this._gate)
        {
            foreach (var other in this._signups.Values)
            {
                other.Players.Remove(player);
            }

            if (!this._signups.TryGetValue(g.Id, out var signup))
            {
                signup = new Signup(g.Name);
                this._signups[g.Id] = signup;
            }

            signup.Players.Add(player);
            return ($"You fight for {g.Name} in the guild war ({signup.Players.Count} signed up; {this._configuration.GuildWarMinMembers} needed).", $"Bạn chiến đấu cho {g.Name} trong bang chiến ({signup.Players.Count} người đã đăng ký; cần {this._configuration.GuildWarMinMembers}).");
        }
    }

    /// <summary>Takes a player off the sign-up list.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if they were signed up.</returns>
    public bool Leave(Player player)
    {
        lock (this._gate)
        {
            return this._signups.Values.Any(s => s.Players.Remove(player));
        }
    }

    /// <summary>Whether a player may hurt another: never their own guild, and fighters only each other.</summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="defender">The defender.</param>
    /// <returns><c>true</c>, if the attack is refused.</returns>
    public bool PreventsAttack(Player attacker, Player defender)
    {
        lock (this._gate)
        {
            if (this._match is not { } match)
            {
                return false;
            }

            var a = match.Sides.GetValueOrDefault(attacker);
            var d = match.Sides.GetValueOrDefault(defender);
            return (a != 0 || d != 0) && (a == d || a == 0 || d == 0 || match.Ended);
        }
    }

    /// <summary>Whether a kill is part of the match (no player-kill penalty).</summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="defender">The defender.</param>
    /// <returns><c>true</c>, if both fight on different sides.</returns>
    public bool AreOpponents(Player attacker, Player defender)
    {
        lock (this._gate)
        {
            return this._match is { } match
                && match.Sides.GetValueOrDefault(attacker) is var a and not 0
                && match.Sides.GetValueOrDefault(defender) is var d and not 0
                && a != d;
        }
    }

    /// <summary>The gate a fighter comes back at: their side's gate on the battle map.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The gate, or <c>null</c>.</returns>
    public ExitGate? RespawnGateOf(Player player)
    {
        var side = this.SideOf(player);
        return side == 0 ? null : this.GateOf(player.GameContext, side);
    }

    /// <summary>Counts a kill of the match: a point for the killer's guild.</summary>
    /// <param name="killed">The fallen player.</param>
    /// <param name="killer">The killer.</param>
    /// <returns>The task.</returns>
    public async ValueTask KilledAsync(Player killed, Player killer)
    {
        Match match;
        lock (this._gate)
        {
            if (this._match is not { Ended: false } m
                || m.Sides.GetValueOrDefault(killer) is not (var k and not 0)
                || m.Sides.GetValueOrDefault(killed) is not (var v and not 0)
                || k == v)
            {
                return;
            }

            match = m;
            if (k == 1)
            {
                m.ScoreA++;
            }
            else
            {
                m.ScoreB++;
            }

            m.Kills[killer] = m.Kills.GetValueOrDefault(killer) + 1;
        }

        await this.TellFightersAsync(match, $"{killer.Name} defeated {killed.Name}. {Score(match)}", $"{killer.Name} hạ {killed.Name}. {Score(match)}").ConfigureAwait(false);
    }

    /// <summary>The guild war part of the Arena window's state, for a player.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The values to put in the JSON.</returns>
    public object StateOf(Player player)
    {
        var now = DateTime.UtcNow;
        var champion = this.Champion(now);
        lock (this._gate)
        {
            var match = this._match;
            var side = match is not null && match.Sides.TryGetValue(player, out var s) ? s : 0;
            var mine = this._signups.FirstOrDefault(e => e.Value.Players.Contains(player));
            return new
            {
                days = this._configuration.GuildWarDays,
                times = this._configuration.GuildWarTimes,
                nextSydney = this._startUtc is { } start ? SydneyText(start, withDay: true) : null,
                secondsToStart = this._startUtc is { } st ? (int)Math.Max(0, Math.Ceiling((st - now).TotalSeconds)) : 0,
                signupOpen = this.SignupOpenLocked(now),
                signedUp = mine.Value is not null,
                myGuild = mine.Value?.Name,
                minMembers = this._configuration.GuildWarMinMembers,
                guilds = this._signups.Values.Where(g => g.Players.Count > 0).OrderByDescending(g => g.Players.Count).Select(g => new { name = g.Name, count = g.Players.Count }).ToList(),
                running = match is not null,
                waiting = this._pending.Count,
                side,
                guildA = match?.GuildA,
                guildB = match?.GuildB,
                scoreA = match?.ScoreA ?? 0,
                scoreB = match?.ScoreB ?? 0,
                secondsLeft = match is null ? 0 : (int)Math.Max(0, Math.Ceiling((match.EndsUtc - now).TotalSeconds)),
                flags = match?.Flags.Select(f => new { x = f.X, y = f.Y, owner = f.Owner, taking = f.Taking, seconds = f.Seconds }).ToList(),
                captureSeconds = this._configuration.GuildWarCaptureSeconds,
                ended = match?.Ended ?? false,
                winner = match?.Winner ?? 0,
                champion,
            };
        }
    }

    /// <summary>The week's guild war champion (most wins this Sydney week), if any guild won.</summary>
    /// <param name="nowUtc">The moment.</param>
    /// <returns>The guild's name.</returns>
    public string? Champion(DateTime nowUtc)
    {
        var week = ArenaRanking.WeekKey(nowUtc);
        return this._store.Read(d => d.Guilds.Values.Where(g => g.Week == week && g.WeekWins > 0)
            .OrderByDescending(g => g.WeekWins).ThenBy(g => g.WeekLosses).ThenBy(g => g.Name, StringComparer.Ordinal).FirstOrDefault()?.Name);
    }

    /// <summary>Plans, announces, starts and ends matches, and keeps the flags (called every second).</summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="teams">The team battles (they share the battle map).</param>
    /// <param name="royale">The battle royale (its fighters can't take part).</param>
    /// <param name="duels">The ranked duels (a fighter leaves their queue).</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The task.</returns>
    public async ValueTask TickAsync(IGameContext gameContext, ArenaTeams teams, ArenaRoyale royale, ArenaDuels duels, ILogger logger)
    {
        if (Interlocked.Exchange(ref this._ticking, 1) == 1)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            (string En, string Vi)? broadcast = null;
            Dictionary<Guid, Signup>? starting = null;
            Match? finish = null;
            Match? close = null;
            Match? running = null;
            PendingMatch? next = null;
            lock (this._gate)
            {
                foreach (var signup in this._signups.Values)
                {
                    signup.Players.RemoveAll(p => p.SelectedCharacter is null || !p.IsConnected);
                }

                if (this._startUtc is null && this._match is null && this._pending.Count == 0
                    && ArenaGuildWarEngine.TryParseDays(this._configuration.GuildWarDays, out var days)
                    && WorldBossSchedule.TryParseTimes(this._configuration.GuildWarTimes, out var times)
                    && ArenaGuildWarEngine.NextStart(days, times, ArenaRanking.SydneyTime, now) is { } nextStart)
                {
                    this._startUtc = nextStart;
                    this._noticeSent = false;
                    this._closedNoticeSent = false;
                    this._forced = false;
                }

                if (this._match is { } match)
                {
                    match.Sides.Keys.Where(p => !p.IsConnected).ToList().ForEach(p => match.Sides.Remove(p));
                    var aLeft = match.Sides.ContainsValue(1);
                    var bLeft = match.Sides.ContainsValue(2);
                    if (!match.Ended && (now >= match.EndsUtc || !aLeft || !bLeft))
                    {
                        match.Ended = true;
                        match.Winner = !aLeft || !bLeft ? (aLeft ? 1 : bLeft ? -1 : 0) : ArenaGuildWarEngine.Winner(match.ScoreA, match.ScoreB);
                        match.ClosesUtc = now + this.AfterMatch;
                        finish = match;
                    }
                    else if (match.Ended && now >= match.ClosesUtc)
                    {
                        this._match = null;
                        close = match;
                    }
                    else if (!match.Ended)
                    {
                        running = match;
                    }
                }
                else if (this._pending.Count > 0)
                {
                    if (!teams.BattleRunning)
                    {
                        next = this._pending[0];
                        this._pending.RemoveAt(0);
                    }
                }
                else if (this._startUtc is { } start)
                {
                    var closeAt = start - TimeSpan.FromMinutes(this._configuration.GuildWarSignupCloseMinutes);
                    if (now >= start)
                    {
                        this._startUtc = null;
                        starting = this._signups.ToDictionary(e => e.Key, e => e.Value);
                        this._signups.Clear();
                    }
                    else if (now >= closeAt && !this._closedNoticeSent)
                    {
                        this._closedNoticeSent = true;
                        this._noticeSent = true;
                        var ready = this._signups.Values.Count(s => s.Players.Count >= this._configuration.GuildWarMinMembers);
                        broadcast = ($"Guild war sign-up closed: {ready} guilds have enough members. The war starts at {SydneyText(start)} (Sydney); stay in the Arena.", $"Đã đóng đăng ký bang chiến: {ready} bang đủ người. Bang chiến bắt đầu lúc {SydneyText(start)} (giờ Sydney); hãy ở lại Đấu trường.");
                    }
                    else if (now >= closeAt - TimeSpan.FromMinutes(10) && !this._noticeSent)
                    {
                        this._noticeSent = true;
                        broadcast = ($"Guild war at {SydneyText(start)} (Sydney) on the Valley of Loren: sign up for your guild in the Arena window (Alt+A) before {SydneyText(closeAt)}.", $"Bang chiến lúc {SydneyText(start)} (giờ Sydney) ở Thung lũng Loren: đăng ký cho bang của bạn trong cửa sổ Đấu trường (Alt+A) trước {SydneyText(closeAt)}.");
                    }
                }
            }

            if (broadcast is { } message)
            {
                foreach (var p in await gameContext.GetPlayersAsync().ConfigureAwait(false))
                {
                    await TellAsync(p, message.En, message.Vi).ConfigureAwait(false);
                }
            }

            if (starting is not null)
            {
                await this.PlanAsync(starting, teams, royale, logger).ConfigureAwait(false);
            }

            if (next is not null)
            {
                await this.StartAsync(gameContext, next, teams, duels, logger).ConfigureAwait(false);
            }

            if (running is not null)
            {
                await this.KeepFlagsAsync(running, now).ConfigureAwait(false);
            }

            if (finish is not null)
            {
                await this.FinishAsync(finish, now, logger).ConfigureAwait(false);
            }

            if (close is not null)
            {
                await this.CloseAsync(gameContext, close).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref this._ticking, 0);
        }
    }

    private static string Score(Match match) => $"{match.GuildA} {match.ScoreA} : {match.ScoreB} {match.GuildB}";

    private static string SydneyText(DateTime utc, bool withDay = false) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ArenaRanking.SydneyTime)
            .ToString(withDay ? "ddd HH:mm" : "HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static ValueTask TellAsync(Player player, string en, string vi) =>
        player.ShowBlueMessageAsync(string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase) ? vi : en);

    private static void Refill(Player player)
    {
        if (player.Attributes is { } attributes)
        {
            attributes[Stats.CurrentHealth] = attributes[Stats.MaximumHealth];
            attributes[Stats.CurrentMana] = attributes[Stats.MaximumMana];
            attributes[Stats.CurrentShield] = attributes[Stats.MaximumShield];
            attributes[Stats.CurrentAbility] = attributes[Stats.MaximumAbility];
        }
    }

    private bool SignupOpenLocked(DateTime nowUtc) =>
        this._match is null && this._pending.Count == 0 && this._startUtc is { } start
        && nowUtc < (this._forced ? start : start - TimeSpan.FromMinutes(this._configuration.GuildWarSignupCloseMinutes));

    private ExitGate? GateOf(IGameContext gameContext, int side)
    {
        var cfg = this._configuration;
        if (gameContext.Configuration.Maps.FirstOrDefault(m => m.Number == cfg.TeamMapNumber) is not { } map)
        {
            return null;
        }

        var (x1, y1, x2, y2) = side == 1 ? (cfg.TeamAX1, cfg.TeamAY1, cfg.TeamAX2, cfg.TeamAY2) : (cfg.TeamBX1, cfg.TeamBY1, cfg.TeamBX2, cfg.TeamBY2);
        return new ExitGate { Map = map, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Direction = side == 1 ? Direction.SouthEast : Direction.NorthWest };
    }

    private async ValueTask TellFightersAsync(Match match, string en, string vi)
    {
        List<Player> fighters;
        lock (this._gate)
        {
            fighters = match.Sides.Keys.ToList();
        }

        foreach (var p in fighters)
        {
            await TellAsync(p, en, vi).ConfigureAwait(false);
        }
    }

    private async ValueTask PlanAsync(Dictionary<Guid, Signup> signups, ArenaTeams teams, ArenaRoyale royale, ILogger logger)
    {
        var cfg = this._configuration;
        var ready = signups.Values
            .Select(s => (s.Name, Players: s.Players.Where(p => p.IsConnected && p.PlayerState.CurrentState == PlayerState.EnteredWorld && teams.SideOf(p) == 0 && !royale.IsFighter(p)).Take(cfg.GuildWarMaxMembers).ToList(), All: s.Players))
            .ToList();
        var enough = ready.Where(r => r.Players.Count >= cfg.GuildWarMinMembers).ToList();
        var records = this._store.Read(d => enough.Select(r => (r.Name, d.Guilds.TryGetValue(r.Name, out var g) ? g.Wins : 0, d.Guilds.TryGetValue(r.Name, out var h) ? h.Losses : 0)).ToList());
        var (pairs, left) = ArenaGuildWarEngine.Pair(records);
        lock (this._gate)
        {
            foreach (var (a, b) in pairs)
            {
                this._pending.Add(new PendingMatch(a, enough.First(r => r.Name == a).Players, b, enough.First(r => r.Name == b).Players));
            }
        }

        foreach (var (name, players, all) in ready)
        {
            var (en, vi) = left == name
                ? ("No other guild came with enough members: your guild has no opponent this time.", "Không có bang nào khác đủ người: bang của bạn không có đối thủ lần này.")
                : players.Count < cfg.GuildWarMinMembers
                    ? ($"Your guild had {players.Count} of the {cfg.GuildWarMinMembers} members needed in the Arena: it can't fight this time.", $"Bang của bạn chỉ có {players.Count} trên {cfg.GuildWarMinMembers} người cần có mặt trong Đấu trường: lần này không được đấu.")
                    : pairs.FirstOrDefault(p => p.A == name || p.B == name) is { A: { } a, B: { } b }
                        ? ($"Guild war: {a} against {b}. Wait for your match; you'll be taken to the Valley of Loren.", $"Bang chiến: {a} đấu {b}. Hãy chờ tới lượt; bạn sẽ được đưa tới Thung lũng Loren.")
                        : (string.Empty, string.Empty);
            if (en.Length > 0)
            {
                foreach (var p in all.Where(p => p.IsConnected))
                {
                    await TellAsync(p, en, vi).ConfigureAwait(false);
                }
            }
        }

        logger.LogInformation("Arena: guild war planned: {Pairs} (no opponent: {Left}).", string.Join(", ", pairs.Select(p => $"{p.A} vs {p.B}")), left ?? "-");
    }

    private async ValueTask StartAsync(IGameContext gameContext, PendingMatch pending, ArenaTeams teams, ArenaDuels duels, ILogger logger)
    {
        var cfg = this._configuration;
        ArenaGuildWarEngine.TryParseFlags(cfg.GuildWarFlags, out var flagTiles);
        var match = new Match(pending.GuildA, pending.GuildB, DateTime.UtcNow + TimeSpan.FromMinutes(cfg.GuildWarMinutes))
        {
            Flags = flagTiles.Select(f => new ArenaGuildWarEngine.Flag(f.X, f.Y)).ToList(),
        };
        foreach (var p in pending.PlayersA.Where(p => p.IsConnected))
        {
            match.Sides[p] = 1;
        }

        foreach (var p in pending.PlayersB.Where(p => p.IsConnected))
        {
            match.Sides[p] = 2;
        }

        lock (this._gate)
        {
            this._match = match;
        }

        var respawn = TimeSpan.FromSeconds(Math.Max(1, cfg.TeamRespawnSeconds));
        var flagsText = string.Join(", ", match.Flags.Select(f => $"({f.X}, {f.Y})"));
        foreach (var (player, side) in match.Sides.ToList())
        {
            teams.Leave(player);
            duels.Leave(player);
            player.RespawnDelay = respawn;
            if (this.GateOf(gameContext, side) is { } gate)
            {
                await player.WarpToAsync(gate).ConfigureAwait(false);
            }

            Refill(player);
            await TellAsync(
                player,
                $"Guild war: {match.GuildA} (red) against {match.GuildB} (blue), {cfg.GuildWarMinutes} minutes. A kill is a point; hold the flags at {flagsText} alone for {cfg.GuildWarCaptureSeconds} s to take them: each gives a point every {cfg.GuildWarFlagPointSeconds} s.",
                $"Bang chiến: {match.GuildA} (đỏ) đấu {match.GuildB} (xanh), {cfg.GuildWarMinutes} phút. Hạ một người được một điểm; đứng một mình ở cờ {flagsText} trong {cfg.GuildWarCaptureSeconds} giây để chiếm: mỗi cờ cho một điểm mỗi {cfg.GuildWarFlagPointSeconds} giây.").ConfigureAwait(false);
        }

        logger.LogInformation("Arena: guild war {A} against {B} started ({Count} fighters).", match.GuildA, match.GuildB, match.Sides.Count);
    }

    private async ValueTask KeepFlagsAsync(Match match, DateTime nowUtc)
    {
        var cfg = this._configuration;
        var changes = new List<(string En, string Vi)>();
        lock (this._gate)
        {
            for (var i = 0; i < match.Flags.Count; i++)
            {
                var flag = match.Flags[i];
                int Near(int side) => match.Sides.Count(e => e.Value == side && e.Key.IsAlive && e.Key.CurrentMap?.MapId == cfg.TeamMapNumber
                    && Math.Max(Math.Abs(e.Key.Position.X - flag.X), Math.Abs(e.Key.Position.Y - flag.Y)) <= cfg.GuildWarFlagRadius);
                var after = ArenaGuildWarEngine.Step(flag, Near(1), Near(2), cfg.GuildWarCaptureSeconds);
                if (after.Owner != flag.Owner)
                {
                    var guild = after.Owner == 1 ? match.GuildA : match.GuildB;
                    changes.Add(($"{guild} took the flag at ({flag.X}, {flag.Y}).", $"{guild} đã chiếm cờ ở ({flag.X}, {flag.Y})."));
                }

                match.Flags[i] = after;
            }

            // Every few seconds each held flag gives its guild a point.
            if (nowUtc >= match.NextFlagPointUtc)
            {
                match.NextFlagPointUtc = nowUtc + TimeSpan.FromSeconds(cfg.GuildWarFlagPointSeconds);
                match.ScoreA += match.Flags.Count(f => f.Owner == 1);
                match.ScoreB += match.Flags.Count(f => f.Owner == 2);
            }
        }

        foreach (var (en, vi) in changes)
        {
            await this.TellFightersAsync(match, $"{en} {Score(match)}", $"{vi} {Score(match)}").ConfigureAwait(false);
        }
    }

    private async ValueTask FinishAsync(Match match, DateTime nowUtc, ILogger logger)
    {
        var cfg = this._configuration;
        var week = ArenaRanking.WeekKey(nowUtc);
        this._store.Mutate(d =>
        {
            foreach (var (name, result) in new[] { (match.GuildA, match.Winner), (match.GuildB, -match.Winner) })
            {
                if (!d.Guilds.TryGetValue(name, out var record))
                {
                    record = new ArenaGuildRecord { Name = name };
                    d.Guilds[name] = record;
                }

                if (record.Week != week)
                {
                    record.Week = week;
                    record.WeekWins = 0;
                    record.WeekLosses = 0;
                }

                switch (result)
                {
                    case > 0:
                        record.Wins++;
                        record.WeekWins++;
                        break;
                    case < 0:
                        record.Losses++;
                        record.WeekLosses++;
                        break;
                    default:
                        record.Draws++;
                        break;
                }
            }

            return (true, 0);
        });

        var day = ArenaRanking.SydneyDay(nowUtc);
        List<(Player Player, int Side)> fighters;
        lock (this._gate)
        {
            fighters = match.Sides.Select(e => (e.Key, e.Value)).ToList();
        }

        foreach (var (player, side) in fighters)
        {
            var won = match.Winner == (side == 1 ? 1 : -1);
            var points = 0;
            if (Persistence.ArenaShared.Current is { } shared && player.Account?.LoginName is { Length: > 0 } login)
            {
                try
                {
                    points = shared.AddPoints(login, won ? cfg.GuildWarPointsWin : cfg.GuildWarPointsLoss, cfg.DailyPointsCap, day);
                }
                catch (Exception ex)
                {
                    player.Logger.LogError(ex, "Arena: the guild war's points couldn't be saved to the shared database.");
                }
            }

            var winnerName = match.Winner > 0 ? match.GuildA : match.GuildB;
            var (en, vi) = match.Winner == 0
                ? ($"The guild war ended in a draw ({Score(match)}). +{points} arena points.", $"Bang chiến hoà ({Score(match)}). +{points} điểm Đấu trường.")
                : won
                    ? ($"{winnerName} won the guild war ({Score(match)})! +{points} arena points.", $"{winnerName} thắng bang chiến ({Score(match)})! +{points} điểm Đấu trường.")
                    : ($"{winnerName} won the guild war ({Score(match)}). +{points} arena points.", $"{winnerName} thắng bang chiến ({Score(match)}). +{points} điểm Đấu trường.");
            await TellAsync(player, en, vi).ConfigureAwait(false);
        }

        logger.LogInformation("Arena: guild war ended {Score} (winner {Winner}).", Score(match), match.Winner);
    }

    private async ValueTask CloseAsync(IGameContext gameContext, Match match)
    {
        var home = this._configuration.HomeGate(gameContext.Configuration);
        foreach (var player in match.Sides.Keys.ToList())
        {
            player.RespawnDelay = null;
            if (!player.IsConnected || !player.IsAlive || home is null)
            {
                continue;
            }

            await player.WarpToAsync(home).ConfigureAwait(false);
            Refill(player);
        }
    }

    private sealed class Signup(string name)
    {
        public string Name { get; } = name;

        public List<Player> Players { get; } = [];
    }

    private sealed record PendingMatch(string GuildA, List<Player> PlayersA, string GuildB, List<Player> PlayersB);

    private sealed class Match(string guildA, string guildB, DateTime endsUtc)
    {
        public string GuildA { get; } = guildA;

        public string GuildB { get; } = guildB;

        public Dictionary<Player, int> Sides { get; } = [];

        public Dictionary<Player, int> Kills { get; } = [];

        public List<ArenaGuildWarEngine.Flag> Flags { get; init; } = [];

        public DateTime EndsUtc { get; } = endsUtc;

        public DateTime NextFlagPointUtc { get; set; }

        public int ScoreA { get; set; }

        public int ScoreB { get; set; }

        public bool Ended { get; set; }

        /// <summary>Gets or sets the winner: 1 for side 1, -1 for side 2, 0 for a draw.</summary>
        public int Winner { get; set; }

        public DateTime ClosesUtc { get; set; }
    }
}

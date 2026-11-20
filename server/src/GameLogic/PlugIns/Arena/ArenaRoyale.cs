// <copyright file="ArenaRoyale.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.WorldBosses;

/// <summary>
/// The HNMO Arena's battle royale on one game server (owner's choices, 2026-11-20): at set Sydney times on Lorencia.
/// A notice goes out ten minutes before, the sign-up opens five minutes before; at the start everyone signed up is
/// spread over the field. One life: the fallen are out and go back to the arena map. A safe zone shrinks towards a
/// point chosen at the start; outside it (and on the town's safe tiles) players lose health every second. The last one
/// standing wins 20 arena points, everyone else 2. One match at a time.
/// </summary>
public sealed class ArenaRoyale
{
    /// <summary>How long before the sign-up the first notice goes out.</summary>
    public static readonly TimeSpan NoticeBeforeSignup = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private readonly List<Player> _signups = [];
    private readonly ArenaConfiguration _configuration;
    private DateTime? _startUtc;
    private bool _noticeSent;
    private bool _signupNoticeSent;
    private Match? _match;
    private int _ticking;

    /// <summary>Initializes a new instance of the <see cref="ArenaRoyale"/> class.</summary>
    /// <param name="configuration">The Arena's configuration, or the defaults.</param>
    public ArenaRoyale(ArenaConfiguration? configuration = null)
    {
        this._configuration = configuration ?? new ArenaConfiguration();
    }

    /// <summary>Gets or sets how long the players stay on the battle map after the end, to see the result.</summary>
    public TimeSpan AfterMatch { get; set; } = TimeSpan.FromSeconds(10);

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

    /// <summary>Gets the next start (UTC), if one is planned.</summary>
    public DateTime? NextStartUtc
    {
        get
        {
            lock (this._gate)
            {
                return this._startUtc;
            }
        }
    }

    /// <summary>Gets a value indicating whether the sign-up is open now.</summary>
    /// <param name="nowUtc">The moment.</param>
    /// <returns><c>true</c>, if open.</returns>
    public bool SignupOpen(DateTime nowUtc)
    {
        lock (this._gate)
        {
            return this._match is null && this._startUtc is { } start && nowUtc >= start - TimeSpan.FromMinutes(this._configuration.RoyaleSignupMinutes) && nowUtc < start;
        }
    }

    /// <summary>Gets a value indicating whether a player takes part in the running match (alive or out).</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if so.</returns>
    public bool IsFighter(Player player)
    {
        lock (this._gate)
        {
            return this._match?.Fighters.ContainsKey(player) ?? false;
        }
    }

    /// <summary>Gets a value indicating whether a player is still standing in the running match.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if so.</returns>
    public bool IsAlive(Player player)
    {
        lock (this._gate)
        {
            return this._match is { Ended: false } match && match.Fighters.TryGetValue(player, out var f) && f.Alive;
        }
    }

    /// <summary>Gets a value indicating whether a player is signed up for the next match.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if so.</returns>
    public bool IsSignedUp(Player player)
    {
        lock (this._gate)
        {
            return this._signups.Contains(player);
        }
    }

    /// <summary>Starts the sign-up now, for a start in the given time (a game master's command; also for tests).</summary>
    /// <param name="startIn">The time until the start.</param>
    /// <param name="nowUtc">The moment.</param>
    public void ForceStart(TimeSpan startIn, DateTime nowUtc)
    {
        lock (this._gate)
        {
            if (this._match is not null)
            {
                return;
            }

            this._startUtc = nowUtc + startIn;
            this._noticeSent = false;
            this._signupNoticeSent = false;
        }
    }

    /// <summary>Signs a player up for the next match (only while the sign-up is open).</summary>
    /// <param name="player">The player.</param>
    /// <returns>The EN and VI message.</returns>
    public (string En, string Vi) Join(Player player)
    {
        var now = DateTime.UtcNow;
        lock (this._gate)
        {
            if (this._match is not null)
            {
                return ("A battle royale is being fought; sign up for the next one.", "Đang có trận đấu sinh tồn; hãy đăng ký trận sau.");
            }

            if (!this.SignupOpenLocked(now))
            {
                return this._startUtc is { } start
                    ? ($"The sign-up opens {this._configuration.RoyaleSignupMinutes} minutes before the start ({SydneyText(start)} Sydney time).", $"Đăng ký mở {this._configuration.RoyaleSignupMinutes} phút trước giờ đấu ({SydneyText(start)} giờ Sydney).")
                    : ("No battle royale is planned.", "Chưa có lịch đấu sinh tồn.");
            }

            if (this._signups.Count >= this._configuration.RoyaleMaxPlayers && !this._signups.Contains(player))
            {
                return ("The battle royale is full.", "Trận đấu sinh tồn đã đủ người.");
            }

            if (!this._signups.Contains(player))
            {
                this._signups.Add(player);
            }

            return ($"You're signed up for the battle royale ({this._signups.Count} so far).", $"Bạn đã đăng ký đấu sinh tồn ({this._signups.Count} người).");
        }
    }

    /// <summary>Takes a player off the sign-up list.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if they were signed up.</returns>
    public bool Leave(Player player)
    {
        lock (this._gate)
        {
            return this._signups.Remove(player);
        }
    }

    /// <summary>Whether a player may hurt another: fighters of the match only each other, while both stand.</summary>
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

            var a = match.Fighters.GetValueOrDefault(attacker);
            var d = match.Fighters.GetValueOrDefault(defender);
            if (a is null && d is null)
            {
                return false;
            }

            return match.Ended || a is not { Alive: true } || d is not { Alive: true };
        }
    }

    /// <summary>Whether a kill is part of the match (no player-kill penalty).</summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="defender">The defender.</param>
    /// <returns><c>true</c>, if both fight in the match.</returns>
    public bool AreOpponents(Player attacker, Player defender)
    {
        lock (this._gate)
        {
            return this._match is { } match && match.Fighters.ContainsKey(attacker) && match.Fighters.ContainsKey(defender);
        }
    }

    /// <summary>A fighter fell (by another, or by the zone when <paramref name="killer"/> is <c>null</c>): they are out.</summary>
    /// <param name="fallen">The fallen player.</param>
    /// <param name="killer">The killer, if any.</param>
    /// <returns>The task.</returns>
    public async ValueTask FellAsync(Player fallen, Player? killer)
    {
        int alive;
        List<Player> fighters;
        lock (this._gate)
        {
            if (this._match is not { Ended: false } match || !match.Fighters.TryGetValue(fallen, out var f) || !f.Alive)
            {
                return;
            }

            f.Alive = false;
            match.Fallen.Add(fallen);
            if (killer is not null && killer != fallen && match.Fighters.TryGetValue(killer, out var k))
            {
                k.Kills++;
            }

            alive = match.Fighters.Values.Count(x => x.Alive);
            fighters = match.Fighters.Keys.ToList();
        }

        foreach (var p in fighters)
        {
            var (en, vi) = killer is not null && killer != fallen
                ? ($"{killer.Name} knocked out {fallen.Name}. {alive} left.", $"{killer.Name} hạ {fallen.Name}. Còn {alive} người.")
                : ($"{fallen.Name} fell outside the safe zone. {alive} left.", $"{fallen.Name} gục ngoài vùng an toàn. Còn {alive} người.");
            await TellAsync(p, en, vi).ConfigureAwait(false);
        }
    }

    /// <summary>The gate a fallen fighter comes back at: the arena's own gate on the arena map.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The gate, or <c>null</c> when the player isn't in the match.</returns>
    public ExitGate? RespawnGateOf(Player player) => this.IsFighter(player) ? this._configuration.HomeGate(player.GameContext.Configuration) : null;

    /// <summary>The battle royale part of the Arena window's state, for a player.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The values to put in the JSON.</returns>
    public object StateOf(Player player)
    {
        var now = DateTime.UtcNow;
        lock (this._gate)
        {
            var match = this._match;
            var me = match is not null && match.Fighters.TryGetValue(player, out var f) ? f : null;
            var zone = match is null ? null : this.ZoneAt(match, now);
            var place = me is null || match is null ? 0 : match.Places.IndexOf(me.Login) + 1;
            return new
            {
                times = this._configuration.RoyaleTimes,
                next = this._startUtc?.ToString("o"),
                nextSydney = this._startUtc is { } s ? SydneyText(s) : null,
                secondsToStart = this._startUtc is { } st ? (int)Math.Max(0, Math.Ceiling((st - now).TotalSeconds)) : 0,
                signupOpen = this.SignupOpenLocked(now),
                signedUp = this._signups.Contains(player),
                signups = this._signups.Count,
                minPlayers = this._configuration.RoyaleMinPlayers,
                running = match is not null,
                fighter = me is not null,
                alive = me?.Alive ?? false,
                kills = me?.Kills ?? 0,
                left = match?.Fighters.Values.Count(x => x.Alive) ?? 0,
                total = match?.Fighters.Count ?? 0,
                secondsLeft = match is null ? 0 : (int)Math.Max(0, Math.Ceiling((match.EndsUtc - now).TotalSeconds)),
                zoneX = zone is null ? 0 : (int)Math.Round(zone.X),
                zoneY = zone is null ? 0 : (int)Math.Round(zone.Y),
                zoneRadius = zone is null ? 0 : (int)Math.Round(zone.Radius),
                finalX = match is null ? 0 : (int)Math.Round(match.FinalCentre.X),
                finalY = match is null ? 0 : (int)Math.Round(match.FinalCentre.Y),
                ended = match?.Ended ?? false,
                place,
                winner = match is { Ended: true } && match.Places.Count > 0 ? match.Fighters.Values.FirstOrDefault(x => x.Login == match.Places[0])?.Name : null,
            };
        }
    }

    /// <summary>Plans, announces, starts and ends matches, and hurts the fighters outside the zone (called every second).</summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="teams">The team battles (a fighter can't be in both).</param>
    /// <param name="duels">The ranked duels (a fighter leaves their queue).</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The task.</returns>
    public async ValueTask TickAsync(IGameContext gameContext, ArenaTeams teams, ArenaDuels duels, ILogger logger)
    {
        if (Interlocked.Exchange(ref this._ticking, 1) == 1)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            (string En, string Vi)? broadcast = null;
            List<Player>? starting = null;
            Match? finish = null;
            Match? close = null;
            Match? running = null;
            lock (this._gate)
            {
                this._signups.RemoveAll(p => p.SelectedCharacter is null || !p.IsConnected);
                if (this._startUtc is null && this._match is null
                    && WorldBossSchedule.TryParseTimes(this._configuration.RoyaleTimes, out var times)
                    && ArenaRoyaleEngine.NextStart(times, ArenaRanking.SydneyTime, now) is { } next)
                {
                    this._startUtc = next;
                    this._noticeSent = false;
                    this._signupNoticeSent = false;
                }

                if (this._match is { } match)
                {
                    foreach (var gone in match.Fighters.Where(e => e.Value.Alive && !e.Key.IsConnected).Select(e => e.Key).ToList())
                    {
                        match.Fighters[gone].Alive = false;
                        match.Fallen.Add(gone);
                    }

                    var alive = match.Fighters.Values.Count(x => x.Alive);
                    if (!match.Ended && (alive <= 1 || now >= match.EndsUtc))
                    {
                        match.Ended = true;
                        match.ClosesUtc = now + this.AfterMatch;
                        match.Places = ArenaRoyaleEngine.Places(
                            match.Fighters.Where(e => e.Value.Alive).Select(e => (e.Value.Login, e.Value.Kills, HealthShare(e.Key))),
                            match.Fallen.Select(p => match.Fighters[p].Login)).ToList();
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
                else if (this._startUtc is { } start)
                {
                    var signupAt = start - TimeSpan.FromMinutes(this._configuration.RoyaleSignupMinutes);
                    if (now >= start)
                    {
                        this._startUtc = null;
                        starting = this._signups.ToList();
                        this._signups.Clear();
                    }
                    else if (now >= signupAt && !this._signupNoticeSent)
                    {
                        this._signupNoticeSent = true;
                        this._noticeSent = true;
                        broadcast = ($"Battle royale sign-up is open: the match starts at {SydneyText(start)} (Sydney). Open the Arena window (Alt+A) to sign up.", $"Đăng ký đấu sinh tồn đã mở: trận bắt đầu lúc {SydneyText(start)} (giờ Sydney). Mở cửa sổ Đấu trường (Alt+A) để đăng ký.");
                    }
                    else if (now >= signupAt - NoticeBeforeSignup && !this._noticeSent)
                    {
                        this._noticeSent = true;
                        broadcast = ($"Battle royale at {SydneyText(start)} (Sydney) on Lorencia: the sign-up opens {this._configuration.RoyaleSignupMinutes} minutes before.", $"Đấu sinh tồn lúc {SydneyText(start)} (giờ Sydney) ở Lorencia: đăng ký mở trước {this._configuration.RoyaleSignupMinutes} phút.");
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
                await this.StartAsync(gameContext, starting, teams, duels, logger).ConfigureAwait(false);
            }

            if (running is not null)
            {
                await this.HurtOutsideAsync(running, now).ConfigureAwait(false);
            }

            if (finish is not null)
            {
                await this.FinishAsync(finish, logger).ConfigureAwait(false);
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

    private static double HealthShare(Player player) =>
        player.Attributes is { } a && a[Stats.MaximumHealth] > 0 ? a[Stats.CurrentHealth] / a[Stats.MaximumHealth] : 0;

    private static string SydneyText(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ArenaRanking.SydneyTime).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

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
        this._match is null && this._startUtc is { } start && nowUtc >= start - TimeSpan.FromMinutes(this._configuration.RoyaleSignupMinutes) && nowUtc < start;

    private ArenaRoyaleEngine.Zone ZoneAt(Match match, DateTime nowUtc) =>
        ArenaRoyaleEngine.ZoneAt(nowUtc - match.StartUtc, this._configuration.RoyaleSettings(), (this._configuration.RoyaleCentreX, this._configuration.RoyaleCentreY), match.FinalCentre);

    private async ValueTask StartAsync(IGameContext gameContext, List<Player> signups, ArenaTeams teams, ArenaDuels duels, ILogger logger)
    {
        var cfg = this._configuration;
        var ready = signups.Where(p => p.IsConnected && p.IsAlive && p.PlayerState.CurrentState == PlayerState.EnteredWorld && teams.SideOf(p) == 0 && p.DuelRoom is null).ToList();
        if (ready.Count < cfg.RoyaleMinPlayers
            || gameContext.Configuration.Maps.FirstOrDefault(m => m.Number == cfg.RoyaleMapNumber) is not { } definition
            || await gameContext.GetMapAsync((ushort)cfg.RoyaleMapNumber).ConfigureAwait(false) is not { } map)
        {
            foreach (var p in signups)
            {
                await TellAsync(p, $"The battle royale is called off: {ready.Count} of the {cfg.RoyaleMinPlayers} players needed came.", $"Trận đấu sinh tồn bị huỷ: chỉ có {ready.Count} trên {cfg.RoyaleMinPlayers} người cần thiết.").ConfigureAwait(false);
            }

            logger.LogInformation("Arena: battle royale called off ({Count} players).", ready.Count);
            return;
        }

        var terrain = map.Terrain;
        var centre = (X: (double)cfg.RoyaleCentreX, Y: (double)cfg.RoyaleCentreY);
        bool Open(int x, int y) => x is >= 0 and < 256 && y is >= 0 and < 256 && terrain.WalkMap[x, y] && !terrain.SafezoneMap[x, y];
        bool OpenAround(int x, int y, int r)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    var (tx, ty) = (x + dx, y + dy);
                    if (tx is >= 0 and < 256 && ty is >= 0 and < 256 && terrain.SafezoneMap[tx, ty])
                    {
                        return false;
                    }
                }
            }

            return Open(x, y);
        }

        // Where the zone closes in: an open tile in reach, with no safe (town) tile near.
        var final = centre;
        var reach = Math.Max(1, cfg.RoyaleStartRadius - cfg.RoyaleFinalRadius) * 0.6;
        for (var i = 0; i < 2000; i++)
        {
            var angle = Rand.NextInt(0, 3600) / 1800.0 * Math.PI;
            var distance = Rand.NextInt(0, (int)reach + 1);
            var (x, y) = ((int)Math.Round(centre.X + (Math.Cos(angle) * distance)), (int)Math.Round(centre.Y + (Math.Sin(angle) * distance)));
            if (OpenAround(x, y, cfg.RoyaleFinalRadius + 2))
            {
                final = (x, y);
                break;
            }
        }

        var match = new Match(DateTime.UtcNow, DateTime.UtcNow + TimeSpan.FromMinutes(cfg.RoyaleMinutes), final);
        foreach (var p in ready)
        {
            match.Fighters[p] = new Fighter(p.Account?.LoginName ?? p.Name, p.Name);
        }

        lock (this._gate)
        {
            this._match = match;
        }

        // Everyone starts on an open tile of the field, spread out (at least 12 tiles apart when the field allows).
        var taken = new List<(int X, int Y)>();
        foreach (var p in ready)
        {
            teams.Leave(p);
            duels.Leave(p);
            (int X, int Y) spot = ((int)centre.X, (int)centre.Y);
            for (var i = 0; i < 3000; i++)
            {
                var (x, y) = (Rand.NextInt(0, 256), Rand.NextInt(0, 256));
                if (Math.Pow(x - centre.X, 2) + Math.Pow(y - centre.Y, 2) > Math.Pow(cfg.RoyaleStartRadius * 0.8, 2) || !Open(x, y))
                {
                    continue;
                }

                spot = (x, y);
                if (taken.All(t => Math.Abs(t.X - x) + Math.Abs(t.Y - y) >= 12) || i > 2500)
                {
                    break;
                }
            }

            taken.Add(spot);
            await p.WarpToAsync(new ExitGate { Map = definition, X1 = (byte)spot.X, Y1 = (byte)spot.Y, X2 = (byte)spot.X, Y2 = (byte)spot.Y, Direction = Direction.South }).ConfigureAwait(false);
            Refill(p);
            await TellAsync(
                p,
                $"Battle royale with {ready.Count} players: one life, the last one standing wins. The safe zone starts shrinking in {cfg.RoyaleGraceMinutes} minutes; outside it you lose health.",
                $"Đấu sinh tồn {ready.Count} người: một mạng, người cuối cùng còn đứng sẽ thắng. Vùng an toàn bắt đầu thu nhỏ sau {cfg.RoyaleGraceMinutes} phút; ở ngoài vùng sẽ mất máu.").ConfigureAwait(false);
        }

        logger.LogInformation("Arena: battle royale started with {Count} players ({Names}); the zone closes in at {X},{Y}.", ready.Count, string.Join(", ", ready.Select(p => p.Name)), final.X, final.Y);
    }

    private async ValueTask HurtOutsideAsync(Match match, DateTime nowUtc)
    {
        var zone = this.ZoneAt(match, nowUtc);
        var share = ArenaRoyaleEngine.DamageShare(nowUtc - match.StartUtc, this._configuration.RoyaleSettings());
        List<Player> alive;
        lock (this._gate)
        {
            alive = match.Fighters.Where(e => e.Value.Alive).Select(e => e.Key).ToList();
        }

        foreach (var p in alive)
        {
            if (!p.IsAlive || p.CurrentMap is not { } map || map.MapId != this._configuration.RoyaleMapNumber || p.Attributes is not { } a)
            {
                continue;
            }

            var (x, y) = (p.Position.X, p.Position.Y);
            if (ArenaRoyaleEngine.Inside(zone, x, y) && !map.Terrain.SafezoneMap[x, y])
            {
                continue;
            }

            var damage = (uint)Math.Max(1, Math.Ceiling(a[Stats.MaximumHealth] * share));
            if (a[Stats.CurrentHealth] <= damage)
            {
                await p.KillInstantlyAsync().ConfigureAwait(false);
            }
            else
            {
                await p.ApplyPoisonDamageAsync(p, damage).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask FinishAsync(Match match, ILogger logger)
    {
        List<(Player Player, Fighter Fighter)> fighters;
        lock (this._gate)
        {
            fighters = match.Fighters.Select(e => (e.Key, e.Value)).ToList();
        }

        var day = ArenaRanking.SydneyDay(DateTime.UtcNow);
        var winnerName = fighters.FirstOrDefault(f => match.Places.Count > 0 && f.Fighter.Login == match.Places[0]).Fighter?.Name ?? "-";
        foreach (var (player, fighter) in fighters)
        {
            var place = match.Places.IndexOf(fighter.Login) + 1;
            var points = 0;
            if (Persistence.ArenaShared.Current is { } shared)
            {
                try
                {
                    points = shared.AddPoints(fighter.Login, ArenaRoyaleEngine.PointsOf(place, this._configuration.RoyaleSettings()), this._configuration.DailyPointsCap, day);
                }
                catch (Exception ex)
                {
                    player.Logger.LogError(ex, "Arena: the battle royale's points couldn't be saved to the shared database.");
                }
            }

            if (!player.IsConnected)
            {
                continue;
            }

            var (en, vi) = place == 1
                ? ($"You won the battle royale! {fighter.Kills} knock-outs, +{points} arena points.", $"Bạn thắng trận đấu sinh tồn! Hạ {fighter.Kills} người, +{points} điểm Đấu trường.")
                : ($"The battle royale is over: {winnerName} won. You came {place} of {fighters.Count} ({fighter.Kills} knock-outs), +{points} arena points.", $"Trận đấu sinh tồn kết thúc: {winnerName} thắng. Bạn hạng {place}/{fighters.Count} (hạ {fighter.Kills} người), +{points} điểm Đấu trường.");
            await TellAsync(player, en, vi).ConfigureAwait(false);
        }

        logger.LogInformation("Arena: battle royale ended; places {Places}.", string.Join(", ", match.Places));
    }

    private async ValueTask CloseAsync(IGameContext gameContext, Match match)
    {
        var home = this._configuration.HomeGate(gameContext.Configuration);
        foreach (var player in match.Fighters.Keys.ToList())
        {
            // The fallen are home already; the survivors go back now.
            if (!player.IsConnected || !player.IsAlive || home is null || player.CurrentMap?.MapId != this._configuration.RoyaleMapNumber)
            {
                continue;
            }

            await player.WarpToAsync(home).ConfigureAwait(false);
            Refill(player);
        }
    }

    private sealed class Fighter(string login, string name)
    {
        public string Login { get; } = login;

        public string Name { get; } = name;

        public bool Alive { get; set; } = true;

        public int Kills { get; set; }
    }

    private sealed class Match(DateTime startUtc, DateTime endsUtc, (double X, double Y) finalCentre)
    {
        public Dictionary<Player, Fighter> Fighters { get; } = [];

        public List<Player> Fallen { get; } = [];

        public DateTime StartUtc { get; } = startUtc;

        public DateTime EndsUtc { get; } = endsUtc;

        public (double X, double Y) FinalCentre { get; } = finalCentre;

        public bool Ended { get; set; }

        public DateTime ClosesUtc { get; set; }

        public List<string> Places { get; set; } = [];
    }
}

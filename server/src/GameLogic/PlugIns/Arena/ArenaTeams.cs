// <copyright file="ArenaTeams.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;

/// <summary>
/// The HNMO Arena's 5v5 team battles on one game server (owner's choice, 2026-11-20): players queue alone or as a party,
/// <see cref="ArenaTeamEngine"/> forms two even teams, and the battle is fought on the Valley of Loren map. Only the other
/// team can be hurt (no player-kill penalty), the fallen come back at their side's gate after a few seconds, and the most
/// kills in ten minutes (or thirty kills first) wins. Both teams get arena points (8 a win, 3 a loss or a draw).
/// One battle at a time.
/// </summary>
public sealed class ArenaTeams
{
    /// <summary>How long the front of the queue waits for a full battle before a smaller even one may start.</summary>
    public static readonly TimeSpan SmallerBattleWait = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private readonly List<Entry> _queue = [];
    private readonly ArenaConfiguration _configuration;
    private long _order;
    private Battle? _battle;
    private int _ticking;

    /// <summary>Initializes a new instance of the <see cref="ArenaTeams"/> class.</summary>
    /// <param name="configuration">The Arena's configuration, or the defaults.</param>
    public ArenaTeams(ArenaConfiguration? configuration = null)
    {
        this._configuration = configuration ?? new ArenaConfiguration();
    }

    /// <summary>Gets or sets how long the players stay on the battle map after the end, to see the result.</summary>
    public TimeSpan AfterBattle { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets the players waiting in the queue.</summary>
    public int Waiting
    {
        get
        {
            lock (this._gate)
            {
                return this._queue.Sum(e => e.Players.Count);
            }
        }
    }

    /// <summary>Gets a value indicating whether a battle is being fought (or just ended).</summary>
    public bool BattleRunning
    {
        get
        {
            lock (this._gate)
            {
                return this._battle is not null;
            }
        }
    }

    /// <summary>Gets a value indicating whether a player is queued.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if queued.</returns>
    public bool IsQueued(Player player)
    {
        lock (this._gate)
        {
            return this._queue.Any(e => e.Players.Contains(player));
        }
    }

    /// <summary>The team side of a player in the battle: 1 or 2, or 0 when not fighting.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The side.</returns>
    public int SideOf(Player player)
    {
        lock (this._gate)
        {
            return this._battle is { } battle && battle.Sides.TryGetValue(player, out var side) ? side : 0;
        }
    }

    /// <summary>Queues a player, with their party (when they lead it) or alone.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The EN and VI message.</returns>
    public (string En, string Vi) Join(Player player)
    {
        if (this.SideOf(player) != 0)
        {
            return ("You're in the team battle already.", "Bạn đang trong trận đấu đội rồi.");
        }

        if (player.DuelRoom is not null)
        {
            return ("Finish your duel first.", "Hãy đấu xong trận tay đôi trước.");
        }

        var group = new List<Player> { player };
        if (player.Party is { } party)
        {
            if (party.PartyMaster != player)
            {
                return ("Only the party leader can queue the party.", "Chỉ trưởng nhóm mới đưa cả nhóm vào hàng chờ.");
            }

            group = party.PartyList.OfType<Player>().Where(p => p.PlayerState.CurrentState == PlayerState.EnteredWorld).ToList();
            if (group.Count > this._configuration.TeamSize)
            {
                return ($"A party may have at most {this._configuration.TeamSize} players.", $"Một nhóm tối đa {this._configuration.TeamSize} người.");
            }
        }

        lock (this._gate)
        {
            // Whoever was queued already (alone or in another group) moves into this group.
            foreach (var entry in this._queue)
            {
                entry.Players.RemoveAll(group.Contains);
            }

            this._queue.RemoveAll(e => e.Players.Count == 0);
            this._queue.Add(new Entry(group, ++this._order, DateTime.UtcNow));
        }

        return group.Count == 1
            ? ($"Looking for a {this._configuration.TeamSize}v{this._configuration.TeamSize} team battle...", $"Đang tìm trận đấu đội {this._configuration.TeamSize}v{this._configuration.TeamSize}...")
            : ($"Your party of {group.Count} is looking for a team battle...", $"Nhóm {group.Count} người của bạn đang tìm trận đấu đội...");
    }

    /// <summary>Takes a player (and the group they queued with) out of the queue.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if they were queued.</returns>
    public bool Leave(Player player)
    {
        lock (this._gate)
        {
            return this._queue.RemoveAll(e => e.Players.Contains(player)) > 0;
        }
    }

    /// <summary>Whether a player may hurt another: never a teammate, and fighters only each other.</summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="defender">The defender.</param>
    /// <returns><c>true</c>, if the attack is refused.</returns>
    public bool PreventsAttack(Player attacker, Player defender)
    {
        lock (this._gate)
        {
            if (this._battle is not { } battle)
            {
                return false;
            }

            var a = battle.Sides.GetValueOrDefault(attacker);
            var d = battle.Sides.GetValueOrDefault(defender);
            return (a != 0 || d != 0) && (a == d || a == 0 || d == 0 || battle.Ended);
        }
    }

    /// <summary>Whether a kill is part of the battle (no player-kill penalty).</summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="defender">The defender.</param>
    /// <returns><c>true</c>, if both fight on different sides.</returns>
    public bool AreOpponents(Player attacker, Player defender)
    {
        lock (this._gate)
        {
            return this._battle is { } battle
                && battle.Sides.GetValueOrDefault(attacker) is var a and not 0
                && battle.Sides.GetValueOrDefault(defender) is var d and not 0
                && a != d;
        }
    }

    /// <summary>The gate a fighter comes back at, if they fight.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The gate, or <c>null</c>.</returns>
    public ExitGate? RespawnGateOf(Player player)
    {
        var side = this.SideOf(player);
        return side == 0 ? null : this.GateOf(player.GameContext, side);
    }

    /// <summary>Counts a kill of the battle.</summary>
    /// <param name="killed">The fallen player.</param>
    /// <param name="killer">The killer.</param>
    /// <returns>The task.</returns>
    public async ValueTask KilledAsync(Player killed, Player killer)
    {
        int scoreA, scoreB, side;
        List<Player> fighters;
        lock (this._gate)
        {
            if (this._battle is not { Ended: false } battle
                || battle.Sides.GetValueOrDefault(killer) is not (var k and not 0)
                || battle.Sides.GetValueOrDefault(killed) is not (var v and not 0)
                || k == v)
            {
                return;
            }

            side = k;
            if (k == 1)
            {
                battle.ScoreA++;
            }
            else
            {
                battle.ScoreB++;
            }

            battle.Kills[killer] = battle.Kills.GetValueOrDefault(killer) + 1;
            (scoreA, scoreB) = (battle.ScoreA, battle.ScoreB);
            fighters = battle.Sides.Keys.ToList();
        }

        foreach (var p in fighters)
        {
            await TellAsync(
                p,
                $"{killer.Name} ({TeamName(side, false)}) defeated {killed.Name}. Red {scoreA} : {scoreB} Blue",
                $"{killer.Name} ({TeamName(side, true)}) hạ {killed.Name}. Đỏ {scoreA} : {scoreB} Xanh").ConfigureAwait(false);
        }
    }

    /// <summary>The team part of the Arena window's state, for a player.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The values to put in the JSON.</returns>
    public object StateOf(Player player)
    {
        lock (this._gate)
        {
            var battle = this._battle;
            var side = battle is not null && battle.Sides.TryGetValue(player, out var s) ? s : 0;
            return new
            {
                queued = this._queue.Any(e => e.Players.Contains(player)),
                waiting = this._queue.Sum(e => e.Players.Count),
                size = this._configuration.TeamSize,
                minSize = this._configuration.TeamMinSize,
                killsToWin = this._configuration.TeamKillsToWin,
                minutes = this._configuration.TeamMinutes,
                running = battle is not null,
                side,
                scoreA = battle?.ScoreA ?? 0,
                scoreB = battle?.ScoreB ?? 0,
                secondsLeft = battle is null ? 0 : (int)Math.Max(0, Math.Ceiling((battle.EndsUtc - DateTime.UtcNow).TotalSeconds)),
                ended = battle?.Ended ?? false,
                winner = battle?.Winner ?? 0,
                teamA = battle?.Sides.Where(e => e.Value == 1).Select(e => new { name = e.Key.Name, kills = battle.Kills.GetValueOrDefault(e.Key) }).ToList(),
                teamB = battle?.Sides.Where(e => e.Value == 2).Select(e => new { name = e.Key.Name, kills = battle.Kills.GetValueOrDefault(e.Key) }).ToList(),
            };
        }
    }

    /// <summary>Forms and starts battles, ends them on time or score and sends the players back (called every second).</summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="duels">The ranked duels (a fighter leaves their queue).</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The task.</returns>
    public async ValueTask TickAsync(IGameContext gameContext, ArenaDuels duels, ILogger logger)
    {
        if (Interlocked.Exchange(ref this._ticking, 1) == 1)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            Battle? start = null;
            Battle? finish = null;
            Battle? close = null;
            lock (this._gate)
            {
                // Only who left the game leaves the queue: a map change (or a respawn) is no reason.
                this._queue.ForEach(e => e.Players.RemoveAll(p => p.SelectedCharacter is null || !p.IsConnected));
                this._queue.RemoveAll(e => e.Players.Count == 0);
                if (this._battle is { } battle)
                {
                    battle.Sides.Keys.Where(p => !p.IsConnected).ToList().ForEach(p => battle.Sides.Remove(p));
                    var redLeft = battle.Sides.ContainsValue(1);
                    var blueLeft = battle.Sides.ContainsValue(2);
                    if (!battle.Ended && (!redLeft || !blueLeft))
                    {
                        // A team that left the game entirely loses at once (both gone: a draw), so the next battle can start.
                        battle.Ended = true;
                        battle.Winner = redLeft ? 1 : blueLeft ? -1 : 0;
                        battle.ClosesUtc = now + this.AfterBattle;
                        finish = battle;
                    }
                    else if (!battle.Ended
                        && ArenaTeamEngine.Outcome(battle.ScoreA, battle.ScoreB, this._configuration.TeamKillsToWin, now >= battle.EndsUtc) is { } outcome)
                    {
                        battle.Ended = true;
                        battle.Winner = outcome.Winner;
                        battle.ClosesUtc = now + this.AfterBattle;
                        finish = battle;
                    }
                    else if (battle.Ended && now >= battle.ClosesUtc)
                    {
                        this._battle = null;
                        close = battle;
                    }
                }
                else if (this._queue.SelectMany(e => e.Players).All(p => p.PlayerState.CurrentState == PlayerState.EnteredWorld && p.IsAlive))
                {
                    var groups = this._queue.Select(e => new ArenaTeamEngine.Group(e.Players.Select(p => p.Account?.LoginName ?? p.Name).ToList(), e.Order, 0)).ToList();
                    var frontWaitedLong = this._queue.Count > 0 && now - this._queue.Min(e => e.JoinedUtc) >= SmallerBattleWait;
                    if (ArenaTeamEngine.FormTeams(groups, this._configuration.TeamSize, this._configuration.TeamMinSize, frontWaitedLong) is { } match)
                    {
                        var byLogin = this._queue.SelectMany(e => e.Players).GroupBy(p => p.Account?.LoginName ?? p.Name).ToDictionary(g => g.Key, g => g.First());
                        start = new Battle(now + TimeSpan.FromMinutes(this._configuration.TeamMinutes));
                        foreach (var login in match.TeamA)
                        {
                            start.Sides[byLogin[login]] = 1;
                        }

                        foreach (var login in match.TeamB)
                        {
                            start.Sides[byLogin[login]] = 2;
                        }

                        this._queue.RemoveAll(e => e.Players.Any(start.Sides.ContainsKey));
                        this._battle = start;
                    }
                }
            }

            if (start is not null)
            {
                await this.StartAsync(gameContext, start, duels, logger).ConfigureAwait(false);
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

    private static string TeamName(int side, bool vi) => (side, vi) switch
    {
        (1, false) => "Red",
        (1, true) => "Đỏ",
        (_, false) => "Blue",
        _ => "Xanh",
    };

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

    private async ValueTask StartAsync(IGameContext gameContext, Battle battle, ArenaDuels duels, ILogger logger)
    {
        var respawn = TimeSpan.FromSeconds(Math.Max(1, this._configuration.TeamRespawnSeconds));
        List<(Player Player, int Side)> fighters;
        lock (this._gate)
        {
            fighters = battle.Sides.Select(e => (e.Key, e.Value)).ToList();
        }

        var size = fighters.Count / 2;
        foreach (var (player, side) in fighters)
        {
            duels.Leave(player);
            player.RespawnDelay = respawn;
            if (this.GateOf(gameContext, side) is { } gate)
            {
                await player.WarpToAsync(gate).ConfigureAwait(false);
            }

            Refill(player);
            var mates = string.Join(", ", fighters.Where(f => f.Side == side && f.Player != player).Select(f => f.Player.Name));
            await TellAsync(
                player,
                $"Team battle {size}v{size} on the {TeamName(side, false)} team{(mates.Length > 0 ? " with " + mates : string.Empty)}: the most kills in {this._configuration.TeamMinutes} minutes, or {this._configuration.TeamKillsToWin} kills first, wins.",
                $"Trận đấu đội {size}v{size}, bạn ở đội {TeamName(side, true)}{(mates.Length > 0 ? " cùng " + mates : string.Empty)}: đội hạ nhiều hơn trong {this._configuration.TeamMinutes} phút, hoặc đủ {this._configuration.TeamKillsToWin} mạng trước, sẽ thắng.").ConfigureAwait(false);
        }

        logger.LogInformation("Arena: team battle {Size}v{Size} started ({Players}).", size, size, string.Join(", ", fighters.Select(f => $"{f.Player.Name}:{f.Side}")));
    }

    private async ValueTask FinishAsync(Battle battle, ILogger logger)
    {
        List<(Player Player, int Side)> fighters;
        lock (this._gate)
        {
            fighters = battle.Sides.Select(e => (e.Key, e.Value)).ToList();
        }

        var now = DateTime.UtcNow;
        var day = ArenaRanking.SydneyDay(now);
        foreach (var (player, side) in fighters)
        {
            var won = battle.Winner == (side == 1 ? 1 : -1);
            var points = 0;
            if (Persistence.ArenaShared.Current is { } shared && player.Account?.LoginName is { Length: > 0 } login)
            {
                try
                {
                    points = shared.AddPoints(login, won ? this._configuration.TeamPointsPerWin : this._configuration.TeamPointsPerLoss, this._configuration.DailyPointsCap, day);
                }
                catch (Exception ex)
                {
                    player.Logger.LogError(ex, "Arena: the team battle's points couldn't be saved to the shared database.");
                }
            }

            var score = $"{battle.ScoreA} : {battle.ScoreB}";
            var (en, vi) = battle.Winner == 0
                ? ($"The team battle ended in a draw ({score}). +{points} arena points.", $"Trận đấu đội hoà ({score}). +{points} điểm Đấu trường.")
                : won
                    ? ($"Your team won the battle ({score})! +{points} arena points.", $"Đội bạn thắng ({score})! +{points} điểm Đấu trường.")
                    : ($"Your team lost the battle ({score}). +{points} arena points.", $"Đội bạn thua ({score}). +{points} điểm Đấu trường.");
            await TellAsync(player, en, vi).ConfigureAwait(false);
        }

        logger.LogInformation("Arena: team battle ended {A}:{B} (winner {Winner}).", battle.ScoreA, battle.ScoreB, battle.Winner);
    }

    private async ValueTask CloseAsync(IGameContext gameContext, Battle battle)
    {
        var cfg = this._configuration;
        var home = cfg.HomeGate(gameContext.Configuration);
        foreach (var player in battle.Sides.Keys.ToList())
        {
            player.RespawnDelay = null;

            // The fallen come back on the battle map by themselves; the Arena's return then brings them home.
            if (!player.IsConnected || !player.IsAlive || home is null)
            {
                continue;
            }

            await player.WarpToAsync(home).ConfigureAwait(false);
            Refill(player);
        }
    }

    private sealed record Entry(List<Player> Players, long Order, DateTime JoinedUtc);

    private sealed class Battle(DateTime endsUtc)
    {
        public Dictionary<Player, int> Sides { get; } = [];

        public Dictionary<Player, int> Kills { get; } = [];

        public DateTime EndsUtc { get; } = endsUtc;

        public int ScoreA { get; set; }

        public int ScoreB { get; set; }

        public bool Ended { get; set; }

        /// <summary>Gets or sets the winner: 1 red, -1 blue, 0 a draw.</summary>
        public int Winner { get; set; }

        public DateTime ClosesUtc { get; set; }
    }
}

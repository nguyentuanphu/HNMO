// <copyright file="ArenaDuels.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.Text.Json;
using System.Threading;
using HNMO.GameLogic.PlayerActions.Duel;

/// <summary>
/// The HNMO Arena's ranked 1v1 duels on one game server: a queue matched by rating (Elo), and a real duel between the
/// two (the game's duel arena, first to <see cref="RoundsToWin"/> kills: best of 3). The result moves both ratings and
/// is kept in the Arena's state file; the players then come back to the arena map.
/// </summary>
public sealed class ArenaDuels
{
    /// <summary>Kills to win a ranked duel (best of 3).</summary>
    public const int RoundsToWin = 2;

    private static readonly TimeZoneInfo SydneyZone = ResolveSydney();

    private readonly object _gate = new();
    private readonly List<(Player Player, DateTime JoinedUtc)> _queue = [];
    private readonly ArenaStore _store;
    private readonly ArenaConfiguration _configuration;
    private int _matching;

    /// <summary>Initializes a new instance of the <see cref="ArenaDuels"/> class.</summary>
    /// <param name="fileName">The state file.</param>
    /// <param name="configuration">The Arena's configuration (points), or the defaults.</param>
    public ArenaDuels(string? fileName, ArenaConfiguration? configuration = null)
    {
        this._store = ArenaStore.Open(fileName);
        this._configuration = configuration ?? new ArenaConfiguration();
    }

    /// <summary>Gets the store of the ranked records.</summary>
    public ArenaStore Store => this._store;

    /// <summary>Gets how many players wait.</summary>
    public int Waiting
    {
        get
        {
            lock (this._gate)
            {
                return this._queue.Count;
            }
        }
    }

    /// <summary>The website's leaderboards as JSON (all time and this Sydney week).</summary>
    /// <param name="store">The store.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string LeaderboardJson(ArenaStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var now = DateTime.UtcNow;
        var (allTime, week) = store.Read(d => ArenaRanking.Leaderboards(d, now, 50));
        static object Row(ArenaPlayerRecord r) => new { name = r.CharacterName, className = r.ClassName, rating = r.Rating, wins = r.Wins, losses = r.Losses, weekWins = r.WeekWins, weekLosses = r.WeekLosses };
        var weekKey = ArenaRanking.WeekKey(now);
        var guilds = store.Read(d => d.Guilds.Values.ToList());
        var guildWeek = guilds.Where(g => g.Week == weekKey && g.WeekWins + g.WeekLosses > 0).OrderByDescending(g => g.WeekWins).ThenBy(g => g.WeekLosses).ThenBy(g => g.Name, StringComparer.Ordinal)
            .Select(g => new { name = g.Name, wins = g.WeekWins, losses = g.WeekLosses }).Take(20).ToList();
        var guildAllTime = guilds.OrderByDescending(g => g.Wins).ThenBy(g => g.Losses).ThenBy(g => g.Name, StringComparer.Ordinal)
            .Select(g => new { name = g.Name, wins = g.Wins, losses = g.Losses, draws = g.Draws }).Take(20).ToList();
        return JsonSerializer.Serialize(new
        {
            week = weekKey,
            allTime = allTime.Select(Row),
            thisWeek = week.Select(Row),
            guildChampion = guildWeek.FirstOrDefault(g => g.wins > 0)?.name,
            guildWeek,
            guildAllTime,
        });
    }

    /// <summary>Gets a value indicating whether a player waits in the queue.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if queued.</returns>
    public bool IsQueued(Player player)
    {
        lock (this._gate)
        {
            return this._queue.Any(e => e.Player == player);
        }
    }

    /// <summary>Puts a player in the queue.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The EN and VI message.</returns>
    public (string En, string Vi) Join(Player player)
    {
        if (player.DuelRoom is not null)
        {
            return ("You're in a duel already.", "Bạn đang đấu tay đôi rồi.");
        }

        lock (this._gate)
        {
            if (this._queue.All(e => e.Player != player))
            {
                this._queue.Add((player, DateTime.UtcNow));
            }
        }

        return ("Looking for a ranked 1v1 opponent...", "Đang tìm đối thủ đấu xếp hạng 1v1...");
    }

    /// <summary>Takes a player out of the queue.</summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if it was queued.</returns>
    public bool Leave(Player player)
    {
        lock (this._gate)
        {
            return this._queue.RemoveAll(e => e.Player == player) > 0;
        }
    }

    /// <summary>Matches waiting players and starts their duels (one round at a time).</summary>
    /// <param name="logger">The logger.</param>
    /// <returns>The task.</returns>
    public async ValueTask MatchAsync(ILogger logger)
    {
        if (Interlocked.Exchange(ref this._matching, 1) == 1)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            List<(Player A, Player B)> pairs = [];
            lock (this._gate)
            {
                // Only who left the game (or is in a duel) leaves the queue; a map change is no reason.
                this._queue.RemoveAll(e => e.Player.SelectedCharacter is null || !e.Player.IsConnected || e.Player.DuelRoom is not null);
                var ratings = this._store.Read(d => this._queue.ToDictionary(e => e.Player, e => ArenaRanking.Of(d, e.Player.Account?.LoginName ?? string.Empty, now).Rating));
                var waiting = this._queue.Where(e => e.Player.PlayerState.CurrentState == PlayerState.EnteredWorld && e.Player.IsAlive).OrderBy(e => e.JoinedUtc).ToList();
                var taken = new HashSet<Player>();
                foreach (var (a, joined) in waiting)
                {
                    if (taken.Contains(a))
                    {
                        continue;
                    }

                    var gap = ArenaRanking.MatchGap(now - joined);
                    var b = waiting.Where(e => e.Player != a && !taken.Contains(e.Player) && e.Player.CurrentMap == a.CurrentMap
                            && !string.Equals(e.Player.Account?.LoginName, a.Account?.LoginName, StringComparison.OrdinalIgnoreCase)
                            && Math.Abs(ratings[e.Player] - ratings[a]) <= gap)
                        .OrderBy(e => Math.Abs(ratings[e.Player] - ratings[a])).Select(e => e.Player).FirstOrDefault();
                    if (b is not null)
                    {
                        taken.Add(a);
                        taken.Add(b);
                        pairs.Add((a, b));
                    }
                }

                this._queue.RemoveAll(e => taken.Contains(e.Player));
            }

            foreach (var (a, b) in pairs)
            {
                await this.StartAsync(a, b, logger).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref this._matching, 0);
        }
    }

    /// <summary>The ranked part of the Arena window's state, for a player.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The values to put in the JSON.</returns>
    public object StateOf(Player player)
    {
        var now = DateTime.UtcNow;
        var login = player.Account?.LoginName ?? string.Empty;
        var (me, allTime, week) = this._store.Read(d => (ArenaRanking.Of(d, login, now), ArenaRanking.Leaderboards(d, now, 10).AllTime, ArenaRanking.Leaderboards(d, now, 10).Week));
        static object Row(ArenaPlayerRecord r) => new { name = r.CharacterName, className = r.ClassName, rating = r.Rating, wins = r.Wins, losses = r.Losses, weekWins = r.WeekWins, weekLosses = r.WeekLosses };
        return new
        {
            rating = me.Rating,
            wins = me.Wins,
            losses = me.Losses,
            weekWins = me.WeekWins,
            weekLosses = me.WeekLosses,
            queued = this.IsQueued(player),
            inDuel = player.DuelRoom is not null,
            waiting = this.Waiting,
            top = allTime.Select(Row).ToList(),
            week = week.Select(Row).ToList(),
        };
    }

    /// <summary>Records a ranked duel's result: both ratings, arena points, and tells both players.</summary>
    /// <param name="winner">The winner.</param>
    /// <param name="loser">The loser.</param>
    /// <returns>The task.</returns>
    internal async ValueTask RecordAsync(Player winner, Player loser)
    {
        var now = DateTime.UtcNow;
        var (w, l) = this._store.Mutate(d =>
        {
            var before = (Winner: ArenaRanking.Of(d, winner.Account?.LoginName ?? string.Empty, now).Rating, Loser: ArenaRanking.Of(d, loser.Account?.LoginName ?? string.Empty, now).Rating);
            var after = ArenaRanking.Record(
                d,
                (winner.Account?.LoginName ?? winner.Name, winner.Name, ClassOf(winner)),
                (loser.Account?.LoginName ?? loser.Name, loser.Name, ClassOf(loser)),
                now);
            return (true, ((Rating: after.Winner.Rating, Change: after.Winner.Rating - before.Winner), (Rating: after.Loser.Rating, Change: after.Loser.Rating - before.Loser)));
        });

        // Arena points and the ratings go to the shared database: points are spent at home, ratings give the titles.
        var (winnerPoints, loserPoints) = (0, 0);
        if (Persistence.ArenaShared.Current is { } shared)
        {
            var day = TimeZoneInfo.ConvertTimeFromUtc(now, SydneyZone).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                if (winner.Account?.LoginName is { Length: > 0 } wl)
                {
                    winnerPoints = shared.AddPoints(wl, this._configuration.PointsPerWin, this._configuration.DailyPointsCap, day);
                    shared.SetRating(wl, w.Rating);
                }

                if (loser.Account?.LoginName is { Length: > 0 } ll)
                {
                    loserPoints = shared.AddPoints(ll, this._configuration.PointsPerLoss, this._configuration.DailyPointsCap, day);
                    shared.SetRating(ll, l.Rating);
                }
            }
            catch (Exception ex)
            {
                winner.Logger.LogError(ex, "Arena: the points and ratings couldn't be saved to the shared database.");
            }
        }

        await TellAsync(winner, $"You won the ranked 1v1! Rating {w.Rating} (+{w.Change}), +{winnerPoints} arena points.", $"Bạn thắng trận xếp hạng 1v1! Điểm {w.Rating} (+{w.Change}), +{winnerPoints} điểm Đấu trường.").ConfigureAwait(false);
        await TellAsync(loser, $"You lost the ranked 1v1. Rating {l.Rating} ({l.Change}), +{loserPoints} arena points.", $"Bạn thua trận xếp hạng 1v1. Điểm {l.Rating} ({l.Change}), +{loserPoints} điểm Đấu trường.").ConfigureAwait(false);

        static async ValueTask TellAsync(Player player, string en, string vi) =>
            await player.ShowBlueMessageAsync(player.Culture.TwoLetterISOLanguageName == "vi" ? vi : en).ConfigureAwait(false);
    }

    private static TimeZoneInfo ResolveSydney()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }

    private static string ClassOf(Player player) => player.SelectedCharacter?.CharacterClass?.Name.ToString() ?? string.Empty;

    private async ValueTask StartAsync(Player a, Player b, ILogger logger)
    {
        if (await a.GameContext.DuelRoomManager.GetFreeDuelRoomAsync(a, b).ConfigureAwait(false) is not { } room)
        {
            // No free duel arena: both wait again, first in line.
            lock (this._gate)
            {
                this._queue.Insert(0, (b, DateTime.UtcNow));
                this._queue.Insert(0, (a, DateTime.UtcNow));
            }

            return;
        }

        room.MaximumScore = RoundsToWin;
        room.Finished = this.RecordAsync;
        room.State = DuelState.DuelRequested;
        a.DuelRoom = room;
        b.DuelRoom = room;
        foreach (var p in new[] { a, b })
        {
            var other = p == a ? b : a;
            var vi = p.Culture.TwoLetterISOLanguageName == "vi";
            await p.ShowBlueMessageAsync(vi
                ? $"Đấu xếp hạng 1v1 với {other.Name}: ai hạ đối thủ {RoundsToWin} lần trước thì thắng."
                : $"Ranked 1v1 against {other.Name}: the first to defeat the other {RoundsToWin} times wins.").ConfigureAwait(false);
        }

        // The game's duel asks an entrance fee in zen; arena characters carry none (zen means nothing in the Arena), so
        // both get exactly the fee, which the duel takes again.
        var fee = a.GameContext.Configuration.DuelConfiguration?.EntranceFee ?? 0;
        foreach (var p in new[] { a, b })
        {
            if (p.Money < fee)
            {
                p.TryAddMoney(fee - p.Money);
            }
        }

        // The game's own duel: b takes a's request (the same checks and the warp into a duel arena).
        await new DuelActions().HandleDuelResponseAsync(b, a, true).ConfigureAwait(false);
        logger.LogInformation("Arena: ranked 1v1 {A} against {B}.", a.Name, b.Name);
    }
}

// <copyright file="ArenaRoyaleTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Arena;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>The Arena's battle royale: the schedule, the shrinking zone, the places and points, and a live match.</summary>
[TestFixture]
public sealed class ArenaRoyaleTest
{
    private static readonly ArenaRoyaleEngine.Settings Settings = new ArenaConfiguration().RoyaleSettings();

    /// <summary>The next start is the next set Sydney time; after the last of the day, the first of the next day.</summary>
    [Test]
    public void TheNextStartIsTheNextSydneyTime()
    {
        var sydney = ArenaRanking.SydneyTime;
        List<TimeOnly> times = [new(12, 0), new(20, 0), new(22, 0)];

        // 2026-11-20 21:00 Sydney (daylight saving, +11) is 10:00 UTC.
        var next = ArenaRoyaleEngine.NextStart(times, sydney, new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc));
        Assert.That(next, Is.EqualTo(new DateTime(2026, 11, 20, 11, 0, 0, DateTimeKind.Utc)), "22:00 Sydney");

        var late = ArenaRoyaleEngine.NextStart(times, sydney, new DateTime(2026, 11, 20, 11, 30, 0, DateTimeKind.Utc));
        Assert.That(late, Is.EqualTo(new DateTime(2026, 11, 21, 1, 0, 0, DateTimeKind.Utc)), "12:00 Sydney the next day");
        Assert.That(ArenaRoyaleEngine.NextStart([], sydney, DateTime.UtcNow), Is.Null);
    }

    /// <summary>The zone covers the field during the grace time, then shrinks and moves to the final centre.</summary>
    [Test]
    public void TheZoneShrinksTowardsTheFinalCentre()
    {
        var start = ArenaRoyaleEngine.ZoneAt(TimeSpan.FromMinutes(1), Settings, (128, 128), (60, 200));
        Assert.That(start, Is.EqualTo(new ArenaRoyaleEngine.Zone(128, 128, 120)));

        var half = ArenaRoyaleEngine.ZoneAt(TimeSpan.FromMinutes(Settings.GraceMinutes + (Settings.ShrinkMinutes / 2.0)), Settings, (128, 128), (60, 200));
        Assert.That(half.Radius, Is.EqualTo(63).Within(0.01));
        Assert.That(half.X, Is.EqualTo(94).Within(0.01));

        var end = ArenaRoyaleEngine.ZoneAt(TimeSpan.FromMinutes(30), Settings, (128, 128), (60, 200));
        Assert.That(end, Is.EqualTo(new ArenaRoyaleEngine.Zone(60, 200, 6)));
        Assert.That(ArenaRoyaleEngine.Inside(end, 64, 200), Is.True);
        Assert.That(ArenaRoyaleEngine.Inside(end, 67, 200), Is.False);
    }

    /// <summary>Outside the zone the loss grows from 2 % to 10 % of the health a second.</summary>
    [Test]
    public void TheZoneHurtsMoreLater()
    {
        Assert.That(ArenaRoyaleEngine.DamageShare(TimeSpan.Zero, Settings), Is.EqualTo(0.02).Within(1e-9));
        Assert.That(ArenaRoyaleEngine.DamageShare(TimeSpan.FromMinutes(Settings.Minutes), Settings), Is.EqualTo(0.10).Within(1e-9));
    }

    /// <summary>Survivors come first (by kills, then health), then the fallen, the last to fall first; only the winner gets many points.</summary>
    [Test]
    public void PlacesAndPoints()
    {
        var places = ArenaRoyaleEngine.Places([("a", 1, 0.5), ("b", 3, 0.1)], ["c", "d"]);
        Assert.That(places, Is.EqualTo(new[] { "b", "a", "d", "c" }));
        Assert.That(ArenaRoyaleEngine.PointsOf(1, Settings), Is.EqualTo(20));
        Assert.That(ArenaRoyaleEngine.PointsOf(2, Settings), Is.EqualTo(2));
    }

    /// <summary>The sign-up is only open in the minutes before the start.</summary>
    [Test]
    public async Task TheSignupOpensBeforeTheStartAsync()
    {
        var (royale, _, players) = await CreateAsync(1).ConfigureAwait(false);
        royale.ForceStart(TimeSpan.FromMinutes(30), DateTime.UtcNow);
        Assert.That(royale.Join(players[0]).En, Does.Contain("opens"));
        Assert.That(royale.IsSignedUp(players[0]), Is.False);

        royale.ForceStart(TimeSpan.FromMinutes(2), DateTime.UtcNow);
        Assert.That(royale.Join(players[0]).En, Does.Contain("signed up"));
        Assert.That(royale.IsSignedUp(players[0]), Is.True);
        Assert.That(royale.Leave(players[0]), Is.True);
    }

    /// <summary>
    /// A live match: four sign up, all start on the field; fighters may only hurt each other (no penalty), the fallen are
    /// out, the last one standing wins; then the match closes.
    /// </summary>
    [Test]
    public async Task TheLastOneStandingWinsAsync()
    {
        var (royale, context, players) = await CreateAsync(5).ConfigureAwait(false);
        var teams = new ArenaTeams();
        var duels = new ArenaDuels(null);
        royale.AfterMatch = TimeSpan.Zero;
        royale.ForceStart(TimeSpan.FromMinutes(1), DateTime.UtcNow);
        players.Take(4).ToList().ForEach(p => royale.Join(p));
        royale.ForceStart(TimeSpan.FromSeconds(-1), DateTime.UtcNow);

        await royale.TickAsync(context, teams, duels, NullLogger.Instance).ConfigureAwait(false);
        Assert.That(royale.Running, Is.True, "the map exists in the test configuration");
        var (a, b, c, d, outsider) = (players[0], players[1], players[2], players[3], players[4]);
        Assert.That(new[] { a, b, c, d }.All(royale.IsAlive), Is.True);
        Assert.That(royale.PreventsAttack(a, b), Is.False);
        Assert.That(royale.AreOpponents(a, b), Is.True);
        Assert.That(royale.PreventsAttack(outsider, a), Is.True);
        Assert.That(royale.PreventsAttack(a, outsider), Is.True);

        await royale.FellAsync(b, a).ConfigureAwait(false);
        await royale.FellAsync(c, null).ConfigureAwait(false); // the zone
        Assert.That(royale.IsAlive(b), Is.False);
        Assert.That(royale.PreventsAttack(b, a), Is.True, "the fallen are out");
        Assert.That(royale.PreventsAttack(a, d), Is.False);
        await royale.FellAsync(d, a).ConfigureAwait(false);

        await royale.TickAsync(context, teams, duels, NullLogger.Instance).ConfigureAwait(false);
        var state = System.Text.Json.JsonSerializer.SerializeToElement(royale.StateOf(a));
        Assert.That(state.GetProperty("ended").GetBoolean(), Is.True);
        Assert.That(state.GetProperty("place").GetInt32(), Is.EqualTo(1));
        Assert.That(state.GetProperty("kills").GetInt32(), Is.EqualTo(2));
        Assert.That(System.Text.Json.JsonSerializer.SerializeToElement(royale.StateOf(d)).GetProperty("place").GetInt32(), Is.EqualTo(2), "the last to fall comes second");
        Assert.That(System.Text.Json.JsonSerializer.SerializeToElement(royale.StateOf(b)).GetProperty("place").GetInt32(), Is.EqualTo(4));

        await royale.TickAsync(context, teams, duels, NullLogger.Instance).ConfigureAwait(false);
        Assert.That(royale.Running, Is.False);
    }

    /// <summary>Too few players: the match is called off and nobody is moved.</summary>
    [Test]
    public async Task TooFewPlayersCallItOffAsync()
    {
        var (royale, context, players) = await CreateAsync(2).ConfigureAwait(false);
        royale.ForceStart(TimeSpan.FromMinutes(1), DateTime.UtcNow);
        players.ForEach(p => royale.Join(p));
        royale.ForceStart(TimeSpan.FromSeconds(-1), DateTime.UtcNow);
        await royale.TickAsync(context, new ArenaTeams(), new ArenaDuels(null), NullLogger.Instance).ConfigureAwait(false);
        Assert.That(royale.Running, Is.False);
        Assert.That(royale.IsSignedUp(players[0]), Is.False);
    }

    private static async ValueTask<(ArenaRoyale Royale, IGameContext Context, List<Player> Players)> CreateAsync(int count)
    {
        var context = GameContextTestHelper.CreateGameContext();
        var royale = new ArenaRoyale(new ArenaConfiguration { RoyaleMinPlayers = 3, RoyaleTimes = string.Empty });
        var players = new List<Player>();
        for (var i = 0; i < count; i++)
        {
            var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
            player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = $"royale{i}", VaultPassword = string.Empty };
            player.SelectedCharacter!.Name = $"Royal{i}";
            await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
            player.IsAlive = true;
            players.Add(player);
        }

        return (royale, context, players);
    }
}

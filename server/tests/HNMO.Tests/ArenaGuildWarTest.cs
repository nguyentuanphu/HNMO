// <copyright file="ArenaGuildWarTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Arena;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>The Arena's guild war: days and times, flags, pairing, and a live match.</summary>
[TestFixture]
public sealed class ArenaGuildWarTest
{
    /// <summary>Days read as names or numbers; the next start is the next Saturday or Sunday at 21:00 Sydney.</summary>
    [Test]
    public void TheNextStartIsTheNextWeekendEvening()
    {
        Assert.That(ArenaGuildWarEngine.TryParseDays("Sat, Sun", out var days), Is.True);
        Assert.That(days, Is.EqualTo(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
        Assert.That(ArenaGuildWarEngine.TryParseDays("6 0", out var numbers), Is.True);
        Assert.That(numbers, Is.EqualTo(days));
        Assert.That(ArenaGuildWarEngine.TryParseDays("Someday", out _), Is.False);

        // Friday 2026-11-20 23:00 Sydney (+11) is 12:00 UTC: next is Saturday 21:00 Sydney = 10:00 UTC.
        var next = ArenaGuildWarEngine.NextStart(days, [new TimeOnly(21, 0)], ArenaRanking.SydneyTime, new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc));
        Assert.That(next, Is.EqualTo(new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc)));

        // Sunday 22:00 Sydney: the next is the Saturday after.
        var after = ArenaGuildWarEngine.NextStart(days, [new TimeOnly(21, 0)], ArenaRanking.SydneyTime, new DateTime(2026, 11, 22, 11, 0, 0, DateTimeKind.Utc));
        Assert.That(after, Is.EqualTo(new DateTime(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>A flag needs a guild alone at it for the capture time; both there stop it; the holder keeps it.</summary>
    [Test]
    public void AFlagIsTakenByAGuildAlone()
    {
        var flag = new ArenaGuildWarEngine.Flag(89, 27);
        for (var i = 0; i < 4; i++)
        {
            flag = ArenaGuildWarEngine.Step(flag, 2, 0, 5);
        }

        Assert.That(flag.Owner, Is.Zero);
        Assert.That(flag.Seconds, Is.EqualTo(4));
        Assert.That(ArenaGuildWarEngine.Step(flag, 2, 1, 5).Seconds, Is.Zero, "contested: no progress");
        flag = ArenaGuildWarEngine.Step(flag, 1, 0, 5);
        Assert.That(flag.Owner, Is.EqualTo(1));
        Assert.That(ArenaGuildWarEngine.Step(flag, 3, 0, 5), Is.EqualTo(flag), "the holder alone changes nothing");
        flag = ArenaGuildWarEngine.Step(flag, 0, 1, 5);
        Assert.That((flag.Owner, flag.Taking, flag.Seconds), Is.EqualTo((1, 2, 1)));
    }

    /// <summary>Guilds are paired by record; an odd one out is left.</summary>
    [Test]
    public void GuildsArePairedByRecord()
    {
        var (pairs, left) = ArenaGuildWarEngine.Pair([("C", 1, 0), ("A", 5, 1), ("B", 3, 0), ("D", 0, 2), ("E", 0, 5)]);
        Assert.That(pairs, Is.EqualTo(new[] { ("A", "B"), ("C", "D") }));
        Assert.That(left, Is.EqualTo("E"));
        Assert.That(ArenaGuildWarEngine.TryParseFlags("79,27; 89,27; 100,33", out var flags), Is.True);
        Assert.That(flags, Has.Count.EqualTo(3));
    }

    /// <summary>
    /// A live match: two guilds of five sign up, the war starts; guildmates can't hurt each other, kills count for the
    /// guild, a guild that left loses; the record goes to the store and the week's champion is known.
    /// </summary>
    [Test]
    public async Task TwoGuildsFightAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var store = ArenaStore.Open(null);
        var war = new ArenaGuildWar(new ArenaConfiguration { GuildWarMinMembers = 3 }, store) { AfterMatch = TimeSpan.Zero };
        var red = Guid.NewGuid();
        var blue = Guid.NewGuid();
        var players = new List<Player>();
        for (var i = 0; i < 7; i++)
        {
            var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
            player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = $"gw{i}", VaultPassword = string.Empty };
            player.SelectedCharacter!.Name = $"Warrior{i}";
            await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
            player.IsAlive = true;
            players.Add(player);
        }

        // gw0-2 in "Red", gw3-5 in "Blue", gw6 in no guild.
        war.GuildOf = login => login switch
        {
            "gw0" or "gw1" or "gw2" => (red, "Red"),
            "gw3" or "gw4" or "gw5" => (blue, "Blue"),
            _ => null,
        };
        war.ForceStart(TimeSpan.FromMinutes(1), DateTime.UtcNow);
        Assert.That(war.Join(players[6]).En, Does.Contain("Only members of a guild"));
        players.Take(6).ToList().ForEach(p => war.Join(p));

        var teams = new ArenaTeams();
        var royale = new ArenaRoyale();
        var duels = new ArenaDuels(null);
        war.ForceStart(TimeSpan.FromSeconds(-1), DateTime.UtcNow); // the start is due now; the sign-ups stay

        await war.TickAsync(context, teams, royale, duels, NullLogger.Instance).ConfigureAwait(false); // plans the pair
        Assert.That(war.Busy, Is.True);
        await war.TickAsync(context, teams, royale, duels, NullLogger.Instance).ConfigureAwait(false); // starts it
        Assert.That(war.Running, Is.True);

        var (a, b) = (players[0], players[3]);
        var redSide = war.SideOf(a);
        Assert.That(redSide, Is.Not.Zero);
        Assert.That(war.SideOf(players[1]), Is.EqualTo(redSide));
        Assert.That(war.SideOf(b), Is.EqualTo(3 - redSide));
        Assert.That(war.PreventsAttack(a, players[1]), Is.True, "no hurting a guildmate");
        Assert.That(war.PreventsAttack(a, b), Is.False);
        Assert.That(war.AreOpponents(a, b), Is.True);
        Assert.That(war.PreventsAttack(players[6], a), Is.True, "outsiders stay out");
        Assert.That(a.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(5)));

        await war.KilledAsync(b, a).ConfigureAwait(false);
        await war.KilledAsync(players[1], a).ConfigureAwait(false); // a guildmate: no point
        var state = System.Text.Json.JsonSerializer.SerializeToElement(war.StateOf(a));
        Assert.That(state.GetProperty(redSide == 1 ? "scoreA" : "scoreB").GetInt32(), Is.EqualTo(1));
        Assert.That(state.GetProperty("flags").GetArrayLength(), Is.EqualTo(3));

        // The blue guild leaves the game: red wins at once.
        foreach (var p in players.Skip(3).Take(3))
        {
            await p.PlayerState.TryAdvanceToAsync(PlayerState.Disconnected).ConfigureAwait(false);
        }

        await war.TickAsync(context, teams, royale, duels, NullLogger.Instance).ConfigureAwait(false);
        state = System.Text.Json.JsonSerializer.SerializeToElement(war.StateOf(a));
        Assert.That(state.GetProperty("ended").GetBoolean(), Is.True);
        Assert.That(state.GetProperty("winner").GetInt32(), Is.EqualTo(redSide == 1 ? 1 : -1));
        Assert.That(war.Champion(DateTime.UtcNow), Is.EqualTo("Red"));
        Assert.That(store.Read(d => (d.Guilds["Red"].Wins, d.Guilds["Blue"].Losses)), Is.EqualTo((1, 1)));

        await war.TickAsync(context, teams, royale, duels, NullLogger.Instance).ConfigureAwait(false);
        Assert.That(war.Running, Is.False);
        Assert.That(a.RespawnDelay, Is.Null);
    }

    /// <summary>The team battles don't start while the guild war holds the battle map.</summary>
    [Test]
    public async Task TeamBattlesWaitForTheGuildWarAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var busy = true;
        var teams = new ArenaTeams(new ArenaConfiguration { TeamSize = 3, TeamMinSize = 3 }) { MapBusy = () => busy };
        for (var i = 0; i < 6; i++)
        {
            var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
            player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = $"wait{i}", VaultPassword = string.Empty };
            await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
            player.IsAlive = true;
            teams.Join(player);
        }

        await teams.TickAsync(context, new ArenaDuels(null), NullLogger.Instance).ConfigureAwait(false);
        Assert.That(teams.BattleRunning, Is.False, "the guild war holds the map");
        busy = false;
        await teams.TickAsync(context, new ArenaDuels(null), NullLogger.Instance).ConfigureAwait(false);
        Assert.That(teams.BattleRunning, Is.True);
    }
}

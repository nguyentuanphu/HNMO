// <copyright file="ArenaTeamsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Arena;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>The Arena's live team battles: queue, two teams, who may hurt whom, kills, the end and the way home.</summary>
[TestFixture]
public sealed class ArenaTeamsTest
{
    /// <summary>Six players queued alone make a 3v3; teammates can't hurt each other, opponents can (without penalty).</summary>
    [Test]
    public async Task SixPlayersMakeTwoTeamsAsync()
    {
        var (teams, context, players) = await CreateAsync(6).ConfigureAwait(false);
        players.ForEach(p => teams.Join(p));
        Assert.That(teams.Waiting, Is.EqualTo(6));

        await teams.TickAsync(context, new ArenaDuels(null), NullLogger.Instance).ConfigureAwait(false);

        Assert.That(teams.BattleRunning, Is.True);
        Assert.That(teams.Waiting, Is.Zero);
        var red = players.Where(p => teams.SideOf(p) == 1).ToList();
        var blue = players.Where(p => teams.SideOf(p) == 2).ToList();
        Assert.That(red, Has.Count.EqualTo(3));
        Assert.That(blue, Has.Count.EqualTo(3));
        Assert.That(teams.PreventsAttack(red[0], red[1]), Is.True, "no hurting a teammate");
        Assert.That(teams.PreventsAttack(red[0], blue[0]), Is.False, "the other team can be hurt");
        Assert.That(teams.AreOpponents(red[0], blue[0]), Is.True, "no player-kill penalty between the teams");
        Assert.That(teams.AreOpponents(red[0], red[1]), Is.False);
        Assert.That(red.All(p => p.RespawnDelay == TimeSpan.FromSeconds(5)), Is.True, "the fallen wait 5 s");
    }

    /// <summary>One player alone stays in the queue while waiting for others.</summary>
    [Test]
    public async Task AWaitingPlayerStaysQueuedAsync()
    {
        var (teams, context, players) = await CreateAsync(1).ConfigureAwait(false);
        teams.Join(players[0]);
        await teams.TickAsync(context, new ArenaDuels(null), NullLogger.Instance).ConfigureAwait(false);
        Assert.That(teams.IsQueued(players[0]), Is.True);
        Assert.That(teams.Waiting, Is.EqualTo(1));
    }

    /// <summary>A player outside the battle can't hurt a fighter, nor be hurt by one.</summary>
    [Test]
    public async Task OutsidersStayOutAsync()
    {
        var (teams, context, players) = await CreateAsync(7).ConfigureAwait(false);
        players.Take(6).ToList().ForEach(p => teams.Join(p));
        await teams.TickAsync(context, new ArenaDuels(null), NullLogger.Instance).ConfigureAwait(false);
        var outsider = players[6];
        Assert.That(teams.SideOf(outsider), Is.Zero);
        Assert.That(teams.PreventsAttack(outsider, players[0]), Is.True);
        Assert.That(teams.PreventsAttack(players[0], outsider), Is.True);
    }

    /// <summary>The kills that win end the battle at once; then everyone goes home and the respawn wait is the usual one.</summary>
    [Test]
    public async Task KillsToWinEndTheBattleAsync()
    {
        var (teams, context, players) = await CreateAsync(6, killsToWin: 2).ConfigureAwait(false);
        teams.AfterBattle = TimeSpan.Zero;
        players.ForEach(p => teams.Join(p));
        var duels = new ArenaDuels(null);
        await teams.TickAsync(context, duels, NullLogger.Instance).ConfigureAwait(false);
        var red = players.Where(p => teams.SideOf(p) == 1).ToList();
        var blue = players.Where(p => teams.SideOf(p) == 2).ToList();

        await teams.KilledAsync(blue[0], red[0]).ConfigureAwait(false);
        await teams.KilledAsync(red[1], red[0]).ConfigureAwait(false); // a teammate kill doesn't count
        Assert.That(State(teams, red[0]).ScoreA, Is.EqualTo(1));
        await teams.KilledAsync(blue[1], red[2]).ConfigureAwait(false);

        await teams.TickAsync(context, duels, NullLogger.Instance).ConfigureAwait(false);
        var ended = State(teams, red[0]);
        Assert.That(ended.Ended, Is.True);
        Assert.That(ended.Winner, Is.EqualTo(1), "red won");
        Assert.That(ended.ScoreA, Is.EqualTo(2));
        Assert.That(teams.PreventsAttack(red[0], blue[0]), Is.True, "no fighting after the end");

        await teams.TickAsync(context, duels, NullLogger.Instance).ConfigureAwait(false);
        Assert.That(teams.BattleRunning, Is.False);
        Assert.That(players.All(p => teams.SideOf(p) == 0 && p.RespawnDelay is null), Is.True);
    }

    /// <summary>A team whose players all left the game loses at once, so the next battle can start.</summary>
    [Test]
    public async Task ATeamThatLeftLosesAsync()
    {
        var (teams, context, players) = await CreateAsync(6).ConfigureAwait(false);
        players.ForEach(p => teams.Join(p));
        var duels = new ArenaDuels(null);
        await teams.TickAsync(context, duels, NullLogger.Instance).ConfigureAwait(false);
        var red = players.Where(p => teams.SideOf(p) == 1).ToList();
        foreach (var p in players.Where(p => teams.SideOf(p) == 2))
        {
            await p.PlayerState.TryAdvanceToAsync(PlayerState.Disconnected).ConfigureAwait(false);
        }

        await teams.TickAsync(context, duels, NullLogger.Instance).ConfigureAwait(false);
        var state = State(teams, red[0]);
        Assert.That(state.Ended, Is.True);
        Assert.That(state.Winner, Is.EqualTo(1), "the team still there wins");
    }

    /// <summary>A party led by one player queues together and lands on one team.</summary>
    [Test]
    public async Task APartyQueuesTogetherAsync()
    {
        var (teams, context, players) = await CreateAsync(6).ConfigureAwait(false);
        var party = new Party(new PartyManager(5, new NullLogger<Party>()), 5, new NullLogger<Party>());
        await party.AddAsync(players[0]).ConfigureAwait(false);
        await party.AddAsync(players[1]).ConfigureAwait(false);
        await party.AddAsync(players[2]).ConfigureAwait(false);

        Assert.That(teams.Join(players[1]).En, Does.Contain("leader"));
        teams.Join(players[0]);
        Assert.That(teams.Waiting, Is.EqualTo(3));
        players.Skip(3).ToList().ForEach(p => teams.Join(p));
        await teams.TickAsync(context, new ArenaDuels(null), NullLogger.Instance).ConfigureAwait(false);

        var side = teams.SideOf(players[0]);
        Assert.That(side, Is.Not.Zero);
        Assert.That(teams.SideOf(players[1]), Is.EqualTo(side));
        Assert.That(teams.SideOf(players[2]), Is.EqualTo(side));
    }

    /// <summary>Reads the team part of the state back (the anonymous object's values).</summary>
    private static (int ScoreA, int ScoreB, bool Ended, int Winner) State(ArenaTeams teams, Player player)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(teams.StateOf(player));
        return (json.GetProperty("scoreA").GetInt32(), json.GetProperty("scoreB").GetInt32(), json.GetProperty("ended").GetBoolean(), json.GetProperty("winner").GetInt32());
    }

    private static async ValueTask<(ArenaTeams Teams, IGameContext Context, List<Player> Players)> CreateAsync(int count, int killsToWin = 30)
    {
        var context = GameContextTestHelper.CreateGameContext();
        var teams = new ArenaTeams(new ArenaConfiguration { TeamSize = 3, TeamMinSize = 3, TeamKillsToWin = killsToWin });
        var players = new List<Player>();
        for (var i = 0; i < count; i++)
        {
            var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
            player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = $"team{i}", VaultPassword = string.Empty };
            player.SelectedCharacter!.Name = $"Fighter{i}";
            await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
            players.Add(player);
        }

        return (teams, context, players);
    }
}

// <copyright file="ArenaTeamEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.Arena;

/// <summary>The Arena's 5v5 team battles: forming two even teams from solo players and parties, and the win condition.</summary>
[TestFixture]
public sealed class ArenaTeamEngineTest
{
    private static ArenaTeamEngine.Group Solo(string login, long order, int rating = 1000) => new([login], order, rating);

    /// <summary>Ten solo players make a 5v5; the chosen ten leave the queue.</summary>
    [Test]
    public void TenSoloPlayersMakeAFiveVFive()
    {
        var queue = Enumerable.Range(0, 11).Select(i => Solo($"p{i}", i)).ToList();
        var match = ArenaTeamEngine.FormTeams(queue, teamSize: 5, minSize: 3, frontWaitedLong: false);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.TeamA, Has.Count.EqualTo(5));
        Assert.That(match.TeamB, Has.Count.EqualTo(5));
        Assert.That(match.TeamA.Concat(match.TeamB).Distinct().Count(), Is.EqualTo(10), "no one on both teams");
        Assert.That(queue, Has.Count.EqualTo(1), "the eleventh waits");
    }

    /// <summary>A party stays on one team; both teams come out even.</summary>
    [Test]
    public void APartyStaysTogether()
    {
        var queue = new List<ArenaTeamEngine.Group>
        {
            new(["a1", "a2", "a3"], 0, 1000),
            new(["b1", "b2"], 1, 1000),
            Solo("c1", 2), Solo("c2", 3), Solo("c3", 4), Solo("c4", 5), Solo("c5", 6),
        };
        var match = ArenaTeamEngine.FormTeams(queue, teamSize: 5, minSize: 3, frontWaitedLong: false);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.TeamA, Has.Count.EqualTo(5));
        Assert.That(match.TeamB, Has.Count.EqualTo(5));
        var teamWithParty = match.TeamA.Contains("a1") ? match.TeamA : match.TeamB;
        Assert.That(teamWithParty, Does.Contain("a1").And.Contain("a2").And.Contain("a3"), "the party is kept together");
    }

    /// <summary>A smaller even battle starts only once the front has waited long enough; never below the minimum.</summary>
    [Test]
    public void ASmallerBattleWaitsForALongWait()
    {
        var six = () => Enumerable.Range(0, 6).Select(i => Solo($"p{i}", i)).ToList();
        Assert.That(ArenaTeamEngine.FormTeams(six(), 5, 3, frontWaitedLong: false), Is.Null, "not a 3v3 until the wait is long");
        var match = ArenaTeamEngine.FormTeams(six(), 5, 3, frontWaitedLong: true);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.TeamA, Has.Count.EqualTo(3));
        Assert.That(match.TeamB, Has.Count.EqualTo(3));

        var four = Enumerable.Range(0, 4).Select(i => Solo($"p{i}", i)).ToList();
        Assert.That(ArenaTeamEngine.FormTeams(four, 5, 3, frontWaitedLong: true), Is.Null, "below the 3v3 minimum: wait");

        var eight = Enumerable.Range(0, 8).Select(i => Solo($"p{i}", i)).ToList();
        Assert.That(ArenaTeamEngine.FormTeams(eight, 5, 3, frontWaitedLong: true)!.TeamA, Has.Count.EqualTo(4), "a 4v4 from eight");
    }

    /// <summary>A party bigger than a team is skipped, not split.</summary>
    [Test]
    public void AnOversizedPartyIsSkipped()
    {
        var queue = new List<ArenaTeamEngine.Group> { new(["big1", "big2", "big3", "big4", "big5", "big6"], 0, 1000) };
        queue.AddRange(Enumerable.Range(0, 10).Select(i => Solo($"p{i}", i + 1)));
        var match = ArenaTeamEngine.FormTeams(queue, teamSize: 5, minSize: 3, frontWaitedLong: false);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.TeamA.Concat(match.TeamB), Does.Not.Contain("big1"));
        Assert.That(queue, Does.Contain(queue.First(g => g.Logins.Contains("big1"))), "the oversized party stays");
    }

    /// <summary>Thirty kills end a battle; so does time, with the most kills winning (a tie is a draw).</summary>
    [Test]
    public void TheWinConditionIsThirtyKillsOrTheMostInTime()
    {
        Assert.That(ArenaTeamEngine.Outcome(29, 10, 30, timeUp: false), Is.Null, "not over");
        Assert.That(ArenaTeamEngine.Outcome(30, 10, 30, timeUp: false), Is.EqualTo((true, 1)));
        Assert.That(ArenaTeamEngine.Outcome(10, 30, 30, timeUp: false), Is.EqualTo((true, -1)));
        Assert.That(ArenaTeamEngine.Outcome(12, 15, 30, timeUp: true), Is.EqualTo((true, -1)), "most kills at time");
        Assert.That(ArenaTeamEngine.Outcome(15, 15, 30, timeUp: true), Is.EqualTo((true, 0)), "a draw");
    }
}

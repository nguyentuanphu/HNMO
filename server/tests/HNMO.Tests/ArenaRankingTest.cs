// <copyright file="ArenaRankingTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.Arena;

/// <summary>The HNMO Arena's ranked 1v1: Elo ratings, Sydney weeks, results and leaderboards.</summary>
[TestFixture]
public sealed class ArenaRankingTest
{
    /// <summary>Equal ratings move by half of K; beating a much stronger player moves more; ratings never go below 0.</summary>
    [Test]
    public void RatingsMoveByElo()
    {
        Assert.That(ArenaRanking.Rate(1000, 1000), Is.EqualTo((1016, 984)));
        var (upset, _) = ArenaRanking.Rate(1000, 1400);
        Assert.That(upset - 1000, Is.GreaterThan(25), "an upset is worth more");
        var (expected, _) = ArenaRanking.Rate(1400, 1000);
        Assert.That(expected - 1400, Is.InRange(1, 6), "a sure win is worth little, but never nothing");
        Assert.That(ArenaRanking.Rate(5000, 3).Loser, Is.GreaterThanOrEqualTo(0));
    }

    /// <summary>Weeks start on Monday, Sydney time: Sunday 23:30 Sydney is still the old week, Monday 00:30 the new one.</summary>
    [Test]
    public void WeeksAreSydneyWeeks()
    {
        // 2026-11-22 is a Sunday; Sydney is UTC+11 in November.
        var sundayLate = new DateTime(2026, 11, 22, 12, 30, 0, DateTimeKind.Utc); // 23:30 Sunday in Sydney
        var mondayEarly = new DateTime(2026, 11, 22, 13, 30, 0, DateTimeKind.Utc); // 00:30 Monday in Sydney
        Assert.That(ArenaRanking.WeekKey(sundayLate), Is.EqualTo("2026-W47"));
        Assert.That(ArenaRanking.WeekKey(mondayEarly), Is.EqualTo("2026-W48"));
    }

    /// <summary>The match gap starts at 100 and widens while a player waits.</summary>
    [Test]
    public void TheMatchGapWidensWhileWaiting()
    {
        Assert.That(ArenaRanking.MatchGap(TimeSpan.Zero), Is.EqualTo(100));
        Assert.That(ArenaRanking.MatchGap(TimeSpan.FromSeconds(25)), Is.EqualTo(200));
        Assert.That(ArenaRanking.MatchGap(TimeSpan.FromHours(1)), Is.EqualTo(1000));
    }

    /// <summary>Results count for both, the week's counts start again each week, and the leaderboards sort.</summary>
    [Test]
    public void ResultsAndLeaderboards()
    {
        var data = new ArenaStoreData();
        var monday = new DateTime(2026, 11, 23, 2, 0, 0, DateTimeKind.Utc);
        ArenaRanking.Record(data, ("alice", "AliceDk", "Dark Knight"), ("bob", "BobDw", "Dark Wizard"), monday);
        ArenaRanking.Record(data, ("alice", "AliceDk", "Dark Knight"), ("carol", "CarolElf", "Fairy Elf"), monday.AddHours(1));
        var alice = ArenaRanking.Of(data, "ALICE", monday);
        Assert.That((alice.Wins, alice.Losses, alice.WeekWins), Is.EqualTo((2, 0, 2)));
        Assert.That(alice.Rating, Is.GreaterThan(ArenaRanking.StartRating));
        Assert.That(ArenaRanking.Of(data, "bob", monday).Losses, Is.EqualTo(1));

        var (allTime, week) = ArenaRanking.Leaderboards(data, monday);
        Assert.That(allTime[0].CharacterName, Is.EqualTo("AliceDk"));
        Assert.That(week[0].CharacterName, Is.EqualTo("AliceDk"));

        var nextWeek = monday.AddDays(7);
        Assert.That(ArenaRanking.Of(data, "alice", nextWeek).WeekWins, Is.Zero, "a new week starts at 0");
        Assert.That(ArenaRanking.Of(data, "alice", nextWeek).Wins, Is.EqualTo(2), "the totals stay");
        Assert.That(ArenaRanking.Leaderboards(data, nextWeek).Week, Is.Empty);
        Assert.That(ArenaRanking.Of(data, "nobody", monday).Rating, Is.EqualTo(ArenaRanking.StartRating));
    }

    /// <summary>
    /// A ranked duel's result (what the duel calls when it's won) moves both ratings in the Arena's state file and tells
    /// both players; the queue matches two waiting players of one map.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ADuelResultIsKeptAsync()
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hnmo-arena-{Guid.NewGuid():N}.json");
        try
        {
            var context = GameContextTestHelper.CreateGameContext([]);
            var winner = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
            var loser = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
            winner.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "winner" };
            loser.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "loser" };
            var duels = new ArenaDuels(file);

            await duels.RecordAsync(winner, loser).ConfigureAwait(false);

            var (w, l) = ArenaStore.Open(file).Read(d => (ArenaRanking.Of(d, "winner", DateTime.UtcNow), ArenaRanking.Of(d, "loser", DateTime.UtcNow)));
            Assert.That((w.Rating, w.Wins), Is.EqualTo((1016, 1)));
            Assert.That((l.Rating, l.Losses), Is.EqualTo((984, 1)));
            Assert.That(ArenaDuels.LeaderboardJson(ArenaStore.Open(file)), Does.Contain("\"rating\":1016"));

            Assert.That(duels.Join(winner).En, Does.StartWith("Looking"));
            Assert.That(duels.IsQueued(winner), Is.True);
            Assert.That(duels.Leave(winner), Is.True);
            Assert.That(duels.IsQueued(winner), Is.False);
        }
        finally
        {
            System.IO.File.Delete(file);
        }
    }
}

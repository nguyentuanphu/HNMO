// <copyright file="LuckyNumberEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.LuckyNumber;
using HNMO.GameLogic.Views;

/// <summary>
/// Unit and plug-in tests for <see cref="LuckyNumberEngine"/>, <see cref="LuckyNumberStore"/>, and <see cref="LuckyNumberPlugIn"/>.
/// </summary>
[TestFixture]
public sealed class LuckyNumberEngineTest
{
    private string _stateFile = null!;

    /// <summary>Sets up a temporary state file.</summary>
    [SetUp]
    public void SetUp() => this._stateFile = Path.Combine(Path.GetTempPath(), $"hnmo-lucky-test-{Guid.NewGuid():N}.json");

    /// <summary>Cleans up temporary state files.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._stateFile))
        {
            File.Delete(this._stateFile);
        }

        if (File.Exists(this._stateFile + ".bak"))
        {
            File.Delete(this._stateFile + ".bak");
        }
    }

    /// <summary>Validates range checks, daily limit, duplicate check, and Sydney draw date rollover.</summary>
    [Test]
    public void EnforcesRangeDailyCapAndDrawDateRollover()
    {
        var cfg = new LuckyNumberConfiguration
        {
            FileName = this._stateFile,
            TimeZoneId = "Australia/Sydney",
            DrawTime = "20:00",
            MinNumber = 0,
            MaxNumber = 99,
            MaxPicksPerAccountPerDay = 2,
        };
        var engine = new LuckyNumberEngine(cfg, LuckyNumberStore.Open(this._stateFile), enabled: true);

        // 2026-11-16 08:30 UTC == 19:30 AEDT (before 20:00 draw) -> round 2026-11-16
        var beforeDrawUtc = new DateTime(2026, 11, 16, 8, 30, 0, DateTimeKind.Utc);
        Assert.That(engine.Pick("alice", "AliceDK", 100, beforeDrawUtc, "game"), Is.EqualTo(LuckyNumberPickResult.OutOfRange));
        Assert.That(engine.Pick("alice", "AliceDK", 27, beforeDrawUtc, "game"), Is.EqualTo(LuckyNumberPickResult.Success));
        Assert.That(engine.Pick("alice", "AliceDK", 27, beforeDrawUtc, "website"), Is.EqualTo(LuckyNumberPickResult.DuplicateNumber));
        Assert.That(engine.Pick("alice", "AliceDK", 37, beforeDrawUtc, "website"), Is.EqualTo(LuckyNumberPickResult.Success));
        Assert.That(engine.Pick("alice", "AliceDK", 47, beforeDrawUtc, "game"), Is.EqualTo(LuckyNumberPickResult.DailyLimitReached));

        // Next pick after 20:00 AEDT (09:05 UTC) goes to 2026-11-17.
        var afterDrawUtc = new DateTime(2026, 11, 16, 9, 5, 0, DateTimeKind.Utc);
        var (nextKey, _) = engine.ResolveActiveRound(afterDrawUtc);
        Assert.That(nextKey, Is.EqualTo("2026-11-17"));
        Assert.That(engine.Pick("alice", "AliceDK", 27, afterDrawUtc, "game"), Is.EqualTo(LuckyNumberPickResult.Success));
    }

    /// <summary>Exact and last-digit matches produce winner records, owed jewels, and persist across store reopen.</summary>
    [Test]
    public void DrawDueRoundsMatchesExactAndLastDigitWinnersAndQueuesJewels()
    {
        var cfg = new LuckyNumberConfiguration
        {
            FileName = this._stateFile,
            DrawTime = "20:00",
            ExactMatchCoins = 200,
            ExactMatchJewelCount = 2,
            EnableLastDigitPrize = true,
            LastDigitCoins = 20,
            LastDigitJewelCount = 1,
        };
        var engine = new LuckyNumberEngine(cfg, LuckyNumberStore.Open(this._stateFile), enabled: true);

        var pickTimeUtc = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
        Assert.That(engine.Pick("winnerExact", "ExactHero", 42, pickTimeUtc, "game"), Is.EqualTo(LuckyNumberPickResult.Success));
        Assert.That(engine.Pick("winnerLast", "LastHero", 82, pickTimeUtc, "website"), Is.EqualTo(LuckyNumberPickResult.Success));
        Assert.That(engine.Pick("loser", "Unlucky", 15, pickTimeUtc, "game"), Is.EqualTo(LuckyNumberPickResult.Success));

        // 09:00 UTC == 20:00 AEDT -> draw is due.
        var drawTimeUtc = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        var outcomes = engine.DrawDueRounds(drawTimeUtc, overrideWinningNumber: 42);
        Assert.That(outcomes, Has.Count.EqualTo(1));
        Assert.That(outcomes[0].DrawnNumber, Is.EqualTo(42));
        Assert.That(outcomes[0].Winners.Select(w => (w.LoginName, w.Tier, w.Coins, w.JewelCount)), Is.EquivalentTo(new[]
        {
            ("winnerExact", "exact", 200, 2),
            ("winnerLast", "lastDigit", 20, 1),
        }));

        // Running draw again does not double-draw the same day.
        Assert.That(engine.DrawDueRounds(drawTimeUtc.AddMinutes(5), overrideWinningNumber: 99), Is.Empty);

        var reopened = new LuckyNumberEngine(cfg, LuckyNumberStore.Open(this._stateFile), enabled: true);
        Assert.That(reopened.PeekOwed("winnerExact"), Has.Count.EqualTo(1));
        Assert.That(reopened.PeekOwed("winnerExact")[0].Count, Is.EqualTo(2));
        Assert.That(reopened.PeekOwed("winnerLast")[0].Count, Is.EqualTo(1));
    }

    /// <summary><see cref="LuckyNumberPlugIn"/> handles in-game picks and sends state via <see cref="IHnmoLuckyNumberViewPlugIn"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlugInPickAndStateSendWorksAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var plugIn = new LuckyNumberPlugIn { Configuration = new LuckyNumberConfiguration { FileName = this._stateFile } };
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account ??= new();
        player.Account.LoginName = "luckyplayer";

        string? capturedJson = null;
        Mock.Get(player.ViewPlugIns.GetPlugIn<IHnmoLuckyNumberViewPlugIn>()!)
            .Setup(v => v.ShowLuckyNumberAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((json, _) => capturedJson = json)
            .Returns(ValueTask.CompletedTask);

        await plugIn.PickAsync(player, 77).ConfigureAwait(false);

        Assert.That(capturedJson, Is.Not.Null);
        using var doc = JsonDocument.Parse(capturedJson!);
        Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(doc.RootElement.GetProperty("myPicks")[0].GetProperty("number").GetInt32(), Is.EqualTo(77));
    }
}

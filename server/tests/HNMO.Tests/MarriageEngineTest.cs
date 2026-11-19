// <copyright file="MarriageEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using Moq;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Marriage;
using HNMO.GameLogic.Views;
using HNMO.Pathfinding;

/// <summary>
/// Unit and plug-in tests for <see cref="MarriageEngine"/>, <see cref="MarriageStore"/>, and <see cref="MarriagePlugIn"/>.
/// </summary>
[TestFixture]
public sealed class MarriageEngineTest
{
    private string _stateFile = null!;

    /// <summary>Creates an isolated temporary state file.</summary>
    [SetUp]
    public void SetUp() => this._stateFile = Path.Combine(Path.GetTempPath(), $"hnmo-marriage-test-{Guid.NewGuid():N}.json");

    /// <summary>Removes temporary state files.</summary>
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

    /// <summary>Covers proposal, ceremony location gating, custom title, divorce, cooldown, and persistence.</summary>
    [Test]
    public void FullMarriageLifecycleCeremonyLocationAndDivorceCooldown()
    {
        var cfg = new MarriageConfiguration
        {
            FileName = this._stateFile,
            MinimumLevel = 100,
            RequireCeremonyLocation = true,
            CeremonyMapNumber = 2,
            CeremonyX = 210,
            CeremonyY = 23,
            CeremonyRadius = 15,
            RemarryCooldownHours = 24,
        };
        var engine = new MarriageEngine(cfg, MarriageStore.Open(this._stateFile), enabled: true);
        var now = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

        Assert.That(engine.Propose("Romeo", "accR", 90, "Juliet", 150, now), Is.EqualTo(MarriageActionResult.LevelTooLow));
        Assert.That(engine.Propose("Romeo", "accR", 150, "Romeo", 150, now), Is.EqualTo(MarriageActionResult.InvalidTarget));
        Assert.That(engine.Propose("Romeo", "accR", 150, "Juliet", 150, now), Is.EqualTo(MarriageActionResult.Success));

        // Away from Devias Church fails ceremony check:
        Assert.That(engine.IsAtCeremonySpot(0, 130, 130), Is.False);
        Assert.That(engine.IsAtCeremonySpot(2, 212, 24), Is.True);
        Assert.That(
            engine.AcceptCeremony("Juliet", "accJ", 150, 150, bothAtCeremonySpot: false, now.AddSeconds(10), out _),
            Is.EqualTo(MarriageActionResult.NotAtCeremonyLocation));

        // Standing at Devias Church completes ceremony:
        Assert.That(
            engine.AcceptCeremony("Juliet", "accJ", 150, 150, bothAtCeremonySpot: true, now.AddSeconds(20), out var couple),
            Is.EqualTo(MarriageActionResult.Success));
        Assert.That(couple, Is.Not.Null);
        Assert.That(engine.SetCustomTitle("Juliet", "Eternal Flame"), Is.EqualTo(MarriageActionResult.Success));

        // Persisted across reopened store:
        var reopened = new MarriageEngine(cfg, MarriageStore.Open(this._stateFile), enabled: true);
        Assert.That(reopened.GetSpouseName("Romeo"), Is.EqualTo("Juliet"));
        Assert.That(reopened.GetCoupleOf("Romeo")!.CustomTitle, Is.EqualTo("Eternal Flame"));

        // Divorce applies 24h cooldown:
        Assert.That(reopened.Divorce("Romeo", now.AddHours(1), out var ex), Is.EqualTo(MarriageActionResult.Success));
        Assert.That(ex, Is.EqualTo("Juliet"));
        Assert.That(reopened.GetSpouseName("Romeo"), Is.Null);
        Assert.That(reopened.Propose("Romeo", "accR", 150, "Juliet", 150, now.AddHours(2)), Is.EqualTo(MarriageActionResult.CooldownActive));
        Assert.That(reopened.Propose("Romeo", "accR", 150, "Juliet", 150, now.AddHours(26)), Is.EqualTo(MarriageActionResult.Success));
    }

    /// <summary>When married players hunt in the same party and map, couple hunting bonus is active.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PartyHuntingTogetherActivatesCoupleBonusAndSendsStateAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var plugIn = new MarriagePlugIn
        {
            Configuration = new MarriageConfiguration
            {
                FileName = this._stateFile,
                RequireCeremonyLocation = false,
                CeremonyZenCost = 0,
                MinimumLevel = 1,
                CoupleExpBonusPercent = 12,
                CoupleDefenseBonus = 20,
            },
        };

        var p1 = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        p1.Account ??= new();
        p1.Account.LoginName = "acc1";
        p1.SelectedCharacter!.Name = "Bride";

        var p2 = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        p2.Account ??= new();
        p2.Account.LoginName = "acc2";
        p2.SelectedCharacter!.Name = "Groom";

        var engine = plugIn.GetEngine();
        Assert.That(engine.Propose("Groom", "acc2", 10, "Bride", 10, DateTime.UtcNow), Is.EqualTo(MarriageActionResult.Success));
        Assert.That(engine.AcceptCeremony("Bride", "acc1", 10, 10, bothAtCeremonySpot: true, DateTime.UtcNow, out _), Is.EqualTo(MarriageActionResult.Success));

        // Put both into a Party on the same map:
        var partyLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Party>.Instance;
        var party = new Party(new PartyManager(5, partyLogger), 5, partyLogger);
        await party.AddAsync(p1).ConfigureAwait(false);
        await party.AddAsync(p2).ConfigureAwait(false);
        Assert.That(plugIn.IsCoupleBonusActive(p1, engine), Is.True);

        string? capturedJson = null;
        Mock.Get(p1.ViewPlugIns.GetPlugIn<IHnmoMarriageViewPlugIn>()!)
            .Setup(v => v.ShowMarriageAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((json, _) => capturedJson = json)
            .Returns(ValueTask.CompletedTask);

        await plugIn.SendStateAsync(p1, requested: true).ConfigureAwait(false);
        Assert.That(capturedJson, Is.Not.Null);
        using var doc = JsonDocument.Parse(capturedJson!);
        var me = doc.RootElement.GetProperty("me");
        Assert.That(me.GetProperty("married").GetBoolean(), Is.True);
        Assert.That(me.GetProperty("spouse").GetString(), Is.EqualTo("Groom"));
        Assert.That(me.GetProperty("bonusActiveNow").GetBoolean(), Is.True);
    }

    /// <summary>
    /// No proposal spam (audit 1.10): a proposer waits <see cref="MarriageEngine.ProposalInterval"/>, and a waiting
    /// proposal isn't replaced by another character's.
    /// </summary>
    [Test]
    public void ProposalsCannotBeSpammed()
    {
        var engine = new MarriageEngine(new MarriageConfiguration { MinimumLevel = 1 }, MarriageStore.Open(this._stateFile), enabled: true);
        var now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.That(engine.Propose("Romeo", "accR", 150, "Juliet", 150, now), Is.EqualTo(MarriageActionResult.Success));
        Assert.That(engine.Propose("Romeo", "accR", 150, "Rosaline", 150, now.AddSeconds(5)), Is.EqualTo(MarriageActionResult.ProposalTooSoon));
        Assert.That(engine.Propose("Paris", "accP", 150, "Juliet", 150, now.AddSeconds(5)), Is.EqualTo(MarriageActionResult.TargetHasProposal));
        Assert.That(engine.Propose("Romeo", "accR", 150, "Rosaline", 150, now + MarriageEngine.ProposalInterval), Is.EqualTo(MarriageActionResult.Success));

        // Once Juliet's proposal expired (two minutes), another character may propose to her.
        Assert.That(engine.Propose("Paris", "accP", 150, "Juliet", 150, now.AddMinutes(3)), Is.EqualTo(MarriageActionResult.Success));
    }

    /// <summary>A deleted character's marriage ends, so a new character of the same name starts single (audit 1.10).</summary>
    [Test]
    public void ADeletedCharacterLeavesNoMarriageBehind()
    {
        var engine = new MarriageEngine(new MarriageConfiguration { MinimumLevel = 1, RequireCeremonyLocation = false }, MarriageStore.Open(this._stateFile), enabled: true);
        var now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        Assert.That(engine.Propose("Romeo", "accR", 150, "Juliet", 150, now), Is.EqualTo(MarriageActionResult.Success));
        Assert.That(engine.AcceptCeremony("Juliet", "accJ", 150, 150, bothAtCeremonySpot: true, now.AddSeconds(10), out _), Is.EqualTo(MarriageActionResult.Success));
        Assert.That(engine.GetSpouseName("Romeo"), Is.EqualTo("Juliet"));

        Assert.That(engine.ForgetCharacter("Romeo"), Is.EqualTo("Juliet"));
        Assert.Multiple(() =>
        {
            Assert.That(engine.GetSpouseName("Romeo"), Is.Null, "a new Romeo is single");
            Assert.That(engine.GetSpouseName("Juliet"), Is.Null, "Juliet is free");
            Assert.That(engine.Propose("Paris", "accP", 150, "Juliet", 150, now.AddMinutes(1)), Is.EqualTo(MarriageActionResult.Success), "with no cooldown");
        });
    }
}

// <copyright file="OnboardingEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.Onboarding;

/// <summary>
/// Tests for HNMO's new-player guide and starter kit: which character is new, the steps in their order (counted and
/// checked by looking), rewards that wait with unique references, optional steps, skipping, the final reward and who gets
/// the starter kit (per character or per account, class items).
/// </summary>
[TestFixture]
public class OnboardingEngineTest
{
    private static readonly DateTime Activated = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Activated.AddDays(1);

    /// <summary>
    /// A character created after the plug-in was turned on is new; an older one only up to the level limit; the
    /// decision is kept.
    /// </summary>
    [Test]
    public void NewCharactersAreTheCreatedOnesOrLowLevels()
    {
        var engine = CreateEngine(new OnboardingConfiguration { NewCharacterMaxLevel = 10 });
        Assert.That(engine.See("a", "acc", "A", Activated.AddHours(1), 50, Now), Is.EqualTo(OnboardingStatus.Active), "Created after: new at any level.");
        Assert.That(engine.See("b", "acc", "B", Activated.AddDays(-30), 8, Now), Is.EqualTo(OnboardingStatus.Active), "Old, but at level 8.");
        Assert.That(engine.See("c", "acc", "C", Activated.AddDays(-30), 80, Now), Is.EqualTo(OnboardingStatus.NotEligible));
        Assert.That(engine.See("c", "acc", "C", Activated.AddDays(-30), 1, Now), Is.EqualTo(OnboardingStatus.NotEligible), "Decided the first time.");
        Assert.That(engine.CurrentStep("c"), Is.Null);

        var strict = CreateEngine(new OnboardingConfiguration { NewCharacterMaxLevel = 0 });
        Assert.That(strict.See("d", "acc", "D", Activated.AddDays(-1), 1, Now), Is.EqualTo(OnboardingStatus.NotEligible), "0: only created ones.");
    }

    /// <summary>
    /// The steps go in their order; only the current step counts, a matching event only, and a done step's reward waits
    /// with a unique reference.
    /// </summary>
    [Test]
    public void StepsGoInOrderAndOnlyTheCurrentOneCounts()
    {
        var configuration = Configuration(
            new OnboardingStep { Id = "kill", Kind = OnboardingStepKind.KillMonsters, Target = 2, MonsterNumber = 3, Reward = new MissionReward { Zen = 100, Coins = 5 } },
            new OnboardingStep { Id = "potion", Kind = OnboardingStepKind.UseItem, Target = 1, ItemGroup = 14 });
        var engine = CreateEngine(configuration);
        engine.See("a", "acc", "A", Now, 1, Now);

        Assert.That(engine.Report("a", OnboardingStepKind.UseItem, 1, null, Now).Any, Is.False, "Not the current step.");
        Assert.That(engine.Report("a", OnboardingStepKind.KillMonsters, 1, s => s.MonsterNumber == 4, Now).Any, Is.False, "Another monster.");
        Assert.That(engine.Progress("a", null)!.Value.Value, Is.EqualTo(0));
        Assert.That(engine.Report("a", OnboardingStepKind.KillMonsters, 1, s => s.MonsterNumber == 3, Now).Any, Is.False);
        Assert.That(engine.Progress("a", null)!.Value.Value, Is.EqualTo(1));
        var advance = engine.Report("a", OnboardingStepKind.KillMonsters, 1, null, Now);
        Assert.That(advance.Completed.Select(s => s.Id), Is.EqualTo(new[] { "kill" }));
        Assert.That(advance.Finished, Is.False);
        Assert.That(engine.CurrentStep("a")!.Id, Is.EqualTo("potion"));

        var owed = engine.Owed("a");
        Assert.That(owed, Has.Count.EqualTo(1));
        Assert.That(owed[0].Reference, Is.EqualTo("onboarding:a:kill"));
        Assert.That(owed[0].Zen, Is.EqualTo(100));
        Assert.That(owed[0].Coins, Is.EqualTo(5));
        Assert.That(owed[0].Login, Is.EqualTo("acc"));
    }

    /// <summary>
    /// The last step finishes the guide with the final reward; nothing counts afterwards.
    /// </summary>
    [Test]
    public void TheLastStepGivesTheFinalReward()
    {
        var configuration = Configuration(new OnboardingStep { Id = "talk", Kind = OnboardingStepKind.TalkToNpc, NpcNumber = 253 });
        configuration.FinalReward = new MissionReward { Zen = 1000, Coins = 50 };
        var engine = CreateEngine(configuration);
        engine.See("a", "acc", "A", Now, 1, Now);
        Assert.That(engine.Report("a", OnboardingStepKind.TalkToNpc, 1, s => s.NpcNumber <= 0 || s.NpcNumber == 251, Now).Any, Is.False, "Another NPC.");
        var advance = engine.Report("a", OnboardingStepKind.TalkToNpc, 1, s => s.NpcNumber <= 0 || s.NpcNumber == 253, Now);
        Assert.That(advance.Finished, Is.True);
        Assert.That(engine.StatusOf("a"), Is.EqualTo(OnboardingStatus.Done));
        Assert.That(engine.Owed("a").Select(o => o.Reference), Is.EquivalentTo(new[] { "onboarding:a:final" }), "The step has no reward; the final one waits.");
        Assert.That(engine.CurrentStep("a"), Is.Null);
    }

    /// <summary>
    /// The steps checked by looking: level, map, spot, party, cash shop; stat points and skills count from when the step
    /// started. Several can be done at once.
    /// </summary>
    [Test]
    public void CheckedStepsAreDoneByLookingAtTheCharacter()
    {
        var configuration = Configuration(
            new OnboardingStep { Id = "level", Kind = OnboardingStepKind.ReachLevel, Target = 5 },
            new OnboardingStep { Id = "map", Kind = OnboardingStepKind.EnterMap, MapNumber = 2 },
            new OnboardingStep { Id = "spot", Kind = OnboardingStepKind.WalkTo, MapNumber = 2, X = 100, Y = 100, Radius = 3 },
            new OnboardingStep { Id = "points", Kind = OnboardingStepKind.AddStatPoints, Target = 5 },
            new OnboardingStep { Id = "party", Kind = OnboardingStepKind.JoinParty },
            new OnboardingStep { Id = "shop", Kind = OnboardingStepKind.OpenCashShop });
        var engine = CreateEngine(configuration);
        engine.See("a", "acc", "A", Now, 1, Now);

        var facts = new OnboardingFacts(Level: 6, Map: 0, X: 10, Y: 10, InParty: false, StatPoints: 100, LearnedSkills: 1, CashShopOpen: false);
        Assert.That(engine.Check("a", facts, Now).Completed.Select(s => s.Id), Is.EqualTo(new[] { "level" }), "Level 6 is enough; the map isn't right.");
        Assert.That(engine.Check("a", facts with { Map = 2 }, Now).Completed.Select(s => s.Id), Is.EqualTo(new[] { "map" }), "The spot is far; the points' baseline is set when it's current.");
        Assert.That(engine.Check("a", facts with { Map = 2, X = 102, Y = 98 }, Now).Completed.Select(s => s.Id), Is.EqualTo(new[] { "spot" }));
        Assert.That(engine.Progress("a", facts)!.Value.Step.Id, Is.EqualTo("points"));
        Assert.That(engine.Check("a", facts with { StatPoints = 100 }, Now).Any, Is.False, "Baseline 100.");
        Assert.That(engine.Check("a", facts with { StatPoints = 104 }, Now).Any, Is.False);
        Assert.That(engine.Progress("a", facts with { StatPoints = 104 })!.Value.Value, Is.EqualTo(4));
        Assert.That(engine.Check("a", facts with { StatPoints = 105 }, Now).Completed.Select(s => s.Id), Is.EqualTo(new[] { "points" }));
        var advance = engine.Check("a", facts with { InParty = true, CashShopOpen = true }, Now);
        Assert.That(advance.Completed.Select(s => s.Id), Is.EqualTo(new[] { "party", "shop" }), "Both at once.");
        Assert.That(advance.Finished, Is.True);
    }

    /// <summary>
    /// An optional step can be skipped (no reward); a required one can't. Skipping the guide ends it, and the rewards
    /// earned before still wait.
    /// </summary>
    [Test]
    public void OptionalStepsAndTheWholeGuideCanBeSkipped()
    {
        var configuration = Configuration(
            new OnboardingStep { Id = "party", Kind = OnboardingStepKind.JoinParty, Optional = true, Reward = new MissionReward { Zen = 10 } },
            new OnboardingStep { Id = "kill", Kind = OnboardingStepKind.KillMonsters, Target = 5, Reward = new MissionReward { Zen = 20 } },
            new OnboardingStep { Id = "level", Kind = OnboardingStepKind.ReachLevel, Target = 50 });
        var engine = CreateEngine(configuration);
        engine.See("a", "acc", "A", Now, 1, Now);
        engine.SkipStep("a", Now);
        Assert.That(engine.CurrentStep("a")!.Id, Is.EqualTo("kill"));
        Assert.That(engine.Owed("a"), Is.Empty, "A skipped step gives nothing.");
        Assert.That(engine.StepsOf("a").Skipped, Is.EqualTo(new[] { "party" }));
        engine.SkipStep("a", Now);
        Assert.That(engine.CurrentStep("a")!.Id, Is.EqualTo("kill"), "Not optional.");
        engine.Report("a", OnboardingStepKind.KillMonsters, 5, null, Now);

        Assert.That(engine.Skip("a", Now), Is.True);
        Assert.That(engine.StatusOf("a"), Is.EqualTo(OnboardingStatus.Skipped));
        Assert.That(engine.CurrentStep("a"), Is.Null);
        Assert.That(engine.Report("a", OnboardingStepKind.KillMonsters, 5, null, Now).Any, Is.False);
        Assert.That(engine.Owed("a").Select(o => o.Reference), Is.EqualTo(new[] { "onboarding:a:kill" }), "No final reward; the earned one waits.");
        Assert.That(engine.Skip("a", Now), Is.False, "Once.");
    }

    /// <summary>
    /// Disabled steps are left out; switching the guide off stops it without ending it.
    /// </summary>
    [Test]
    public void DisabledStepsAndTheGuideSwitch()
    {
        var configuration = Configuration(
            new OnboardingStep { Id = "off", Enabled = false, Kind = OnboardingStepKind.JoinParty },
            new OnboardingStep { Id = "kill", Kind = OnboardingStepKind.KillMonsters });
        var engine = CreateEngine(configuration);
        engine.See("a", "acc", "A", Now, 1, Now);
        Assert.That(engine.CurrentStep("a")!.Id, Is.EqualTo("kill"));
        configuration.GuideEnabled = false;
        Assert.That(engine.CurrentStep("a"), Is.Null);
        Assert.That(engine.StatusOf("a"), Is.EqualTo(OnboardingStatus.Active));
        configuration.GuideEnabled = true;
        Assert.That(engine.CurrentStep("a")!.Id, Is.EqualTo("kill"));
    }

    /// <summary>
    /// The starter kit: once per new character with its class items; the switched-off pieces are left out; a character
    /// which isn't new gets nothing.
    /// </summary>
    [Test]
    public void TheStarterKitIsGivenOncePerNewCharacter()
    {
        var configuration = Configuration();
        configuration.StarterKit = new StarterKitConfiguration
        {
            Zen = 1000, Coins = 20, CoinsEnabled = false,
            Items = [new() { Group = 14, Number = 1, Count = 2, Durability = 3 }, new() { Group = 0, Number = 1, Level = 3, Skill = true, ClassNumber = 4 }, new() { Group = 5, Number = 0, ClassNumber = 0 }],
            ExperienceBuff = new StarterKitBuffConfiguration { Minutes = 30 },
        };
        var engine = CreateEngine(configuration);
        engine.See("knight", "acc", "K", Now, 1, Now);
        var kit = engine.TakeStarterKit("knight", "acc", 4, Now)!;
        Assert.That(kit.Reference, Is.EqualTo("onboarding:knight:kit"));
        Assert.That(kit.Items.Select(i => (i.Group, i.Number)), Is.EqualTo(new[] { ((short)14, (short)1), ((short)0, (short)1) }), "The knight's sword, not the wizard's staff.");
        Assert.That(kit.Items[1].Skill, Is.True);
        Assert.That(kit.Zen, Is.EqualTo(1000));
        Assert.That(kit.Coins, Is.EqualTo(0), "Coins are off.");
        Assert.That(kit.BuffMinutes, Is.EqualTo(30));
        Assert.That(engine.TakeStarterKit("knight", "acc", 4, Now), Is.Null, "Once.");
        Assert.That(engine.Owed("knight").Count(o => o.Kind == "kit"), Is.EqualTo(1));
        Assert.That(engine.TakeKitNotice("knight"), Is.True);
        Assert.That(engine.TakeKitNotice("knight"), Is.False, "Told once.");

        engine.See("second", "acc", "S", Now, 1, Now);
        Assert.That(engine.TakeStarterKit("second", "acc", 0, Now), Is.Not.Null, "Per character: the account's second character gets one too.");

        engine.See("old", "acc", "O", Activated.AddDays(-9), 200, Now);
        Assert.That(engine.TakeStarterKit("old", "acc", 0, Now), Is.Null, "Not new.");
    }

    /// <summary>
    /// Per account, only the account's first new character gets the kit; its stat points are given when it's created.
    /// </summary>
    [Test]
    public void PerAccountTheFirstNewCharacterGetsTheKit()
    {
        var configuration = Configuration();
        configuration.StarterKit.PerAccount = true;
        configuration.StarterKit.PointsEnabled = true;
        configuration.StarterKit.LevelUpPoints = 15;
        var engine = CreateEngine(configuration);
        Assert.That(engine.CharacterCreated("first", "acc", "F", Now), Is.EqualTo(15));
        Assert.That(engine.CharacterCreated("second", "acc", "S", Now), Is.EqualTo(0), "The account's kit is the first character's.");
        Assert.That(engine.CharacterCreated("other", "other", "O", Now), Is.EqualTo(15), "Another account.");

        engine.See("second", "acc", "S", Now, 1, Now);
        Assert.That(engine.TakeStarterKit("second", "acc", 0, Now), Is.Null);
        engine.See("first", "acc", "F", Now, 1, Now);
        Assert.That(engine.TakeStarterKit("first", "acc", 0, Now), Is.Not.Null);

        configuration.StarterKit.Enabled = false;
        Assert.That(engine.CharacterCreated("third", "new", "T", Now), Is.EqualTo(0), "The kit is off.");
    }

    /// <summary>
    /// Waiting rewards are taken and given back without being doubled; the store keeps everything in its file.
    /// </summary>
    [Test]
    public void WaitingRewardsAndTheFileOutlastARestart()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-onboarding-{Guid.NewGuid():N}.json");
        try
        {
            var configuration = Configuration(new OnboardingStep { Id = "kill", Kind = OnboardingStepKind.KillMonsters, Target = 3, Reward = new MissionReward { Zen = 5 } }, new OnboardingStep { Id = "level", Kind = OnboardingStepKind.ReachLevel, Target = 99 });
            var store = new OnboardingStore(file, Activated);
            var engine = new OnboardingEngine(configuration, store);
            engine.See("a", "acc", "A", Now, 1, Now);
            engine.Report("a", OnboardingStepKind.KillMonsters, 2, null, Now);
            var taken = engine.TakeOwed("a");
            Assert.That(taken, Is.Empty);
            engine.Report("a", OnboardingStepKind.KillMonsters, 1, null, Now);
            taken = engine.TakeOwed("a");
            Assert.That(taken, Has.Count.EqualTo(1));
            Assert.That(engine.Owed("a"), Is.Empty);
            engine.ReturnOwed("a", taken);
            engine.ReturnOwed("a", taken);
            Assert.That(engine.Owed("a"), Has.Count.EqualTo(1), "Not doubled.");
            lock (store.Lock)
            {
                store.SaveIfChanged();
            }

            var reloaded = new OnboardingEngine(configuration, new OnboardingStore(file, Now.AddDays(5)));
            Assert.That(reloaded.Store.Data.ActivatedAtUtc, Is.EqualTo(Activated), "The activation time stays.");
            Assert.That(reloaded.CurrentStep("a")!.Id, Is.EqualTo("level"));
            Assert.That(reloaded.Owed("a"), Has.Count.EqualTo(1));
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// The client's state and the website's list: the steps with their rewards, the current step's count and where it
    /// leads, and the starter kit (the website lists every class item).
    /// </summary>
    [Test]
    public void TheStateAndThePublicListAreWritten()
    {
        var configuration = new OnboardingConfiguration();
        var engine = CreateEngine(configuration);
        engine.See("a", "acc", "A", Now, 1, Now);
        engine.TakeStarterKit("a", "acc", 4, Now);
        var json = OnboardingStateWriter.WriteState(engine, "a", null, new OnboardingTarget(0, 127, 86, "Potion Girl Amy"), true, 4, new OnboardingBoardEvent("kit", "Ok"));
        Assert.That(json, Is.Not.Empty);
        Assert.That(json.All(c => c < 128), Is.True, "Plain ASCII.");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("active"));
        Assert.That(root.GetProperty("total").GetInt32(), Is.EqualTo(configuration.EnabledSteps().Count()));
        Assert.That(root.GetProperty("current").GetProperty("id").GetString(), Is.EqualTo("talk-merchant"));
        Assert.That(root.GetProperty("current").GetProperty("where").GetProperty("x").GetInt32(), Is.EqualTo(127));
        Assert.That(root.GetProperty("kit").GetProperty("items").GetArrayLength(), Is.EqualTo(4), "Three for everyone and the knight's sword.");
        Assert.That(root.GetProperty("owed")[0].GetProperty("kind").GetString(), Is.EqualTo("kit"));
        Assert.That(root.GetProperty("event").GetProperty("type").GetString(), Is.EqualTo("kit"));

        var publicJson = OnboardingStateWriter.WritePublic(configuration, (g, n) => g == 14 && n == 1 ? "Small Healing Potion" : null, [(4, "Dark Knight")]);
        using var publicDocument = JsonDocument.Parse(publicJson);
        var kit = publicDocument.RootElement.GetProperty("kit");
        Assert.That(kit.GetProperty("items").GetArrayLength(), Is.EqualTo(configuration.StarterKit.Items.Count));
        Assert.That(kit.GetProperty("items")[0].GetProperty("name").GetString(), Is.EqualTo("Small Healing Potion"));
        Assert.That(kit.GetProperty("items")[4].GetProperty("className").GetString(), Is.EqualTo("Dark Knight"));
        Assert.That(publicDocument.RootElement.GetProperty("steps")[0].GetProperty("titleVi").GetString(), Is.EqualTo("Nói chuyện với một thương nhân"));
    }

    private static OnboardingEngine CreateEngine(OnboardingConfiguration configuration) => new(configuration, new OnboardingStore(null, Activated));

    private static OnboardingConfiguration Configuration(params OnboardingStep[] steps) => new()
    {
        Steps = steps.ToList(),
        FinalReward = new MissionReward(),
    };
}

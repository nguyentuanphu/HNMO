// <copyright file="OutlawPointsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Reflection;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.ChatCommands;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;
using MonsterSpawnArea = HNMO.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// Per-season player killer (outlaw) rules tests covering Season 6.3 / Arena (3 hours per stage, 1/sec + mob/2)
/// and Season 9 (ReqPoint 21,600 / 43,200 / 86,400, MurdererPointIncrease 3,600, no per-second decay, mob/2 + /pkclear).
/// </summary>
[TestFixture]
public class OutlawPointsTest
{
    /// <summary>
    /// Season 6.3 / Arena: 1st kill Warning (3h), 2nd kill PK1 (3h -> Warning 3h), 3rd kill Murderer (3h -> PK1 3h -> Warning 3h),
    /// 4th kill adds another 3h, and PK1 holds at 1s and 2h59m before falling to Warning at 3h.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SeasonSixKillsHoldThreeHoursPerStageAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        killer.IsSeasonNineOutlawRules = false;
        var character = killer.SelectedCharacter!;

        var expected = new (HeroState State, int Points)[]
        {
            (HeroState.PlayerKillWarning, 10_800),
            (HeroState.PlayerKiller1stStage, 21_600),
            (HeroState.PlayerKiller2ndStage, 32_400),
            (HeroState.PlayerKiller2ndStage, 43_200),
        };

        foreach (var (state, points) in expected)
        {
            var victim = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
            await killer.AfterKilledPlayerAsync(victim).ConfigureAwait(false);

            Assert.That(character.State, Is.EqualTo(state));
            Assert.That(character.StateRemainingSeconds, Is.EqualTo(points));
        }

        // Verify S6 PK1 hold duration: still PK1 after 1s and 2h59m, Warning after 3h, Normal after 6h.
        character.State = HeroState.PlayerKiller1stStage;
        character.PlayerKillCount = 2;
        character.StateRemainingSeconds = 21_600;
        SetLastRegenerate(killer, DateTime.UtcNow.AddSeconds(-1));
        await InvokeAsync(killer, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage), "PK1 holds after 1s");

        character.StateRemainingSeconds = 21_600 - (2 * 3600 + 59 * 60);
        SetLastRegenerate(killer, DateTime.UtcNow.AddSeconds(-1));
        await InvokeAsync(killer, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage), "PK1 holds after 2h59m");

        character.StateRemainingSeconds = 10_801;
        SetLastRegenerate(killer, DateTime.UtcNow.AddSeconds(-1));
        await InvokeAsync(killer, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKillWarning), "Warning at 3h elapsed");

        character.StateRemainingSeconds = 1;
        SetLastRegenerate(killer, DateTime.UtcNow.AddSeconds(-1));
        await InvokeAsync(killer, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.Normal), "Normal at 6h elapsed");
        Assert.That(character.PlayerKillCount, Is.Zero);
    }

    /// <summary>
    /// Season 9: 1st..4th kills land inside 21,600 / 64,800 / 90,000 / 93,600, stay unchanged after 1h idle online,
    /// drop only via monster kills (-floor(level/2)) or /pkclear.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SeasonNineKillsStayInsideStageAndOnlyDecayByMonstersAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        killer.IsSeasonNineOutlawRules = true;
        var character = killer.SelectedCharacter!;

        var expected = new (HeroState State, int Points)[]
        {
            (HeroState.PlayerKillWarning, 21_600),
            (HeroState.PlayerKiller1stStage, 64_800),
            (HeroState.PlayerKiller2ndStage, 90_000),
            (HeroState.PlayerKiller2ndStage, 93_600),
        };

        foreach (var (state, points) in expected)
        {
            var victim = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
            await killer.AfterKilledPlayerAsync(victim).ConfigureAwait(false);

            Assert.That(character.State, Is.EqualTo(state));
            Assert.That(character.StateRemainingSeconds, Is.EqualTo(points));
        }

        // Reset to 2nd kill PK1 (64,800): 1 hour online idle changes NOTHING.
        character.State = HeroState.PlayerKiller1stStage;
        character.PlayerKillCount = 2;
        character.StateRemainingSeconds = 64_800;
        SetLastRegenerate(killer, DateTime.UtcNow.AddHours(-1));
        await InvokeAsync(killer, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage));
        Assert.That(character.StateRemainingSeconds, Is.EqualTo(64_800), "no per-second decay in S9");

        // Killing one level-100 monster subtracts 50: still inside PK1.
        var monster = await CreateMonsterAsync(gameContext, 100).ConfigureAwait(false);
        await monster.ReflectDamageAsync(killer, 1_000_000).ConfigureAwait(false);
        Assert.That(character.StateRemainingSeconds, Is.EqualTo(64_750));
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage));

        // Enough monster kills cross below 43,200 into Warning.
        await killer.RemoveOutlawPointsAsync(21_551).ConfigureAwait(false);
        Assert.That(character.StateRemainingSeconds, Is.EqualTo(43_199));
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKillWarning));
    }

    /// <summary>
    /// Verifies /pkclear works for outlaws in both Season 6.3 and Season 9 rules with 1,000,000 Zen * kill count.
    /// </summary>
    /// <param name="isSeasonNine">Whether Season 9 rules apply.</param>
    /// <returns>The task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task PkClearClearsOutlawAndChargesPerKillInBothSeasonsAsync(bool isSeasonNine)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.IsSeasonNineOutlawRules = isSeasonNine;
        player.SelectedCharacter!.CharacterStatus = CharacterStatus.Normal;
        player.SelectedCharacter.State = HeroState.PlayerKiller1stStage;
        player.SelectedCharacter.PlayerKillCount = 2;
        player.SelectedCharacter.StateRemainingSeconds = isSeasonNine ? 64_800 : 21_600;
        player.Money = 5_000_000;

        var plugin = new PkClearChatCommandPlugIn();
        await plugin.HandleCommandAsync(player, "/pkclear").ConfigureAwait(false);

        Assert.That(player.SelectedCharacter.State, Is.EqualTo(HeroState.Normal));
        Assert.That(player.SelectedCharacter.StateRemainingSeconds, Is.Zero);
        Assert.That(player.SelectedCharacter.PlayerKillCount, Is.Zero);
        Assert.That(player.Money, Is.EqualTo(3_000_000));
    }

    /// <summary>
    /// Saved characters keep their saved outlaw stage without jumping on login after updating in both seasons.
    /// </summary>
    /// <param name="isSeasonNine">Season flag.</param>
    /// <param name="state">Saved state.</param>
    /// <param name="savedRemaining">Saved remaining points/time.</param>
    /// <param name="expectedPoints">Normalized remaining total.</param>
    /// <returns>The task.</returns>
    [TestCase(false, HeroState.PlayerKillWarning, 5_000, 5_000)]
    [TestCase(false, HeroState.PlayerKillWarning, 20_000, 10_800)]
    [TestCase(false, HeroState.PlayerKiller1stStage, 5_000, 15_800)]
    [TestCase(false, HeroState.PlayerKiller1stStage, 43_200, 21_600)]
    [TestCase(false, HeroState.PlayerKiller2ndStage, 10_000, 31_600)]
    [TestCase(false, HeroState.PlayerKiller2ndStage, 90_000, 90_000)]
    [TestCase(true, HeroState.PlayerKillWarning, 5_000, 5_000)]
    [TestCase(true, HeroState.PlayerKillWarning, 21_600, 21_600)]
    [TestCase(true, HeroState.PlayerKiller1stStage, 5_000, 48_200)]
    [TestCase(true, HeroState.PlayerKiller1stStage, 64_800, 64_800)]
    [TestCase(true, HeroState.PlayerKiller2ndStage, 10_000, 96_400)]
    [TestCase(true, HeroState.PlayerKiller2ndStage, 90_000, 90_000)]
    public async Task SavedCharactersKeepTheirStageAcrossLoginConversionAsync(
        bool isSeasonNine,
        HeroState state,
        int savedRemaining,
        int expectedPoints)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.IsSeasonNineOutlawRules = isSeasonNine;
        var character = player.SelectedCharacter!;
        character.State = state;
        character.PlayerKillCount = 10;
        character.StateRemainingSeconds = savedRemaining;

        await InvokeAsync(player, "LimitHeroStateRemainingTimeAsync").ConfigureAwait(false);

        Assert.That(character.State, Is.EqualTo(state));
        Assert.That(character.StateRemainingSeconds, Is.EqualTo(expectedPoints));
    }

    private static void SetLastRegenerate(Player player, DateTime utc)
    {
        var field = typeof(Player).GetField("_lastRegenerate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field!.SetValue(player, utc);
    }

    private static async ValueTask InvokeAsync(Player player, string methodName)
    {
        var method = typeof(Player).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        await ((ValueTask)method!.Invoke(player, null)!).ConfigureAwait(false);
    }

    private static async ValueTask<Monster> CreateMonsterAsync(IGameContext gameContext, int level)
    {
        // A unique id, because the attributes of monster definitions are cached by their id.
        var monsterDefinition = new MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = NpcObjectKind.Monster };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });
        var map = await gameContext.GetMapAsync(0).ConfigureAwait(false);
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map!.Definition,
            X1 = 100,
            Y1 = 100,
            X2 = 100,
            Y2 = 100,
            Quantity = 1,
        };

        var monster = new Monster(
            spawnArea,
            monsterDefinition,
            map,
            NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object,
            gameContext.PlugInManager,
            gameContext.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        return monster;
    }
}

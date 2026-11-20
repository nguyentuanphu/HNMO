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
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;
using MonsterSpawnArea = HNMO.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// Tests for the player killer points, as the Season 6/9 server suites count them: one running total
/// (first kill 21,600, second 43,200, third 86,400, +3,600 per further kill), which goes down by one per
/// second online and by half of the level of a killed monster; the stage follows the total
/// (from 86,400 murderer, from 43,200 PK level 2, above 0 Warning).
/// </summary>
[TestFixture]
public class OutlawPointsTest
{
    /// <summary>
    /// Each kill raises the total, and the stage follows it.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task KillsRaiseTheTotalAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var character = killer.SelectedCharacter!;

        var expected = new (HeroState State, int Points)[]
        {
            (HeroState.PlayerKillWarning, 21_600),
            (HeroState.PlayerKiller1stStage, 43_200),
            (HeroState.PlayerKiller2ndStage, 86_400),
            (HeroState.PlayerKiller2ndStage, 90_000),
        };

        foreach (var (state, points) in expected)
        {
            var victim = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
            await killer.AfterKilledPlayerAsync(victim).ConfigureAwait(false);

            Assert.That(character.State, Is.EqualTo(state));
            Assert.That(character.StateRemainingSeconds, Is.EqualTo(points));
        }
    }

    /// <summary>
    /// A kill of a Warning-stage outlaw whose total went down makes it PK level 2 with 43,200 again.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SecondKillSetsPkLevelTwoAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var character = killer.SelectedCharacter!;
        character.State = HeroState.PlayerKillWarning;
        character.StateRemainingSeconds = 5_000;
        character.PlayerKillCount = 1;

        await killer.AfterKilledPlayerAsync(await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false)).ConfigureAwait(false);

        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage));
        Assert.That(character.StateRemainingSeconds, Is.EqualTo(43_200));
    }

    /// <summary>
    /// The stage follows the total down while it counts down online, without starting the lower stage anew.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StageFollowsTheTotalDownAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        character.State = HeroState.PlayerKiller2ndStage;
        character.PlayerKillCount = 3;

        character.StateRemainingSeconds = 86_400;
        await InvokeAsync(player, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller2ndStage), "86,400 is still a murderer");

        character.StateRemainingSeconds = 86_000;
        await InvokeAsync(player, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage));
        Assert.That(character.StateRemainingSeconds, Is.InRange(85_998, 86_000), "the total carries on");

        character.StateRemainingSeconds = 43_000;
        await InvokeAsync(player, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKillWarning));
        Assert.That(character.StateRemainingSeconds, Is.InRange(42_998, 43_000));

        character.StateRemainingSeconds = 0;
        await InvokeAsync(player, "RegenerateHeroStateAsync").ConfigureAwait(false);
        Assert.That(character.State, Is.EqualTo(HeroState.Normal));
        Assert.That(character.PlayerKillCount, Is.Zero);
    }

    /// <summary>
    /// A monster killed by an outlaw removes half of its level from the total, and the stage follows at once.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task MonsterKillRemovesHalfOfItsLevelAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var character = killer.SelectedCharacter!;
        character.State = HeroState.PlayerKiller2ndStage;
        character.PlayerKillCount = 3;
        character.StateRemainingSeconds = 86_420;
        var monster = await CreateMonsterAsync(gameContext, 100).ConfigureAwait(false);

        await monster.ReflectDamageAsync(killer, 1_000_000).ConfigureAwait(false);

        Assert.That(monster.LastDeath, Is.Not.Null);
        Assert.That(character.StateRemainingSeconds, Is.EqualTo(86_420 - 50));
        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage));
    }

    /// <summary>
    /// The remaining time of an outlaw saved before the points system (3 hours per stage, counted per stage)
    /// becomes a total with the same time until the character is clear, and the stage follows that total.
    /// </summary>
    /// <param name="state">The saved state.</param>
    /// <param name="remaining">The saved remaining seconds of the stage.</param>
    /// <param name="expectedPoints">The expected total.</param>
    /// <param name="expectedState">The expected state.</param>
    /// <returns>The task.</returns>
    [TestCase(HeroState.PlayerKillWarning, 5_000, 5_000, HeroState.PlayerKillWarning)]
    [TestCase(HeroState.PlayerKiller1stStage, 5_000, 15_800, HeroState.PlayerKillWarning)]
    [TestCase(HeroState.PlayerKiller2ndStage, 10_000, 31_600, HeroState.PlayerKillWarning)]
    [TestCase(HeroState.PlayerKiller2ndStage, 30_000, 51_600, HeroState.PlayerKiller1stStage)]
    [TestCase(HeroState.PlayerKiller2ndStage, 90_000, 90_000, HeroState.PlayerKiller2ndStage)]
    public async Task FormerRemainingTimeIsConvertedAsync(HeroState state, int remaining, int expectedPoints, HeroState expectedState)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        character.State = state;
        character.PlayerKillCount = 10;
        character.StateRemainingSeconds = remaining;

        await InvokeAsync(player, "LimitHeroStateRemainingTimeAsync").ConfigureAwait(false);

        Assert.That(character.StateRemainingSeconds, Is.EqualTo(expectedPoints));
        Assert.That(character.State, Is.EqualTo(expectedState));
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

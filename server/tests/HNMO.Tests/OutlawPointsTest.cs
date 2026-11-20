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
/// Tests for the player killer points, as the Season 6/9 server suites count them:
/// Warning 21,600, PK level 2 43,200, murderer 86,400, +3,600 per further kill,
/// and a killed monster removes half of its level.
/// </summary>
[TestFixture]
public class OutlawPointsTest
{
    /// <summary>
    /// Each kill steps up the outlaw state and starts it with the points of that state;
    /// further kills as a murderer add 3,600 points.
    /// </summary>
    [Test]
    public async Task KillsStartTheStagesWithTheirPointsAsync()
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
    /// When the points of a stage run out, the state falls back one stage and starts with the points of that stage.
    /// </summary>
    [Test]
    public async Task FallingBackStartsTheLowerStageWithItsPointsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        character.State = HeroState.PlayerKiller2ndStage;
        character.PlayerKillCount = 3;
        character.StateRemainingSeconds = 0;

        await InvokeRegenerateHeroStateAsync(player).ConfigureAwait(false);

        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKiller1stStage));
        Assert.That(character.StateRemainingSeconds, Is.InRange(43_200 - 2, 43_200));

        character.StateRemainingSeconds = 0;
        await InvokeRegenerateHeroStateAsync(player).ConfigureAwait(false);

        Assert.That(character.State, Is.EqualTo(HeroState.PlayerKillWarning));
        Assert.That(character.StateRemainingSeconds, Is.InRange(21_600 - 2, 21_600));
    }

    /// <summary>
    /// A monster killed by an outlaw removes half of its level from the points.
    /// </summary>
    [Test]
    public async Task MonsterKillRemovesHalfOfItsLevelAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var character = killer.SelectedCharacter!;
        character.State = HeroState.PlayerKillWarning;
        character.StateRemainingSeconds = 10_000;
        var monster = await CreateMonsterAsync(gameContext, 100).ConfigureAwait(false);

        await monster.ReflectDamageAsync(killer, 1_000_000).ConfigureAwait(false);

        Assert.That(monster.LastDeath, Is.Not.Null);
        Assert.That(character.StateRemainingSeconds, Is.EqualTo(10_000 - 50));
    }

    private static async ValueTask InvokeRegenerateHeroStateAsync(Player player)
    {
        var method = typeof(Player).GetMethod("RegenerateHeroStateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ((ValueTask)method.Invoke(player, null)!).ConfigureAwait(false);
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

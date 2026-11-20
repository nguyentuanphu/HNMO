// <copyright file="PlayerLosesExperienceAfterDeathDefaultsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests the default experience loss on death of the <see cref="PlayerLosesExperienceAfterDeathPlugIn"/>, which
/// follows the rows of the Season 6/9 server suites: normal players 3 % (level 11-150), 2 % (151-220) and
/// 1 % (221 and up), Warning 5 %, PK level 2 10 % and murderer 20 %.
/// </summary>
[TestFixture]
public class PlayerLosesExperienceAfterDeathDefaultsTest
{
    /// <summary>
    /// The default loss in percent of the experience of the current level.
    /// </summary>
    /// <param name="level">The character level.</param>
    /// <param name="state">The hero state.</param>
    /// <param name="expectedPercentage">The expected loss in percent.</param>
    /// <returns>The task.</returns>
    [TestCase(11, HeroState.Normal, 3)]
    [TestCase(150, HeroState.Normal, 3)]
    [TestCase(151, HeroState.Normal, 2)]
    [TestCase(220, HeroState.Normal, 2)]
    [TestCase(221, HeroState.Normal, 1)]
    [TestCase(350, HeroState.Normal, 1)]
    [TestCase(100, HeroState.Hero, 3)]
    [TestCase(100, HeroState.PlayerKillWarning, 5)]
    [TestCase(300, HeroState.PlayerKillWarning, 5)]
    [TestCase(100, HeroState.PlayerKiller1stStage, 10)]
    [TestCase(300, HeroState.PlayerKiller1stStage, 10)]
    [TestCase(100, HeroState.PlayerKiller2ndStage, 20)]
    [TestCase(300, HeroState.PlayerKiller2ndStage, 20)]
    [TestCase(10, HeroState.Normal, 0)]
    public async Task DefaultLossAsync(int level, HeroState state, int expectedPercentage)
    {
        var gameContext = GameContextTestHelper.CreateGameContext(maximumLevel: 400);
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        character.State = state;
        player.Attributes![Stats.Level] = level;
        Assert.That(player.Attributes[Stats.Level], Is.EqualTo(level));

        var table = player.GameContext.ExperienceTable;
        var levelExperience = table[level + 1] - table[level];
        character.Experience = table[level + 1] - 1;

        await new PlayerLosesExperienceAfterDeathPlugIn().AttackableGotKilledAsync(player, await CreateMonsterAsync().ConfigureAwait(false)).ConfigureAwait(false);

        Assert.That(character.Experience, Is.EqualTo(table[level + 1] - 1 - (int)(levelExperience * (expectedPercentage / 100.0))));
    }

    private static async ValueTask<Monster> CreateMonsterAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var map = await gameContext.GetMapAsync(0).ConfigureAwait(false);
        var definition = new BasicModel.MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = NpcObjectKind.Monster };
        var spawnArea = new BasicModel.MonsterSpawnArea { MonsterDefinition = definition, GameMap = map!.Definition, Quantity = 1 };
        return new Monster(spawnArea, definition, map, NullDropGenerator.Instance, new Mock<INpcIntelligence>().Object, gameContext.PlugInManager, gameContext.PathFinderPool);
    }
}

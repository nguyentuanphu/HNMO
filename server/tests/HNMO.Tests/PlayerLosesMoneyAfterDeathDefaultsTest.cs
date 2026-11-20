// <copyright file="PlayerLosesMoneyAfterDeathDefaultsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests the default zen loss on death of the <see cref="PlayerLosesMoneyAfterDeathPlugIn"/>: as the Season 6/9
/// server suites, it takes 1-4 % by level from the bag only, never from the vault.
/// </summary>
[TestFixture]
public class PlayerLosesMoneyAfterDeathDefaultsTest
{
    private const int Money = 100_000;

    /// <summary>
    /// A death with the default settings takes 1 % (level 100) from the bag and nothing from the vault.
    /// </summary>
    [Test]
    public async Task DefaultLossTakesOnlyFromTheBagAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account = new BasicModel.Account { Vault = new BasicModel.ItemStorage { Money = Money } };
        player.Money = Money;
        player.Attributes![Stats.Level] = 100;
        var plugIn = new PlayerLosesMoneyAfterDeathPlugIn();

        await plugIn.AttackableGotKilledAsync(player, await CreateMonsterAsync().ConfigureAwait(false)).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(player.Money, Is.EqualTo(Money - (Money / 100)), "1 % from the bag");
            Assert.That(player.Account.Vault!.Money, Is.EqualTo(Money), "nothing from the vault");
        });
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

// <copyright file="TalismanStateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.ItemConsumeActions;
using HNMO.GameLogic.PlugIns.Talismans;
using HNMO.Persistence.BasicModel;

/// <summary>
/// HNMO's stat items (talisman plug-in) are used under the same state check as every other item: not while
/// dead, trading or in an NPC dialog (owner, 21 Nov 2026).
/// </summary>
[TestFixture]
public class TalismanStateTest
{
    private const byte ScrollSlot = 14;

    /// <summary>A stat item isn't used while the player trades.</summary>
    [Test]
    public async Task StatItemIsNotUsedWhileTradingAsync()
    {
        var player = await CreatePlayerWithScrollAsync().ConfigureAwait(false);
        var points = player.SelectedCharacter!.LevelUpPoints;
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.TradeRequested).ConfigureAwait(false), Is.True, "test setup");

        await new ItemConsumeAction().HandleConsumeRequestAsync(player, ScrollSlot, 0, default).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(points));
            Assert.That(player.Inventory!.GetItem(ScrollSlot), Is.Not.Null);
        });
    }

    /// <summary>A stat item is used in the world.</summary>
    [Test]
    public async Task StatItemIsUsedInTheWorldAsync()
    {
        var player = await CreatePlayerWithScrollAsync().ConfigureAwait(false);
        var points = player.SelectedCharacter!.LevelUpPoints;

        await new ItemConsumeAction().HandleConsumeRequestAsync(player, ScrollSlot, 0, default).ConfigureAwait(false);

        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(points + 10));
    }

    private static async Task<Player> CreatePlayerWithScrollAsync()
    {
        var cfg = new TalismanConfiguration
        {
            FileName = string.Empty, // isolate in-memory store
            GlobalStatPointsCap = 100,
            StatConsumables =
            [
                new StatConsumableDefinitionConfig { ItemGroup = 14, ItemNumber = 91, NameEn = "Stat Scroll", PointsPerUse = 10, TargetBucket = "levelUpPoints" },
            ],
        };
        var pluginCfg = new PlugInConfiguration { TypeId = typeof(TalismanPlugIn).GUID, IsActive = true, CustomConfiguration = JsonSerializer.Serialize(cfg) };
        var context = GameContextTestHelper.CreateGameContext([pluginCfg]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);

        // The claimed points are counted per character; an own id keeps them apart from the other talisman tests.
        player.SelectedCharacter!.Id = Guid.NewGuid();
        var scrollDefinition = new ItemDefinition { Group = 14, Number = 91, Width = 1, Height = 1, Durability = 1 };
        var scroll = new Item { Id = Guid.NewGuid(), Definition = scrollDefinition, Durability = 1 };
        Assert.That(await player.Inventory!.AddItemAsync(ScrollSlot, scroll).ConfigureAwait(false), Is.True);
        return player;
    }
}

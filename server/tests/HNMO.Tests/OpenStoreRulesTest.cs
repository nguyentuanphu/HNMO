// <copyright file="OpenStoreRulesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// A personal store opens from level 6 and not while trading, in an NPC dialog, dead or duelling (owner, 21 Nov 2026).
/// </summary>
[TestFixture]
public class OpenStoreRulesTest
{
    /// <summary>A store opens at level 6 in the world.</summary>
    [Test]
    public async Task OpensAtLevelSixAsync()
    {
        var player = await CreateSellerAsync(level: 6).ConfigureAwait(false);

        await new OpenStoreAction().OpenStoreAsync(player, "Store").ConfigureAwait(false);

        Assert.That(player.ShopStorage!.StoreOpen, Is.True);
    }

    /// <summary>A store doesn't open below level 6.</summary>
    [Test]
    public async Task DoesNotOpenBelowLevelSixAsync()
    {
        var player = await CreateSellerAsync(level: 5).ConfigureAwait(false);

        await new OpenStoreAction().OpenStoreAsync(player, "Store").ConfigureAwait(false);

        Assert.That(player.ShopStorage!.StoreOpen, Is.False);
    }

    /// <summary>A store doesn't open while the player trades.</summary>
    [Test]
    public async Task DoesNotOpenWhileTradingAsync()
    {
        var player = await CreateSellerAsync(level: 6).ConfigureAwait(false);
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.TradeRequested).ConfigureAwait(false), Is.True, "test setup");

        await new OpenStoreAction().OpenStoreAsync(player, "Store").ConfigureAwait(false);

        Assert.That(player.ShopStorage!.StoreOpen, Is.False);
    }

    /// <summary>A store doesn't open while the player is dead.</summary>
    [Test]
    public async Task DoesNotOpenWhileDeadAsync()
    {
        var player = await CreateSellerAsync(level: 6).ConfigureAwait(false);
        player.IsAlive = false;

        await new OpenStoreAction().OpenStoreAsync(player, "Store").ConfigureAwait(false);

        Assert.That(player.ShopStorage!.StoreOpen, Is.False);
    }

    private static async Task<Player> CreateSellerAsync(int level)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = level;
        var item = new BasicModel.Item
        {
            Definition = new BasicModel.ItemDefinition { Number = 1, Group = 0, Width = 1, Height = 1, Durability = 20, IsPersonalStoreSellable = true },
            ItemSlot = InventoryConstants.FirstStoreItemSlotIndex,
            Durability = 20,
            StorePrice = 1000,
        };
        Assert.That(await player.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, item).ConfigureAwait(false), Is.True);
        return player;
    }
}

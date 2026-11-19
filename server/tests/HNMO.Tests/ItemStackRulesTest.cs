// <copyright file="ItemStackRulesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ItemStacks;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// HNMO's bigger stacks: free kinds stack up to their limit for everybody, unlock kinds only for an account which
/// unlocked them, an existing stack keeps stacking, the unlock costs coins, and a stack sells for its pieces.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ItemStackRulesTest
{
    private string _file = null!;

    /// <summary>Sets up an isolated state file and the default rules.</summary>
    [SetUp]
    public void SetUp()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        ItemStackRules.ResetCache();
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-stacks-{Guid.NewGuid():N}.json");
        ItemStackRules.Configure(new ItemStackConfiguration { FileName = this._file });
        ItemStackRules.CurrentPlayer = null;
    }

    /// <summary>Puts the default rules back.</summary>
    [TearDown]
    public void TearDown()
    {
        ItemStackRules.CurrentPlayer = null;
        ItemStackRules.ResetCache();
        ItemStackRules.Configure(new ItemStackConfiguration());
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>A potion (a free kind) stacks up to 255; a jewel only after its account unlocked jewels.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FreeKindsStackForAllAndJewelsAfterTheUnlockAsync()
    {
        var potion = NewItem(14, 1, definitionDurability: 3, durability: 3);
        Assert.That(potion.MaximumStackSize(), Is.EqualTo(255));

        var jewel = NewItem(14, 13, definitionDurability: 1, durability: 1);
        var other = NewItem(14, 13, definitionDurability: 1, durability: 1);
        other.Definition = jewel.Definition;
        Assert.That(jewel.IsStackable(), Is.False, "no player: a single jewel doesn't stack");

        var context = GameContextTestHelper.CreateGameContext([]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "stacker" };
        ItemStackRules.CurrentPlayer = player;
        Assert.That(jewel.CanCompletelyStackOn(other), Is.False, "jewels aren't unlocked yet");

        ItemStackRules.AddUnlock("stacker", "jewels");
        Assert.That(jewel.MaximumStackSize(), Is.EqualTo(255));
        Assert.That(jewel.CanCompletelyStackOn(other), Is.True);

        // Another account hasn't unlocked them.
        player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "someone" };
        Assert.That(jewel.CanCompletelyStackOn(other), Is.False);
    }

    /// <summary>A stack which holds more than one piece keeps stacking (and counting) where no player is known.</summary>
    [Test]
    public void AnExistingStackKeepsItsLimitWithoutAPlayer()
    {
        var stack = NewItem(14, 13, definitionDurability: 1, durability: 20);
        Assert.That(stack.IsStackable(), Is.True);
        Assert.That(stack.MaximumStackSize(), Is.EqualTo(255));
    }

    /// <summary>The unlock costs its coins, is kept for the account, and is refused without enough coins.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheUnlockCostsCoinsAndIsKeptAsync()
    {
        var cfg = new PlugInConfiguration
        {
            TypeId = typeof(ItemStackPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new ItemStackConfiguration { FileName = this._file }),
        };
        var context = GameContextTestHelper.CreateGameContext([cfg]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = "buyer", WCoinC = 700 };
        var plugIn = ItemStackPlugIn.Get(context)!;

        var (ok, _) = await plugIn.UnlockAsync(player, "jewels").ConfigureAwait(false);
        Assert.That(ok, Is.True);
        Assert.That(player.Account.WCoinC, Is.EqualTo(200));
        Assert.That(ItemStackRules.IsUnlocked("buyer", "jewels"), Is.True);

        // Kept in the state rows: a fresh cache reads it back.
        ItemStackRules.ResetCache();
        Assert.That(ItemStackRules.IsUnlocked("buyer", "jewels"), Is.True);

        var (boxes, _) = await plugIn.UnlockAsync(player, "boxes").ConfigureAwait(false);
        Assert.That(boxes, Is.False, "200 coins aren't enough");
        Assert.That(player.Account.WCoinC, Is.EqualTo(200));
        Assert.That(ItemStackRules.IsUnlocked("buyer", "boxes"), Is.False);

        var (potions, _) = await plugIn.UnlockAsync(player, "potions").ConfigureAwait(false);
        Assert.That(potions, Is.True, "a free kind needs no unlock");
        Assert.That(player.Account.WCoinC, Is.EqualTo(200));
    }

    /// <summary>A stack of 20 jewels sells for 20 jewels.</summary>
    [Test]
    public void AStackSellsForItsPieces()
    {
        var one = NewItem(14, 13, definitionDurability: 1, durability: 1);
        var twenty = NewItem(14, 13, definitionDurability: 1, durability: 20);
        twenty.Definition = one.Definition;
        one.Definition!.Value = 9_000_000;
        var calculator = new ItemPriceCalculator();
        Assert.That(calculator.CalculateSellingPrice(twenty), Is.EqualTo(20 * calculator.CalculateSellingPrice(one)));
    }

    private static Item NewItem(byte group, short number, byte definitionDurability, byte durability)
    {
        var definition = new HNMO.Persistence.BasicModel.ItemDefinition { Group = group, Number = number, Name = $"{group}:{number}", Width = 1, Height = 1, Durability = definitionDurability };
        return new HNMO.Persistence.BasicModel.Item { Definition = definition, Durability = durability };
    }
}

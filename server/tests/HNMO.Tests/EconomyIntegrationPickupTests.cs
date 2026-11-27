// <copyright file="EconomyIntegrationPickupTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.GameLogic;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Picking up stackable drops: one drop picked twice counts once, and a drop bigger than the room left on a stack
/// fills the stack to its cap and keeps the rest as a new piece, so nothing is lost or doubled.
/// </summary>
[TestFixture]
public class EconomyIntegrationPickupTests
{
    private static readonly BasicModel.ItemDefinition Bless = new()
    {
        Group = 14,
        Number = 13,
        Name = "Jewel of Bless",
        Width = 1,
        Height = 1,
        Durability = 5,
        MaximumItemLevel = 0,
        IsBoundToCharacter = false,
    };

    private Func<HNMO.DataModel.Entities.Item, int?>? _stackLimit;

    /// <summary>
    /// Uses the item definition's own cap: <see cref="HNMO.DataModel.ItemExtensions.StackLimit"/> is static and the item stack
    /// plug-in (or another test) may have left a rule in it.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._stackLimit = HNMO.DataModel.ItemExtensions.StackLimit;
        HNMO.DataModel.ItemExtensions.StackLimit = null;
    }

    /// <summary>Puts the stack rule back.</summary>
    [TearDown]
    public void TearDown() => HNMO.DataModel.ItemExtensions.StackLimit = this._stackLimit;

    /// <summary>The second pickup of the same drop fails and adds nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PickingTheSameDropTwiceCountsItOnceAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        HNMO.DataModel.ItemExtensions.StackLimit = null; // the game context's item stack plug-in sets its own rule
        var existing = new BasicModel.Item { Definition = Bless, Durability = 3 };
        await player.Inventory!.AddItemAsync(12, existing).ConfigureAwait(false);
        var drop = new DroppedItem(new BasicModel.Item { Definition = Bless, Durability = 2 }, player.Position, player.CurrentMap!, player);
        await player.CurrentMap!.AddAsync(drop).ConfigureAwait(false);

        var first = await drop.TryPickUpByAsync(player).ConfigureAwait(false);
        var second = await drop.TryPickUpByAsync(player).ConfigureAwait(false);

        Assert.That(first.Success, Is.True);
        Assert.That(second.Success, Is.False);
        Assert.That(TotalBless(player), Is.EqualTo(5));
    }

    /// <summary>A stack of 4 (cap 5) and a drop of 3: the stack is filled to 5 and a new piece of 2 is added.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADropOverTheStackCapFillsTheStackAndKeepsTheRestAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        HNMO.DataModel.ItemExtensions.StackLimit = null; // the game context's item stack plug-in sets its own rule
        var existing = new BasicModel.Item { Definition = Bless, Durability = 4 };
        await player.Inventory!.AddItemAsync(12, existing).ConfigureAwait(false);
        var drop = new DroppedItem(new BasicModel.Item { Definition = Bless, Durability = 3 }, player.Position, player.CurrentMap!, player);
        await player.CurrentMap!.AddAsync(drop).ConfigureAwait(false);

        var (success, _) = await drop.TryPickUpByAsync(player).ConfigureAwait(false);

        Assert.That(success, Is.True);
        Assert.That(existing.Durability, Is.EqualTo(existing.MaximumStackSize()));
        Assert.That(player.Inventory.Items.Where(i => i.Definition == Bless).All(i => i.Durability <= i.MaximumStackSize()), Is.True);
        Assert.That(TotalBless(player), Is.EqualTo(7));
    }

    private static int TotalBless(Player player) =>
        player.Inventory!.Items.Where(i => i.Definition == Bless).Sum(i => (int)i.Durability);
}

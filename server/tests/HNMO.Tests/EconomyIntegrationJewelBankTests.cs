// <copyright file="EconomyIntegrationJewelBankTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.GiftCodes;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// What a Jewel Bank withdrawal puts into the bag: it asks <see cref="GiftCodeRewards.GiveGoodsAsync"/> for one item of count 1
/// per jewel withdrawn, with <c>GiftCodeItem.Durability</c> left at 0, so the item definition's durability decides the
/// number of pieces (<c>GiftCodeRewards.cs:140</c>).
/// </summary>
[TestFixture]
public class EconomyIntegrationJewelBankTests
{
    /// <summary>With the real jewel definition (durability 1) one withdrawn jewel is exactly one piece.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithdrawingOneJewelGivesOnePieceAsync()
    {
        var (player, bless) = await CreatePlayerWithJewelAsync(definitionDurability: 1).ConfigureAwait(false);

        Assert.That(await GiftCodeRewards.GiveGoodsAsync(player, WithdrawalOf(1)).ConfigureAwait(false), Is.True);

        var pieces = player.Inventory!.Items.Where(i => i.Definition == bless).Sum(i => (int)i.Durability);
        Assert.That(pieces, Is.EqualTo(1));
    }

    /// <summary>Three withdrawals of one jewel give three pieces, not more.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithdrawingThreeJewelsOneByOneGivesThreePiecesAsync()
    {
        var (player, bless) = await CreatePlayerWithJewelAsync(definitionDurability: 1).ConfigureAwait(false);

        for (var i = 0; i < 3; i++)
        {
            Assert.That(await GiftCodeRewards.GiveGoodsAsync(player, WithdrawalOf(1)).ConfigureAwait(false), Is.True);
        }

        var pieces = player.Inventory!.Items.Where(i => i.Definition == bless).Sum(i => (int)i.Durability);
        Assert.That(pieces, Is.EqualTo(3));
    }

    /// <summary>A full bag refuses the withdrawal and leaves nothing behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithdrawalIntoAFullBagGivesNothingAsync()
    {
        var (player, bless) = await CreatePlayerWithJewelAsync(definitionDurability: 1).ConfigureAwait(false);
        var filler = new BasicModel.ItemDefinition { Group = 14, Number = 0, Width = 1, Height = 1, Durability = 1 };
        player.GameContext.Configuration.Items.Add(filler);
        for (var slot = InventoryConstants.EquippableSlotsCount; slot < InventoryConstants.EquippableSlotsCount + 64; slot++)
        {
            var item = player.PersistenceContext.CreateNew<Item>();
            item.Definition = filler;
            item.Durability = 1;
            Assert.That(await player.Inventory!.AddItemAsync((byte)slot, item).ConfigureAwait(false), Is.True);
        }

        var before = player.Inventory!.Items.Count();
        Assert.That(await GiftCodeRewards.GiveGoodsAsync(player, WithdrawalOf(1)).ConfigureAwait(false), Is.False);

        Assert.That(player.Inventory.Items.Count(), Is.EqualTo(before));
        Assert.That(player.Inventory.Items.Any(i => i.Definition == bless), Is.False);
    }

    private static GiftCodeOwedReward WithdrawalOf(int count)
        => new() { Items = [new GiftCodeItem { Group = 14, Number = 13, Count = count }] };

    private static async Task<(Player Player, BasicModel.ItemDefinition Bless)> CreatePlayerWithJewelAsync(int definitionDurability)
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var bless = new BasicModel.ItemDefinition { Group = 14, Number = 13, Width = 1, Height = 1, Durability = (byte)definitionDurability };
        context.Configuration.Items.Add(bless);
        return (player, bless);
    }
}

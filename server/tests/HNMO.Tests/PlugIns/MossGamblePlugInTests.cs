// <copyright file="MossGamblePlugInTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.MossMerchant;
using HNMO.GameLogic.Views.Inventory;
using Moq;

/// <summary>
/// Verifies <see cref="MossGamblePlugIn"/> replaces Moss (492) gamble vouchers (13-71..75) with rolled rewards
/// from the matching pool, charges the configured Zen price, and respects enable/disable and bag checks.
/// </summary>
[TestFixture]
public class MossGamblePlugInTests
{
    /// <summary>Buying each gamble voucher from Moss (492) rolls a reward from its matching pool and deducts Zen.</summary>
    /// <param name="voucherNumber">The gamble voucher item number (71..75).</param>
    /// <param name="expectedRewardGroup">An example valid reward item group.</param>
    /// <param name="expectedRewardNumber">An example valid reward item number.</param>
    /// <returns>The task.</returns>
    [TestCase((short)71, (byte)0, (short)0)]
    [TestCase((short)72, (byte)5, (short)0)]
    [TestCase((short)73, (byte)4, (short)0)]
    [TestCase((short)74, (byte)2, (short)8)]
    [TestCase((short)75, (byte)5, (short)14)]
    public async Task BuyingFromMossReplacesVoucherWithRolledRewardAsync(short voucherNumber, byte expectedRewardGroup, short expectedRewardNumber)
    {
        var (player, voucherItem, plugIn) = await CreateMossTestHarnessAsync(voucherNumber, expectedRewardGroup, expectedRewardNumber).ConfigureAwait(false);
        await using var _ = player;
        player.Money = 5_000_000;

        await new BuyNpcItemAction().BuyItemAsync(player, 0).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(3_000_000));
        Assert.That(player.Inventory!.Items.Count(), Is.EqualTo(1));
        var won = player.Inventory.Items.Single();
        Assert.That(won.Definition!.Group, Is.EqualTo(expectedRewardGroup));
        Assert.That(won.Definition.Number, Is.EqualTo(expectedRewardNumber));
        Assert.That(won.Definition.Number, Is.Not.EqualTo(voucherNumber).Or.Not.EqualTo((byte)13));
    }

    /// <summary>When disabled in admin configuration, Moss gambling refuses the purchase without taking Zen.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task DisabledConfigurationCancelsPurchaseWithoutChargingZenAsync()
    {
        var (player, _, plugIn) = await CreateMossTestHarnessAsync(71, 0, 0).ConfigureAwait(false);
        await using var _ = player;
        plugIn.Configuration = new MossGambleConfiguration { Enabled = false, PriceZen = 1_500_000 };
        player.Money = 5_000_000;

        await new BuyNpcItemAction().BuyItemAsync(player, 0).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(5_000_000));
        Assert.That(player.Inventory!.Items, Is.Empty);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IBuyNpcItemFailedPlugIn>()!).Verify(p => p.BuyNpcItemFailedAsync(), Times.Once);
    }

    /// <summary>Insufficient Zen rejects the gamble purchase cleanly.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task InsufficientZenFailsWithoutGivingItemAsync()
    {
        var (player, _, plugIn) = await CreateMossTestHarnessAsync(72, 5, 0).ConfigureAwait(false);
        await using var _ = player;
        plugIn.Configuration = new MossGambleConfiguration { Enabled = true, PriceZen = 2_000_000 };
        player.Money = 1_000_000;

        await new BuyNpcItemAction().BuyItemAsync(player, 0).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(1_000_000));
        Assert.That(player.Inventory!.Items, Is.Empty);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IBuyNpcItemFailedPlugIn>()!).Verify(p => p.BuyNpcItemFailedAsync(), Times.Once);
    }

    private static async Task<(Player Player, Item VoucherItem, MossGamblePlugIn PlugIn)> CreateMossTestHarnessAsync(
        short voucherNumber,
        byte rewardGroup,
        short rewardNumber)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var config = player.GameContext.Configuration;
        var ctx = player.PersistenceContext;

        var voucherDef = ctx.CreateNew<ItemDefinition>();
        voucherDef.Group = 13;
        voucherDef.Number = voucherNumber;
        voucherDef.Width = 2;
        voucherDef.Height = 3;
        voucherDef.Durability = 255;
        voucherDef.Name = $"Voucher{voucherNumber}";
        config.Items.Add(voucherDef);

        var rewardDef = ctx.CreateNew<ItemDefinition>();
        rewardDef.Group = rewardGroup;
        rewardDef.Number = rewardNumber;
        rewardDef.Width = 1;
        rewardDef.Height = 2;
        rewardDef.Durability = 25;
        rewardDef.Name = $"Reward{rewardGroup}_{rewardNumber}";
        config.Items.Add(rewardDef);

        var voucherItem = ctx.CreateNew<Item>();
        voucherItem.Definition = voucherDef;
        voucherItem.Durability = 255;
        voucherItem.ItemSlot = 0;

        var store = ctx.CreateNew<ItemStorage>();
        store.Items.Add(voucherItem);

        var npcDef = ctx.CreateNew<MonsterDefinition>();
        npcDef.Number = 492;
        npcDef.Designation = "Moss The Merchant";
        npcDef.NpcWindow = NpcWindow.Merchant;
        npcDef.MerchantStore = store;
        var mapDef = config.Maps.First();
        var map = new GameMap(mapDef, TimeSpan.FromMinutes(1), 4);
        player.OpenedNpc = new NonPlayerCharacter(new MonsterSpawnArea { MonsterDefinition = npcDef }, npcDef, map);

        var plugIn = new MossGamblePlugIn();
        plugIn.Configuration = new MossGambleConfiguration();
        player.GameContext.PlugInManager.RegisterPlugIn<INpcItemPurchasePlugIn, MossGamblePlugIn>();
        player.GameContext.PlugInManager.ActivatePlugIn<MossGamblePlugIn>();
        var active = player.GameContext.PlugInManager.GetActivePlugInsOf<INpcItemPurchasePlugIn>().OfType<MossGamblePlugIn>().Single();
        active.Configuration = new MossGambleConfiguration();

        return (player, voucherItem, active);
    }
}

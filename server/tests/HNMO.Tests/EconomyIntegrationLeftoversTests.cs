// <copyright file="EconomyIntegrationLeftoversTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.PlugIns.CombiningLeftovers;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Combining Leftovers with stacked materials: amounts taken across slots, the remainder left in the bag, locked
/// materials never used, and the reward that waits while the bag is full, delivered once after a login.
/// </summary>
[TestFixture]
public class EconomyIntegrationLeftoversTests
{
    private const string RecipeId = "test-mix";

    private const string ExchangeId = "test-exchange";

    private static readonly byte FirstBagSlot = InventoryConstants.EquippableSlotsCount;

    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated directory for the state file.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-economy-leftovers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes the directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Five materials are taken as two from one stack and three from another; the locked stack is skipped.</summary>
    [Test]
    public void ThePlanTakesAmountsAcrossSlotsAndSkipsLockedMaterials()
    {
        var engine = this.CreateEngine(outputWidth: 1);
        IReadOnlyList<BagItemDescriptor> bag =
        [
            new(12, 14, 16, 0, Locked: false, Quantity: 2),
            new(13, 14, 16, 0, Locked: true, Quantity: 9),
            new(14, 14, 16, 0, Locked: false, Quantity: 4),
        ];

        Assert.That(engine.PlanCombine(RecipeId, bag, 0, null, out var picked), Is.EqualTo(LeftoversCheckResult.Ok));

        Assert.That(picked.Count(s => s == 12), Is.EqualTo(2));
        Assert.That(picked.Count(s => s == 13), Is.Zero, "a locked stack is never used");
        Assert.That(picked.Count(s => s == 14), Is.EqualTo(3));
    }

    /// <summary>When only locked stacks would make up the amount, the plan is refused and picks nothing.</summary>
    [Test]
    public void LockedMaterialsDoNotMakeUpAMissingAmount()
    {
        var engine = this.CreateEngine(outputWidth: 1);
        IReadOnlyList<BagItemDescriptor> bag =
        [
            new(12, 14, 16, 0, Locked: false, Quantity: 4),
            new(13, 14, 16, 0, Locked: true, Quantity: 9),
        ];

        Assert.That(engine.PlanCombine(RecipeId, bag, 0, null, out var picked), Is.EqualTo(LeftoversCheckResult.MissingIngredients));
        Assert.That(picked, Is.Empty);
    }

    /// <summary>
    /// A real combine with stacks of 2 and 4: the stack of 2 is used up and removed, the other keeps 1, and the
    /// output lands in the bag.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CombiningStackedMaterialsLeavesTheRemainderAsync()
    {
        var (plugIn, player, life, output) = await this.CreatePlayerAsync(outputWidth: 1).ConfigureAwait(false);
        var first = await AddAsync(player, life, FirstBagSlot, 2).ConfigureAwait(false);
        var second = await AddAsync(player, life, (byte)(FirstBagSlot + 1), 4).ConfigureAwait(false);

        await plugIn.CombineAsync(player, RecipeId).ConfigureAwait(false);

        Assert.That(player.Inventory!.Items, Does.Not.Contain(first), "the used-up stack is removed");
        Assert.That(player.Inventory.GetItem((byte)(FirstBagSlot + 1)), Is.SameAs(second));
        Assert.That(second.Durability, Is.EqualTo(1));
        Assert.That(player.Inventory.Items.Count(i => i.Definition == output), Is.EqualTo(1));
    }

    /// <summary>
    /// With no room for the output the reward waits in the store; a login while the bag is still full keeps it
    /// waiting (not lost); the first login with room delivers it, and a further login doesn't deliver it again.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AWaitingRewardIsDeliveredOnceAfterLoginAsync()
    {
        var (plugIn, player, life, output) = await this.CreatePlayerAsync(outputWidth: 2).ConfigureAwait(false);
        await AddAsync(player, life, FirstBagSlot, 5).ConfigureAwait(false);
        var filler = new BasicModel.ItemDefinition { Group = 14, Number = 0, Width = 1, Height = 1, Durability = 1 };
        player.GameContext.Configuration.Items.Add(filler);
        var fillers = new List<Item>();
        for (var slot = FirstBagSlot + 1; slot < FirstBagSlot + 64; slot++)
        {
            fillers.Add(await AddAsync(player, filler, (byte)slot, 1).ConfigureAwait(false));
        }

        await plugIn.CombineAsync(player, RecipeId).ConfigureAwait(false);

        var characterId = player.SelectedCharacter!.GetId().ToString();
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == output), Is.Zero);
        Assert.That(PeekOwed(plugIn, characterId), Is.EqualTo(1), "the reward waits");

        await plugIn.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        Assert.That(PeekOwed(plugIn, characterId), Is.EqualTo(1), "a login with a full bag keeps it waiting");

        foreach (var item in fillers.Take(10))
        {
            await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
        }

        await plugIn.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        await plugIn.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);

        Assert.That(player.Inventory.Items.Count(i => i.Definition == output), Is.EqualTo(1));
        Assert.That(PeekOwed(plugIn, characterId), Is.Zero);
    }

    /// <summary>
    /// A reward that has to wait (full bag) carries a marker saved together with its materials, and is delivered once
    /// that marker is in the database: at the next login with room, once.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AWaitingRewardIsDeliveredOnceItsMaterialsWereSavedAsync()
    {
        var (plugIn, player, life, output) = await this.CreatePlayerAsync(outputWidth: 2).ConfigureAwait(false);
        await AddAsync(player, life, FirstBagSlot, 5).ConfigureAwait(false);
        var fillers = await FillBagAsync(player).ConfigureAwait(false);
        var characterId = player.SelectedCharacter!.GetId().ToString();
        string? markerAtSave = null;
        var markerSavedWithMaterials = false;
        CashShopActionsTest.SaveFailingContext.Install(player, () =>
        {
            if (markerAtSave is null && TakeAndReturnOwed(plugIn, characterId) is [{ PaidMarker: { } marker }])
            {
                markerAtSave = marker;
                markerSavedWithMaterials = player.Inventory!.Items.All(i => i.Definition != life);
            }

            return false;
        });

        await plugIn.CombineAsync(player, RecipeId).ConfigureAwait(false);

        Assert.That(markerAtSave, Is.Not.Null, "the waiting reward is written with its marker before the save");
        Assert.That(markerSavedWithMaterials, Is.True, "the marker goes into the save without the materials");
        Assert.That(TakeAndReturnOwed(plugIn, characterId), Has.Count.EqualTo(1), "the reward waits for room");

        await FreeAsync(player, fillers).ConfigureAwait(false);
        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.EnteredWorld).ConfigureAwait(false);
        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.EnteredWorld).ConfigureAwait(false);

        Assert.That(player.Inventory!.Items.Count(i => i.Definition == output), Is.EqualTo(1), "delivered once");
        Assert.That(TakeAndReturnOwed(plugIn, characterId), Is.Empty);
        Assert.That(await player.PersistenceContext.GetCashShopCoinGrantByReferenceAsync(markerAtSave!).ConfigureAwait(false), Is.Null, "the marker is removed");
    }

    /// <summary>
    /// A waiting reward whose materials' save never reached the database (a stop before it: no marker) is dropped at the
    /// next login, since the materials are still in the saved bag.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AWaitingRewardWhoseMaterialsWereNeverSavedIsDroppedAtLoginAsync()
    {
        var (plugIn, player, _, output) = await this.CreatePlayerAsync(outputWidth: 1).ConfigureAwait(false);
        var characterId = player.SelectedCharacter!.GetId().ToString();
        plugIn.GetEngine().Store.AddOwed(characterId, new GiftCodeOwedReward
        {
            Reference = "leftover-cb:test-mix:stopped",
            Items = [new GiftCodeItem { Group = 13, Number = 90 }],
            PaidMarker = "hnmo-leftovers-paid:leftover-cb:test-mix:stopped",
        });

        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.EnteredWorld).ConfigureAwait(false);

        Assert.That(player.Inventory!.Items.Count(i => i.Definition == output), Is.Zero);
        Assert.That(TakeAndReturnOwed(plugIn, characterId), Is.Empty);
    }

    /// <summary>
    /// The coins of a surplus exchange are a grant in the player's own save, with the materials: a failed save applies
    /// none, and the next good save gives them once.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExchangeCoinsGoIntoTheMaterialsSaveAsync()
    {
        var (plugIn, player, life, _) = await this.CreatePlayerAsync(outputWidth: 1).ConfigureAwait(false);
        await AddAsync(player, life, FirstBagSlot, 5).ConfigureAwait(false);
        var fails = true;
        CashShopActionsTest.SaveFailingContext.Install(player, () => fails);

        await plugIn.ExchangeAsync(player, ExchangeId).ConfigureAwait(false);
        Assert.That(player.Account!.WCoinC, Is.Zero, "no coins while the materials aren't saved");

        fails = false;
        Assert.That(await player.SaveProgressAsync().ConfigureAwait(false), Is.True);
        await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
        await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);

        Assert.That(player.Account.WCoinC, Is.EqualTo(7), "given once");
        Assert.That(player.Inventory!.Items.Where(i => i.Definition == life), Is.Empty);
    }

    private static List<GiftCodeOwedReward> TakeAndReturnOwed(CombiningLeftoversPlugIn plugIn, string characterId)
    {
        var store = plugIn.GetEngine().Store;
        var owed = store.TakeOwed(characterId);
        store.ReturnOwed(characterId, owed);
        return owed;
    }

    private static async Task<List<Item>> FillBagAsync(Player player)
    {
        var filler = new BasicModel.ItemDefinition { Group = 14, Number = 0, Width = 1, Height = 1, Durability = 1 };
        player.GameContext.Configuration.Items.Add(filler);
        var items = new List<Item>();
        for (var slot = FirstBagSlot + 1; slot < FirstBagSlot + 64; slot++)
        {
            items.Add(await AddAsync(player, filler, (byte)slot, 1).ConfigureAwait(false));
        }

        return items;
    }

    private static async Task FreeAsync(Player player, IEnumerable<Item> items)
    {
        foreach (var item in items)
        {
            await player.Inventory!.RemoveItemAsync(item).ConfigureAwait(false);
        }
    }

    private static int PeekOwed(CombiningLeftoversPlugIn plugIn, string characterId)
    {
        var store = plugIn.GetEngine().Store;
        var owed = store.TakeOwed(characterId);
        store.ReturnOwed(characterId, owed);
        return owed.Count;
    }

    private static async Task<Item> AddAsync(Player player, BasicModel.ItemDefinition definition, byte slot, int count)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Durability = (byte)count;
        Assert.That(await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false), Is.True);
        return item;
    }

    private CombiningLeftoversConfiguration CreateConfiguration() => new()
    {
        FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
        Recipes =
        [
            new LeftoverRecipe
            {
                Id = RecipeId,
                NameEn = "Test mix",
                NameVi = "Ghép thử",
                SuccessRatePercent = 100,
                ZenCost = 0,
                OutputGroup = 13,
                OutputNumber = 90,
                Inputs = [new LeftoverIngredient { Group = 14, Number = 16, Count = 5 }],
            },
        ],
        ExchangeOffers =
        [
            new LeftoverExchangeOffer
            {
                Id = ExchangeId,
                NameEn = "Test exchange",
                NameVi = "Đổi thử",
                InputGroup = 14,
                InputNumber = 16,
                InputCount = 5,
                RewardCoins = 7,
                RewardCoinType = CashShopCoinType.WCoinC,
            },
        ],
    };

    private CombiningLeftoversEngine CreateEngine(int outputWidth)
    {
        _ = outputWidth;
        var configuration = this.CreateConfiguration();
        return new CombiningLeftoversEngine(configuration, CombiningLeftoversStore.Open(configuration.FileName), enabled: true);
    }

    private async Task<(CombiningLeftoversPlugIn PlugIn, Player Player, BasicModel.ItemDefinition Life, BasicModel.ItemDefinition Output)> CreatePlayerAsync(int outputWidth)
    {
        var context = GameContextTestHelper.CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account!.LoginName = "leftovers-test";
        var life = new BasicModel.ItemDefinition { Group = 14, Number = 16, Width = 1, Height = 1, Durability = 255 };
        var output = new BasicModel.ItemDefinition { Group = 13, Number = 90, Width = (byte)outputWidth, Height = (byte)outputWidth, Durability = 1 };
        context.Configuration.Items.Add(life);
        context.Configuration.Items.Add(output);
        return (new CombiningLeftoversPlugIn { Configuration = this.CreateConfiguration() }, player, life, output);
    }
}

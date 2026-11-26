// <copyright file="EconomyIntegrationLeftoversTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.CombiningLeftovers;
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
    /// The materials are saved as taken before a waiting reward is written to the store: a stop in between can't give
    /// the materials back while the reward is kept.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheMaterialsAreSavedBeforeAWaitingRewardIsWrittenAsync()
    {
        var (plugIn, player, life, _) = await this.CreatePlayerAsync(outputWidth: 2).ConfigureAwait(false);
        await AddAsync(player, life, FirstBagSlot, 5).ConfigureAwait(false);
        var filler = new BasicModel.ItemDefinition { Group = 14, Number = 0, Width = 1, Height = 1, Durability = 1 };
        player.GameContext.Configuration.Items.Add(filler);
        for (var slot = FirstBagSlot + 1; slot < FirstBagSlot + 64; slot++)
        {
            await AddAsync(player, filler, (byte)slot, 1).ConfigureAwait(false);
        }

        var characterId = player.SelectedCharacter!.GetId().ToString();
        int? owedAtFirstSave = null;
        CashShopActionsTest.SaveFailingContext.Install(player, () =>
        {
            owedAtFirstSave ??= PeekOwed(plugIn, characterId);
            return false;
        });

        await plugIn.CombineAsync(player, RecipeId).ConfigureAwait(false);

        Assert.That(owedAtFirstSave, Is.EqualTo(0), "the reward was written before the materials were saved");
        Assert.That(PeekOwed(plugIn, characterId), Is.EqualTo(1));
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
        ExchangeOffers = [],
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

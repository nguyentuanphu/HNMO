// <copyright file="PentagramTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using Moq;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.PlugIns;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The Season 9 pentagram and errtels: option values from HNMO's errtel data, the elemental damage of a hit (against a
/// monster and in PvP, with the element advantage), wearing and taking off, putting errtels in and out, a full bag,
/// a failed save, and no effect outside Season 9 realms.
/// </summary>
[TestFixture]
public class PentagramTests
{
    private const byte FirstBagSlot = 12;

    /// <summary>Option values follow the data at levels 0, 5 and 10, linear in between.</summary>
    [Test]
    public void OptionValuesFollowTheErrtelData()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Pentagram.OptionValue(0, 0), Is.EqualTo(30));
            Assert.That(Pentagram.OptionValue(0, 5), Is.EqualTo(65));
            Assert.That(Pentagram.OptionValue(0, 10), Is.EqualTo(168));
            Assert.That(Pentagram.OptionValue(0, 3), Is.EqualTo(51));
            Assert.That(Pentagram.OptionValue(999, 3), Is.Zero);
        });
    }

    /// <summary>The errtel slots of a pentagram are written and read back unchanged.</summary>
    [Test]
    public void SlotsAreWrittenAndReadBack()
    {
        var pentagram = new BasicModel.Item();
        var slots = new ErrtelInfo?[] { new(221, PentagramElement.Fire, [(0, 3), (10, 1)]), null, new(231, PentagramElement.Fire, [(50, 0)]), null, null };
        Pentagram.WriteSlots(pentagram, slots);

        var read = Pentagram.ReadSlots(pentagram);
        Assert.Multiple(() =>
        {
            Assert.That(read[0]!.Number, Is.EqualTo(221));
            Assert.That(read[0]!.Options, Is.EqualTo(new[] { (0, (byte)3), (10, (byte)1) }));
            Assert.That(read[1], Is.Null);
            Assert.That(read[2]!.Element, Is.EqualTo(PentagramElement.Fire));
        });
    }

    /// <summary>Against a monster (no element), the errtels' elemental damage counts without advantage.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AHitOnAMonsterGetsTheElementalDamageAsync()
    {
        var (attacker, _, _) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        Wear(attacker, PentagramElement.Fire, new ErrtelInfo(221, PentagramElement.Fire, [(0, 0)]));

        Assert.That(Pentagram.ElementalDamage(attacker, new Mock<IAttackable>().Object, isPvp: false), Is.EqualTo(30));
    }

    /// <summary>In PvP, the defender's elemental defense comes off and fire beats darkness by the advantage (15 %).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APvpHitHasTheDefenseAndTheAdvantageAsync()
    {
        var (attacker, defender, _) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        Wear(attacker, PentagramElement.Fire, new ErrtelInfo(221, PentagramElement.Fire, [(0, 0)]));
        Wear(defender, PentagramElement.Darkness, new ErrtelInfo(231, PentagramElement.Darkness, [(50, 0)]));

        // (30 - 10) * 1.15
        Assert.That(Pentagram.ElementalDamage(attacker, defender, isPvp: true), Is.EqualTo(23));

        // The other way round darkness is weaker against fire: (30 - 0) * 0.925 with no defense of the fire side.
        Wear(defender, PentagramElement.Darkness, new ErrtelInfo(221, PentagramElement.Darkness, [(0, 0)]));
        Assert.That(Pentagram.ElementalDamage(defender, attacker, isPvp: true), Is.EqualTo(28));
    }

    /// <summary>Outside Season 9 realms a pentagram adds nothing, and can't be worn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingOutsideSeasonNineAsync()
    {
        var (attacker, _, definitions) = await CreateAsync(seasonNine: false).ConfigureAwait(false);
        Wear(attacker, PentagramElement.Fire, new ErrtelInfo(221, PentagramElement.Fire, [(0, 10)]));
        Assert.That(Pentagram.ElementalDamage(attacker, new Mock<IAttackable>().Object, isPvp: false), Is.Zero);

        var (other, _, _) = await CreateAsync(seasonNine: false).ConfigureAwait(false);
        await AddAsync(other, definitions.Pentagram, FirstBagSlot, PentagramElement.Fire).ConfigureAwait(false);
        Assert.That(await PentagramActions.EquipAsync(other, FirstBagSlot).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Disabled));
    }

    /// <summary>
    /// A pentagram is worn and taken off; an errtel of its element goes into a slot and back into the bag; one of
    /// another element is refused.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WearAttachRemoveAndTakeOffAsync()
    {
        var (player, _, definitions) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        await AddAsync(player, definitions.Pentagram, FirstBagSlot, PentagramElement.Water).ConfigureAwait(false);
        var errtel = await AddAsync(player, definitions.Anger, FirstBagSlot + 3, PentagramElement.Water).ConfigureAwait(false);
        Pentagram.WriteErrtel(errtel, new ErrtelInfo(221, PentagramElement.Water, [(0, 2)]));
        var wrong = await AddAsync(player, definitions.Anger, FirstBagSlot + 5, PentagramElement.Fire).ConfigureAwait(false);

        Assert.That(await PentagramActions.EquipAsync(player, FirstBagSlot).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Ok));
        Assert.That(Pentagram.Worn(player), Is.Not.Null);
        Assert.That(player.Inventory!.GetItem(FirstBagSlot), Is.Null);

        Assert.That(await PentagramActions.AttachErrtelAsync(player, (byte)(FirstBagSlot + 5), 0).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.ElementMismatch));
        Assert.That(await PentagramActions.AttachErrtelAsync(player, (byte)(FirstBagSlot + 3), 0).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Ok));
        Assert.That(player.Inventory.GetItem((byte)(FirstBagSlot + 3)), Is.Null);
        Assert.That(Pentagram.ReadSlots(Pentagram.Worn(player)!)[0]!.Options, Is.EqualTo(new[] { (0, (byte)2) }));
        Assert.That(await PentagramActions.AttachErrtelAsync(player, (byte)(FirstBagSlot + 5), 0).ConfigureAwait(false), Is.Not.EqualTo(PentagramActionResult.Ok));

        Assert.That(await PentagramActions.RemoveErrtelAsync(player, 0).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Ok));
        var back = player.Inventory.Items.Single(i => i.Definition == definitions.Anger && i != wrong);
        Assert.That(Pentagram.ReadErrtel(back).Options, Is.EqualTo(new[] { (0, (byte)2) }));
        Assert.That(Pentagram.ReadSlots(Pentagram.Worn(player)!)[0], Is.Null);

        Assert.That(await PentagramActions.UnequipAsync(player).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Ok));
        Assert.That(Pentagram.Worn(player), Is.Null);
        Assert.That(player.Inventory.Items.Count(i => i.Definition == definitions.Pentagram), Is.EqualTo(1));
    }

    /// <summary>Taking an errtel out with a full bag changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFullBagKeepsTheErrtelInItsSlotAsync()
    {
        var (player, _, definitions) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        Wear(player, PentagramElement.Fire, new ErrtelInfo(221, PentagramElement.Fire, [(0, 1)]));
        var filler = new BasicModel.ItemDefinition { Group = 14, Number = 0, Width = 1, Height = 1, Durability = 1 };
        player.GameContext.Configuration.Items.Add(filler);
        for (var slot = FirstBagSlot; slot < FirstBagSlot + 64; slot++)
        {
            await AddAsync(player, filler, (byte)slot, PentagramElement.Undefined).ConfigureAwait(false);
        }

        Assert.That(await PentagramActions.RemoveErrtelAsync(player, 0).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.BagFull));
        Assert.That(Pentagram.ReadSlots(Pentagram.Worn(player)!)[0], Is.Not.Null);
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == definitions.Anger), Is.Zero);
    }

    /// <summary>When the save of an attach fails, the errtel is back in its bag slot and the pentagram's slot is empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFailedSaveUndoesTheAttachAsync()
    {
        var (player, _, definitions) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        Wear(player, PentagramElement.Earth);
        var errtel = await AddAsync(player, definitions.Anger, FirstBagSlot, PentagramElement.Earth).ConfigureAwait(false);
        Pentagram.WriteErrtel(errtel, new ErrtelInfo(221, PentagramElement.Earth, [(0, 4)]));
        CashShopActionsTest.SaveFailingContext.Install(player, () => true);

        Assert.That(await PentagramActions.AttachErrtelAsync(player, FirstBagSlot, 0).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.SaveFailed));

        var back = player.Inventory!.GetItem(FirstBagSlot);
        Assert.That(back?.Definition, Is.SameAs(definitions.Anger));
        Assert.That(Pentagram.ReadErrtel(back!).Options, Is.EqualTo(new[] { (0, (byte)4) }));
        Assert.That(Pentagram.ReadSlots(Pentagram.Worn(player)!)[0], Is.Null);
    }

    /// <summary>Copying an item (as moving it between storages does) keeps its element and errtels.</summary>
    [Test]
    public void ACopyKeepsTheElementAndTheErrtels()
    {
        var pentagram = new BasicModel.Item { PentagramElement = 4 };
        Pentagram.WriteSlots(pentagram, [new ErrtelInfo(221, PentagramElement.Wind, [(0, 7)])]);
        var copy = new BasicModel.Item();
        copy.AssignValues(pentagram);

        Assert.That(copy.PentagramElement, Is.EqualTo(4));
        Assert.That(copy.PentagramErrtels, Is.EqualTo(pentagram.PentagramErrtels));
    }

    private static void Wear(Player player, PentagramElement element, params ErrtelInfo[] errtels)
    {
        var inventory = player.SelectedCharacter!.Inventory!;
        foreach (var old in inventory.Items.Where(i => i.ItemSlot == InventoryConstants.PentagramSlot).ToList())
        {
            inventory.Items.Remove(old);
        }

        var definition = player.GameContext.Configuration.Items.First(d => d.Group == 12 && d.Number == 210);
        var pentagram = new BasicModel.Item { Definition = definition, ItemSlot = InventoryConstants.PentagramSlot, Durability = 1, PentagramElement = (byte)element };
        Pentagram.WriteSlots(pentagram, errtels);
        inventory.Items.Add(pentagram);
    }

    private static async Task<Item> AddAsync(Player player, ItemDefinition definition, int slot, PentagramElement element)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Durability = 1;
        item.PentagramElement = (byte)element;
        Assert.That(await player.Inventory!.AddItemAsync((byte)slot, item).ConfigureAwait(false), Is.True);
        return item;
    }

    private static async Task<(Player Attacker, Player Defender, (ItemDefinition Pentagram, ItemDefinition Anger) Definitions)> CreateAsync(bool seasonNine)
    {
        var configuration = new PlugInConfiguration
        {
            TypeId = typeof(SeasonNineSystemsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new SeasonNineSystemsConfiguration { Enabled = true, PentagramEnabled = true }),
        };
        var gameContext = GameContextTestHelper.CreateGameContext([configuration]);
        SeasonNineSystemsPlugIn.Get(gameContext)!.IsSeasonNineRealm = () => seasonNine;
        var pentagram = new BasicModel.ItemDefinition { Group = 12, Number = 210, Width = 3, Height = 3, Durability = 1 };
        var anger = new BasicModel.ItemDefinition { Group = 12, Number = 221, Width = 2, Height = 2, Durability = 1 };
        var blessing = new BasicModel.ItemDefinition { Group = 12, Number = 231, Width = 2, Height = 2, Durability = 1 };
        gameContext.Configuration.Items.Add(pentagram);
        gameContext.Configuration.Items.Add(anger);
        gameContext.Configuration.Items.Add(blessing);
        var attacker = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var defender = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        return (attacker, defender, (pentagram, anger));
    }
}

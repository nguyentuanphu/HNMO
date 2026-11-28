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
[NonParallelizable]
public class PentagramTests
{
    private const byte FirstBagSlot = 12;

    private Func<int, int> _random = null!;

    /// <summary>Keeps the random source.</summary>
    [SetUp]
    public void KeepRandom() => this._random = PentagramActions.NextRandom;

    /// <summary>Puts the random source back.</summary>
    [TearDown]
    public void RestoreRandom() => PentagramActions.NextRandom = this._random;

    /// <summary>Option values follow the exact 11-level errtel curve in the catalog.</summary>
    [Test]
    public void OptionValuesFollowTheErrtelData()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Pentagram.OptionValue(0, 0), Is.EqualTo(30));
            Assert.That(Pentagram.OptionValue(0, 3), Is.EqualTo(45));
            Assert.That(Pentagram.OptionValue(0, 5), Is.EqualTo(65));
            Assert.That(Pentagram.OptionValue(0, 10), Is.EqualTo(168));
            Assert.That(Pentagram.OptionValue(999, 3), Is.Zero);
            Assert.That(PentagramData.LevelUpZen(null, 2), Is.EqualTo(150_000));
            Assert.That(PentagramData.LevelUpZen(null, 9), Is.EqualTo(500_000));
            Assert.That(PentagramData.LevelUpRatePct(null, 0, 10), Is.EqualTo(50));
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

    /// <summary>Taking an errtel out costs its Zen; with a failed extraction roll it breaks, the Zen is gone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TakingAnErrtelOutCanBreakItAsync()
    {
        var (player, _, definitions) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        var money = player.Money;
        Wear(player, PentagramElement.Fire, new ErrtelInfo(221, PentagramElement.Fire, [(0, 9)]), new ErrtelInfo(221, PentagramElement.Fire, [(0, 9)]));

        PentagramActions.NextRandom = max => max - 1; // the highest roll: fails every rate below 100 %
        Assert.That(await PentagramActions.RemoveErrtelAsync(player, 0).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Broken));
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == definitions.Anger), Is.Zero);
        Assert.That(Pentagram.ReadSlots(Pentagram.Worn(player)!)[0], Is.Null);
        Assert.That(player.Money, Is.EqualTo(money - 100_000));

        PentagramActions.NextRandom = _ => 0;
        Assert.That(await PentagramActions.RemoveErrtelAsync(player, 1).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Ok));
        Assert.That(player.Inventory.Items.Count(i => i.Definition == definitions.Anger), Is.EqualTo(1));
    }

    /// <summary>With a mix NPC set, the mixes need that NPC's window open; without enough Zen nothing happens.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixesNeedTheirPlaceAndTheirZenAsync()
    {
        var (player, _, definitions) = await CreateAsync(seasonNine: true, c => c.PentagramMixNpcNumber = 543).ConfigureAwait(false);
        var errtel = await AddAsync(player, definitions.Anger, FirstBagSlot, PentagramElement.Fire).ConfigureAwait(false);
        Pentagram.WriteErrtel(errtel, new ErrtelInfo(221, PentagramElement.Fire, [(0, 0)]));
        Assert.That(await PentagramActions.LevelUpErrtelAsync(player, FirstBagSlot).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.WrongPlace));

        var (poor, _, poorDefinitions) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        poor.TryRemoveMoney(poor.Money);
        var poorErrtel = await AddAsync(poor, poorDefinitions.Anger, FirstBagSlot, PentagramElement.Fire).ConfigureAwait(false);
        Pentagram.WriteErrtel(poorErrtel, new ErrtelInfo(221, PentagramElement.Fire, [(0, 0)]));
        Assert.That(await PentagramActions.LevelUpErrtelAsync(poor, FirstBagSlot).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.NotEnoughZen));
        Assert.That(Pentagram.ReadErrtel(poorErrtel).Options, Is.EqualTo(new[] { (0, (byte)0) }));
    }

    /// <summary>A level-up raises the errtel by one and costs the Zen of the new level; a failed one keeps the level.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ALevelUpRaisesOrKeepsTheLevelAsync()
    {
        var (player, _, definitions) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        var errtel = await AddAsync(player, definitions.Anger, FirstBagSlot, PentagramElement.Fire).ConfigureAwait(false);
        Pentagram.WriteErrtel(errtel, new ErrtelInfo(221, PentagramElement.Fire, [(0, 2)]));
        var money = player.Money;

        PentagramActions.NextRandom = _ => 0;
        Assert.That(await PentagramActions.LevelUpErrtelAsync(player, FirstBagSlot).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Ok));
        Assert.That(Pentagram.ReadErrtel(errtel).Options, Is.EqualTo(new[] { (0, (byte)3) }));
        Assert.That(player.Money, Is.EqualTo(money - 150_000));

        PentagramActions.NextRandom = max => max - 1;
        Assert.That(await PentagramActions.LevelUpErrtelAsync(player, FirstBagSlot).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Failed));
        Assert.That(Pentagram.ReadErrtel(errtel).Options, Is.EqualTo(new[] { (0, (byte)3) }));
    }

    /// <summary>A rank-up uses a second errtel of the same kind and rank and adds an option of the next rank.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARankUpAddsAnOptionOfTheNextRankAsync()
    {
        var (player, _, definitions) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        var first = await AddAsync(player, definitions.Anger, FirstBagSlot, PentagramElement.Wind).ConfigureAwait(false);
        Pentagram.WriteErrtel(first, new ErrtelInfo(221, PentagramElement.Wind, [(0, 4)]));
        var second = await AddAsync(player, definitions.Anger, FirstBagSlot + 3, PentagramElement.Wind).ConfigureAwait(false);
        Pentagram.WriteErrtel(second, new ErrtelInfo(221, PentagramElement.Wind, [(0, 0)]));

        PentagramActions.NextRandom = _ => 0;
        Assert.That(await PentagramActions.RankUpErrtelAsync(player, FirstBagSlot, (byte)(FirstBagSlot + 3)).ConfigureAwait(false), Is.EqualTo(PentagramActionResult.Ok));

        var options = Pentagram.ReadErrtel(first).Options;
        Assert.That(options, Has.Count.EqualTo(2));
        Assert.That(PentagramData.Option(null, options[1].Index)!.Rank, Is.EqualTo(2));
        Assert.That(player.Inventory!.GetItem((byte)(FirstBagSlot + 3)), Is.Null, "the second errtel is used up");
    }

    /// <summary>Drops are off by default; with a chance set, a pentagram with an element drops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsAreOffByDefaultAndFollowTheChanceAsync()
    {
        var (player, _, _) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        var configuration = SeasonNineSystemsPlugIn.Get(player.GameContext)!.Configuration!;
        PentagramActions.NextRandom = _ => 0;
        Assert.That(PentagramDrops.Roll(player.GameContext, configuration), Is.Null);

        configuration.PentagramDropChance = 10;
        var drop = PentagramDrops.Roll(player.GameContext, configuration);
        Assert.That(Pentagram.IsPentagram(drop?.Definition), Is.True);
        Assert.That(drop!.PentagramElement, Is.InRange(1, 5));

        configuration.PentagramDropChance = 0;
        configuration.ErrtelDropChance = 10;
        var errtel = PentagramDrops.Roll(player.GameContext, configuration);
        Assert.That(Pentagram.IsErrtel(errtel?.Definition), Is.True);
        Assert.That(Pentagram.ReadErrtel(errtel!).Options, Has.Count.EqualTo(1));
    }

    /// <summary>The admin's tables replace the built-in ones: an option's values and a monster's element.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheAdminTablesReplaceTheDefaultsAsync()
    {
        var (attacker, _, _) = await CreateAsync(seasonNine: true, c =>
        {
            c.ErrtelOptions = [new ErrtelOptionSetting { Index = 0, ErrtelNumber = 221, Rank = 1, Rate = 1, ValueLv0 = 100, ValueLv5 = 200, ValueLv10 = 300, Name = "Elemental Dmg +%d" }];
            c.MonsterElements = [new PentagramMonsterElement { MonsterNumber = 7, Element = PentagramElement.Darkness }];
        }).ConfigureAwait(false);
        var configuration = SeasonNineSystemsPlugIn.Get(attacker.GameContext)!.Configuration!;
        Assert.That(Pentagram.OptionValue(configuration, 0, 5), Is.EqualTo(200));
        Assert.That(PentagramData.MonsterElement(configuration, 7), Is.EqualTo(PentagramElement.Darkness));
        Assert.That(PentagramData.MonsterElement(configuration, 8), Is.EqualTo(PentagramElement.Undefined));

        Wear(attacker, PentagramElement.Fire, new ErrtelInfo(221, PentagramElement.Fire, [(0, 0)]));
        Assert.That(Pentagram.ElementalDamage(attacker, new Mock<IAttackable>().Object, isPvp: false), Is.EqualTo(100));
    }

    /// <summary>The state the web client gets names the worn pentagram, its slots and the bag's errtels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheStateNamesTheWornPentagramAndTheBagAsync()
    {
        var (player, _, definitions) = await CreateAsync(seasonNine: true).ConfigureAwait(false);
        Wear(player, PentagramElement.Earth, new ErrtelInfo(221, PentagramElement.Earth, [(0, 5)]));
        var errtel = await AddAsync(player, definitions.Anger, FirstBagSlot, PentagramElement.Earth).ConfigureAwait(false);
        Pentagram.WriteErrtel(errtel, new ErrtelInfo(221, PentagramElement.Earth, [(0, 1)]));

        using var json = JsonDocument.Parse(PentagramStateWriter.Write(player, PentagramActionResult.Ok));
        var root = json.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("worn").GetProperty("element").GetInt32(), Is.EqualTo(3));
            Assert.That(root.GetProperty("worn").GetProperty("errtels")[0].GetProperty("options")[0].GetProperty("value").GetInt32(), Is.EqualTo(65));
            Assert.That(root.GetProperty("items")[0].GetProperty("slot").GetInt32(), Is.EqualTo(FirstBagSlot));
            Assert.That(root.GetProperty("result").GetString(), Is.EqualTo("Ok"));
        });

        var (s6, _, _) = await CreateAsync(seasonNine: false).ConfigureAwait(false);
        Assert.That(PentagramStateWriter.Write(s6), Is.EqualTo(PentagramStateWriter.DisabledState));
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

    private static async Task<(Player Attacker, Player Defender, (ItemDefinition Pentagram, ItemDefinition Anger) Definitions)> CreateAsync(bool seasonNine, Action<SeasonNineSystemsConfiguration>? tweak = null)
    {
        var settings = new SeasonNineSystemsConfiguration { Enabled = true, PentagramEnabled = true };
        tweak?.Invoke(settings);
        var configuration = new PlugInConfiguration
        {
            TypeId = typeof(SeasonNineSystemsPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(settings),
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
        foreach (var player in new[] { attacker, defender })
        {
            player.TryAddMoney(10_000_000);
            player.CurrentMap!.Terrain.SafezoneMap[player.Position.X, player.Position.Y] = true;
        }

        return (attacker, defender, (pentagram, anger));
    }
}

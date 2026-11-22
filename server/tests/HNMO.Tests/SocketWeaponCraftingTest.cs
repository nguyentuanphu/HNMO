// <copyright file="SocketWeaponCraftingTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;

/// <summary>
/// Tests the Chaos Machine socket weapon creation mix (mix ID 57, 40 %, 1,000,000 zen)
/// for success across all 13 notes, failure, wrong notes, and socket items without mounted seed spheres.
/// </summary>
[TestFixture]
public class SocketWeaponCraftingTest
{
    private const byte SocketWeaponMixId = 57;
    private const int MixCost = 1_000_000;

    private GameConfiguration _gameConfiguration = null!;

    private IGameContext _gameContext = null!;

    /// <summary>
    /// Sets up a fresh Season 6 configuration and game context.
    /// </summary>
    [OneTimeSetUp]
    public async ValueTask SetupAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(0, false).ConfigureAwait(false);
        this._gameConfiguration = (await contextProvider.CreateNewConfigurationContext().GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        var mapInitializer = new MapInitializer(this._gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(this._gameConfiguration, contextProvider, mapInitializer, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;
        this._gameContext = gameContext;
    }

    /// <summary>
    /// Verifies each of the 13 Creation Notes (14-191..203) crafts its matching 5-socket weapon,
    /// consumes all 5 ingredients, and deducts 1,000,000 zen.
    /// </summary>
    /// <param name="noteNumber">The note item number (group 14).</param>
    /// <param name="expectedGroup">The expected weapon group.</param>
    /// <param name="expectedNumber">The expected weapon number.</param>
    [TestCase(191, (byte)0, (short)29)] // Sonic Combination Note -> Sonic Blade
    [TestCase(192, (byte)0, (short)36)] // Cyclone Combination Note -> Cyclone Sword
    [TestCase(193, (byte)0, (short)37)] // Blast Combination Note -> Blast Break
    [TestCase(194, (byte)3, (short)12)] // Magma Combination Note -> Magmus Peer
    [TestCase(195, (byte)2, (short)20)] // Horn Combination Note -> Horn of Steal
    [TestCase(196, (byte)4, (short)25)] // Angelic Combination Note -> Angelic Bow
    [TestCase(197, (byte)4, (short)26)] // Devil Cross Combination Note -> Devil Crossbow
    [TestCase(198, (byte)5, (short)35)] // Miracle Combination Note -> Miracle Staff
    [TestCase(199, (byte)5, (short)37)] // Spite Combination Note -> Spite Staff
    [TestCase(200, (byte)0, (short)30)] // Asura Combination Note -> Asura
    [TestCase(201, (byte)2, (short)19)] // Thunder Combination Note -> Thunderbolt
    [TestCase(202, (byte)5, (short)32)] // Soul Combination Note -> Summon Spirit Stick
    [TestCase(203, (byte)0, (short)38)] // Greater Combination Note -> Frick Large Glove
    public async ValueTask CraftCreatesMatchingFiveSocketWeaponOnSuccessAsync(short noteNumber, byte expectedGroup, short expectedNumber)
    {
        var player = await this.CreatePlayerAtChaosGoblinAsync().ConfigureAwait(false);
        player.Money = 2_500_000;

        await this.AddStandardIngredientsAsync(player, noteNumber, withMountedSeedSphere: true).ConfigureAwait(false);

        var action = new ItemCraftAction();
        var crafting = action.FindAppropriateCraftingByItems(player);
        Assert.That(crafting, Is.Not.Null);
        Assert.That(crafting!.Number, Is.EqualTo(SocketWeaponMixId));
        var settings = crafting.SimpleCraftingSettings!;
        Assert.That(settings.SuccessPercent, Is.EqualTo(40));
        Assert.That(settings.Money, Is.EqualTo(MixCost));

        var previousMax = settings.MaximumSuccessPercent;
        try
        {
            settings.SuccessPercent = 100;
            settings.MaximumSuccessPercent = 100;

            await action.MixItemsAsync(player, SocketWeaponMixId, 0).ConfigureAwait(false);
        }
        finally
        {
            settings.SuccessPercent = 40;
            settings.MaximumSuccessPercent = previousMax;
        }

        var items = player.TemporaryStorage!.Items.ToList();
        Assert.That(items, Has.Count.EqualTo(1));
        var result = items[0];
        Assert.That(result.Definition!.Group, Is.EqualTo(expectedGroup));
        Assert.That(result.Definition.Number, Is.EqualTo(expectedNumber));
        Assert.That(result.Definition.MaximumSockets, Is.EqualTo(5));
        Assert.That(result.SocketCount, Is.EqualTo(5));
        Assert.That(player.Money, Is.EqualTo(2_500_000 - MixCost));
    }

    /// <summary>
    /// Verifies that a failed combination consumes ingredients and zen and produces no weapon.
    /// </summary>
    [Test]
    public async ValueTask FailureConsumesIngredientsAndZenWithoutCreatingWeaponAsync()
    {
        var player = await this.CreatePlayerAtChaosGoblinAsync().ConfigureAwait(false);
        player.Money = 2_500_000;

        await this.AddStandardIngredientsAsync(player, 191, withMountedSeedSphere: true).ConfigureAwait(false);

        var action = new ItemCraftAction();
        var settings = action.FindAppropriateCraftingByItems(player)!.SimpleCraftingSettings!;
        var previousMax = settings.MaximumSuccessPercent;
        try
        {
            settings.SuccessPercent = 0;
            settings.MaximumSuccessPercent = 0;

            await action.MixItemsAsync(player, SocketWeaponMixId, 0).ConfigureAwait(false);
        }
        finally
        {
            settings.SuccessPercent = 40;
            settings.MaximumSuccessPercent = previousMax;
        }

        Assert.That(player.TemporaryStorage!.Items, Is.Empty);
        Assert.That(player.Money, Is.EqualTo(2_500_000 - MixCost));
    }

    /// <summary>
    /// Verifies that an unrelated paper/note item cannot substitute for a Creation Note,
    /// leaving all items and zen untouched.
    /// </summary>
    [Test]
    public async ValueTask WrongNoteIsRejectedAndLeavesItemsAndZenUntouchedAsync()
    {
        var player = await this.CreatePlayerAtChaosGoblinAsync().ConfigureAwait(false);
        player.Money = 2_500_000;

        // Provide 4 valid items plus an unrelated paper item (14-101 Suspicious Scrap of Paper) instead of 14-191..203.
        await this.AddSocketItemAsync(player, withMountedSeedSphere: true).ConfigureAwait(false);
        await this.AddItemAsync(player, 12, 100).ConfigureAwait(false); // Seed Sphere (Fire Lv.1)
        await this.AddItemAsync(player, 12, 15).ConfigureAwait(false); // Jewel of Chaos
        await this.AddItemAsync(player, 14, 22).ConfigureAwait(false); // Jewel of Creation
        await this.AddItemAsync(player, 14, 101).ConfigureAwait(false); // Suspicious Scrap of Paper

        await new ItemCraftAction().MixItemsAsync(player, SocketWeaponMixId, 0).ConfigureAwait(false);

        Assert.That(player.TemporaryStorage!.Items.ToList(), Has.Count.EqualTo(5));
        Assert.That(player.Money, Is.EqualTo(2_500_000));
    }

    /// <summary>
    /// Verifies that a socket item with zero mounted seed spheres is rejected even with valid jewels and note.
    /// </summary>
    [Test]
    public async ValueTask SocketItemWithoutMountedSeedSphereIsRejectedAsync()
    {
        var player = await this.CreatePlayerAtChaosGoblinAsync().ConfigureAwait(false);
        player.Money = 2_500_000;

        await this.AddStandardIngredientsAsync(player, 191, withMountedSeedSphere: false).ConfigureAwait(false);

        await new ItemCraftAction().MixItemsAsync(player, SocketWeaponMixId, 0).ConfigureAwait(false);

        Assert.That(player.TemporaryStorage!.Items.ToList(), Has.Count.EqualTo(5));
        Assert.That(player.Money, Is.EqualTo(2_500_000));
    }

    private async ValueTask<Player> CreatePlayerAtChaosGoblinAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        var chaosGoblin = this._gameConfiguration.Monsters.First(m => m.NpcWindow == NpcWindow.ChaosMachine);
        player.OpenedNpc = new NonPlayerCharacter(null!, chaosGoblin, null!);
        return player;
    }

    private async ValueTask AddStandardIngredientsAsync(Player player, short noteNumber, bool withMountedSeedSphere)
    {
        await this.AddSocketItemAsync(player, withMountedSeedSphere).ConfigureAwait(false);
        await this.AddItemAsync(player, 12, 100).ConfigureAwait(false); // Seed Sphere (Fire)
        await this.AddItemAsync(player, 12, 15).ConfigureAwait(false); // Jewel of Chaos
        await this.AddItemAsync(player, 14, 22).ConfigureAwait(false); // Jewel of Creation
        await this.AddItemAsync(player, 14, noteNumber).ConfigureAwait(false);
    }

    private async ValueTask AddSocketItemAsync(Player player, bool withMountedSeedSphere)
    {
        var flameberge = this.GetItem(0, 26); // Flameberge (socket item)
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = flameberge;
        item.Durability = flameberge.Durability;
        item.SocketCount = 3;

        if (withMountedSeedSphere)
        {
            var fireOption = flameberge.PossibleItemOptions
                .SelectMany(o => o.PossibleOptions)
                .First(o => o.OptionType == ItemOptionTypes.SocketOption);
            var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
            link.ItemOption = fireOption;
            link.Level = 1;
            link.Index = 0;
            item.ItemOptions.Add(link);
        }

        Assert.That(await player.TemporaryStorage!.AddItemAsync(item).ConfigureAwait(false), Is.True);
    }

    private async ValueTask AddItemAsync(Player player, byte group, short number)
    {
        var def = this.GetItem(group, number);
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = def;
        item.Durability = def.Durability == 0 ? (byte)1 : def.Durability;
        Assert.That(await player.TemporaryStorage!.AddItemAsync(item).ConfigureAwait(false), Is.True);
    }

    private ItemDefinition GetItem(byte group, short number) =>
        this._gameConfiguration.Items.Single(item => item.Group == group && item.Number == number);
}

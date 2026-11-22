// <copyright file="FenrirSecondClassEquipTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Verifies runtime equip and requirement checks for Horn of Fenrir (13, 37) require second class and up.
/// </summary>
[TestFixture]
public class FenrirSecondClassEquipTest
{
    private GameConfiguration _gameConfiguration = null!;

    /// <summary>Creates the Season 6 configuration once for all tests.</summary>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new Persistence.Initialization.VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        this._gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>First-class characters cannot comply with or equip Horn of Fenrir.</summary>
    /// <param name="classNumber">The first-class character number.</param>
    /// <returns>The task.</returns>
    [TestCase(CharacterClassNumber.DarkWizard)]
    [TestCase(CharacterClassNumber.DarkKnight)]
    [TestCase(CharacterClassNumber.FairyElf)]
    [TestCase(CharacterClassNumber.Summoner)]
    public async ValueTask FirstClassCannotEquipFenrirAsync(CharacterClassNumber classNumber)
    {
        var (player, fenrir) = await this.CreatePlayerWithFenrirAsync(classNumber).ConfigureAwait(false);

        Assert.That(player.CompliesRequirements(fenrir), Is.False);

        await new MoveItemAction().MoveItemAsync(player, 20, Storages.Inventory, InventoryConstants.PetSlot, Storages.Inventory).ConfigureAwait(false);

        Assert.That(player.Inventory!.GetItem(InventoryConstants.PetSlot), Is.Null);
        Assert.That(player.Inventory.GetItem(20), Is.SameAs(fenrir));
    }

    /// <summary>Second-class, special, and master-class characters at level 300 can equip Horn of Fenrir.</summary>
    /// <param name="classNumber">The qualified character class number.</param>
    /// <returns>The task.</returns>
    [TestCase(CharacterClassNumber.SoulMaster)]
    [TestCase(CharacterClassNumber.GrandMaster)]
    [TestCase(CharacterClassNumber.BladeKnight)]
    [TestCase(CharacterClassNumber.BladeMaster)]
    [TestCase(CharacterClassNumber.MuseElf)]
    [TestCase(CharacterClassNumber.HighElf)]
    [TestCase(CharacterClassNumber.MagicGladiator)]
    [TestCase(CharacterClassNumber.DuelMaster)]
    [TestCase(CharacterClassNumber.DarkLord)]
    [TestCase(CharacterClassNumber.LordEmperor)]
    [TestCase(CharacterClassNumber.BloodySummoner)]
    [TestCase(CharacterClassNumber.DimensionMaster)]
    [TestCase(CharacterClassNumber.RageFighter)]
    [TestCase(CharacterClassNumber.FistMaster)]
    public async ValueTask SecondClassAndUpCanEquipFenrirAsync(CharacterClassNumber classNumber)
    {
        var (player, fenrir) = await this.CreatePlayerWithFenrirAsync(classNumber).ConfigureAwait(false);

        Assert.That(player.CompliesRequirements(fenrir), Is.True);

        await new MoveItemAction().MoveItemAsync(player, 20, Storages.Inventory, InventoryConstants.PetSlot, Storages.Inventory).ConfigureAwait(false);

        Assert.That(player.Inventory!.GetItem(InventoryConstants.PetSlot), Is.SameAs(fenrir));
        Assert.That(player.Inventory.GetItem(20), Is.Null);
    }

    private async ValueTask<(Player Player, Item Fenrir)> CreatePlayerWithFenrirAsync(CharacterClassNumber classNumber)
    {
        var gameConfig = new Mock<GameConfiguration>();
        gameConfig.SetupAllProperties();
        gameConfig.Setup(c => c.Maps).Returns(new List<GameMapDefinition>());
        gameConfig.Setup(c => c.Items).Returns(new List<HNMO.DataModel.Configuration.Items.ItemDefinition>());
        gameConfig.Setup(c => c.ItemStatProfiles).Returns(this._gameConfiguration.ItemStatProfiles);
        gameConfig.Setup(c => c.Skills).Returns(new List<Skill>());
        gameConfig.Setup(c => c.Monsters).Returns(new List<HNMO.DataModel.Configuration.MonsterDefinition>());
        gameConfig.Setup(c => c.PlugInConfigurations).Returns(new List<PlugInConfiguration>());
        gameConfig.Setup(c => c.CharacterClasses).Returns(new List<CharacterClass>());
        gameConfig.Setup(c => c.GlobalAttributeCombinations).Returns(new List<HNMO.AttributeSystem.AttributeRelationship>());
        gameConfig.Setup(c => c.GlobalBaseAttributeValues).Returns(new List<HNMO.AttributeSystem.ConstValueAttribute>
        {
            new(1, Stats.MoneyAmountRate),
        });
        var map = new Mock<GameMapDefinition>();
        map.SetupAllProperties();
        map.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        map.Setup(m => m.MonsterSpawns).Returns(new List<MonsterSpawnArea>());
        map.Setup(m => m.ExitGates).Returns(new List<ExitGate>());
        map.Object.TerrainData = new byte[ushort.MaxValue + 3];
        gameConfig.Object.RecoveryInterval = int.MaxValue;
        gameConfig.Object.Maps.Add(map.Object);

        var mapInitializer = new MapInitializer(gameConfig.Object, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(gameConfig.Object, new InMemoryPersistenceContextProvider(), mapInitializer, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;

        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.SelectedCharacter!.CharacterClass = this._gameConfiguration.CharacterClasses.Single(c => c.Number == (byte)classNumber);
        player.Attributes![Stats.Level] = 300;

        var fenrirDefinition = this._gameConfiguration.Items.Single(i => i.Group == 13 && i.Number == 37);
        var fenrir = player.PersistenceContext.CreateNew<Item>();
        fenrir.Definition = fenrirDefinition;
        fenrir.Durability = 255;
        await player.Inventory!.AddItemAsync(20, fenrir).ConfigureAwait(false);
        return (player, fenrir);
    }
}

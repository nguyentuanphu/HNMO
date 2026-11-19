// <copyright file="RateTuningTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.ItemConsumeActions;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Unit tests for <see cref="RateTuningPlugIn"/> (Jewel of Soul/Life rates, +16/+28 cap, +15 Chaos broadcast, Zen drop, class PvP/PvM damage).
/// </summary>
[TestFixture]
public class RateTuningTests
{
    /// <summary>Verifies default Soul, Life, Zen, and Class multipliers preserve vanilla HNMO behavior when disabled.</summary>
    [Test]
    public async ValueTask PreservesDefaultsWhenDisabledAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        Assert.That(RateTuningPlugIn.GetEffectiveSoulRates(player, 50, 25), Is.EqualTo((50, 25)));
        Assert.That(RateTuningPlugIn.GetEffectiveLifeOptionSettings(player, 0.5), Is.EqualTo((0.5, (int?)null)));
        Assert.That(RateTuningPlugIn.GetZenDropMultiplier(player), Is.EqualTo(1.0));
        Assert.That(RateTuningPlugIn.ApplyClassDamageMultiplier(player, isPvp: true, 1000), Is.EqualTo(1000));
    }

    /// <summary>Verifies enabled Soul, Life, Zen, and +16/+28 Life option cap overrides apply cleanly.</summary>
    [Test]
    public async ValueTask AppliesConfiguredRateOverridesAndLifeOptionCapAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var plugin = new RateTuningPlugIn
        {
            Configuration = new RateTuningConfiguration
            {
                Enabled = true,
                OverrideSoulRates = true,
                SoulSuccessRatePercent = 80,
                SoulLuckBonusPercent = 15,
                OverrideLifeRate = true,
                LifeSuccessRatePercent = 90,
                MaxLifeOptionLevel = 7,
                ZenDropMultiplierPercent = 250,
            },
        };
        player.GameContext.FeaturePlugIns.AddPlugIn(plugin, true);

        Assert.That(RateTuningPlugIn.GetEffectiveSoulRates(player, 50, 25), Is.EqualTo((80, 15)));
        Assert.That(RateTuningPlugIn.GetEffectiveLifeOptionSettings(player, 0.5), Is.EqualTo((0.9, (int?)7)));
        Assert.That(RateTuningPlugIn.GetZenDropMultiplier(player), Is.EqualTo(2.5));
    }

    /// <summary>Verifies Jewel of Life stops upgrading once <see cref="RateTuningConfiguration.MaxLifeOptionLevel"/> is reached.</summary>
    [Test]
    public async ValueTask LifeJewelRespectsMaxOptionLevelCapAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var plugin = new RateTuningPlugIn
        {
            Configuration = new RateTuningConfiguration
            {
                Enabled = true,
                OverrideLifeRate = true,
                LifeSuccessRatePercent = 100,
                MaxLifeOptionLevel = 4,
            },
        };
        player.GameContext.FeaturePlugIns.AddPlugIn(plugin, true);

        var (targetItem, lifeOptionDef) = CreateUpgradeableTestItem(player, initialLevel: 9, maxOptionLevelCount: 7);
        var optLink = player.PersistenceContext.CreateNew<ItemOptionLink>();
        optLink.ItemOption = lifeOptionDef;
        optLink.Level = 4; // Already +16
        targetItem.ItemOptions.Add(optLink);
        await player.Inventory!.AddItemAsync(12, targetItem).ConfigureAwait(false);

        var jewel = CreateSimpleJewel(16);
        await player.Inventory.AddItemAsync(13, jewel).ConfigureAwait(false);

        var handler = new LifeJewelConsumeHandlerPlugIn();
        var consumedAtCap4 = await handler.ConsumeItemAsync(player, jewel, targetItem, FruitUsage.Undefined).ConfigureAwait(false);
        Assert.That(consumedAtCap4, Is.False);
        Assert.That(optLink.Level, Is.EqualTo(4));

        // Now raise cap to 7 (+28) and retry -> upgrades to level 5 cleanly.
        plugin.Configuration.MaxLifeOptionLevel = 7;
        var consumedAtCap7 = await handler.ConsumeItemAsync(player, jewel, targetItem, FruitUsage.Undefined).ConfigureAwait(false);
        Assert.That(consumedAtCap7, Is.True);
        Assert.That(optLink.Level, Is.EqualTo(5));
    }

    /// <summary>Verifies Jewel of Soul consumes configured Soul success and Luck rates from <see cref="RateTuningPlugIn"/>.</summary>
    [Test]
    public async ValueTask SoulJewelUsesRateTuningSuccessAndLuckRatesAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var plugin = new RateTuningPlugIn
        {
            Configuration = new RateTuningConfiguration
            {
                Enabled = true,
                OverrideSoulRates = true,
                SoulSuccessRatePercent = 100,
                SoulLuckBonusPercent = 0,
            },
        };
        player.GameContext.FeaturePlugIns.AddPlugIn(plugin, true);

        var (targetItem, _) = CreateUpgradeableTestItem(player, initialLevel: 3, maxOptionLevelCount: 4);
        await player.Inventory!.AddItemAsync(12, targetItem).ConfigureAwait(false);

        var soul = CreateSimpleJewel(14);
        await player.Inventory.AddItemAsync(13, soul).ConfigureAwait(false);

        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextRandomBool(It.IsAny<int>())).Returns<int>(p => p > 0);
        var handler = new SoulJewelConsumeHandlerPlugIn(randomizer.Object);
        var consumed = await handler.ConsumeItemAsync(player, soul, targetItem, FruitUsage.Undefined).ConfigureAwait(false);
        Assert.That(consumed, Is.True);
        Assert.That(targetItem.Level, Is.EqualTo(4));

        // Now test 0% Soul override fails reliably.
        plugin.Configuration.SoulSuccessRatePercent = 0;
        var soul2 = CreateSimpleJewel(14);
        await player.Inventory.AddItemAsync(14, soul2).ConfigureAwait(false);
        await handler.ConsumeItemAsync(player, soul2, targetItem, FruitUsage.Undefined).ConfigureAwait(false);
        Assert.That(targetItem.Level, Is.EqualTo(3));
    }

    /// <summary>Verifies per-class PvP vs PvM multipliers scale combat damage for 1st/2nd/3rd job numbers.</summary>
    [Test]
    public async ValueTask ScalesClassPvpAndPvmDamageAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.CharacterClass = new CharacterClass { Number = 7, Name = "Blade Master" }; // DK line (4)
        var plugin = new RateTuningPlugIn
        {
            Configuration = new RateTuningConfiguration
            {
                Enabled = true,
            },
        };
        var dkRule = plugin.Configuration.ClassDamageRates.First(c => c.ClassLine == 4);
        dkRule.PvpDamagePercent = 80;
        dkRule.PvmDamagePercent = 125;
        player.GameContext.FeaturePlugIns.AddPlugIn(plugin, true);

        Assert.That(RateTuningPlugIn.ApplyClassDamageMultiplier(player, isPvp: true, 1000), Is.EqualTo(800));
        Assert.That(RateTuningPlugIn.ApplyClassDamageMultiplier(player, isPvp: false, 1000), Is.EqualTo(1250));
    }

    /// <summary>Verifies +15 Chaos Mix success triggers global GoldenCenter broadcast only at/above configured level.</summary>
    [Test]
    public async ValueTask BroadcastsHighLevelChaosMixAsync()
    {
        var gameConfig = PlayerTestHelper.CreateGameConfiguration();
        var mapInit = new MapInitializer(gameConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(gameConfig, new InMemoryPersistenceContextProvider(), mapInit, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInit.PlugInManager = gameContext.PlugInManager;
        mapInit.PathFinderPool = gameContext.PathFinderPool;

        var player = new BroadcastTestPlayer(gameContext);
        await gameContext.AddPlayerAsync(player).ConfigureAwait(false);
        await PlayerTestHelper.EnterWorldAsync(player, PlayerTestHelper.CreateCharacter(gameContext)).ConfigureAwait(false);

        var messages = new List<string>();
        var showMock = Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!);
        showMock
            .Setup(m => m.ShowMessageAsync(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Callback<string, MessageType>((msg, type) =>
            {
                if (type == MessageType.GoldenCenter)
                {
                    messages.Add(msg);
                }
            })
            .Returns(ValueTask.CompletedTask);

        var plugin = new RateTuningPlugIn
        {
            Configuration = new RateTuningConfiguration
            {
                Enabled = true,
                BroadcastHighLevelChaosMix = true,
                BroadcastMinChaosMixLevel = 15,
            },
        };
        gameContext.FeaturePlugIns.AddPlugIn(plugin, true);

        var plus14 = new Item { Level = 14, Definition = new ItemDefinition { Name = "Dragon Armor" } };
        await RateTuningPlugIn.TryBroadcastChaosMixSuccessAsync(player, plus14).ConfigureAwait(false);
        Assert.That(messages, Is.Empty);

        var plus15 = new Item { Level = 15, Definition = new ItemDefinition { Name = "Dragon Armor" } };
        await RateTuningPlugIn.TryBroadcastChaosMixSuccessAsync(player, plus15).ConfigureAwait(false);
        Assert.That(messages, Has.Count.EqualTo(1));
        Assert.That(messages[0], Does.Contain("+15"));
    }

    private static Item CreateSimpleJewel(short number) => new()
    {
        Definition = new ItemDefinition { Number = number, Group = 14, Width = 1, Height = 1, Durability = 1 },
        Durability = 1,
    };

    private static (Item Item, IncreasableItemOption OptionDef) CreateUpgradeableTestItem(
        Player player,
        byte initialLevel,
        int maxOptionLevelCount)
    {
        var itemMock = new Mock<Item>();
        itemMock.SetupAllProperties();
        itemMock.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        itemMock.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());

        var defMock = new Mock<ItemDefinition>();
        defMock.SetupAllProperties();
        defMock.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        defMock.Setup(d => d.BasePowerUpAttributes).Returns(new List<ItemBasePowerUpDefinition>());
        defMock.Object.Name = "Test Armor";
        defMock.Object.MaximumItemLevel = 15;
        defMock.Object.Width = 2;
        defMock.Object.Height = 2;
        defMock.Object.Durability = 50;

        var slotMock = new Mock<ItemSlotType>();
        slotMock.Setup(s => s.ItemSlots).Returns(new List<int> { HNMO.DataModel.InventoryConstants.LeftHandSlot });
        defMock.Setup(d => d.ItemSlot).Returns(slotMock.Object);

        var optSetMock = new Mock<ItemOptionDefinition>();
        optSetMock.SetupAllProperties();
        optSetMock.Setup(o => o.PossibleOptions).Returns(new List<IncreasableItemOption>());

        var lifeOptMock = new Mock<IncreasableItemOption>();
        lifeOptMock.SetupAllProperties();
        lifeOptMock.Setup(o => o.LevelDependentOptions).Returns(new List<ItemOptionOfLevel>());
        lifeOptMock.Object.OptionType = ItemOptionTypes.Option;
        for (var lvl = 1; lvl <= maxOptionLevelCount; lvl++)
        {
            lifeOptMock.Object.LevelDependentOptions.Add(new ItemOptionOfLevel { Level = lvl, RequiredItemLevel = 0 });
        }

        optSetMock.Object.PossibleOptions.Add(lifeOptMock.Object);
        defMock.Object.PossibleItemOptions.Add(optSetMock.Object);

        itemMock.Object.Definition = defMock.Object;
        itemMock.Object.Level = initialLevel;
        itemMock.Object.Durability = 50;
        return (itemMock.Object, lifeOptMock.Object);
    }

    private sealed class BroadcastTestPlayer : Player
    {
        public BroadcastTestPlayer(IGameContext gameContext)
            : base(gameContext)
        {
        }

        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new MockViewPlugInContainer();
    }
}

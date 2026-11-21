// <copyright file="JewelOfLifeOption28UpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The additional option goes up to +28 (option level 7) on Season 6, like the Season 6/9 server suites: in a new
/// database, and by the update in an existing one. The earlier versions keep their maximum.
/// </summary>
[TestFixture]
internal class JewelOfLifeOption28UpdateTest
{
    /// <summary>
    /// The items, their option's attribute and the value the option adds per level.
    /// </summary>
    private static readonly (byte Group, short Number, AttributeDefinition Attribute, float Step)[] CheckedItems =
    [
        (0, 0, Stats.PhysicalBaseDmg, 4), // Kris: +28 damage
        (8, 0, Stats.DefenseBase, 4), // Bronze Armor: +28 defense
        (6, 0, Stats.DefenseRatePvm, 5), // Small Shield: +35 defense rate
        (12, 2, Stats.PhysicalBaseDmg, 4), // Wings of Satan: +28 damage
        (12, 0, Stats.HealthRecoveryMultiplier, 0.01f), // Wings of Elf: 7 % recovery
    ];

    /// <summary>A new Season 6 database has the option levels 1 to 7, with the same step per level.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasOptionLevelsUpToSevenAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(provider => new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory())).ConfigureAwait(false);

        AssertLevelsUpToSeven(gameConfiguration);
        Assert.That(gameConfiguration.MaximumItemOptionLevelDrop, Is.EqualTo(3), "drops keep their maximum");
        var ringOption = OptionOf(gameConfiguration, 13, 8);
        Assert.That(ringOption.LevelDependentOptions.Max(l => l.Level), Is.EqualTo(3), "the rings keep their 3 %");
    }

    /// <summary>A new 0.95d database keeps the maximum of +16.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseOfVersion095dKeepsLevelFourAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(provider => new Version095d.DataInitialization(provider, new NullLoggerFactory())).ConfigureAwait(false);

        var option = OptionOf(gameConfiguration, 0, 0);
        Assert.That(option.LevelDependentOptions.Max(l => l.Level), Is.EqualTo(4));
    }

    /// <summary>
    /// An existing Season 6 database, whose options end at level 4, gets the levels 5 to 7 once, also when the update
    /// runs twice.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateAddsTheLevelsFiveToSevenOnceAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(provider => new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory())).ConfigureAwait(false);
        var options = gameConfiguration.ItemOptions.SelectMany(o => o.PossibleOptions).Where(o => o.OptionType == ItemOptionTypes.Option).ToList();
        foreach (var option in options)
        {
            foreach (var higherLevel in option.LevelDependentOptions.Where(l => l.Level > 4).ToList())
            {
                option.LevelDependentOptions.Remove(higherLevel);
            }
        }

        Assert.That(OptionOf(gameConfiguration, 0, 0).LevelDependentOptions.Max(l => l.Level), Is.EqualTo(4), "the database before the update");

        var contextProvider = new InMemoryPersistenceContextProvider();
        await new JewelOfLifeOption28UpdatePlugIn().ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        await new JewelOfLifeOption28UpdatePlugIn().ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        AssertLevelsUpToSeven(gameConfiguration);
        Assert.That(
            options.Where(o => o.LevelDependentOptions.Any()),
            Has.All.Matches<IncreasableItemOption>(o => o.LevelDependentOptions.GroupBy(l => l.Level).All(g => g.Count() == 1)),
            "every level once");
        Assert.That(OptionOf(gameConfiguration, 13, 8).LevelDependentOptions.Max(l => l.Level), Is.EqualTo(3), "the rings keep their 3 %");
    }

    /// <summary>Saved rate tuning settings with the earlier default cap (+16), saved by the admin panel, get +28.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SavedRateTuningCapOfFourBecomesSevenAsync()
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration
        {
            TypeId = typeof(RateTuningPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = """{ "$id": "1", "Enabled": true, "MaxLifeOptionLevel": 4, "ZenDropMultiplierPercent": 150, "ClassDamageRates": { "$id": "2", "$values": [ { "$id": "3", "ClassLine": 4, "PvpDamagePercent": 90 } ] } }""",
        };
        configuration.PlugInConfigurations.Add(plugIn);

        await new JewelOfLifeOption28UpdatePlugIn().ApplyUpdateAsync(new InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);

        var saved = plugIn.GetConfiguration<RateTuningConfiguration>(null)!;
        Assert.That(saved.MaxLifeOptionLevel, Is.EqualTo(7));
        Assert.That(saved.Enabled, Is.True);
        Assert.That(saved.ZenDropMultiplierPercent, Is.EqualTo(150), "the other settings stay");
        Assert.That(saved.ClassDamageRates.Single().PvpDamagePercent, Is.EqualTo(90));
    }

    /// <summary>A cap the host chose (not the earlier default) stays exactly as saved.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SavedRateTuningCapChosenByTheHostStaysAsync()
    {
        const string savedSettings = """{ "Enabled": true, "MaxLifeOptionLevel": 5 }""";
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var plugIn = new HNMO.Persistence.BasicModel.PlugInConfiguration
        {
            TypeId = typeof(RateTuningPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = savedSettings,
        };
        configuration.PlugInConfigurations.Add(plugIn);

        await new JewelOfLifeOption28UpdatePlugIn().ApplyUpdateAsync(new InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);

        Assert.That(plugIn.CustomConfiguration, Is.EqualTo(savedSettings));
    }

    private static void AssertLevelsUpToSeven(GameConfiguration gameConfiguration)
    {
        foreach (var (group, number, attribute, step) in CheckedItems)
        {
            var option = OptionOf(gameConfiguration, group, number);
            Assert.That(option.LevelDependentOptions.Max(l => l.Level), Is.EqualTo(7), () => $"item {group}/{number}");
            for (var level = 1; level <= 7; level++)
            {
                var powerUp = option.LevelDependentOptions.FirstOrDefault(l => l.Level == level)?.PowerUpDefinition ?? option.PowerUpDefinition;
                Assert.That(powerUp?.TargetAttribute, Is.EqualTo(attribute), () => $"item {group}/{number}, level {level}");
                Assert.That(powerUp!.Boost!.ConstantValue.Value, Is.EqualTo(level * step).Within(0.0001f), () => $"item {group}/{number}, level {level}");
            }
        }
    }

    private static IncreasableItemOption OptionOf(GameConfiguration gameConfiguration, byte group, short number)
    {
        return gameConfiguration.Items.First(i => i.Group == group && i.Number == number)
            .PossibleItemOptions.SelectMany(o => o.PossibleOptions)
            .First(o => o.OptionType == ItemOptionTypes.Option);
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync(Func<IPersistenceContextProvider, DataInitializationBase> createInitialization)
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await createInitialization(contextProvider).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        return (await contextProvider.CreateNewContext().GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}

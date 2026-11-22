// <copyright file="SocketWeaponMixDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.Updates.SeasonNine;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests the socket weapon mix data (13 Creation Notes 14-191..203, 13 Lv.400 5-socket weapons,
/// and Chaos Machine mix #57) across fresh Season 6 databases and Season 6 / Season 9 mandatory updates.
/// </summary>
[TestFixture]
internal class SocketWeaponMixDataTest
{
    private const byte CraftingNumber = 57;

    private static readonly (short NoteNumber, byte WeaponGroup, short WeaponNumber)[] Recipes =
    [
        (191, 0, 29),
        (192, 0, 36),
        (193, 0, 37),
        (194, 3, 12),
        (195, 2, 20),
        (196, 4, 25),
        (197, 4, 26),
        (198, 5, 35),
        (199, 5, 37),
        (200, 0, 30),
        (201, 2, 19),
        (202, 5, 32),
        (203, 0, 38),
    ];

    /// <summary>
    /// Verifies that a newly initialized Season 6 configuration includes all 13 Creation Notes,
    /// all 13 matching 5-socket weapons, and crafting #57 (40 %, 1,000,000 zen).
    /// </summary>
    [Test]
    public async Task NewSeason6DatabaseContainsSocketWeaponMixAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertMixData(config);
    }

    /// <summary>
    /// Verifies that <see cref="SocketWeaponMixUpdatePlugIn"/> is mandatory, idempotent, adds all missing
    /// mix data to an older Season 6 database, and reuses definitions without duplicating Season 9 items.
    /// </summary>
    [Test]
    public async Task MandatoryUpdateAddsMixWithoutDuplicatingSeason9ItemsAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var chaosGoblin = config.Monsters.First(m => m.NpcWindow == NpcWindow.ChaosMachine);
        chaosGoblin.ItemCraftings.Where(c => c.Number == CraftingNumber).ToList().ForEach(c => chaosGoblin.ItemCraftings.Remove(c));
        foreach (var (noteNumber, weaponGroup, weaponNumber) in Recipes)
        {
            config.Items.Where(i => (i.Group == 14 && i.Number == noteNumber) || (i.Group == weaponGroup && i.Number == weaponNumber))
                .ToList()
                .ForEach(i => config.Items.Remove(i));
        }

        var update = new SocketWeaponMixUpdatePlugIn();
        Assert.That(update.IsMandatory, Is.True);

        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        AssertMixData(config);

        var seasonNineUpdate = new SeasonNineOnlyItemsUpdatePlugIn(() => true);
        await seasonNineUpdate.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        foreach (var (noteNumber, weaponGroup, weaponNumber) in Recipes)
        {
            Assert.That(config.Items.Count(i => i.Group == 14 && i.Number == noteNumber), Is.EqualTo(1));
            Assert.That(config.Items.Count(i => i.Group == weaponGroup && i.Number == weaponNumber), Is.EqualTo(1));
        }
    }

    /// <summary>
    /// Verifies update plug-in caption and recipe translations across en, vi, de, zh-CN.
    /// </summary>
    [Test]
    public void CaptionsAreTranslatedInAllFourLanguages()
    {
        var plugin = new SocketWeaponMixUpdatePlugIn();
        var prev = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "en", "vi", "de", "zh-CN" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Assert.That(plugin.Name, Is.Not.Null.And.Not.Empty, culture);
                Assert.That(plugin.Description, Is.Not.Null.And.Not.Empty, culture);
                Assert.That(CraftingNames.ResourceManager.GetString("SocketWeapon", CultureInfo.CurrentUICulture), Is.Not.Null.And.Not.Empty, culture);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
        }
    }

    private static void AssertMixData(GameConfiguration config)
    {
        foreach (var (noteNumber, weaponGroup, weaponNumber) in Recipes)
        {
            var note = config.Items.SingleOrDefault(i => i.Group == 14 && i.Number == noteNumber);
            Assert.That(note, Is.Not.Null, $"Missing note 14-{noteNumber}");

            var weapon = config.Items.SingleOrDefault(i => i.Group == weaponGroup && i.Number == weaponNumber);
            Assert.That(weapon, Is.Not.Null, $"Missing weapon {weaponGroup}-{weaponNumber}");
            Assert.That(weapon!.MaximumSockets, Is.EqualTo(5));
        }

        var chaosGoblin = config.Monsters.First(m => m.NpcWindow == NpcWindow.ChaosMachine);
        var crafting = chaosGoblin.ItemCraftings.SingleOrDefault(c => c.Number == CraftingNumber);
        Assert.That(crafting, Is.Not.Null);
        Assert.That(crafting!.SimpleCraftingSettings, Is.Not.Null);
        Assert.That(crafting.SimpleCraftingSettings!.SuccessPercent, Is.EqualTo(40));
        Assert.That(crafting.SimpleCraftingSettings.Money, Is.EqualTo(1_000_000));
    }
}

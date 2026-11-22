// <copyright file="ArenaGuardAndJinShopsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.NPC;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.Updates.SeasonNine;

/// <summary>
/// Verifies <see cref="ArenaGuardShopUpdatePlugIn"/> and <see cref="SeasonNineJinShopsUpdatePlugIn"/> equip
/// Arena Guard (239) and the Season 9 Jin merchants (583, 604) with the 32-item potion/arrow shop while
/// preserving existing customizations and keeping Season 6 untouched by Season 9 Jin updates.
/// </summary>
[TestFixture]
internal class ArenaGuardAndJinShopsDataTest
{
    /// <summary>New Season 6 databases include the Arena Guard (239) shop.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ArenaGuardHasMerchantShopAndCustomizationsPreservedAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var guard = config.Monsters.Single(m => m.Number == 239);
        Assert.That(guard.NpcWindow, Is.EqualTo(NpcWindow.Merchant));
        Assert.That(guard.MerchantStore, Is.Not.Null);
        Assert.That(guard.MerchantStore!.Items, Has.Count.EqualTo(32));
        Assert.That(guard.MerchantStore.Items.Any(i => i.Definition is { Group: 14, Number: 10 }), Is.True, "Town Portal Scroll");
        Assert.That(guard.MerchantStore.Items.Any(i => i.Definition is { Group: 13, Number: 29 }), Is.True, "Armor of Guardsman");
        Assert.That(guard.MerchantStore.Items.Any(i => i.Definition is { Group: 13, Number: 7 }), Is.False, "13-7 omitted");

        // Running again or when customized preserves the existing shop.
        guard.MerchantStore.Items.Clear();
        var update = new ArenaGuardShopUpdatePlugIn();
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        Assert.That(guard.MerchantStore.Items, Is.Empty, "Customized store must be preserved.");

        // An existing database lacking Arena Guard's shop receives it via the mandatory update plug-in.
        guard.MerchantStore = null;
        guard.NpcWindow = NpcWindow.Undefined;
        await update.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        Assert.That(guard.NpcWindow, Is.EqualTo(NpcWindow.Merchant));
        Assert.That(guard.MerchantStore, Is.Not.Null);
        Assert.That(guard.MerchantStore!.Items, Has.Count.EqualTo(32));
    }

    /// <summary>Season 9 Jin (583, 604) receives the 32-item shop and map 91/92 spawns only in Season 9 mode.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SeasonNineJinShopsAndSpawnsApplyOnlyInSeasonNineAsync()
    {
        var (s6Context, s6Config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using (s6Context)
        {
            await new SeasonNineJinShopsUpdatePlugIn(() => false).ApplyUpdateAsync(s6Context, s6Config).ConfigureAwait(false);
            Assert.That(s6Config.Monsters.Any(m => m.Number is 583 or 604), Is.False, "Season 6 stays unchanged.");
        }

        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        await new SeasonNineMonstersUpdatePlugIn(() => true).ApplyUpdateAsync(context, config).ConfigureAwait(false);
        await new SeasonNineMapsAndGatesUpdatePlugIn(() => true).ApplyUpdateAsync(context, config).ConfigureAwait(false);

        var jinUpdate = new SeasonNineJinShopsUpdatePlugIn(() => true);
        await jinUpdate.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        var jin583 = config.Monsters.Single(m => m.Number == 583);
        var jin604 = config.Monsters.Single(m => m.Number == 604);
        Assert.Multiple(() =>
        {
            Assert.That(jin583.NpcWindow, Is.EqualTo(NpcWindow.Merchant));
            Assert.That(jin583.MerchantStore!.Items, Has.Count.EqualTo(32));
            Assert.That(jin604.NpcWindow, Is.EqualTo(NpcWindow.Merchant));
            Assert.That(jin604.MerchantStore!.Items, Has.Count.EqualTo(32));
            Assert.That(jin583.MerchantStore, Is.Not.SameAs(jin604.MerchantStore));

            var acheron = config.Maps.Single(m => m.Number == 91);
            var acheronWar = config.Maps.Single(m => m.Number == 92);
            Assert.That(acheron.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == 583 && s.X1 == 55 && s.Y1 == 195), Is.True);
            Assert.That(acheronWar.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == 604 && s.X1 == 56 && s.Y1 == 202), Is.True);
        });

        // Localization check in all 4 supported cultures.
        foreach (var culture in new[] { "en", "vi", "de", "zh-CN" })
        {
            var prev = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Assert.That(new ArenaGuardShopUpdatePlugIn().Name, Is.Not.Empty);
                Assert.That(jinUpdate.Name, Is.Not.Empty);
            }
            finally
            {
                CultureInfo.CurrentUICulture = prev;
            }
        }
    }
}

// <copyright file="Season6ShopListsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the NPC shop lists of the Season 6 server suite: Silvia (415) scrolls, Rhea (416) potions, Marce (417) the
/// weapons Bolo sells, Natasha (379) armors and weapons, Lumen (255) the Illusion Temple tickets up to +6.
/// </summary>
[TestFixture]
internal class Season6ShopListsDataTest
{
    /// <summary>A new Season 6 database has the suite's lists.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuitesListsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertLists(gameConfiguration);
    }

    /// <summary>The update rearranges HNMO's old lists, once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateRearrangesTheOldListsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var scrolls = Npc(gameConfiguration, 415).MerchantStore;
        var potions = Npc(gameConfiguration, 416).MerchantStore;
        var armors = Npc(gameConfiguration, 379).MerchantStore;
        Npc(gameConfiguration, 415).MerchantStore = potions;
        Npc(gameConfiguration, 416).MerchantStore = armors;
        Npc(gameConfiguration, 417).MerchantStore = scrolls;
        Npc(gameConfiguration, 379).MerchantStore = null;
        var lumen = Npc(gameConfiguration, 255).MerchantStore!;
        foreach (var ticket in lumen.Items.Where(i => i.Level == 6 && i.Definition!.Group == 13 && i.Definition.Number is 49 or 50).ToList())
        {
            lumen.Items.Remove(ticket);
        }

        var update = new Season6ShopListsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertLists(gameConfiguration);
    }

    private static MonsterDefinition Npc(GameConfiguration gameConfiguration, short number)
        => gameConfiguration.Monsters.Single(m => m.Number == number);

    private static void AssertLists(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            var silvia = Npc(gameConfiguration, 415).MerchantStore!.Items;
            Assert.That(silvia.Select(i => (i.Definition!.Group, i.Definition.Number)).OrderBy(x => x.Number), Is.EqualTo(new (byte, short)[] { (15, 1), (15, 3), (15, 6), (15, 10), (15, 20) }), "Silvia");

            var rhea = Npc(gameConfiguration, 416).MerchantStore!.Items;
            Assert.That(rhea.Any(i => i.Definition!.Group == 14 && i.Definition.Number == 3), Is.True, "Rhea sells potions");
            Assert.That(rhea.Any(i => i.Definition!.Group is >= 7 and <= 11), Is.False, "Rhea sells no armor");

            var marce = Npc(gameConfiguration, 417).MerchantStore!.Items.Select(i => (i.Definition!.Group, i.Definition.Number, i.Level)).OrderBy(x => x).ToList();
            var bolo = Npc(gameConfiguration, 578).MerchantStore!.Items.Select(i => (i.Definition!.Group, i.Definition.Number, i.Level)).OrderBy(x => x).ToList();
            Assert.That(marce, Is.EqualTo(bolo), "Marce sells Bolo's weapons");
            Assert.That(marce, Has.Count.EqualTo(6));

            var natasha = Npc(gameConfiguration, 379);
            Assert.That(natasha.NpcWindow, Is.EqualTo(NpcWindow.Merchant));
            Assert.That(natasha.MerchantStore!.Items, Has.Count.EqualTo(20), "Natasha sells the armors and weapons");

            var lumen = Npc(gameConfiguration, 255).MerchantStore!.Items;
            Assert.That(lumen.Count(i => i.Level == 6 && i.Definition!.Group == 13 && i.Definition.Number == 49), Is.EqualTo(1), "Old Scroll + 6");
            Assert.That(lumen.Count(i => i.Level == 6 && i.Definition!.Group == 13 && i.Definition.Number == 50), Is.EqualTo(1), "Illusion Sorcerer Covenant + 6");
        });
    }
}

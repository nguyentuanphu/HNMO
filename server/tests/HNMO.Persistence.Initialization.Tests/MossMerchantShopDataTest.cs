// <copyright file="MossMerchantShopDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Verifies that the five Moss gamble items (13-71..75) and Moss the Merchant (492) shop exist on new
/// databases and are installed by <see cref="MossMerchantShopUpdatePlugIn"/> on existing saves without
/// overwriting admin customizations.
/// </summary>
[TestFixture]
internal class MossMerchantShopDataTest
{
    /// <summary>New Season 6 configurations define 13-71..75 at 2,000,000 Zen and equip Moss (492) with them.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasMossItemsAndMerchantShopAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertGambleItemsAndShop(config);

        // Customized shop is preserved on re-run.
        var moss = config.Monsters.Single(m => m.Number == 492);
        moss.MerchantStore!.Items.Clear();
        await new MossMerchantShopUpdatePlugIn().ApplyUpdateAsync(context, config).ConfigureAwait(false);
        Assert.That(moss.MerchantStore.Items, Is.Empty);

        // Missing definitions + empty Moss shop are populated on existing saves.
        moss.MerchantStore = null;
        moss.NpcWindow = NpcWindow.Undefined;
        foreach (var def in config.Items.Where(i => i.Group == 13 && i.Number is >= 71 and <= 75).ToList())
        {
            config.Items.Remove(def);
        }

        await new MossMerchantShopUpdatePlugIn().ApplyUpdateAsync(context, config).ConfigureAwait(false);
        AssertGambleItemsAndShop(config);
    }

    private static void AssertGambleItemsAndShop(GameConfiguration config)
    {
        var gambleDefs = Enumerable.Range(71, 5)
            .Select(n => config.Items.SingleOrDefault(i => i.Group == 13 && i.Number == n))
            .ToList();
        Assert.That(gambleDefs, Has.All.Not.Null);

        var calc = new ItemPriceCalculator();
        foreach (var def in gambleDefs)
        {
            Assert.Multiple(() =>
            {
                Assert.That(def!.Width, Is.EqualTo(2));
                Assert.That(def.Height, Is.EqualTo(3));
                Assert.That(def.Durability, Is.EqualTo(255));
                Assert.That(def.DropsFromMonsters, Is.False);
                Assert.That(def.QualifiedCharacters, Has.Count.EqualTo(config.CharacterClasses.Count));
                Assert.That(calc.CalculateFinalBuyingPrice(new TemporaryItem { Definition = def, Durability = def.Durability }), Is.EqualTo(2_000_000));
                foreach (var culture in new[] { "en", "vi", "de", "zh-CN" })
                {
                    Assert.That(def.Name.GetTranslation(CultureInfo.GetCultureInfo(culture), false), Is.Not.Null.And.Not.Empty);
                }
            });
        }

        var moss = config.Monsters.Single(m => m.Number == 492);
        Assert.That(moss.NpcWindow, Is.EqualTo(NpcWindow.Merchant));
        Assert.That(moss.MerchantStore, Is.Not.Null);
        Assert.That(moss.MerchantStore!.Items.Select(i => (i.ItemSlot, i.Definition!.Group, i.Definition.Number)).OrderBy(x => x.ItemSlot),
            Is.EqualTo(new (byte, byte, short)[]
            {
                (0, 13, 71),
                (2, 13, 72),
                (4, 13, 73),
                (6, 13, 74),
                (24, 13, 75),
            }));
    }
}

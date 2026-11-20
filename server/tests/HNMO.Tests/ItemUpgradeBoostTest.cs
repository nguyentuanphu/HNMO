// <copyright file="ItemUpgradeBoostTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlayerActions.ItemConsumeActions;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The upgrade settings "boost option type" and "success chance boost" (e.g. more chance for an item with luck)
/// had no effect; they now raise the success chance.
/// </summary>
[TestFixture]
public class ItemUpgradeBoostTest
{
    /// <summary>An item with luck gets the boost; one without doesn't; the chance stays at most 1.</summary>
    [Test]
    public void ItemWithTheBoostOptionGetsMoreChance()
    {
        var handler = new LifeJewelConsumeHandlerPlugIn
        {
            Configuration = new ItemUpgradeConfiguration
            {
                OptionType = ItemOptionTypes.Option,
                AddsOption = true,
                IncreasesOption = true,
                SuccessChance = 0.5,
                BoostOptionType = ItemOptionTypes.Luck,
                SuccessChanceBoost = 0.25,
            },
        };

        Assert.Multiple(() =>
        {
            Assert.That(handler.GetBoostedSuccessChance(CreateItem(withLuck: true), 0.5), Is.EqualTo(0.75).Within(0.0001));
            Assert.That(handler.GetBoostedSuccessChance(CreateItem(withLuck: false), 0.5), Is.EqualTo(0.5).Within(0.0001));
            Assert.That(handler.GetBoostedSuccessChance(CreateItem(withLuck: true), 0.9), Is.EqualTo(1.0).Within(0.0001));
        });
    }

    /// <summary>Without a boost configured the chance is unchanged.</summary>
    [Test]
    public void NoBoostConfiguredKeepsTheChance()
    {
        var handler = new LifeJewelConsumeHandlerPlugIn
        {
            Configuration = new ItemUpgradeConfiguration { OptionType = ItemOptionTypes.Option, AddsOption = true, IncreasesOption = true, SuccessChance = 0.5 },
        };

        Assert.That(handler.GetBoostedSuccessChance(CreateItem(withLuck: true), 0.5), Is.EqualTo(0.5).Within(0.0001));
    }

    private static BasicModel.Item CreateItem(bool withLuck)
    {
        var item = new BasicModel.Item { Definition = new BasicModel.ItemDefinition { Name = "Kris" } };
        if (withLuck)
        {
            item.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = new BasicModel.IncreasableItemOption { OptionType = ItemOptionTypes.Luck } });
        }

        return item;
    }
}

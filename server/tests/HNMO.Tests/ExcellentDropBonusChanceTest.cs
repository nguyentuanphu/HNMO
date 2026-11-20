// <copyright file="ExcellentDropBonusChanceTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The Happy Hour's extra excellent chance (5/10000 like the Season 6/9 server suites) only raises the common
/// excellent drop group.
/// </summary>
[TestFixture]
public class ExcellentDropBonusChanceTest
{
    /// <summary>The common excellent group gets the bonus: 0.2 % becomes 0.25 %.</summary>
    [Test]
    public void CommonExcellentGroupGetsTheBonus()
    {
        var group = new BasicModel.DropItemGroup { ItemType = SpecialItemType.Excellent, Chance = 0.002 };

        Assert.Multiple(() =>
        {
            Assert.That(DefaultDropGenerator.GetChance(group, 0.0005), Is.EqualTo(0.0025).Within(0.0000001));
            Assert.That(DefaultDropGenerator.GetChance(group, 0), Is.EqualTo(0.002).Within(0.0000001));
        });
    }

    /// <summary>Other groups (random items, a box's excellent items) keep their chance.</summary>
    [Test]
    public void OtherGroupsKeepTheirChance()
    {
        var randomItems = new BasicModel.DropItemGroup { ItemType = SpecialItemType.RandomItem, Chance = 0.3 };
        var boxExcellent = new BasicModel.DropItemGroup { ItemType = SpecialItemType.Excellent, Chance = 0.5 };
        boxExcellent.PossibleItems.Add(new BasicModel.ItemDefinition { Name = "Kris" });

        Assert.Multiple(() =>
        {
            Assert.That(DefaultDropGenerator.GetChance(randomItems, 0.0005), Is.EqualTo(0.3));
            Assert.That(DefaultDropGenerator.GetChance(boxExcellent, 0.0005), Is.EqualTo(0.5));
        });
    }
}

// <copyright file="FixedItemOptionsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.FixedItemOptions;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Unit tests for <see cref="FixedItemOptionsEngine"/>.
/// </summary>
[TestFixture]
public class FixedItemOptionsTests
{
    /// <summary>Verifies source/map/item filters and option pinning without duplicate options.</summary>
    [Test]
    public void PinsSkillLuckLevelOptionAndExcellentWithoutDuplicates()
    {
        var def = BuildSwordDefinition();
        var item = new TemporaryItem { Definition = def, Level = 2 };

        var cfg = new FixedItemOptionsConfiguration
        {
            Enabled = true,
            Rules =
            [
                new()
                {
                    Id = "event_swords",
                    Enabled = true,
                    ItemGroup = 0,
                    ItemNumber = 5,
                    SourceFilter = "box_event",
                    MapNumber = -1,
                    PinSkill = true,
                    PinLuck = true,
                    MinLevel = 7,
                    MinOptionLevel = 3,
                    MinExcellentCount = 2,
                    ForcedExcellentBits = 1,
                },
            ],
        };

        // Ignored when source is monster drop (rule specifies box_event) or plug-in inactive.
        Assert.That(FixedItemOptionsEngine.ApplyRules(item, cfg, plugInActive: false, "box_event", 0), Is.Zero);
        Assert.That(FixedItemOptionsEngine.ApplyRules(item, cfg, plugInActive: true, "drop", 0), Is.Zero);

        var applied = FixedItemOptionsEngine.ApplyRules(item, cfg, plugInActive: true, "box_event", 0);
        Assert.That(applied, Is.EqualTo(1));
        Assert.That(item.HasSkill, Is.True);
        Assert.That(item.Level, Is.EqualTo(7));
        Assert.That(item.ItemOptions.Count(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Luck)), Is.EqualTo(1));
        Assert.That(item.ItemOptions.First(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Option)).Level, Is.EqualTo(3));
        Assert.That(item.ItemOptions.Count(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent)), Is.EqualTo(2));
        Assert.That(item.ItemOptions.Any(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent) && o.ItemOption?.Number == 1), Is.True);

        // Re-applying must be idempotent (no duplicate Luck or Excellent entries).
        FixedItemOptionsEngine.ApplyRules(item, cfg, plugInActive: true, "box_event", 0);
        Assert.That(item.ItemOptions.Count(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Luck)), Is.EqualTo(1));
        Assert.That(item.ItemOptions.Count(o => Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent)), Is.EqualTo(2));
    }

    /// <summary>Verifies <see cref="FixedItemOptionsEngine.Matches"/> respects group, number, and map filters.</summary>
    [Test]
    public void RespectsGroupNumberAndMapFilters()
    {
        var def = BuildSwordDefinition();
        var item = new TemporaryItem { Definition = def };
        var rule = new FixedItemOptionRule
        {
            Enabled = true,
            ItemGroup = 0,
            ItemNumber = -1,
            SourceFilter = "drop",
            MapNumber = 2,
            PinLuck = true,
        };

        Assert.That(FixedItemOptionsEngine.Matches(rule, item, "drop", 0), Is.False);
        Assert.That(FixedItemOptionsEngine.Matches(rule, item, "box_event", 2), Is.False);
        Assert.That(FixedItemOptionsEngine.Matches(rule, item, "drop", 2), Is.True);
    }

    private static ItemDefinition BuildSwordDefinition()
    {
        var skill = new BasicModel.Skill { Id = Guid.NewGuid(), Number = 22, Name = "Slash" };
        var luckOpt = new BasicModel.ItemOptionDefinition { Id = Guid.NewGuid(), MaximumOptionsPerItem = 1 };
        luckOpt.PossibleOptions.Add(new BasicModel.IncreasableItemOption { Id = Guid.NewGuid(), OptionType = ItemOptionTypes.Luck, Number = 1 });

        var normalOpt = new BasicModel.ItemOptionDefinition { Id = Guid.NewGuid(), MaximumOptionsPerItem = 1 };
        var normalChoice = new BasicModel.IncreasableItemOption { Id = Guid.NewGuid(), OptionType = ItemOptionTypes.Option, Number = 1 };
        normalChoice.LevelDependentOptions.Add(new BasicModel.ItemOptionOfLevel { Id = Guid.NewGuid(), Level = 1 });
        normalChoice.LevelDependentOptions.Add(new BasicModel.ItemOptionOfLevel { Id = Guid.NewGuid(), Level = 2 });
        normalChoice.LevelDependentOptions.Add(new BasicModel.ItemOptionOfLevel { Id = Guid.NewGuid(), Level = 3 });
        normalOpt.PossibleOptions.Add(normalChoice);

        var excOpt = new BasicModel.ItemOptionDefinition { Id = Guid.NewGuid(), MaximumOptionsPerItem = 6 };
        excOpt.PossibleOptions.Add(new BasicModel.IncreasableItemOption { Id = Guid.NewGuid(), OptionType = ItemOptionTypes.Excellent, Number = 1 });
        excOpt.PossibleOptions.Add(new BasicModel.IncreasableItemOption { Id = Guid.NewGuid(), OptionType = ItemOptionTypes.Excellent, Number = 2 });
        excOpt.PossibleOptions.Add(new BasicModel.IncreasableItemOption { Id = Guid.NewGuid(), OptionType = ItemOptionTypes.Excellent, Number = 4 });

        var def = new BasicModel.ItemDefinition
        {
            Group = 0,
            Number = 5,
            Name = "Blade",
            MaximumItemLevel = 15,
            Skill = skill,
        };
        def.PossibleItemOptions.Add(luckOpt);
        def.PossibleItemOptions.Add(normalOpt);
        def.PossibleItemOptions.Add(excOpt);
        return def;
    }
}

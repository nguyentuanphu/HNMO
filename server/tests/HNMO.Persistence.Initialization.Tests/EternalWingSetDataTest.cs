// <copyright file="EternalWingSetDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the summoner set 44, the Eternal Wing set of the Season 6/9 server suites and of the clients' item data:
/// level 380, strength 62, agility 20, energy 170, durability 86, drop levels 116/131/122/102/99,
/// defense 35/49/41/29/32, no attack speed on the gloves.
/// </summary>
[TestFixture]
internal class EternalWingSetDataTest
{
    /// <summary>A new Season 6 database has the Eternal Wing set.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheEternalWingSetAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertSet(gameConfiguration);
    }

    /// <summary>The update turns the old Lilium values into the Eternal Wing set.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateTurnsTheOldSetIntoEternalWingAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        foreach (var (group, oldDropLevel, _, _) in EternalWingSetUpdatePlugIn.Pieces)
        {
            var item = GetItem(gameConfiguration, group);
            item.DropLevel = oldDropLevel;
            item.Durability = 80;
            item.BasePowerUpAttributes.Single(p => p.TargetAttribute == Stats.DefenseBase).BaseValue = 50;
            foreach (var requirement in item.Requirements.Where(r => r.Attribute == Stats.Level || r.Attribute == Stats.TotalEnergyRequirementValue).ToList())
            {
                item.Requirements.Remove(requirement);
            }

            item.Requirements.Single(r => r.Attribute == Stats.TotalStrengthRequirementValue).MinimumValue = 80;
            if (group == 10)
            {
                var speed = context.CreateNew<ItemBasePowerUpDefinition>();
                speed.TargetAttribute = Stats.AttackSpeedAny.GetPersistent(gameConfiguration);
                speed.BaseValue = 6;
                speed.AggregateType = AggregateType.AddRaw;
                item.BasePowerUpAttributes.Add(speed);
            }
        }

        var update = new EternalWingSetUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertSet(gameConfiguration);
    }

    private static ItemDefinition GetItem(GameConfiguration gameConfiguration, byte group)
        => gameConfiguration.Items.Single(i => i.Group == group && i.Number == 44);

    private static void AssertSet(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            foreach (var (group, _, dropLevel, defense) in EternalWingSetUpdatePlugIn.Pieces)
            {
                var item = GetItem(gameConfiguration, group);
                Assert.That(item.Name.ValueInNeutralLanguage, Does.StartWith("Eternal Wing"), $"{group}");
                Assert.That(item.DropLevel, Is.EqualTo(dropLevel), $"{group} drop level");
                Assert.That(item.Durability, Is.EqualTo(EternalWingSetUpdatePlugIn.Durability), $"{group} durability");
                Assert.That(item.BasePowerUpAttributes.Single(p => p.TargetAttribute == Stats.DefenseBase).BaseValue, Is.EqualTo(defense), $"{group} defense");
                Assert.That(item.BasePowerUpAttributes.Any(p => p.TargetAttribute == Stats.AttackSpeedAny), Is.False, $"{group} speed");
                Assert.That(Requirement(item, Stats.Level), Is.EqualTo(380), $"{group} level");
                Assert.That(Requirement(item, Stats.TotalStrengthRequirementValue), Is.EqualTo(62), $"{group} strength");
                Assert.That(Requirement(item, Stats.TotalAgilityRequirementValue), Is.EqualTo(20), $"{group} agility");
                Assert.That(Requirement(item, Stats.TotalEnergyRequirementValue), Is.EqualTo(170), $"{group} energy");
                Assert.That(item.Requirements.Count(r => r.Attribute == Stats.Level), Is.EqualTo(1), $"{group} one level requirement");
            }
        });
    }

    private static int Requirement(ItemDefinition item, AttributeDefinition attribute)
        => item.Requirements.SingleOrDefault(r => r.Attribute == attribute)?.MinimumValue ?? 0;
}

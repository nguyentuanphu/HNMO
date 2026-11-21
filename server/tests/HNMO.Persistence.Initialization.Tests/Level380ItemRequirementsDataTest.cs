// <copyright file="Level380ItemRequirementsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the requirements of the level 380 items (Season 6/9 server suites, item list: ReqLevel 380 for the 380 sets,
/// weapons and shields; ReqEnergy of the Destroy, Faith and Hades gloves; ReqVitality 85 of the Phoenix Soul set).
/// </summary>
[TestFixture]
internal class Level380ItemRequirementsDataTest
{
    /// <summary>A new Season 6 database has the suites' requirements.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuitesRequirementsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertRequirements(gameConfiguration);
    }

    /// <summary>The update adds the missing requirements once; a requirement the host set stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateAddsTheMissingRequirementsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        foreach (var (group, number, level, energy, vitality) in Level380ItemRequirementsUpdatePlugIn.Requirements)
        {
            var item = GetItem(gameConfiguration, group, number);
            foreach (var requirement in item.Requirements.ToList())
            {
                if ((level > 0 && requirement.Attribute == Stats.Level)
                    || (energy > 0 && requirement.Attribute == Stats.TotalEnergyRequirementValue)
                    || (vitality > 0 && requirement.Attribute == Stats.TotalVitalityRequirementValue))
                {
                    item.Requirements.Remove(requirement);
                }
            }
        }

        var hostLevel = context.CreateNew<DataModel.Configuration.Items.AttributeRequirement>();
        hostLevel.Attribute = Stats.Level.GetPersistent(gameConfiguration);
        hostLevel.MinimumValue = 350;
        GetItem(gameConfiguration, 9, 45).Requirements.Add(hostLevel);

        var update = new Level380ItemRequirementsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(GetRequirement(gameConfiguration, 9, 45, Stats.Level), Is.EqualTo(350), "the host's own requirement stays");
        GetItem(gameConfiguration, 9, 45).Requirements.Single(r => r.Attribute == Stats.Level).MinimumValue = 380;
        AssertRequirements(gameConfiguration);
    }

    private static void AssertRequirements(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            foreach (var (group, number, level, energy, vitality) in Level380ItemRequirementsUpdatePlugIn.Requirements)
            {
                var item = GetItem(gameConfiguration, group, number);
                if (level > 0)
                {
                    Assert.That(item.Requirements.Count(r => r.Attribute == Stats.Level), Is.EqualTo(1), $"{item.Name} level count");
                    Assert.That(GetRequirement(gameConfiguration, group, number, Stats.Level), Is.EqualTo(level), $"{item.Name} level");
                }

                if (energy > 0)
                {
                    Assert.That(GetRequirement(gameConfiguration, group, number, Stats.TotalEnergyRequirementValue), Is.EqualTo(energy), $"{item.Name} energy");
                }

                if (vitality > 0)
                {
                    Assert.That(GetRequirement(gameConfiguration, group, number, Stats.TotalVitalityRequirementValue), Is.EqualTo(vitality), $"{item.Name} vitality");
                }
            }

            Assert.That(Level380ItemRequirementsUpdatePlugIn.Requirements, Has.Count.EqualTo(7 + 38 + 4));
        });
    }

    private static DataModel.Configuration.Items.ItemDefinition GetItem(GameConfiguration gameConfiguration, byte group, short number)
        => gameConfiguration.Items.Single(i => i.Group == group && i.Number == number);

    private static int GetRequirement(GameConfiguration gameConfiguration, byte group, short number, AttributeDefinition attribute)
        => GetItem(gameConfiguration, group, number).Requirements.SingleOrDefault(r => r.Attribute == attribute)?.MinimumValue ?? 0;
}

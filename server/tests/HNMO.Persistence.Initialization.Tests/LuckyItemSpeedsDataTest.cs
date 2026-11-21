// <copyright file="LuckyItemSpeedsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the attack speed of the lucky gloves and the walk speed of the lucky boots (Season 6/9 server suites,
/// item list: e.g. Scale Gloves (10, 62) speed 10, Scale Boots (11, 62) walk speed 8).
/// </summary>
[TestFixture]
internal class LuckyItemSpeedsDataTest
{
    /// <summary>A new Season 6 database has the speeds, once each.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSpeedsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertSpeeds(gameConfiguration);
    }

    /// <summary>The update adds the speeds once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateAddsTheSpeedsOnceAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        foreach (var (group, number, _) in LuckyItemSpeedsUpdatePlugIn.Speeds)
        {
            var item = gameConfiguration.Items.Single(i => i.Group == group && i.Number == number);
            foreach (var powerUp in item.BasePowerUpAttributes.Where(p => p.TargetAttribute == Stats.AttackSpeedAny || p.TargetAttribute == Stats.WalkSpeed).ToList())
            {
                item.BasePowerUpAttributes.Remove(powerUp);
            }
        }

        var update = new LuckyItemSpeedsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertSpeeds(gameConfiguration);
    }

    private static void AssertSpeeds(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            foreach (var (group, number, speed) in LuckyItemSpeedsUpdatePlugIn.Speeds)
            {
                var item = gameConfiguration.Items.Single(i => i.Group == group && i.Number == number);
                var attribute = group == 10 ? Stats.AttackSpeedAny : Stats.WalkSpeed;
                var powerUps = item.BasePowerUpAttributes.Where(p => p.TargetAttribute == attribute).ToList();
                Assert.That(powerUps, Has.Count.EqualTo(1), $"{group},{number}");
                Assert.That(powerUps.Select(p => p.BaseValue), Is.All.EqualTo(speed), $"{group},{number}");
            }
        });
    }
}

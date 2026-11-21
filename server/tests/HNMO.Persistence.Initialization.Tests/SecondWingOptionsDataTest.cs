// <copyright file="SecondWingOptionsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the 2nd wings' excellent options (Season 6/9 server suites: life, mana, ignore defense 3 %, maximum AG +50,
/// attack speed +5) and that the Cape of Fighter keeps only its three options.
/// </summary>
[TestFixture]
internal class SecondWingOptionsDataTest
{
    private static readonly short[] SecondWings = [3, 4, 5, 6, 42];

    /// <summary>A new Season 6 database has the five 2nd wing options; the Cape of Fighter has three.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheFiveOptionsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertOptions(gameConfiguration);
    }

    /// <summary>The update adds the two options and splits the Cape of Fighter's options off, once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateAddsTheOptionsOnceAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var secondWings = SecondWingDefinition(gameConfiguration);
        foreach (var option in secondWings.PossibleOptions.Where(o => o.Number > 3).ToList())
        {
            secondWings.PossibleOptions.Remove(option);
        }

        var capeOfFighter = gameConfiguration.Items.Single(i => i.Group == 12 && i.Number == 49);
        var capeOptions = capeOfFighter.PossibleItemOptions.Single(o => o.GetId() == GuidHelper.CreateGuid<ItemOptionDefinition>(SecondWingOptionsUpdatePlugIn.CapeOfFighterOptionsNumber));
        capeOfFighter.PossibleItemOptions.Remove(capeOptions);
        gameConfiguration.ItemOptions.Remove(capeOptions);
        capeOfFighter.PossibleItemOptions.Add(secondWings);

        var update = new SecondWingOptionsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertOptions(gameConfiguration);
    }

    private static ItemOptionDefinition SecondWingDefinition(GameConfiguration gameConfiguration)
        => gameConfiguration.ItemOptions.Single(o => o.GetId() == GuidHelper.CreateGuid<ItemOptionDefinition>(ItemOptionDefinitionNumbers.Wing2nd));

    private static void AssertOptions(GameConfiguration gameConfiguration)
    {
        var secondWings = SecondWingDefinition(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(secondWings.PossibleOptions.Select(o => o.Number).OrderBy(n => n), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            var ag = secondWings.PossibleOptions.Single(o => o.Number == 4).PowerUpDefinition!;
            Assert.That(ag.TargetAttribute, Is.EqualTo(Stats.MaximumAbility));
            Assert.That(ag.Boost!.ConstantValue.Value, Is.EqualTo(50));
            var speed = secondWings.PossibleOptions.Single(o => o.Number == 5).PowerUpDefinition!;
            Assert.That(speed.TargetAttribute, Is.EqualTo(Stats.AttackSpeedAny));
            Assert.That(speed.Boost!.ConstantValue.Value, Is.EqualTo(5));

            foreach (var number in SecondWings)
            {
                var wing = gameConfiguration.Items.Single(i => i.Group == 12 && i.Number == number);
                Assert.That(wing.PossibleItemOptions, Does.Contain(secondWings), $"wing {number}");
            }

            var capeOfFighter = gameConfiguration.Items.Single(i => i.Group == 12 && i.Number == 49);
            Assert.That(capeOfFighter.PossibleItemOptions, Does.Not.Contain(secondWings));
            var capeOptions = capeOfFighter.PossibleItemOptions.Single(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Wing));
            Assert.That(capeOptions.PossibleOptions.Select(o => o.Number).OrderBy(n => n), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(capeOptions.PossibleOptions.Single(o => o.Number == 1).LevelDependentOptions, Has.Count.EqualTo(secondWings.PossibleOptions.Single(o => o.Number == 1).LevelDependentOptions.Count));
            Assert.That(gameConfiguration.ItemOptions.Count(o => o.GetId() == capeOptions.GetId()), Is.EqualTo(1));
        });
    }
}

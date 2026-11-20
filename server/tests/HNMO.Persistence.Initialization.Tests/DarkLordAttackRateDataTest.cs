// <copyright file="DarkLordAttackRateDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests the Dark Lord attack rate (PvM) of the season 6 data against the Season 6/9 server suites:
/// level * 5 + agility * 3 + strength / 4 + command / 10.
/// </summary>
[TestFixture]
internal class DarkLordAttackRateDataTest
{
    private static readonly CharacterClassNumber[] DarkLordClasses = [CharacterClassNumber.DarkLord, CharacterClassNumber.LordEmperor];

    /// <summary>
    /// A new database has the Dark Lord attack rate of the server suites.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasDarkLordAttackRateAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        foreach (var number in DarkLordClasses)
        {
            AssertAttackRate(gameConfiguration, number);
        }
    }

    /// <summary>
    /// A database made with agility * 2.5 and strength / 6 gets the new factors; applying the update twice is safe.
    /// </summary>
    [Test]
    public async Task UpdateSetsDarkLordAttackRateAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        foreach (var number in DarkLordClasses)
        {
            GetRelationship(gameConfiguration, number, Stats.TotalAgility).InputOperand = 2.5f;
            GetRelationship(gameConfiguration, number, Stats.TotalStrength).InputOperand = 1.0f / 6;
        }

        var update = new DarkLordAttackRateUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        foreach (var number in DarkLordClasses)
        {
            AssertAttackRate(gameConfiguration, number);
        }

        Assert.That(GetRelationship(gameConfiguration, CharacterClassNumber.DarkKnight, Stats.TotalAgility).InputOperand, Is.EqualTo(1.5f), "Other classes keep their attack rate.");
    }

    private static void AssertAttackRate(GameConfiguration gameConfiguration, CharacterClassNumber number)
    {
        Assert.Multiple(() =>
        {
            Assert.That(GetRelationship(gameConfiguration, number, Stats.TotalLevel).InputOperand, Is.EqualTo(5f), number.ToString());
            Assert.That(GetRelationship(gameConfiguration, number, Stats.TotalAgility).InputOperand, Is.EqualTo(3f), number.ToString());
            Assert.That(GetRelationship(gameConfiguration, number, Stats.TotalStrength).InputOperand, Is.EqualTo(0.25f), number.ToString());
            Assert.That(GetRelationship(gameConfiguration, number, Stats.TotalLeadership).InputOperand, Is.EqualTo(0.1f).Within(0.0001f), number.ToString());
        });
    }

    private static AttributeRelationship GetRelationship(GameConfiguration gameConfiguration, CharacterClassNumber number, AttributeDefinition input)
    {
        var characterClass = gameConfiguration.CharacterClasses.Single(c => c.Number == (byte)number);
        return characterClass.AttributeCombinations.Single(r => r.TargetAttribute == Stats.AttackRatePvm && r.InputAttribute == input);
    }

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return (context, gameConfiguration);
    }
}

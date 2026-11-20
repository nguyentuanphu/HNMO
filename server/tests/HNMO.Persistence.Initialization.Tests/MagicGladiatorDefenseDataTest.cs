// <copyright file="MagicGladiatorDefenseDataTest.cs" company="HNMO">
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
/// Tests the Magic Gladiator defense of the season 6 data against the Season 6/9 server suites.
/// </summary>
[TestFixture]
internal class MagicGladiatorDefenseDataTest
{
    /// <summary>
    /// The Magic Gladiator's defense is agility / 4 (Season 6/9 server suites), like the Dark Wizard's.
    /// </summary>
    [Test]
    public async Task MagicGladiatorDefenseIsAgilityQuarterAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        foreach (var number in new[] { CharacterClassNumber.MagicGladiator, CharacterClassNumber.DuelMaster })
        {
            var relationship = GetRelationship(gameConfiguration, number, Stats.DefenseBase, Stats.TotalAgility);
            Assert.That(relationship.InputOperand, Is.EqualTo(0.25f), number.ToString());
        }
    }

    /// <summary>
    /// A database made with agility / 5 gets agility / 4 for the Magic Gladiator; applying the update twice is safe.
    /// </summary>
    [Test]
    public async Task UpdateSetsMagicGladiatorDefenseAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        foreach (var number in new[] { CharacterClassNumber.MagicGladiator, CharacterClassNumber.DuelMaster })
        {
            GetRelationship(gameConfiguration, number, Stats.DefenseBase, Stats.TotalAgility).InputOperand = 1.0f / 5;
        }

        var update = new MagicGladiatorDefenseUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        foreach (var number in new[] { CharacterClassNumber.MagicGladiator, CharacterClassNumber.DuelMaster })
        {
            Assert.That(GetRelationship(gameConfiguration, number, Stats.DefenseBase, Stats.TotalAgility).InputOperand, Is.EqualTo(0.25f), number.ToString());
        }

        Assert.That(GetRelationship(gameConfiguration, CharacterClassNumber.DarkKnight, Stats.DefenseBase, Stats.TotalAgility).InputOperand, Is.EqualTo(1.0f / 3), "Other classes keep their defense.");
    }

    private static AttributeRelationship GetRelationship(GameConfiguration gameConfiguration, CharacterClassNumber number, AttributeDefinition target, AttributeDefinition input)
    {
        var characterClass = gameConfiguration.CharacterClasses.Single(c => c.Number == (byte)number);
        return characterClass.AttributeCombinations.Single(r => r.TargetAttribute == target && r.InputAttribute == input);
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

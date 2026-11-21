// <copyright file="SecondClassItemsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the items of class rank 2 (Season 6/9 server suites, item list: e.g. Grand Soul set DarkWizard="2",
/// Dark Phoenix set DarkKnight="2"): the first class can't wear them, the second and third class can.
/// </summary>
[TestFixture]
internal class SecondClassItemsDataTest
{
    /// <summary>First class number to the second class number.</summary>
    private static readonly Dictionary<byte, byte> SecondClassOf = new() { [0] = 2, [4] = 6, [8] = 10, [20] = 22 };

    /// <summary>A new Season 6 database gives the rank 2 items to the second class and up only; rank 1 items stay.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasSecondClassItemsAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertItems(gameConfiguration);
    }

    /// <summary>The update removes the first class from the rank 2 items.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateRemovesTheFirstClassAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        foreach (var (group, number, firstClass) in SecondClassItemsUpdatePlugIn.Items)
        {
            var item = gameConfiguration.Items.Single(i => i.Group == group && i.Number == number);
            item.QualifiedCharacters.Add(gameConfiguration.CharacterClasses.Single(c => c.Number == firstClass));
        }

        var update = new SecondClassItemsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertItems(gameConfiguration);
    }

    private static void AssertItems(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            Assert.That(SecondClassItemsUpdatePlugIn.Items, Has.Count.EqualTo(102));
            foreach (var (group, number, firstClass) in SecondClassItemsUpdatePlugIn.Items)
            {
                var item = gameConfiguration.Items.Single(i => i.Group == group && i.Number == number);
                var classes = item.QualifiedCharacters.Select(c => c.Number).ToList();
                Assert.That(classes, Does.Not.Contain(firstClass), $"{item.Name} first class");
                Assert.That(classes, Does.Contain(SecondClassOf[firstClass]), $"{item.Name} second class");
            }

            // Rank 1 items stay with the first class: Kris (0, 0) for every class, Bronze Helm (7, 0) for the knight.
            Assert.That(gameConfiguration.Items.Single(i => i.Group == 0 && i.Number == 0).QualifiedCharacters.Select(c => c.Number), Does.Contain((byte)0).And.Contain((byte)4));
            Assert.That(gameConfiguration.Items.Single(i => i.Group == 7 && i.Number == 0).QualifiedCharacters.Select(c => c.Number), Does.Contain((byte)4));
        });
    }
}

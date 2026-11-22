// <copyright file="FenrirSecondClassDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Verifies that Horn of Fenrir (13, 37) requires second class and up, matching the Season 6 and
/// Season 9 server suites.
/// </summary>
[TestFixture]
internal class FenrirSecondClassDataTest
{
    private static readonly byte[] ExcludedFirstClasses =
    [
        (byte)CharacterClassNumber.DarkWizard,
        (byte)CharacterClassNumber.DarkKnight,
        (byte)CharacterClassNumber.FairyElf,
        (byte)CharacterClassNumber.Summoner,
    ];

    private static readonly byte[] AllowedSecondClassAndUp =
    [
        (byte)CharacterClassNumber.SoulMaster,
        (byte)CharacterClassNumber.GrandMaster,
        (byte)CharacterClassNumber.BladeKnight,
        (byte)CharacterClassNumber.BladeMaster,
        (byte)CharacterClassNumber.MuseElf,
        (byte)CharacterClassNumber.HighElf,
        (byte)CharacterClassNumber.MagicGladiator,
        (byte)CharacterClassNumber.DuelMaster,
        (byte)CharacterClassNumber.DarkLord,
        (byte)CharacterClassNumber.LordEmperor,
        (byte)CharacterClassNumber.BloodySummoner,
        (byte)CharacterClassNumber.DimensionMaster,
        (byte)CharacterClassNumber.RageFighter,
        (byte)CharacterClassNumber.FistMaster,
    ];

    /// <summary>
    /// A fresh Season 6 database assigns Horn of Fenrir (13, 37) only to second class and up.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseRestrictsFenrirToSecondClassAndUpAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertFenrirClasses(gameConfiguration);
    }

    /// <summary>
    /// The mandatory update removes the four first classes when all four are still assigned,
    /// and preserves host customizations when at least one first class was already changed.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateRemovesFirstClassesAndPreservesHostChangesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var fenrir = gameConfiguration.Items.Single(i => i.Group == 13 && i.Number == 37);
        foreach (var firstClass in ExcludedFirstClasses)
        {
            fenrir.QualifiedCharacters.Add(gameConfiguration.CharacterClasses.Single(c => c.Number == firstClass));
        }

        var update = new FenrirSecondClassUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(update.IsMandatory, Is.True);
        AssertFenrirClasses(gameConfiguration);

        // Host explicitly allows DarkKnight only among first classes; re-running leaves it untouched.
        var darkKnight = gameConfiguration.CharacterClasses.Single(c => c.Number == (byte)CharacterClassNumber.DarkKnight);
        fenrir.QualifiedCharacters.Add(darkKnight);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(fenrir.QualifiedCharacters.Select(c => c.Number), Does.Contain((byte)CharacterClassNumber.DarkKnight));
    }

    private static void AssertFenrirClasses(GameConfiguration gameConfiguration)
    {
        var fenrir = gameConfiguration.Items.Single(i => i.Group == 13 && i.Number == 37);
        var classes = fenrir.QualifiedCharacters.Select(c => c.Number).ToList();

        Assert.Multiple(() =>
        {
            foreach (var excluded in ExcludedFirstClasses)
            {
                Assert.That(classes, Does.Not.Contain(excluded), $"First class {excluded} must not qualify for Horn of Fenrir.");
            }

            foreach (var allowed in AllowedSecondClassAndUp)
            {
                Assert.That(classes, Does.Contain(allowed), $"Class {allowed} must qualify for Horn of Fenrir.");
            }
        });
    }
}

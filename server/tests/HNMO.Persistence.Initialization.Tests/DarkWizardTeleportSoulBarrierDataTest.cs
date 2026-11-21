// <copyright file="DarkWizardTeleportSoulBarrierDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests that the Dark Wizard can use Teleport (6) and Soul Barrier (16), as in the Season 6/9 server suites
/// (skill list: Dark Wizard rank 1), like the scrolls which teach them.
/// </summary>
[TestFixture]
internal class DarkWizardTeleportSoulBarrierDataTest
{
    private static readonly byte[] Wizards = [(byte)CharacterClassNumber.DarkWizard, (byte)CharacterClassNumber.SoulMaster, (byte)CharacterClassNumber.GrandMaster];

    /// <summary>A new Season 6 database lets all three wizard classes use both skills, and only them.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseLetsTheDarkWizardUseThemAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        AssertWizardsOnly(gameConfiguration);
    }

    /// <summary>The update adds the Dark Wizard once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateAddsTheDarkWizardOnceAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var darkWizard = gameConfiguration.CharacterClasses.Single(c => c.Number == (byte)CharacterClassNumber.DarkWizard);
        foreach (var number in new short[] { 6, 16 })
        {
            GetSkill(gameConfiguration, number).QualifiedCharacters.Remove(darkWizard);
        }

        var update = new DarkWizardTeleportSoulBarrierUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertWizardsOnly(gameConfiguration);
    }

    private static void AssertWizardsOnly(GameConfiguration gameConfiguration)
    {
        Assert.Multiple(() =>
        {
            foreach (var number in new short[] { 6, 16 })
            {
                var classes = GetSkill(gameConfiguration, number).QualifiedCharacters.Select(c => c.Number).OrderBy(n => n);
                Assert.That(classes, Is.EqualTo(Wizards), $"skill {number}");
            }

            // The scrolls which teach them can be read by the same classes.
            foreach (var scroll in new short[] { 5, 15 })
            {
                var classes = gameConfiguration.Items.Single(i => i.Group == 15 && i.Number == scroll).QualifiedCharacters.Select(c => c.Number).OrderBy(n => n);
                Assert.That(classes, Is.EqualTo(Wizards), $"scroll {scroll}");
            }
        });
    }

    private static Skill GetSkill(GameConfiguration gameConfiguration, short number)
        => gameConfiguration.Skills.Single(s => s.Number == number);
}

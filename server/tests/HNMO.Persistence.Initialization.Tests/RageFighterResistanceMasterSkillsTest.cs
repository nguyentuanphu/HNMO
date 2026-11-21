// <copyright file="RageFighterResistanceMasterSkillsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// The Rage Fighter's resistance master skills add as much resistance as the same skills of the other classes.
/// </summary>
[TestFixture]
internal class RageFighterResistanceMasterSkillsTest
{
    /// <summary>A new database: the Rage Fighter's skills have the formula of the other classes' (1.2 to 9.0).</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSameFormulaAsTheOtherClassesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var others = Formula(gameConfiguration, SkillNumber.PoisonResistanceInc);
        foreach (var number in RageFighterResistanceMasterSkillsUpdatePlugIn.Skills)
        {
            Assert.That(Formula(gameConfiguration, number), Is.EqualTo(SkillsInitializer.Formula120), number.ToString());
        }

        Assert.That(others, Is.EqualTo(SkillsInitializer.Formula120), "the other classes' poison resistance skill (after its update)");
    }

    /// <summary>The update changes the old hundredth; a formula the host changed stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesTheOldFormulaAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        var poison = Definition(gameConfiguration, SkillNumber.IncreasePoisonResistance);
        var ice = Definition(gameConfiguration, SkillNumber.IncreaseIceResistance);
        poison.ValueFormula = SkillsInitializer.Formula120Value;
        ice.ValueFormula = "level";

        await new RageFighterResistanceMasterSkillsUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(poison.ValueFormula, Is.EqualTo(SkillsInitializer.Formula120));
        Assert.That(ice.ValueFormula, Is.EqualTo("level"), "the host's own formula stays");
    }

    private static MasterSkillDefinition Definition(GameConfiguration gameConfiguration, SkillNumber number)
        => gameConfiguration.Skills.First(s => s.Number == (short)number).MasterDefinition!;

    private static string Formula(GameConfiguration gameConfiguration, SkillNumber number) => Definition(gameConfiguration, number).ValueFormula;
}

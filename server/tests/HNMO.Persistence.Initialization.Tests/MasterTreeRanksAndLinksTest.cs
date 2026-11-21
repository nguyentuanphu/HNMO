// <copyright file="MasterTreeRanksAndLinksTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Ranks and required skills of the master tree as in <c>MasterSkillTree.xml</c>.
/// </summary>
[TestFixture]
internal class MasterTreeRanksAndLinksTest
{
    /// <summary>A new database has the tree file's ranks and links.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseFollowsTheTreeFileAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        Assert.That(Definition(gameConfiguration, SkillNumber.LightningResistanceInc).Rank, Is.EqualTo(3));
        Assert.That(Definition(gameConfiguration, SkillNumber.IceResistanceIncrease).Rank, Is.EqualTo(4));
        Assert.That(Required(gameConfiguration, SkillNumber.SummonedMonsterStr2), Is.EqualTo(new[] { (short)SkillNumber.SummonedMonsterStr1 }));
        Assert.That(Required(gameConfiguration, SkillNumber.CommandAttackInc), Is.EqualTo(new[] { (short)SkillNumber.UseScepterPetStr }));
    }

    /// <summary>The update moves the old values; a rank the host changed stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateMovesTheOldValuesAsync()
    {
        var (context, gameConfiguration) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;
        Definition(gameConfiguration, SkillNumber.LightningResistanceInc).Rank = 2;
        Definition(gameConfiguration, SkillNumber.IceResistanceIncrease).Rank = 5;
        var summonedStr2 = Definition(gameConfiguration, SkillNumber.SummonedMonsterStr2);
        summonedStr2.RequiredMasterSkills.Clear();
        summonedStr2.RequiredMasterSkills.Add(gameConfiguration.Skills.First(s => s.Number == (short)SkillNumber.SummonGoblin));
        Definition(gameConfiguration, SkillNumber.CommandAttackInc).RequiredMasterSkills.Clear();

        await new MasterTreeRanksAndLinksUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(Definition(gameConfiguration, SkillNumber.LightningResistanceInc).Rank, Is.EqualTo(3));
        Assert.That(Definition(gameConfiguration, SkillNumber.IceResistanceIncrease).Rank, Is.EqualTo(5), "the host's own rank stays");
        Assert.That(Required(gameConfiguration, SkillNumber.SummonedMonsterStr2), Is.EqualTo(new[] { (short)SkillNumber.SummonedMonsterStr1 }));
        Assert.That(Required(gameConfiguration, SkillNumber.CommandAttackInc), Is.EqualTo(new[] { (short)SkillNumber.UseScepterPetStr }));
    }

    private static MasterSkillDefinition Definition(GameConfiguration gameConfiguration, SkillNumber number)
        => gameConfiguration.Skills.First(s => s.Number == (short)number).MasterDefinition!;

    private static short[] Required(GameConfiguration gameConfiguration, SkillNumber number)
        => Definition(gameConfiguration, number).RequiredMasterSkills.Select(s => s.Number).ToArray();
}

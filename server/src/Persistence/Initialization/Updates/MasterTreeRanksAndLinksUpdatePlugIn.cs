// <copyright file="MasterTreeRanksAndLinksUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.PlugIns;

/// <summary>
/// Puts four master skills where the master tree file (<c>MasterSkillTree.xml</c>) has them:
/// Lightning Resistance at rank 3 and Ice Resistance at rank 4 (both were rank 2, so the resistance chain didn't open
/// the ranks above it), Summoned Monster Strengthener 2 after Strengthener 1 (it needed only Summon Goblin), and
/// Command Attack Increase after Scepter: Pet Strengthener (it needed nothing). Values the host changed are kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A6C34E91-2D7B-4F05-8E19-7B3D52C0F6A8")]
public class MasterTreeRanksAndLinksUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Master tree ranks and links";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Lightning Resistance at rank 3, Ice Resistance at rank 4, Summoned Monster Str 2 after Str 1, Command Attack Increase after Scepter Pet Str, as in the master tree.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 21, 10, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        SetRank(gameConfiguration, SkillNumber.LightningResistanceInc, 3);
        SetRank(gameConfiguration, SkillNumber.IceResistanceIncrease, 4);

        if (Definition(gameConfiguration, SkillNumber.SummonedMonsterStr2) is { } summonedStr2
            && summonedStr2.RequiredMasterSkills.Count == 1
            && summonedStr2.RequiredMasterSkills.First().Number == (short)SkillNumber.SummonGoblin
            && Skill(gameConfiguration, SkillNumber.SummonedMonsterStr1) is { } summonedStr1)
        {
            summonedStr2.RequiredMasterSkills.Clear();
            summonedStr2.RequiredMasterSkills.Add(summonedStr1);
        }

        if (Definition(gameConfiguration, SkillNumber.CommandAttackInc) is { RequiredMasterSkills.Count: 0 } commandAttack
            && Skill(gameConfiguration, SkillNumber.UseScepterPetStr) is { } scepterPet)
        {
            commandAttack.RequiredMasterSkills.Add(scepterPet);
        }

        return ValueTask.CompletedTask;
    }

    private static void SetRank(GameConfiguration gameConfiguration, SkillNumber number, byte rank)
    {
        if (Definition(gameConfiguration, number) is { Rank: 2 } definition)
        {
            definition.Rank = rank;
        }
    }

    private static Skill? Skill(GameConfiguration gameConfiguration, SkillNumber number)
        => gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)number);

    private static MasterSkillDefinition? Definition(GameConfiguration gameConfiguration, SkillNumber number)
        => Skill(gameConfiguration, number)?.MasterDefinition;
}

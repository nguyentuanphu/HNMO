// <copyright file="AbolishMagicNoDamageUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// This update stops the Dark Lord's Abolish Magic (skill 72) from hitting its target with the lord's full damage.
/// It was saved as a direct hit skill without a damage type, so the server attacked with it; in classic MU it doesn't
/// do damage. Only a save which still has it as a direct hit skill is changed.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("4AD92A37-9561-418B-B0DF-FCB8BA8E56DA")]
public class AbolishMagicNoDamageUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Abolish Magic does no damage";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The Dark Lord's Abolish Magic no longer hits its target with damage.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.AbolishMagic) is { SkillType: SkillType.DirectHit, DamageType: DamageType.None } skill)
        {
            skill.SkillType = SkillType.Other;
        }

        return ValueTask.CompletedTask;
    }
}

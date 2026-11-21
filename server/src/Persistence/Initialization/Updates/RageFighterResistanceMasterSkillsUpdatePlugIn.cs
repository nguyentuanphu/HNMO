// <copyright file="RageFighterResistanceMasterSkillsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.PlugIns;

/// <summary>
/// The Rage Fighter's poison, lightning and ice resistance master skills add resistance points (1.2 to 9.0 by level), as
/// the same skills of the other classes do since <see cref="FixJeweleryPetsDamageCalcsPlugInSeason6"/>. They added a
/// hundredth of that (0.012 to 0.09), which hardly changes the chance to be poisoned, shocked or frozen.
/// A formula the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5E0B7C21-9A43-4F18-B6D2-3C8E1A7F4D90")]
public class RageFighterResistanceMasterSkillsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Rage Fighter resistance master skills";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The Rage Fighter's poison, lightning and ice resistance master skills add 1.2 to 9.0 resistance (was a hundredth of it), like the other classes.";

    /// <summary>The skills.</summary>
    internal static readonly SkillNumber[] Skills = [SkillNumber.IncreasePoisonResistance, SkillNumber.IncreaseLightningResistance, SkillNumber.IncreaseIceResistance];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 21, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var number in Skills)
        {
            if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)number)?.MasterDefinition is { ValueFormula: SkillsInitializer.Formula120Value } definition)
            {
                definition.ValueFormula = SkillsInitializer.Formula120;
            }
        }

        return ValueTask.CompletedTask;
    }
}

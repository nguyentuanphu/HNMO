// <copyright file="MagicGladiatorDefenseUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.PlugIns;

/// <summary>
/// This update sets the defense of the Magic Gladiator and the Duel Master to agility / 4, as in the Season 6/9
/// server suites (it was agility / 5). A value the host changed by hand is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("3C9E5B27-81D4-4A6F-9E02-B7F4D1A86C53")]
public class MagicGladiatorDefenseUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Magic Gladiator defense: agility / 4";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Sets the defense of the Magic Gladiator and the Duel Master to agility / 4 instead of agility / 5.";

    private const float OldOperand = 1.0f / 5;

    private const float NewOperand = 1.0f / 4;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var characterClass in gameConfiguration.CharacterClasses.Where(c => c.Number is (byte)CharacterClassNumber.MagicGladiator or (byte)CharacterClassNumber.DuelMaster))
        {
            if (characterClass.AttributeCombinations.FirstOrDefault(r => r.TargetAttribute == Stats.DefenseBase && r.InputAttribute == Stats.TotalAgility) is { } relationship
                && Math.Abs(relationship.InputOperand - OldOperand) < 0.0001f)
            {
                relationship.InputOperand = NewOperand;
            }
        }

        return ValueTask.CompletedTask;
    }
}

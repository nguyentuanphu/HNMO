// <copyright file="DarkLordAttackRateUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.PlugIns;

/// <summary>
/// This update sets the attack rate (PvM) of the Dark Lord and the Lord Emperor to
/// level * 5 + agility * 3 + strength / 4 + command / 10, as in the Season 6/9 server suites
/// (it was agility * 2.5 and strength / 6). Values the host changed by hand are kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("8A41D6F0-2B7C-4E95-A3D8-5F06C9B21E74")]
public class DarkLordAttackRateUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Dark Lord attack rate: agility * 3, strength / 4";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Sets the Dark Lord's attack rate against monsters to level * 5 + agility * 3 + strength / 4 + command / 10.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 13, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var characterClass in gameConfiguration.CharacterClasses.Where(c => c.Number is (byte)CharacterClassNumber.DarkLord or (byte)CharacterClassNumber.LordEmperor))
        {
            Replace(characterClass, Stats.TotalAgility, 2.5f, 3f);
            Replace(characterClass, Stats.TotalStrength, 1.0f / 6, 1.0f / 4);
        }

        return ValueTask.CompletedTask;
    }

    private static void Replace(CharacterClass characterClass, AttributeDefinition input, float oldOperand, float newOperand)
    {
        if (characterClass.AttributeCombinations.FirstOrDefault(r => r.TargetAttribute == Stats.AttackRatePvm && r.InputAttribute == input) is { } relationship
            && Math.Abs(relationship.InputOperand - oldOperand) < 0.0001f)
        {
            relationship.InputOperand = newOperand;
        }
    }
}

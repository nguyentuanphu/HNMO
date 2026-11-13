// <copyright file="AddEnergyBallForDarkWizard.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Adds the energy ball skill to a created dark wizard character.
/// </summary>
[Guid("6A721FED-51CD-41AD-BA7C-EC1642FFF00A")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AddEnergyBallForDarkWizard_Name), Description = nameof(PlugInResources.AddEnergyBallForDarkWizard_Description), ResourceType = typeof(PlugInResources))]
public class AddEnergyBallForDarkWizard : AddInitialSkillPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddEnergyBallForDarkWizard" /> class.
    /// </summary>
    public AddEnergyBallForDarkWizard()
        : base((byte)CharacterClassNumber.DarkWizard, (ushort)SkillNumber.EnergyBall)
    {
    }
}
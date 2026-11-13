// <copyright file="AddSpiralSlashForMagicGladiator.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Adds the Spiral Slash skill (active in castle siege) to a created magic gladiator character.
/// </summary>
[Guid("83B0D163-7E97-40FC-851A-D5500B4BB33E")]
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddSpiralSlashForMagicGladiator_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddSpiralSlashForMagicGladiator_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
public class AddSpiralSlashForMagicGladiator : AddInitialSkillPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddSpiralSlashForMagicGladiator"/> class.
    /// </summary>
    public AddSpiralSlashForMagicGladiator()
        : base((byte)CharacterClassNumber.MagicGladiator, (ushort)SkillNumber.SpiralSlash)
    {
    }
}
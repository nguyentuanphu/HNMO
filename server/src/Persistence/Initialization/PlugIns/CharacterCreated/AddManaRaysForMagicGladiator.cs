// <copyright file="AddManaRaysForMagicGladiator.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Adds the Mana Rays skill (active in castle siege) to a created magic gladiator character.
/// </summary>
[Guid("D83347DB-4D33-47E9-B898-5EDB2777B8A1")]
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddManaRaysForMagicGladiator_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddManaRaysForMagicGladiator_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
public class AddManaRaysForMagicGladiator : AddInitialSkillPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddManaRaysForMagicGladiator"/> class.
    /// </summary>AddManaRaysForMagicGladiator.cs
    public AddManaRaysForMagicGladiator()
        : base((byte)CharacterClassNumber.MagicGladiator, (ushort)SkillNumber.ManaRays)
    {
    }
}
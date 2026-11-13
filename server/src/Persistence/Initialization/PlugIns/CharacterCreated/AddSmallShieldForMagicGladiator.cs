// <copyright file="AddSmallShieldForMagicGladiator.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Items;
using HNMO.PlugIns;

/// <summary>
/// Adds a small shield to a created magic gladiator character.
/// </summary>
[Guid("74FC7D85-7AA0-4437-88BA-CE008FD31745")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AddSmallShieldForMagicGladiator_Name), Description = nameof(PlugInResources.AddSmallShieldForMagicGladiator_Description), ResourceType = typeof(PlugInResources))]
public class AddSmallShieldForMagicGladiator : AddInitialItemPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddSmallShieldForMagicGladiator" /> class.
    /// </summary>
    public AddSmallShieldForMagicGladiator()
        : base((byte)CharacterClassNumber.MagicGladiator, (byte)ItemGroups.Shields, 0, 1)
    {
    }
}
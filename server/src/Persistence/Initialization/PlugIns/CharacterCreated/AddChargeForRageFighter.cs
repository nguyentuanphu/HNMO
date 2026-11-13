// <copyright file="AddChargeForRageFighter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Adds the Charge skill to a created rage fighter character.
/// </summary>
[Guid("53F23DDD-3676-4D24-8DFF-2EF657255832")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AddChargeForRageFighter_Name), Description = nameof(PlugInResources.AddChargeForRageFighter_Description), ResourceType = typeof(PlugInResources))]
public class AddChargeForRageFighter : AddInitialSkillPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddChargeForRageFighter"/> class.
    /// </summary>
    public AddChargeForRageFighter()
        : base((byte)CharacterClassNumber.RageFighter, (ushort)SkillNumber.Charge)
    {
    }
}
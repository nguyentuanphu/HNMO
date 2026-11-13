// <copyright file="AddLanceForSummoner.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Adds the Lance skill to a created summoner character.
/// </summary>
[Guid("AC326833-D60E-4705-A7DF-740FA37ACBA8")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AddLanceForSummoner_Name), Description = nameof(PlugInResources.AddLanceForSummoner_Description), ResourceType = typeof(PlugInResources))]
public class AddLanceForSummoner : AddInitialSkillPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddLanceForSummoner"/> class.
    /// </summary>
    public AddLanceForSummoner()
        : base((byte)CharacterClassNumber.Summoner, (ushort)SkillNumber.Lance)
    {
    }
}
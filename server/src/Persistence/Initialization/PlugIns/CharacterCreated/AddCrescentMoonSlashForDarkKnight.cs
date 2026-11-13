// <copyright file="AddCrescentMoonSlashForDarkKnight.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Adds the crescent moon slash skill (active in castle siege) to a created dark knight character.
/// </summary>
[Guid("2BB94D35-0DEF-4458-84AC-ECAAE6E896BE")]
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddCrescentMoonSlashForDarkKnight_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddCrescentMoonSlashForDarkKnight_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
public class AddCrescentMoonSlashForDarkKnight : AddInitialSkillPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddCrescentMoonSlashForDarkKnight"/> class.
    /// </summary>
    public AddCrescentMoonSlashForDarkKnight()
        : base((byte)CharacterClassNumber.DarkKnight, (ushort)SkillNumber.CrescentMoonSlash)
    {
    }
}
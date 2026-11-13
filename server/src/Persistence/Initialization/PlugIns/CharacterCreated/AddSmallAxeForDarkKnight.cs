// <copyright file="AddSmallAxeForDarkKnight.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Items;
using HNMO.PlugIns;

/// <summary>
/// Adds a small axe to a created dark knight character.
/// </summary>
[Guid("2377C222-4418-4F17-8388-1F8825E6243C")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AddSmallAxeForDarkKnight_Name), Description = nameof(PlugInResources.AddSmallAxeForDarkKnight_Description), ResourceType = typeof(PlugInResources))]
public class AddSmallAxeForDarkKnight : AddInitialItemPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddSmallAxeForDarkKnight" /> class.
    /// </summary>
    public AddSmallAxeForDarkKnight()
        : base((byte)CharacterClassNumber.DarkKnight, (byte)ItemGroups.Axes, 0, 0)
    {
    }
}
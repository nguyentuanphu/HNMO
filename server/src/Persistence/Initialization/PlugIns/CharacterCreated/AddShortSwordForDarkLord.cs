// <copyright file="AddShortSwordForDarkLord.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Items;
using HNMO.PlugIns;

/// <summary>
/// Adds a short sword to a created dark lord character.
/// </summary>
[Guid("BAC120D0-D981-4EBB-8F5A-0EC19434AF16")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AddShortSwordForDarkLord_Name), Description = nameof(PlugInResources.AddShortSwordForDarkLord_Description), ResourceType = typeof(PlugInResources))]
public class AddShortSwordForDarkLord : AddInitialItemPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddShortSwordForDarkLord" /> class.
    /// </summary>
    public AddShortSwordForDarkLord()
        : base((byte)CharacterClassNumber.DarkLord, (byte)ItemGroups.Swords, 1, 0)
    {
    }
}
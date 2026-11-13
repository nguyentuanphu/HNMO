// <copyright file="AddArrowsForFairyElf.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Items;
using HNMO.PlugIns;

/// <summary>
/// Adds arrows to a created fairy elf character.
/// </summary>
[Guid("71B6EB8D-E676-4B22-9E7E-15C7C3969852")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AddArrowsForFairyElf_Name), Description = nameof(PlugInResources.AddArrowsForFairyElf_Description), ResourceType = typeof(PlugInResources))]
public class AddArrowsForFairyElf : AddInitialItemPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddArrowsForFairyElf" /> class.
    /// </summary>
    public AddArrowsForFairyElf()
        : base((byte)CharacterClassNumber.FairyElf, (byte)ItemGroups.Bows, 15, 0)
    {
    }

    /// <inheritdoc />
    protected override Item? CreateItem(IContext persistenceContext, GameConfiguration gameConfiguration, ILogger logger)
    {
        if (base.CreateItem(persistenceContext, gameConfiguration, logger) is { } item)
        {
            item.Durability = 255;
            return item;
        }

        return null;
    }
}
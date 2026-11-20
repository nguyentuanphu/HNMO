// <copyright file="ArenaTemplateItem.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>One item of an arena character's gear: where it goes and what it is.</summary>
public sealed class ArenaTemplateItem
{
    /// <summary>Gets or sets the slot: 0 and 1 the hands, 2 helm, 3 armor, 4 pants, 5 gloves, 6 boots, 7 wings, 8 pet, 9 pendant, 10 and 11 rings, 12 and up the bag.</summary>
    public byte Slot { get; set; }

    /// <summary>Gets or sets the item group.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets the item level (+0 to +15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has its skill.</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has luck.</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets the additional option's level (0 to 4).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets the excellent options (bits 0 to 5).</summary>
    public byte ExcellentOptions { get; set; }

    /// <summary>Gets or sets the durability or stack count (0: the item's own).</summary>
    public byte Durability { get; set; }
}

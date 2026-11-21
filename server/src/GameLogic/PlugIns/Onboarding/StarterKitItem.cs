// <copyright file="StarterKitItem.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// An item of the starter kit.
/// </summary>
public class StarterKitItem
{
    /// <summary>Gets or sets the item group.</summary>
    [Range(0, 15)]
    public short Group { get; set; }

    /// <summary>Gets or sets the item number in its group.</summary>
    [Range(0, 511)]
    public short Number { get; set; }

    /// <summary>Gets or sets the item level.</summary>
    [Range(0, 15)]
    public byte Level { get; set; }

    /// <summary>Gets or sets how many of the item are given (separate pieces).</summary>
    [Range(1, 50)]
    public int Count { get; set; } = 1;

    /// <summary>Gets or sets the durability of a piece; for stackable items (potions, arrows) the stack size. 0 is full.</summary>
    [Range(0, 255)]
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has its skill (when it has one).</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has luck (when it can).</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets the level of the item's additional option (0-4, when it can have one).</summary>
    [Range(0, 7)]
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets the excellent options as bits (1-63; 0 is none).</summary>
    [Range(0, 63)]
    public byte ExcellentOptions { get; set; }

    /// <summary>Gets or sets the character class number which gets it (a class a new character can have); -1 is every class.</summary>
    [Range(-1, 255)]
    public short ClassNumber { get; set; } = -1;
}

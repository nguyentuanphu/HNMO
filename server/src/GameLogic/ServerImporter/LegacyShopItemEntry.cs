// <copyright file="LegacyShopItemEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Single item slot sold by a merchant.</summary>
public sealed class LegacyShopItemEntry
{
    /// <summary>Gets or sets item group (0-15).</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item level (0-15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability.</summary>
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has its skill.</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has luck.</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets item option level (0-7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets excellent option bitmask.</summary>
    public byte ExcellentBits { get; set; }

    /// <summary>Gets or sets Season 9 elemental attribute flag.</summary>
    public int ElementalAttribute { get; set; }
}

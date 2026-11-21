// <copyright file="LegacyDecodedItem.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Decoded 16-byte item slot from legacy inventory/warehouse binary blobs.</summary>
public sealed class LegacyDecodedItem
{
    /// <summary>Gets or sets slot index in storage.</summary>
    public byte Slot { get; set; }

    /// <summary>Gets or sets item group (0-15).</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number within group.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item enhancement level (0-15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability.</summary>
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether weapon skill is attached.</summary>
    public bool HasSkill { get; set; }

    /// <summary>Gets or sets a value indicating whether luck is attached.</summary>
    public bool HasLuck { get; set; }

    /// <summary>Gets or sets additional option level (0-7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets excellent flags byte.</summary>
    public byte ExcellentFlags { get; set; }

    /// <summary>Gets or sets ancient flag byte.</summary>
    public byte AncientFlags { get; set; }
}

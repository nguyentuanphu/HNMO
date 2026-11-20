// <copyright file="ZenTableEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>The zen a monster drops on a map: an amount between a minimum and a maximum.</summary>
public sealed class ZenTableEntry
{
    /// <summary>Gets or sets the map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets the least zen of a drop.</summary>
    public int MinimumZen { get; set; } = 100;

    /// <summary>Gets or sets the most zen of a drop.</summary>
    public int MaximumZen { get; set; } = 100;
}

// <copyright file="LegacyChaosRateEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Parsed Chaos Machine success rate entry.</summary>
public sealed class LegacyChaosRateEntry
{
    /// <summary>Gets or sets source recipe/key name.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets target HNMO crafting name pattern.</summary>
    public string CraftNamePattern { get; set; } = string.Empty;

    /// <summary>Gets or sets target success percentage (0-100).</summary>
    public int SuccessPercent { get; set; }

    /// <summary>Gets or sets optional Luck bonus percent.</summary>
    public int LuckBonusPercent { get; set; }

    /// <summary>
    /// Gets or sets the HNMO crafting number when the rate is the highest success rate of a crafting whose rate comes
    /// from the items put in (wings, capes, the Feather of Condor), rather than a base rate.
    /// </summary>
    public byte? MaximumOfCrafting { get; set; }
}

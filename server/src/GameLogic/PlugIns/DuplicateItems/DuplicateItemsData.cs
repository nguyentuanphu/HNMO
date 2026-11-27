// <copyright file="DuplicateItemsData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.DuplicateItems;

/// <summary>What the duplicate scan keeps in the HNMO state file.</summary>
public sealed class DuplicateItemsData
{
    /// <summary>Gets or sets the cases found, newest last.</summary>
    public List<DuplicateItemCase> Cases { get; set; } = [];

    /// <summary>Gets or sets when the last scan finished.</summary>
    public DateTime? LastScanUtc { get; set; }

    /// <summary>Gets or sets how many items the last scan gave a serial.</summary>
    public int LastScanSerialsGiven { get; set; }
}

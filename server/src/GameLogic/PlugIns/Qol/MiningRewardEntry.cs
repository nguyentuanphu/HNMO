// <copyright file="MiningRewardEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Qol;

/// <summary>
/// Weighted mineral / jewel reward row for the <c>/mine</c> excavation mini-system.
/// </summary>
public sealed class MiningRewardEntry
{
    /// <summary>Gets or sets relative selection weight (&gt; 0).</summary>
    public int Weight { get; set; } = 100;

    /// <summary>Gets or sets English reward description.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese reward description.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets Zen gained (0 = none).</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets item group (-1 = Zen-only roll).</summary>
    public int ItemGroup { get; set; } = -1;

    /// <summary>Gets or sets item number when <see cref="ItemGroup"/> &gt;= 0.</summary>
    public short ItemNumber { get; set; }
}

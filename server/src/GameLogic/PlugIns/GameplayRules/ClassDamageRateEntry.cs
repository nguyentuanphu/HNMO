// <copyright file="ClassDamageRateEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>
/// Per-class damage scaling rule expressed as percentages (100 = default unscaled damage).
/// </summary>
public sealed class ClassDamageRateEntry
{
    /// <summary>Gets or sets base character class line number (0=DW, 4=DK, 8=FE, 12=MG, 16=DL, 20=SUM, 24=RF).</summary>
    public byte ClassLine { get; set; }

    /// <summary>Gets or sets English class label.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese class label.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets PvP damage percentage (10..500, default 100).</summary>
    public int PvpDamagePercent { get; set; } = 100;

    /// <summary>Gets or sets PvM damage percentage (10..500, default 100).</summary>
    public int PvmDamagePercent { get; set; } = 100;
}

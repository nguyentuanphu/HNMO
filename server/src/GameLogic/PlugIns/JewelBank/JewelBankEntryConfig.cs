// <copyright file="JewelBankEntryConfig.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// Configurable definition for a jewel type stored in the Jewel Bank.
/// </summary>
public sealed class JewelBankEntryConfig
{
    /// <summary>Gets or sets the unique jewel key (e.g. <c>bless</c>, <c>soul</c>).</summary>
    [StringLength(24)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the English display name.</summary>
    [StringLength(64)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese display name.</summary>
    [StringLength(64)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the item group.</summary>
    [Range(0, 15)]
    public byte Group { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    [Range(0, 511)]
    public short Number { get; set; }

    /// <summary>Gets or sets a value indicating whether deposits and withdrawals for this jewel are enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the maximum number an account may bank.</summary>
    [Range(1, 1_000_000)]
    public int MaxCapacity { get; set; } = 10_000;
}

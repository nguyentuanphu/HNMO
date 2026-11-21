// <copyright file="JewelBankConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// Configuration for <see cref="JewelBankPlugIn"/> (OFF by default).
/// </summary>
public sealed class JewelBankConfiguration
{
    /// <summary>Gets or sets the shared HNMO state file name.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets a value indicating whether website browsing and withdrawals are permitted.</summary>
    public bool AllowWebsite { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether in-game deposits/withdrawals require a town safe zone.</summary>
    public bool SafeZoneOnly { get; set; } = true;

    /// <summary>Gets or sets the flat Zen fee charged per withdrawal transaction.</summary>
    [Range(0, 100_000_000)]
    public int WithdrawZenFee { get; set; }

    /// <summary>Gets or sets the flat Zen fee charged per deposit transaction.</summary>
    [Range(0, 100_000_000)]
    public int DepositZenFee { get; set; }

    /// <summary>Gets or sets a value indicating whether players may reset/clear master skill points from Jewel Bank / website.</summary>
    public bool AllowClearMasterSkills { get; set; } = true;

    /// <summary>Gets or sets the Zen fee charged when clearing master skill points in-game.</summary>
    [Range(0, 2_000_000_000)]
    public int ClearMasterZen { get; set; } = 5_000_000;

    /// <summary>Gets or sets the coin cost charged when clearing master skill points in-game.</summary>
    [Range(0, 1_000_000)]
    public int ClearMasterCoins { get; set; }

    /// <summary>Gets or sets the coin currency for clearing master skill points.</summary>
    public CashShopCoinType ClearMasterCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the configured list of supported jewels.</summary>
    public List<JewelBankEntryConfig> Jewels { get; set; } = DefaultJewels();

    /// <summary>Creates the standard MU jewel set.</summary>
    /// <returns>Default jewel definitions.</returns>
    public static List<JewelBankEntryConfig> DefaultJewels() =>
    [
        new() { Key = "bless", NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện", Group = 14, Number = 13, Enabled = true, MaxCapacity = 10_000 },
        new() { Key = "soul", NameEn = "Jewel of Soul", NameVi = "Ngọc Tâm Linh", Group = 14, Number = 14, Enabled = true, MaxCapacity = 10_000 },
        new() { Key = "life", NameEn = "Jewel of Life", NameVi = "Ngọc Sinh Mệnh", Group = 14, Number = 16, Enabled = true, MaxCapacity = 10_000 },
        new() { Key = "creation", NameEn = "Jewel of Creation", NameVi = "Ngọc Sáng Tạo", Group = 14, Number = 22, Enabled = true, MaxCapacity = 10_000 },
        new() { Key = "chaos", NameEn = "Jewel of Chaos", NameVi = "Ngọc Hỗn Nguyên", Group = 12, Number = 15, Enabled = true, MaxCapacity = 10_000 },
        new() { Key = "harmony", NameEn = "Jewel of Harmony", NameVi = "Đá Tạo Hóa", Group = 14, Number = 42, Enabled = true, MaxCapacity = 10_000 },
        new() { Key = "guardian", NameEn = "Jewel of Guardian", NameVi = "Ngọc Hộ Mệnh", Group = 14, Number = 31, Enabled = true, MaxCapacity = 10_000 },
    ];
}

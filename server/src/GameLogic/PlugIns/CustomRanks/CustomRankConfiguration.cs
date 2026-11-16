// <copyright file="CustomRankConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CustomRanks;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;

/// <summary>
/// Configuration for <see cref="CustomRanksPlugIn"/> (OFF by default via <see cref="IDisabledByDefault"/>).
/// </summary>
public sealed class CustomRankConfiguration
{
    /// <summary>Gets or sets a value indicating whether custom rank badges are shown to players.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether custom titles appear on overhead player nameplates.</summary>
    [Display(Name = "Show by Nameplate")]
    public bool ShowByNameplate { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether custom titles prefix character names in chat.</summary>
    [Display(Name = "Show in Chat")]
    public bool ShowInChat { get; set; } = true;

    /// <summary>Gets or sets the persistence key/file under the configured state folder.</summary>
    [Display(Name = "State File")]
    public string FileName { get; set; } = "custom-ranks-state.json";

    /// <summary>Gets or sets the admin-defined rank tiers.</summary>
    [Display(Name = "Rank Tiers")]
    public List<CustomRankTierConfig> Tiers { get; set; } = CreateDefaultTiers();

    /// <summary>Creates starter custom rank tiers spanning level, resets, VIP, and achievements.</summary>
    /// <returns>The starter tiers.</returns>
    public static List<CustomRankTierConfig> CreateDefaultTiers() =>
    [
        new()
        {
            Id = "novice",
            Enabled = true,
            Priority = 10,
            TitleEn = "[Explorer]",
            TitleVi = "[Tân Thủ]",
            ColorHex = "#93c5fd",
            MinLevel = 150,
        },
        new()
        {
            Id = "veteran",
            Enabled = true,
            Priority = 20,
            TitleEn = "[Veteran]",
            TitleVi = "[Chiến Binh]",
            ColorHex = "#86efac",
            MinLevel = 350,
            MinResets = 1,
        },
        new()
        {
            Id = "achiever",
            Enabled = true,
            Priority = 30,
            TitleEn = "[Achiever]",
            TitleVi = "[Kỳ Tích]",
            ColorHex = "#c4b5fd",
            MinMissionsClaimed = 10,
        },
        new()
        {
            Id = "vip",
            Enabled = true,
            Priority = 40,
            TitleEn = "[VIP]",
            TitleVi = "[VIP]",
            ColorHex = "#fcd34d",
            MinVipLevel = 1,
        },
        new()
        {
            Id = "legend",
            Enabled = true,
            Priority = 50,
            TitleEn = "[Legend]",
            TitleVi = "[Huyền Thoại]",
            ColorHex = "#fb7185",
            MinResets = 10,
        },
    ];
}

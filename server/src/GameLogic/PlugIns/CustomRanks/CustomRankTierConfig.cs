// <copyright file="CustomRankTierConfig.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CustomRanks;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Admin-defined custom rank / title tier evaluated against character progression, VIP tier, and achievements.
/// </summary>
public sealed class CustomRankTierConfig
{
    /// <summary>Gets or sets the unique stable identifier (e.g. <c>legend</c>).</summary>
    [Display(Name = "Tier Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this tier is active.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the display priority (higher priority wins when multiple tiers qualify).</summary>
    [Display(Name = "Priority")]
    public int Priority { get; set; } = 10;

    /// <summary>Gets or sets the English badge text shown beside the character's name (e.g. <c>[Legend]</c>).</summary>
    [Display(Name = "Title (EN)")]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese badge text shown beside the character's name (e.g. <c>[Huyền Thoại]</c>).</summary>
    [Display(Name = "Title (VI)")]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the CSS hex color for the title badge (e.g. <c>#ffd166</c>).</summary>
    [Display(Name = "Color (Hex)")]
    public string ColorHex { get; set; } = "#ffd166";

    /// <summary>Gets or sets the minimum normal character level required (<c>0</c> = no requirement).</summary>
    [Display(Name = "Min Level")]
    public int MinLevel { get; set; }

    /// <summary>Gets or sets the minimum master level required (<c>0</c> = no requirement).</summary>
    [Display(Name = "Min Master Level")]
    public int MinMasterLevel { get; set; }

    /// <summary>Gets or sets the minimum reset count required (<c>0</c> = no requirement).</summary>
    [Display(Name = "Min Resets")]
    public int MinResets { get; set; }

    /// <summary>Gets or sets the minimum VIP level required (<c>0</c> = no requirement).</summary>
    [Display(Name = "Min VIP Level")]
    public int MinVipLevel { get; set; }

    /// <summary>Gets or sets the minimum completed/claimed achievements or missions required (<c>0</c> = no requirement).</summary>
    [Display(Name = "Min Achievements")]
    public int MinMissionsClaimed { get; set; }

    /// <summary>Gets or sets the minimum HNMO Arena ranked rating required (<c>0</c> = no requirement; 1 = any ranked duel played).</summary>
    [Display(Name = "Min Arena Rating")]
    public int MinArenaRating { get; set; }

    /// <summary>Gets or sets an optional achievement key explicitly granted to special winners/events (empty = not required).</summary>
    [Display(Name = "Required Achievement Key")]
    public string RequiredAchievementKey { get; set; } = string.Empty;
}

// <copyright file="MarriageConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="MarriagePlugIn"/> (OFF by default).
/// </summary>
public sealed class MarriageConfiguration
{
    /// <summary>Gets or sets the shared HNMO state file name.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets the minimum level required for both characters to marry.</summary>
    [Range(1, 400)]
    public int MinimumLevel { get; set; } = 100;

    /// <summary>Gets or sets a value indicating whether the ceremony requires both players to stand at the ceremony spot (Devias Church).</summary>
    public bool RequireCeremonyLocation { get; set; } = true;

    /// <summary>Gets or sets the ceremony map number (2 = Devias).</summary>
    [Range(0, 255)]
    public short CeremonyMapNumber { get; set; } = 2;

    /// <summary>Gets or sets the ceremony altar center X coordinate.</summary>
    [Range(0, 255)]
    public byte CeremonyX { get; set; } = 210;

    /// <summary>Gets or sets the ceremony altar center Y coordinate.</summary>
    [Range(0, 255)]
    public byte CeremonyY { get; set; } = 23;

    /// <summary>Gets or sets the allowed tile radius around the ceremony altar.</summary>
    [Range(2, 60)]
    public int CeremonyRadius { get; set; } = 18;

    /// <summary>Gets or sets the zen cost charged to the proposer at ceremony completion.</summary>
    [Range(0, 2000000000)]
    public int CeremonyZenCost { get; set; } = 1_000_000;

    /// <summary>Gets or sets a value indicating whether couples can set a custom title.</summary>
    public bool AllowCustomTitle { get; set; } = true;

    /// <summary>Gets or sets default English couple title template (<c>{spouse}</c> is replaced).</summary>
    [StringLength(48)]
    public string DefaultTitleEn { get; set; } = "Heart of {spouse}";

    /// <summary>Gets or sets default Vietnamese couple title template.</summary>
    [StringLength(48)]
    public string DefaultTitleVi { get; set; } = "Phu thê cùng {spouse}";

    /// <summary>Gets or sets bonus EXP percent while spouses hunt in the same party.</summary>
    [Range(0, 100)]
    public int CoupleExpBonusPercent { get; set; } = 10;

    /// <summary>Gets or sets flat bonus defense while spouses hunt in the same party.</summary>
    [Range(0, 500)]
    public int CoupleDefenseBonus { get; set; } = 15;

    /// <summary>Gets or sets the zen fee to file a divorce.</summary>
    [Range(0, 2000000000)]
    public int DivorceZenFee { get; set; } = 2_000_000;

    /// <summary>Gets or sets hours a divorced character must wait before remarrying.</summary>
    [Range(0, 720)]
    public int RemarryCooldownHours { get; set; } = 24;

    /// <summary>Gets or sets a value indicating whether married players may warp to their spouse via the panel.</summary>
    public bool AllowSpouseWarp { get; set; } = true;
}

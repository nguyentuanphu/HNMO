// <copyright file="UltimateAutoPlayConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.UltimateAutoPlay;

/// <summary>
/// Access level for an Ultimate Autoplay feature in the Admin Panel.
/// </summary>
public enum UltimateFeatureMode
{
    /// <summary>Feature is switched off for all players.</summary>
    Off = 0,

    /// <summary>Feature is available to all players (free and VIP).</summary>
    Free = 1,

    /// <summary>Feature requires active VIP (<c>Stats.IsVip</c>).</summary>
    VipOnly = 2,
}

/// <summary>
/// Admin-configurable switches and Free vs VIP gating for HNMO Ultimate Autoplay.
/// </summary>
public class UltimateAutoPlayConfiguration
{
    /// <summary>Gets or sets a value indicating whether Ultimate Autoplay is enabled server-wide.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets access for 3D ground sparkle trail and hunting-range ring.</summary>
    public UltimateFeatureMode SparkleGuide { get; set; } = UltimateFeatureMode.Free;

    /// <summary>Gets or sets access for auto-drinking Mana/SD potions and town Mana restock.</summary>
    public UltimateFeatureMode ManaSdPotions { get; set; } = UltimateFeatureMode.VipOnly;

    /// <summary>Gets or sets access for automatic Jewel Bank deposit when bag is full.</summary>
    public UltimateFeatureMode JewelDeposit { get; set; } = UltimateFeatureMode.VipOnly;

    /// <summary>Gets or sets access for target priority (Aggro/Lowest HP) and rare-drop priority.</summary>
    public UltimateFeatureMode TargetPriority { get; set; } = UltimateFeatureMode.VipOnly;

    /// <summary>Gets or sets access for multi-spot patrol when a spawn is empty.</summary>
    public UltimateFeatureMode Patrol { get; set; } = UltimateFeatureMode.VipOnly;

    /// <summary>Gets or sets access for emergency low-HP retreat and auto-return after death.</summary>
    public UltimateFeatureMode Survival { get; set; } = UltimateFeatureMode.VipOnly;

    /// <summary>Gets or sets access for World Boss & Quest focus and boss spawn alerts.</summary>
    public UltimateFeatureMode BossFocus { get; set; } = UltimateFeatureMode.VipOnly;

    /// <summary>Gets or sets access for presets, auto-pick best skills, anti-stuck watchdog, and live HUD stats.</summary>
    public UltimateFeatureMode PresetsAndStats { get; set; } = UltimateFeatureMode.Free;
}

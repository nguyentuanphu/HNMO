// <copyright file="UltimateAutoPlayPolicyWriter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.UltimateAutoPlay;

using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes Ultimate Autoplay policy for packet <c>F5 57</c>, resolved against a player's <c>Stats.IsVip</c>.
/// </summary>
public static class UltimateAutoPlayPolicyWriter
{
    /// <summary>Disabled policy payload sent when the plug-in is inactive or disabled.</summary>
    public const string DisabledPolicyJson = "{\"v\":2,\"enabled\":false,\"isVip\":false,\"vipLevel\":0,\"features\":{}}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Builds the JSON policy payload for a player.</summary>
    /// <param name="cfg">The settings.</param>
    /// <param name="plugInActive">Whether the plug-in is active.</param>
    /// <param name="isVip">Whether the player has VIP (treated as VIP level 2 when <c>true</c> for legacy callers).</param>
    /// <returns>The policy JSON.</returns>
    public static string Write(UltimateAutoPlayConfiguration cfg, bool plugInActive, bool isVip) =>
        Write(cfg, plugInActive, isVip ? 2 : 0);

    /// <summary>Builds the JSON policy payload for a player with an explicit VIP level (0 = Free, 1 = Bronze, 2+ = Silver/Gold).</summary>
    /// <param name="cfg">The settings.</param>
    /// <param name="plugInActive">Whether the plug-in is active.</param>
    /// <param name="vipLevel">The player's effective VIP level.</param>
    /// <returns>The policy JSON.</returns>
    public static string Write(UltimateAutoPlayConfiguration cfg, bool plugInActive, int vipLevel)
    {
        var effectiveVipLevel = Math.Max(0, vipLevel);
        var enabled = plugInActive && cfg.Enabled;
        var features = new Dictionary<string, object>(9)
        {
            ["sparkleGuide"] = Feature(cfg.SparkleGuide, enabled, effectiveVipLevel),
            ["extrasTown"] = Feature(cfg.ExtrasTown, enabled, effectiveVipLevel),
            ["manaSdPotions"] = Feature(cfg.ManaSdPotions, enabled, effectiveVipLevel),
            ["jewelDeposit"] = Feature(cfg.JewelDeposit, enabled, effectiveVipLevel),
            ["targetPriority"] = Feature(cfg.TargetPriority, enabled, effectiveVipLevel),
            ["patrol"] = Feature(cfg.Patrol, enabled, effectiveVipLevel),
            ["survival"] = Feature(cfg.Survival, enabled, effectiveVipLevel),
            ["bossFocus"] = Feature(cfg.BossFocus, enabled, effectiveVipLevel),
            ["presetsStats"] = Feature(cfg.PresetsAndStats, enabled, effectiveVipLevel),
        };

        return JsonSerializer.Serialize(new { v = 2, enabled, isVip = effectiveVipLevel > 0, vipLevel = effectiveVipLevel, features }, JsonOptions);
    }

    private static object Feature(UltimateFeatureMode mode, bool enabled, int vipLevel)
    {
        var requiredVipLevel = mode switch
        {
            UltimateFeatureMode.VipOnly => 1,
            UltimateFeatureMode.HigherVip => 2,
            _ => 0,
        };
        var allowed = enabled && (mode == UltimateFeatureMode.Free || (requiredVipLevel > 0 && vipLevel >= requiredVipLevel));
        return new
        {
            mode = (int)mode,
            allowed,
            vipRequired = enabled && requiredVipLevel > 0 && vipLevel < requiredVipLevel,
            requiredVipLevel,
        };
    }
}

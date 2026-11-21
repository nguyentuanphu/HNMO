// <copyright file="UltimateAutoPlayPolicyWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
    public const string DisabledPolicyJson = "{\"v\":1,\"enabled\":false,\"isVip\":false,\"features\":{}}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Builds the JSON policy payload for a player.</summary>
    /// <param name="cfg">The settings.</param>
    /// <param name="plugInActive">Whether the plug-in is active.</param>
    /// <param name="isVip">Whether the player has VIP.</param>
    /// <returns>The policy JSON.</returns>
    public static string Write(UltimateAutoPlayConfiguration cfg, bool plugInActive, bool isVip)
    {
        var enabled = plugInActive && cfg.Enabled;
        var features = new Dictionary<string, object>(8)
        {
            ["sparkleGuide"] = Feature(cfg.SparkleGuide, enabled, isVip),
            ["manaSdPotions"] = Feature(cfg.ManaSdPotions, enabled, isVip),
            ["jewelDeposit"] = Feature(cfg.JewelDeposit, enabled, isVip),
            ["targetPriority"] = Feature(cfg.TargetPriority, enabled, isVip),
            ["patrol"] = Feature(cfg.Patrol, enabled, isVip),
            ["survival"] = Feature(cfg.Survival, enabled, isVip),
            ["bossFocus"] = Feature(cfg.BossFocus, enabled, isVip),
            ["presetsStats"] = Feature(cfg.PresetsAndStats, enabled, isVip),
        };

        return JsonSerializer.Serialize(new { v = 1, enabled, isVip, features }, JsonOptions);
    }

    private static object Feature(UltimateFeatureMode mode, bool enabled, bool isVip)
    {
        var allowed = enabled && (mode == UltimateFeatureMode.Free || (mode == UltimateFeatureMode.VipOnly && isVip));
        return new
        {
            mode = (int)mode,
            allowed,
            vipRequired = enabled && mode == UltimateFeatureMode.VipOnly && !isVip,
        };
    }
}

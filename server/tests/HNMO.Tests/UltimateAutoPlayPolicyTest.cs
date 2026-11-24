// <copyright file="UltimateAutoPlayPolicyTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.UltimateAutoPlay;

/// <summary>
/// The Ultimate Autoplay policy a player's client gets: Off is never allowed, Free always, VIP Only only with VIP,
/// and nothing at all when the master switch is off.
/// </summary>
[TestFixture]
public class UltimateAutoPlayPolicyTest
{
    /// <summary>The 3-tier defaults: sparkles free, Extras town trips at VIP 1+, and Ultimate features at Higher VIP 2+.</summary>
    /// <param name="vipLevel">Player VIP level (0 = Free, 1 = Bronze VIP, 2 = Silver+ Higher VIP).</param>
    /// <param name="extrasAllowed">Whether VIP Extras town automation is unlocked.</param>
    /// <param name="ultimateAllowed">Whether Higher VIP Ultimate features are unlocked.</param>
    [TestCase(0, false, false)]
    [TestCase(1, true, false)]
    [TestCase(2, true, true)]
    public void TheDefaultsSplitFreeVipAndHigherVip(int vipLevel, bool extrasAllowed, bool ultimateAllowed)
    {
        var features = Features(UltimateAutoPlayPolicyWriter.Write(new UltimateAutoPlayConfiguration(), plugInActive: true, vipLevel));
        Assert.That(features.GetProperty("sparkleGuide").GetProperty("allowed").GetBoolean(), Is.True);
        Assert.That(features.GetProperty("extrasTown").GetProperty("allowed").GetBoolean(), Is.EqualTo(extrasAllowed));
        Assert.That(features.GetProperty("extrasTown").GetProperty("vipRequired").GetBoolean(), Is.EqualTo(!extrasAllowed));
        Assert.That(features.GetProperty("extrasTown").GetProperty("requiredVipLevel").GetInt32(), Is.EqualTo(1));
        Assert.That(features.GetProperty("manaSdPotions").GetProperty("allowed").GetBoolean(), Is.EqualTo(ultimateAllowed));
        Assert.That(features.GetProperty("manaSdPotions").GetProperty("vipRequired").GetBoolean(), Is.EqualTo(!ultimateAllowed));
        Assert.That(features.GetProperty("manaSdPotions").GetProperty("requiredVipLevel").GetInt32(), Is.EqualTo(2));
        Assert.That(features.GetProperty("presetsStats").GetProperty("allowed").GetBoolean(), Is.EqualTo(ultimateAllowed));
    }

    /// <summary>A function switched off is off for VIP players too.</summary>
    [Test]
    public void OffIsOffForEveryone()
    {
        var features = Features(UltimateAutoPlayPolicyWriter.Write(new UltimateAutoPlayConfiguration { Patrol = UltimateFeatureMode.Off }, plugInActive: true, isVip: true));
        Assert.That(features.GetProperty("patrol").GetProperty("allowed").GetBoolean(), Is.False);
        Assert.That(features.GetProperty("patrol").GetProperty("vipRequired").GetBoolean(), Is.False);
    }

    /// <summary>The master switch off (or the plug-in off): no function is allowed, even a free one.</summary>
    /// <param name="enabled">The master switch.</param>
    /// <param name="plugInActive">Whether the plug-in is active.</param>
    [TestCase(false, true)]
    [TestCase(true, false)]
    public void TheMasterSwitchTurnsEverythingOff(bool enabled, bool plugInActive)
    {
        using var document = JsonDocument.Parse(UltimateAutoPlayPolicyWriter.Write(new UltimateAutoPlayConfiguration { Enabled = enabled }, plugInActive, isVip: true));
        Assert.That(document.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
        foreach (var feature in document.RootElement.GetProperty("features").EnumerateObject())
        {
            Assert.That(feature.Value.GetProperty("allowed").GetBoolean(), Is.False, feature.Name);
        }
    }

    private static JsonElement Features(string json) => JsonDocument.Parse(json).RootElement.GetProperty("features").Clone();
}

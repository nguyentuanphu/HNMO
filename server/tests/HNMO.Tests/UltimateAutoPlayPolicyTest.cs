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
    /// <summary>The defaults: sparkles and presets free, the rest for VIP players only.</summary>
    /// <param name="isVip">Whether the player has VIP.</param>
    /// <param name="potionsAllowed">Whether the VIP-only potions are allowed.</param>
    [TestCase(false, false)]
    [TestCase(true, true)]
    public void TheDefaultsSplitFreeAndVip(bool isVip, bool potionsAllowed)
    {
        var features = Features(UltimateAutoPlayPolicyWriter.Write(new UltimateAutoPlayConfiguration(), plugInActive: true, isVip));
        Assert.That(features.GetProperty("sparkleGuide").GetProperty("allowed").GetBoolean(), Is.True);
        Assert.That(features.GetProperty("presetsStats").GetProperty("allowed").GetBoolean(), Is.True);
        Assert.That(features.GetProperty("manaSdPotions").GetProperty("allowed").GetBoolean(), Is.EqualTo(potionsAllowed));
        Assert.That(features.GetProperty("manaSdPotions").GetProperty("vipRequired").GetBoolean(), Is.EqualTo(!isVip));
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

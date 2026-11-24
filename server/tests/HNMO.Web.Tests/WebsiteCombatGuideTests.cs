// <copyright file="WebsiteCombatGuideTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.Web.AdminPanel.API;

/// <summary>Checks public combat facts against the engine's rules and host settings.</summary>
[TestFixture]
public sealed class WebsiteCombatGuideTests
{
    /// <summary>Publishes the same example and penalty thresholds as the combat runtime.</summary>
    [Test]
    public void HitExampleUsesActualCombatCalculation()
    {
        var data = Read(WebsiteCombatGuideController.BuildHitRules());
        var example = data["example"]!;
        var chance = AttackableExtensions.GetPvpHitChance(
            example["attackRate"]!.GetValue<int>(),
            example["defenseRate"]!.GetValue<int>(),
            example["attackerLevel"]!.GetValue<int>(),
            example["defenderLevel"]!.GetValue<int>());
        Assert.That(example["chance"]!.GetValue<float>(), Is.EqualTo(chance).Within(0.0001));
        Assert.That(chance, Is.EqualTo(0.43).Within(0.0001));
        Assert.That(data["penalties"]![2]!["levelGap"]!.GetValue<int>(), Is.EqualTo(100));
        Assert.That(data["duelFactor"]!.GetValue<double>(), Is.EqualTo(AttackableExtensions.DuelDamageFactor));
    }

    /// <summary>Different host configurations give different example shares and pickup rules.</summary>
    /// <param name="split">Whether experience is split.</param>
    /// <param name="highest">Whether the highest contributor owns the loot.</param>
    /// <param name="firstShare">The share of the first attacker's experience.</param>
    [TestCase(true, false, 0.75)]
    [TestCase(false, true, 0.0)]
    public void LootExampleFollowsHostSwitches(bool split, bool highest, double firstShare)
    {
        var data = Read(WebsiteCombatGuideController.BuildLootRules(new MonsterAiConfiguration
        {
            SplitExpByDamage = split,
            DropToTopDamageDealer = highest,
        }));
        Assert.That(data["dropToTopDamageDealer"]!.GetValue<bool>(), Is.EqualTo(highest));
        Assert.That(data["example"]!["firstShare"]!.GetValue<double>(), Is.EqualTo(firstShare));
        Assert.That(data["example"]!["finishingShare"]!.GetValue<double>(), Is.EqualTo(1 - firstShare));
    }

    /// <summary>Inactive caps are hidden, active limits reflect engine clamping, and empty boss tiers are omitted.</summary>
    [Test]
    public void OnlyActiveLimitsAndConfiguredRewardTiersAreShown()
    {
        var limits = new CombatLimitsConfiguration { PvpDamageReductionPercent = 110, MaxDamagePerHit = 42, CastleSiegeDamageReductionPercent = 50 };
        Assert.That(WebsiteCombatGuideController.BuildLimits(limits), Is.Null);
        limits.Enabled = true;
        var active = Read(WebsiteCombatGuideController.BuildLimits(limits)!);
        Assert.That(active["pvpReduction"]!.GetValue<int>(), Is.EqualTo(95));
        Assert.That(active["damageCap"]!.GetValue<int>(), Is.EqualTo(42));
        var boss = Read(WebsiteCombatGuideController.BuildBossRule(new WorldBossDefinition
        {
            RankBy = WorldBossRankBy.Party,
            Top1 = new MissionReward { Zen = 100 },
            Participation = new MissionReward { Coins = 5 },
            ParticipationMinimumPercent = 7,
            ParticipationMinimumDamage = 3000,
        }));
        Assert.That(boss["partyRank"]!.GetValue<bool>(), Is.True);
        Assert.That(boss["topRanks"]!.AsArray().Select(rank => rank!.GetValue<int>()), Is.EqualTo(new[] { 1 }));
        Assert.That(boss["hasParticipation"]!.GetValue<bool>(), Is.True);
        Assert.That(boss["hasLastHit"]!.GetValue<bool>(), Is.False);
        Assert.That(boss["participationMinimumPercent"]!.GetValue<double>(), Is.EqualTo(7));
        Assert.That(boss["participationMinimumDamage"]!.GetValue<long>(), Is.EqualTo(3000));
        Assert.That(boss.ContainsKey("login"), Is.False);
    }

    /// <summary>Verifies that the host switch has a native label and description.</summary>
    /// <param name="locale">The admin panel locale.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void HostSwitchIsLocalized(string locale)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
            Assert.That(WebsiteServerPageLabels.CombatGuide, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.CombatGuide)));
            Assert.That(WebsiteServerPageLabels.CombatGuideDescription, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.CombatGuideDescription)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static JsonObject Read(object data)
        => JsonSerializer.SerializeToNode(data, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
}

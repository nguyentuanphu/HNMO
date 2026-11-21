// <copyright file="TeleportNpcTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.TeleportNpc;

/// <summary>
/// Unit tests for <see cref="TeleportNpcEngine"/>.
/// </summary>
[TestFixture]
public class TeleportNpcTests
{
    /// <summary>Verifies NPC number routing respects plug-in active flag and configured IDs.</summary>
    [Test]
    public void HandlesNpcOnlyWhenActiveAndConfigured()
    {
        var cfg = new TeleportNpcConfiguration { Enabled = true, NpcNumbers = [247, 540] };

        Assert.That(TeleportNpcEngine.HandlesNpc(cfg, plugInActive: false, 247), Is.False);
        Assert.That(TeleportNpcEngine.HandlesNpc(cfg, plugInActive: true, 247), Is.True);
        Assert.That(TeleportNpcEngine.HandlesNpc(cfg, plugInActive: true, 999), Is.False);
    }

    /// <summary>Verifies level caps, reset gate, VIP gate, PK gate, MiniGame block, and Zen cost enforcement.</summary>
    [Test]
    public void EvaluatesWarpRequirementsAndZenCostAccurately()
    {
        var cfg = new TeleportNpcConfiguration
        {
            Enabled = true,
            Destinations =
            [
                new()
                {
                    Id = "icarus",
                    NameEn = "Icarus",
                    NameVi = "Icarus",
                    MapNumber = 10,
                    TargetX = 15,
                    TargetY = 13,
                    MinLevel = 170,
                    MaxLevel = 399,
                    MinResets = 2,
                    ZenCost = 50_000,
                    VipOnly = true,
                    AllowPk = false,
                },
            ],
        };

        Assert.That(
            TeleportNpcEngine.EvaluateWarp(cfg, true, "missing", 200, 3, 1, 100_000, false, false).Reason,
            Is.EqualTo(TeleportDenialReason.DestinationNotFound));

        Assert.That(
            TeleportNpcEngine.EvaluateWarp(cfg, true, "icarus", 200, 3, 1, 100_000, false, true).Reason,
            Is.EqualTo(TeleportDenialReason.InMiniGame));

        Assert.That(
            TeleportNpcEngine.EvaluateWarp(cfg, true, "icarus", 200, 3, 1, 100_000, true, false).Reason,
            Is.EqualTo(TeleportDenialReason.PkBlocked));

        Assert.That(
            TeleportNpcEngine.EvaluateWarp(cfg, true, "icarus", 200, 3, 0, 100_000, false, false).Reason,
            Is.EqualTo(TeleportDenialReason.VipRequired));

        Assert.That(
            TeleportNpcEngine.EvaluateWarp(cfg, true, "icarus", 150, 3, 1, 100_000, false, false).Reason,
            Is.EqualTo(TeleportDenialReason.LevelTooLow));

        Assert.That(
            TeleportNpcEngine.EvaluateWarp(cfg, true, "icarus", 400, 3, 1, 100_000, false, false).Reason,
            Is.EqualTo(TeleportDenialReason.LevelTooHigh));

        Assert.That(
            TeleportNpcEngine.EvaluateWarp(cfg, true, "icarus", 200, 1, 1, 100_000, false, false).Reason,
            Is.EqualTo(TeleportDenialReason.InsufficientResets));

        Assert.That(
            TeleportNpcEngine.EvaluateWarp(cfg, true, "icarus", 200, 3, 1, 10_000, false, false).Reason,
            Is.EqualTo(TeleportDenialReason.InsufficientZen));

        var ok = TeleportNpcEngine.EvaluateWarp(cfg, true, "icarus", 200, 3, 1, 60_000, false, false);
        Assert.That(ok.Allowed, Is.True);
        Assert.That(ok.Reason, Is.EqualTo(TeleportDenialReason.None));
        Assert.That(ok.ZenCost, Is.EqualTo(50_000));
        Assert.That(ok.MessageEn, Does.Contain("50,000"));
        Assert.That(ok.MessageVi, Does.Contain("Icarus"));
    }
}

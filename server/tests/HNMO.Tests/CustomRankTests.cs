// <copyright file="CustomRankTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.CustomRanks;

/// <summary>
/// Unit tests for <see cref="CustomRankEngine"/> and <see cref="CustomRankStore"/>.
/// </summary>
[TestFixture]
public class CustomRankTests
{
    /// <summary>Verifies tier qualification across level, master level, resets, VIP, missions, and special keys.</summary>
    [Test]
    public void EvaluatesTiersByLevelResetsVipAndAchievements()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"custom-ranks-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = CustomRankStore.Open(tempFile);
            var config = new CustomRankConfiguration
            {
                Enabled = true,
                FileName = tempFile,
                Tiers =
                [
                    new() { Id = "novice", Priority = 10, TitleEn = "[Explorer]", TitleVi = "[Tân Thủ]", MinLevel = 150 },
                    new() { Id = "vip", Priority = 40, TitleEn = "[VIP]", TitleVi = "[VIP]", MinVipLevel = 1 },
                    new() { Id = "legend", Priority = 50, TitleEn = "[Legend]", TitleVi = "[Huyền Thoại]", MinResets = 10, MinMissionsClaimed = 5 },
                    new() { Id = "siege", Priority = 90, TitleEn = "[Siege Lord]", TitleVi = "[Chủ Thành]", RequiredAchievementKey = "castle_winner" },
                ],
            };

            var engine = new CustomRankEngine(config, store);
            var hero = new CustomRankSubject("HeroOne", Level: 400, MasterLevel: 50, Resets: 12, VipLevel: 1, MissionsClaimed: 8);

            var unlocked = engine.GetUnlockedTiers(hero);
            Assert.That(unlocked.Select(t => t.Id).ToArray(), Is.EqualTo(new[] { "legend", "vip", "novice" }));
            Assert.That(engine.ResolveActiveTier(hero)?.Id, Is.EqualTo("legend"));

            store.GrantAchievement("HeroOne", "castle_winner");
            Assert.That(engine.ResolveActiveTier(hero)?.Id, Is.EqualTo("siege"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>Verifies players can equip an unlocked title or reset to auto, while locked titles are rejected.</summary>
    [Test]
    public void TrySelectRankHonorsUnlockedTiersAndRejectsLockedOnes()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"custom-ranks-select-{Guid.NewGuid():N}.json");
        try
        {
            var store = CustomRankStore.Open(tempFile);
            var config = new CustomRankConfiguration
            {
                Enabled = true,
                FileName = tempFile,
            };
            var engine = new CustomRankEngine(config, store);
            var hero = new CustomRankSubject("HeroTwo", Level: 400, MasterLevel: 0, Resets: 2, VipLevel: 1, MissionsClaimed: 0);

            Assert.That(engine.ResolveActiveTier(hero)?.Id, Is.EqualTo("vip"));

            Assert.That(engine.TrySelectRank(hero, "novice", out var errEn, out var errVi), Is.True);
            Assert.That(errEn, Is.Empty);
            Assert.That(errVi, Is.Empty);
            Assert.That(engine.ResolveActiveTier(hero)?.Id, Is.EqualTo("novice"));

            Assert.That(engine.TrySelectRank(hero, "legend", out errEn, out errVi), Is.False);
            Assert.That(errEn, Does.Contain("locked"));
            Assert.That(errVi, Does.Contain("chưa được mở khóa"));

            Assert.That(engine.TrySelectRank(hero, "auto", out _, out _), Is.True);
            Assert.That(engine.ResolveActiveTier(hero)?.Id, Is.EqualTo("vip"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

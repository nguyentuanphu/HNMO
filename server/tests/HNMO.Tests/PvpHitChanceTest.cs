// <copyright file="PvpHitChanceTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;

/// <summary>
/// Tests the chance that a player hits another player, against the Season 6/9 server suites.
/// </summary>
[TestFixture]
public class PvpHitChanceTest
{
    /// <summary>
    /// Equal attack and defense rates at equal levels hit half of the time (it was 3%).
    /// </summary>
    [Test]
    public void EqualRatesAndLevelsHitHalfOfTheTime()
    {
        Assert.That(AttackableExtensions.GetPvpHitChance(1300, 1300, 400, 400), Is.EqualTo(0.5f).Within(0.0001f));
    }

    /// <summary>
    /// An Elf (level 400, 1000 agility: rate 600 + 1200 = 1800) attacks a Dark Knight (level 400, 1000 agility:
    /// rate 500 + 800 = 1300): 2 * 1800 / 3100 * 400 / 800 = 58% (it was 1 - 1300 / 1800 = 28%).
    /// </summary>
    [Test]
    public void ElfAgainstDarkKnight()
    {
        Assert.That(AttackableExtensions.GetPvpHitChance(1800, 1300, 400, 400), Is.EqualTo(1800f / 3100f).Within(0.0001f));
    }

    /// <summary>
    /// A defender 100, 200 or 300 levels higher lowers the chance by 5, 10 or 15 percent points.
    /// </summary>
    /// <param name="defenseLevel">The defender's level.</param>
    /// <param name="penalty">The expected penalty.</param>
    [TestCase(500, 0.05f)]
    [TestCase(600, 0.10f)]
    [TestCase(700, 0.15f)]
    public void HigherDefenderLowersTheChance(int defenseLevel, float penalty)
    {
        const float attackLevel = 400;
        var expected = (2f * 0.5f * attackLevel / (attackLevel + defenseLevel)) - penalty;
        Assert.That(AttackableExtensions.GetPvpHitChance(1000, 1000, attackLevel, defenseLevel), Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>
    /// The chance stays between 0 and 1.
    /// </summary>
    [Test]
    public void ChanceIsClamped()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AttackableExtensions.GetPvpHitChance(100_000, 1, 800, 1), Is.EqualTo(1f));
            Assert.That(AttackableExtensions.GetPvpHitChance(1, 100_000, 1, 800), Is.EqualTo(0f));
        });
    }
}

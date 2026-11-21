// <copyright file="KillMoneyTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.NPC;

/// <summary>
/// The zen of a kill follows the kill's experience, but not the server's experience rate: with a rate of 1000 the
/// zen was 1000 times the normal amount.
/// </summary>
[TestFixture]
public class KillMoneyTest
{
    /// <summary>The server's experience rate is taken out of the experience the zen is based on.</summary>
    [TestCase(150_000, 1000f, 150)]
    [TestCase(150, 1f, 150)]
    [TestCase(300, 2f, 150)]
    [TestCase(75, 0.5f, 150)]
    [TestCase(0, 1000f, 0)]
    [TestCase(150, 0f, 150)]
    public void ZenDoesNotFollowTheServerExperienceRate(int gainedExperience, float rate, int expected)
    {
        Assert.That(KillMoney.ExperienceForMoney(gainedExperience, rate), Is.EqualTo(expected));
    }
}

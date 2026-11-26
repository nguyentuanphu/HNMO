// <copyright file="BlessPotionDamageTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;

/// <summary>
/// The Potion of Bless raises the damage only against Castle Siege gates and statues.
/// </summary>
[TestFixture]
public class BlessPotionDamageTest
{
    /// <summary>Tests when the damage is raised.</summary>
    /// <param name="hasBless">Whether the attacker has the Potion of Bless effect.</param>
    /// <param name="siegeStructure">Whether the defender is a Castle Siege gate or statue.</param>
    /// <param name="expected">Whether the damage is raised.</param>
    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void OnlySiegeStructuresAreHitHarder(bool hasBless, bool siegeStructure, bool expected)
    {
        Assert.That(AttackableExtensions.IsBlessBoosted(hasBless, siegeStructure), Is.EqualTo(expected));
    }
}

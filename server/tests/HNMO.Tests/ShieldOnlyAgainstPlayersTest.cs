// <copyright file="ShieldOnlyAgainstPlayersTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using Moq;

/// <summary>
/// Tests that the shield (SD) only takes damage from players (classic MU Season 6: monsters hit the life directly).
/// </summary>
[TestFixture]
public class ShieldOnlyAgainstPlayersTest
{
    /// <summary>
    /// A monster's hit on a player with SD goes fully to the life.
    /// </summary>
    [Test]
    public async ValueTask MonsterHitSkipsTheShieldAsync()
    {
        var defender = await CreateDefenderAsync().ConfigureAwait(false);
        var monster = new Mock<IAttacker>();
        monster.Setup(m => m.Attributes).Returns(new Mock<IAttributeSystem>().Object);

        var hitInfo = defender.GetHitInfo(1000, DamageAttributes.Undefined, monster.Object);

        Assert.Multiple(() =>
        {
            Assert.That(hitInfo.HealthDamage, Is.EqualTo(1000));
            Assert.That(hitInfo.ShieldDamage, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// A player's hit on a player with SD is split 90% to the SD and 10% to the life.
    /// </summary>
    [Test]
    public async ValueTask PlayerHitIsSplitWithTheShieldAsync()
    {
        var defender = await CreateDefenderAsync().ConfigureAwait(false);
        var attacker = await PlayerTestHelper.CreatePlayerAsync(defender.GameContext).ConfigureAwait(false);

        var hitInfo = defender.GetHitInfo(1000, DamageAttributes.Undefined, attacker);

        Assert.Multiple(() =>
        {
            Assert.That(hitInfo.HealthDamage, Is.EqualTo(100));
            Assert.That(hitInfo.ShieldDamage, Is.EqualTo(900));
        });
    }

    private static async ValueTask<Player> CreateDefenderAsync()
    {
        var defender = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        defender.Attributes![Stats.CurrentShield] = 5000;
        defender.Attributes.AddElement(new SimpleElement(0.9f, AggregateType.AddRaw), Stats.ShieldRatio);
        return defender;
    }
}

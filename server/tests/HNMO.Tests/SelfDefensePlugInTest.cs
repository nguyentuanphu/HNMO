// <copyright file="SelfDefensePlugInTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.DataModel.Configuration;

/// <summary>
/// Tests for <see cref="SelfDefensePlugIn"/>.
/// </summary>
[TestFixture]
public class SelfDefensePlugInTest
{
    /// <summary>
    /// Ensures that hitting an own summon doesn't start self-defense.
    /// </summary>
    [Test]
    public async ValueTask OwnSummonHitDoesNotStartSelfDefenseAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        var summonMock = new Mock<IAttackable>();
        var summonable = summonMock.As<ISummonable>();
        summonable.Setup(s => s.SummonedBy).Returns(player);
        summonable.Setup(s => s.Definition).Returns(new MonsterDefinition());

        var plugIn = new SelfDefensePlugIn();
        plugIn.AttackableGotHit(summonMock.Object, player, new HitInfo(1, 0, DamageAttributes.Undefined));

        Assert.That(player.GameContext.SelfDefenseState, Is.Empty);
    }
}
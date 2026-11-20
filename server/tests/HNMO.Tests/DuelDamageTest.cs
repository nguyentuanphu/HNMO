// <copyright file="DuelDamageTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests that duel hits do 60 % damage for both duelists, also with shield (Season 6/9 server suites: DamageReduction 60).
/// </summary>
[TestFixture]
public class DuelDamageTest
{
    /// <summary>
    /// Both duelists' hits at each other are reduced, although Season 6 players have a shield.
    /// </summary>
    [Test]
    public async ValueTask BothDuelistsHitWithSixtyPercentAsync()
    {
        var requester = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var opponent = await PlayerTestHelper.CreatePlayerAsync(requester.GameContext).ConfigureAwait(false);
        var room = new DuelRoom(new BasicModel.DuelArea(), requester, opponent);
        requester.DuelRoom = room;
        opponent.DuelRoom = room;

        Assert.Multiple(() =>
        {
            Assert.That(requester.Attributes![HNMO.GameLogic.Attributes.Stats.MaximumShield], Is.GreaterThan(0), "players have a shield");
            Assert.That(AttackableExtensions.GetDuelDamageFactor(requester, opponent), Is.EqualTo(0.6));
            Assert.That(AttackableExtensions.GetDuelDamageFactor(opponent, requester), Is.EqualTo(0.6));
        });
    }

    /// <summary>
    /// Hits outside of a duel, or at a third player, do full damage.
    /// </summary>
    [Test]
    public async ValueTask OtherHitsAreNotReducedAsync()
    {
        var requester = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var opponent = await PlayerTestHelper.CreatePlayerAsync(requester.GameContext).ConfigureAwait(false);
        var bystander = await PlayerTestHelper.CreatePlayerAsync(requester.GameContext).ConfigureAwait(false);

        Assert.That(AttackableExtensions.GetDuelDamageFactor(requester, opponent), Is.EqualTo(1));

        var room = new DuelRoom(new BasicModel.DuelArea(), requester, opponent);
        requester.DuelRoom = room;
        opponent.DuelRoom = room;

        Assert.Multiple(() =>
        {
            Assert.That(AttackableExtensions.GetDuelDamageFactor(requester, bystander), Is.EqualTo(1));
            Assert.That(AttackableExtensions.GetDuelDamageFactor(bystander, requester), Is.EqualTo(1));
            Assert.That(AttackableExtensions.GetDuelDamageFactor(null, requester), Is.EqualTo(1));
        });
    }
}

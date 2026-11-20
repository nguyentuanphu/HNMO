// <copyright file="SummonPartyDuelTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.Pathfinding;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The Dark Lord's party summon follows the rules of a warp: a duellist isn't pulled out of its duel.
/// </summary>
[TestFixture]
public class SummonPartyDuelTest
{
    /// <summary>A duellist stays; a member who isn't duelling is summoned.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask DuellistIsNotSummonedAsync()
    {
        var summoner = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var duellist = await PlayerTestHelper.CreatePlayerAsync(summoner.GameContext).ConfigureAwait(false);
        var opponent = await PlayerTestHelper.CreatePlayerAsync(summoner.GameContext).ConfigureAwait(false);
        var member = await PlayerTestHelper.CreatePlayerAsync(summoner.GameContext).ConfigureAwait(false);
        summoner.Position = new Point(100, 100);
        duellist.Position = new Point(150, 150);
        member.Position = new Point(160, 160);
        var room = new DuelRoom(new BasicModel.DuelArea(), duellist, opponent) { State = DuelState.DuelStarted };
        duellist.DuelRoom = room;
        opponent.DuelRoom = room;

        await new SummonPartySkillPlugin().SummonTargetsAsync(summoner, [duellist, member]).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(duellist.Position, Is.EqualTo(new Point(150, 150)), "the duellist stays in its duel");
            Assert.That(member.Position.EuclideanDistanceTo(summoner.Position), Is.LessThan(5), "the other member is summoned");
        });
    }
}

// <copyright file="DuelPlayerKillTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests that the duel exemptions (no outlaw state for the kill, no self-defense) only apply between
/// the two duelists of the same running duel, not to anything a duelist does to a third player.
/// </summary>
[TestFixture]
public class DuelPlayerKillTest
{
    /// <summary>
    /// Killing the duel opponent doesn't make the killer an outlaw.
    /// </summary>
    [Test]
    public async ValueTask KillOfTheDuelOpponentIsFreeAsync()
    {
        var (requester, opponent, _) = await CreateDuelAsync().ConfigureAwait(false);

        await requester.AfterKilledPlayerAsync(opponent).ConfigureAwait(false);

        Assert.That(requester.SelectedCharacter!.PlayerKillCount, Is.Zero);
    }

    /// <summary>
    /// A duelist who kills a third player is a player killer like anyone else.
    /// </summary>
    [Test]
    public async ValueTask KillOfAThirdPlayerByADuelistCountsAsync()
    {
        var (requester, _, bystander) = await CreateDuelAsync().ConfigureAwait(false);

        await requester.AfterKilledPlayerAsync(bystander).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(requester.SelectedCharacter!.PlayerKillCount, Is.EqualTo(1));
            Assert.That(requester.SelectedCharacter.State, Is.GreaterThan(HeroState.Normal));
        });
    }

    /// <summary>
    /// A duelist who kills a player of another running duel is a player killer, too.
    /// </summary>
    [Test]
    public async ValueTask KillOfADuelistOfAnotherDuelCountsAsync()
    {
        var (requester, _, _) = await CreateDuelAsync().ConfigureAwait(false);
        var otherRequester = await PlayerTestHelper.CreatePlayerAsync(requester.GameContext).ConfigureAwait(false);
        var otherOpponent = await PlayerTestHelper.CreatePlayerAsync(requester.GameContext).ConfigureAwait(false);
        var otherRoom = new DuelRoom(new BasicModel.DuelArea(), otherRequester, otherOpponent) { State = DuelState.DuelStarted };
        otherRequester.DuelRoom = otherRoom;
        otherOpponent.DuelRoom = otherRoom;

        await requester.AfterKilledPlayerAsync(otherOpponent).ConfigureAwait(false);

        Assert.That(requester.SelectedCharacter!.PlayerKillCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Hits between the duelists start no self-defense.
    /// </summary>
    [Test]
    public async ValueTask HitAtTheDuelOpponentStartsNoSelfDefenseAsync()
    {
        var (requester, opponent, _) = await CreateDuelAsync().ConfigureAwait(false);

        new SelfDefensePlugIn().AttackableGotHit(opponent, requester, new HitInfo(1, 0, DamageAttributes.Undefined));

        Assert.That(requester.GameContext.SelfDefenseState, Is.Empty);
    }

    /// <summary>
    /// A duelist who hits a third player gives that player the right of self-defense, so hitting back
    /// doesn't make the third player an outlaw.
    /// </summary>
    [Test]
    public async ValueTask HitAtAThirdPlayerByADuelistStartsSelfDefenseAsync()
    {
        var (requester, _, bystander) = await CreateDuelAsync().ConfigureAwait(false);

        new SelfDefensePlugIn().AttackableGotHit(bystander, requester, new HitInfo(1, 0, DamageAttributes.Undefined));

        Assert.That(bystander.IsSelfDefenseActive(requester), Is.True);
    }

    private static async ValueTask<(Player Requester, Player Opponent, Player Bystander)> CreateDuelAsync()
    {
        var requester = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var opponent = await PlayerTestHelper.CreatePlayerAsync(requester.GameContext).ConfigureAwait(false);
        var bystander = await PlayerTestHelper.CreatePlayerAsync(requester.GameContext).ConfigureAwait(false);
        var room = new DuelRoom(new BasicModel.DuelArea(), requester, opponent) { State = DuelState.DuelStarted };
        requester.DuelRoom = room;
        opponent.DuelRoom = room;
        return (requester, opponent, bystander);
    }
}

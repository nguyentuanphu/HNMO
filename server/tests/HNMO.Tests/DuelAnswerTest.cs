// <copyright file="DuelAnswerTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Duel;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// A duel request is answered once, by the requested player. A repeated answer (e.g. a resent packet)
/// took the entrance fees again and started the duel a second time.
/// </summary>
[TestFixture]
public class DuelAnswerTest
{
    private const int Fee = 30000;
    private const int Money = 100000;

    /// <summary>
    /// A second "accept", after both duelists arrived on the duel map, neither takes the entrance fees again
    /// nor cancels the duel which is starting.
    /// </summary>
    [Test]
    public async Task RepeatedAcceptTakesTheFeesOnceAsync()
    {
        var (requester, opponent) = await CreateRequestedDuelAsync().ConfigureAwait(false);
        var actions = new DuelActions();

        await actions.HandleDuelResponseAsync(opponent, requester, true).ConfigureAwait(false);

        // The game clients acknowledge the warp to the duel places.
        await requester.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        await opponent.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);

        var room = requester.DuelRoom;
        await actions.HandleDuelResponseAsync(opponent, requester, true).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(requester.DuelRoom, Is.SameAs(room), "the duel was cancelled");
            Assert.That(room!.State, Is.EqualTo(DuelState.DuelAccepted));
            Assert.That(requester.Money, Is.EqualTo(Money - Fee), "requester");
            Assert.That(opponent.Money, Is.EqualTo(Money - Fee), "opponent");
        });
    }

    /// <summary>
    /// The requester can't accept their own request.
    /// </summary>
    [Test]
    public async Task RequesterCannotAnswerOwnRequestAsync()
    {
        var (requester, opponent) = await CreateRequestedDuelAsync().ConfigureAwait(false);

        await new DuelActions().HandleDuelResponseAsync(requester, opponent, true).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(requester.DuelRoom?.State, Is.EqualTo(DuelState.DuelRequested));
            Assert.That(requester.Money, Is.EqualTo(Money));
        });
    }

    /// <summary>
    /// The first accept takes the fees.
    /// </summary>
    [Test]
    public async Task AcceptTakesTheFeesAsync()
    {
        var (requester, opponent) = await CreateRequestedDuelAsync().ConfigureAwait(false);

        await new DuelActions().HandleDuelResponseAsync(opponent, requester, true).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(requester.Money, Is.EqualTo(Money - Fee));
            Assert.That(opponent.Money, Is.EqualTo(Money - Fee));
        });
    }

    private static async Task<(Player Requester, Player Opponent)> CreateRequestedDuelAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var requester = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var opponent = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        // The duel places are on another map, as in the game: a warp within the duel map ends the duel.
        var map = new BasicModel.GameMapDefinition { Number = 1, TerrainData = new byte[ushort.MaxValue + 3] };
        context.Configuration.Maps.Add(map);

        ExitGate Gate(byte x) => new BasicModel.ExitGate { Map = map, X1 = x, X2 = x, Y1 = 100, Y2 = 100 };

        var area = new BasicModel.DuelArea
        {
            Index = 0,
            FirstPlayerGate = Gate(100),
            SecondPlayerGate = Gate(110),
            SpectatorsGate = Gate(120),
        };
        var configuration = new BasicModel.DuelConfiguration { EntranceFee = Fee, MaximumSpectatorsPerDuelRoom = 10, Exit = Gate(10) };
        configuration.DuelAreas.Add(area);
        context.Configuration.DuelConfiguration = configuration;
        ((GameContext)context).DuelRoomManager = new DuelRoomManager(configuration);

        requester.Money = Money;
        opponent.Money = Money;
        var room = await context.DuelRoomManager.GetFreeDuelRoomAsync(requester, opponent).ConfigureAwait(false);
        room!.State = DuelState.DuelRequested;
        requester.DuelRoom = room;
        opponent.DuelRoom = room;
        return (requester, opponent);
    }
}

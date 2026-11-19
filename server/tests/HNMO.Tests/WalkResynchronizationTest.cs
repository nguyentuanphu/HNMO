// <copyright file="WalkResynchronizationTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Threading.Tasks;
using Moq;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views.World;
using HNMO.Pathfinding;
using NUnit.Framework;

/// <summary>
/// The client moves its hero as soon as it sends a walk. These tests check that whenever the server does not apply a
/// walk, the client is put back to the server position (an instant move of the player to itself), so its hero never
/// stays on a cell where nobody else sees it.
/// </summary>
[TestFixture]
public class WalkResynchronizationTest
{
    private static readonly Point Start = new(100, 100);
    private static readonly Point Gate = new(120, 120);

    /// <summary>
    /// A walk that arrives between two maps (after a warp, before the client said it is ready on the new map) is not
    /// applied, and the client is put back once the player is on the map: the case where a browser walked right after
    /// <c>/move</c> and stayed elsewhere on its own screen for good.
    /// </summary>
    [Test]
    public async Task WalkWhileChangingMapsResynchronizesTheClientOnceOnTheMapAsync()
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);
        await player.WarpToAsync(GateOnSameMap(player)).ConfigureAwait(false);
        Assert.That(player.CurrentMap, Is.Null, "between the two maps until the client is ready");
        var movedBefore = InstantMovesOf(player);

        await player.WalkToAsync(new Point((byte)(Gate.X + 3), Gate.Y), StepsFrom(Gate, 3)).ConfigureAwait(false);

        Assert.That(InstantMovesOf(player), Is.EqualTo(movedBefore), "nothing to send while the player is on no map");

        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(player.CurrentMap, Is.Not.Null);
            Assert.That(player.Position, Is.EqualTo(Gate), "the dropped walk did not move the player");
            Assert.That(player.IsWalking, Is.False);
            Assert.That(InstantMovesOf(player), Is.EqualTo(movedBefore + 1), "the client is put back to the gate once on the map");
        });
    }

    /// <summary>
    /// A map change without a walk in between sends no extra position: the resynchronization is only for a dropped walk,
    /// and it is sent once.
    /// </summary>
    [Test]
    public async Task MapChangeWithoutAWalkSendsNoExtraPositionAsync()
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);
        await player.WarpToAsync(GateOnSameMap(player)).ConfigureAwait(false);
        var movedBefore = InstantMovesOf(player);

        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        Assert.That(InstantMovesOf(player), Is.EqualTo(movedBefore));

        // A later map change doesn't repeat an earlier resynchronization either.
        await player.WarpToAsync(GateOnSameMap(player)).ConfigureAwait(false);
        await player.WalkToAsync(new Point((byte)(Gate.X + 1), Gate.Y), StepsFrom(Gate, 1)).ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        var afterResync = InstantMovesOf(player);
        await player.WarpToAsync(GateOnSameMap(player)).ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        Assert.That(InstantMovesOf(player), Is.EqualTo(afterResync));
    }

    /// <summary>
    /// A player who can't move (frozen, stunned or asleep) keeps its position, and the client is put back right away.
    /// </summary>
    /// <param name="state">The attribute which holds the player.</param>
    [TestCase(nameof(Stats.IsFrozen))]
    [TestCase(nameof(Stats.IsStunned))]
    [TestCase(nameof(Stats.IsAsleep))]
    public async Task WalkWhileUnableToMoveResynchronizesTheClientAsync(string state)
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);
        var attribute = state switch
        {
            nameof(Stats.IsFrozen) => Stats.IsFrozen,
            nameof(Stats.IsStunned) => Stats.IsStunned,
            _ => Stats.IsAsleep,
        };
        player.Attributes!.AddElement(new SimpleElement(1, AggregateType.AddRaw), attribute);
        var movedBefore = InstantMovesOf(player);

        await player.WalkToAsync(new Point((byte)(Start.X + 3), Start.Y), StepsFrom(Start, 3)).ConfigureAwait(false);

        AssertKeptAndResynchronized(player, Start, movedBefore);
    }

    /// <summary>
    /// A walk without steps keeps the position, and the client is put back right away.
    /// </summary>
    [Test]
    public async Task WalkWithoutStepsResynchronizesTheClientAsync()
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);
        var movedBefore = InstantMovesOf(player);

        await player.WalkToAsync(new Point((byte)(Start.X + 3), Start.Y), System.Memory<WalkingStep>.Empty).ConfigureAwait(false);

        AssertKeptAndResynchronized(player, Start, movedBefore);
    }

    /// <summary>
    /// A walk which arrives before the player's attributes are set (a moment while entering the world) keeps the
    /// position, and the client is put back right away.
    /// </summary>
    [Test]
    public async Task WalkBeforeTheAttributesAreSetResynchronizesTheClientAsync()
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);
        typeof(Player).GetProperty(nameof(Player.Attributes))!.SetValue(player, null);
        var movedBefore = InstantMovesOf(player);

        await player.WalkToAsync(new Point((byte)(Start.X + 3), Start.Y), StepsFrom(Start, 3)).ConfigureAwait(false);

        AssertKeptAndResynchronized(player, Start, movedBefore);
    }

    /// <summary>
    /// A walk the server applies sends no instant move: the resynchronization is only for walks it does not apply.
    /// </summary>
    [Test]
    public async Task AppliedWalkSendsNoInstantMoveAsync()
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);
        var movedBefore = InstantMovesOf(player);

        await player.WalkToAsync(new Point((byte)(Start.X + 1), Start.Y), StepsFrom(Start, 1)).ConfigureAwait(false);

        Assert.That(InstantMovesOf(player), Is.EqualTo(movedBefore));
    }

    private static async Task<Player> CreatePlayerAtStartAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Position = Start;
        return player;
    }

    private static ExitGate GateOnSameMap(Player player) => new()
    {
        Map = player.CurrentMap?.Definition ?? player.SelectedCharacter!.CurrentMap,
        X1 = Gate.X,
        X2 = Gate.X,
        Y1 = Gate.Y,
        Y2 = Gate.Y,
        Direction = Direction.South,
    };

    private static WalkingStep[] StepsFrom(Point from, int count)
    {
        var steps = new WalkingStep[count];
        for (var i = 0; i < count; i++)
        {
            steps[i] = new WalkingStep
            {
                From = new Point((byte)(from.X + i), from.Y),
                To = new Point((byte)(from.X + i + 1), from.Y),
                Direction = Direction.East,
            };
        }

        return steps;
    }

    private static int InstantMovesOf(Player player)
    {
        var plugIn = player.ViewPlugIns.GetPlugIn<IObjectMovedPlugIn>();
        Assert.That(plugIn, Is.Not.Null);
        return Mock.Get(plugIn!).Invocations.Count(i =>
            i.Method.Name == nameof(IObjectMovedPlugIn.ObjectMovedAsync)
            && ReferenceEquals(i.Arguments[0], player)
            && (MoveType)i.Arguments[1] == MoveType.Instant);
    }

    private static void AssertKeptAndResynchronized(Player player, Point position, int movedBefore)
    {
        Assert.Multiple(() =>
        {
            Assert.That(player.Position, Is.EqualTo(position), "the server keeps its position");
            Assert.That(player.IsWalking, Is.False);
            Assert.That(InstantMovesOf(player), Is.EqualTo(movedBefore + 1), "the client is put back to the server position");
        });
    }
}

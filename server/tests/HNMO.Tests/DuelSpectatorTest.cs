// <copyright file="DuelSpectatorTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Duel;
using HNMO.GameLogic.PlugIns;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// A duel spectator is made invisible while watching. Leaving the duel has to make it visible again,
/// otherwise the monsters keep ignoring it for a whole day.
/// </summary>
[TestFixture]
public class DuelSpectatorTest
{
    /// <summary>The quit request of a spectator removes it from the room and makes it visible again.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task QuittingSpectatorBecomesVisibleAgainAsync()
    {
        var (room, spectator) = await CreateRoomWithSpectatorAsync().ConfigureAwait(false);
        Assert.That(spectator.Attributes![Stats.IsInvisible], Is.GreaterThan(0));
        Assert.That(spectator.DuelRoom, Is.SameAs(room));

        await new DuelActions().HandleDuelChannelQuitRequestAsync(spectator).ConfigureAwait(false);

        Assert.That(room.Spectators, Does.Not.Contain(spectator));
        Assert.That(spectator.DuelRoom, Is.Null);
        Assert.That(spectator.Attributes[Stats.IsInvisible], Is.Zero);
    }

    /// <summary>Removing a spectator doesn't fail, also when other spectators are still watching.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RemovingSpectatorDoesNotThrowAsync()
    {
        var (room, spectator) = await CreateRoomWithSpectatorAsync().ConfigureAwait(false);
        var other = await PlayerTestHelper.CreatePlayerAsync(room.Requester.GameContext).ConfigureAwait(false);
        Assert.That(await room.TryAddSpectatorAsync(other).ConfigureAwait(false), Is.True);

        Assert.DoesNotThrowAsync(async () => await room.RemoveSpectatorAsync(spectator).ConfigureAwait(false));
        Assert.That(spectator.Attributes![Stats.IsInvisible], Is.Zero);
        Assert.That(room.Spectators, Is.EquivalentTo(new[] { other }));
    }

    /// <summary>A spectator who is still in the room when the duel ends gets visible again.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpectatorBecomesVisibleWhenTheRoomIsDisposedAsync()
    {
        var (room, spectator) = await CreateRoomWithSpectatorAsync().ConfigureAwait(false);

        await room.DisposeAsync().ConfigureAwait(false);

        Assert.That(spectator.Attributes![Stats.IsInvisible], Is.Zero);
        Assert.That(spectator.DuelRoom, Is.Null);
    }

    /// <summary>A player who already is in a duel can't watch another one.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task DuelistCantBecomeSpectatorAsync()
    {
        var (room, _) = await CreateRoomWithSpectatorAsync().ConfigureAwait(false);

        Assert.That(await room.TryAddSpectatorAsync(room.Requester).ConfigureAwait(false), Is.False);
        Assert.That(room.Requester.Attributes![Stats.IsInvisible], Is.Zero);
    }

    /// <summary>A spectator may warp away; only the duelists can't while the duel runs.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpectatorMayWarpDuringTheDuelAsync()
    {
        var (room, spectator) = await CreateRoomWithSpectatorAsync().ConfigureAwait(false);
        room.State = DuelState.DuelStarted;
        var map = spectator.CurrentMap!.Definition;

        Assert.That(HnmoWarpGuard.Refusal(spectator, map), Is.Null);
        Assert.That(HnmoWarpGuard.Refusal(room.Requester, map)?.En, Does.Contain("duel"));
    }

    private static async Task<(DuelRoom Room, Player Spectator)> CreateRoomWithSpectatorAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        context.Configuration.MagicEffects.Add(new BasicModel.MagicEffectDefinition
        {
            Number = 18,
            InformObservers = false,
            Duration = new BasicModel.PowerUpDefinitionValue { ConstantValue = { Value = (float)TimeSpan.FromDays(1).TotalSeconds } },
            PowerUpDefinitions =
            {
                new BasicModel.PowerUpDefinition
                {
                    TargetAttribute = Stats.IsInvisible,
                    Boost = new BasicModel.PowerUpDefinitionValue { ConstantValue = { Value = 1, AggregateType = AggregateType.AddRaw } },
                },
            },
        });

        var requester = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var opponent = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var spectator = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var map = requester.CurrentMap!.Definition;

        ExitGate Gate(byte x) => new BasicModel.ExitGate { Map = map, X1 = x, X2 = x, Y1 = 100, Y2 = 100 };

        var area = new BasicModel.DuelArea
        {
            Index = 0,
            FirstPlayerGate = Gate(100),
            SecondPlayerGate = Gate(110),
            SpectatorsGate = Gate(120),
        };
        context.Configuration.DuelConfiguration = new BasicModel.DuelConfiguration { MaximumSpectatorsPerDuelRoom = 10, Exit = Gate(10) };
        context.Configuration.DuelConfiguration.DuelAreas.Add(area);

        var room = new DuelRoom(area, requester, opponent);
        requester.DuelRoom = room;
        opponent.DuelRoom = room;

        Assert.That(await room.TryAddSpectatorAsync(spectator).ConfigureAwait(false), Is.True);

        // The game client acknowledges the warp to the spectators place.
        await spectator.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        return (room, spectator);
    }
}

// <copyright file="WarpWhileTradingTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// The warp of the M menu and /move, and walking into a gate, refuse a player with an open trade or a dead one,
/// like HNMO's own warps. A trade state can't change to the map change state, so the warp left the player
/// off the map without a map change, and the zen was paid.
/// </summary>
[TestFixture]
public class WarpWhileTradingTest
{
    /// <summary>A warp with an open trade is refused, keeps the zen and the player on its map.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WarpWithOpenTradeIsRefusedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        player.TryAddMoney(10_000);
        var warpInfo = CreateWarpInfo(player);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.TradeRequested).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.TradeOpened).ConfigureAwait(false);

        await new WarpAction().WarpToAsync(player, warpInfo).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(10_000));
        Assert.That(player.CurrentMap, Is.Not.Null);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.TradeOpened));
    }

    /// <summary>A dead player can't warp.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task DeadPlayerCantWarpAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        player.TryAddMoney(10_000);
        player.IsAlive = false;

        await new WarpAction().WarpToAsync(player, CreateWarpInfo(player)).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(10_000));
        Assert.That(player.CurrentMap, Is.Not.Null);
    }

    /// <summary>A free, alive player still warps and pays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FreePlayerWarpsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        player.TryAddMoney(10_000);

        await new WarpAction().WarpToAsync(player, CreateWarpInfo(player)).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(8_000));
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.ChangingMap));
    }

    private static BasicModel.WarpInfo CreateWarpInfo(Player player)
    {
        var gate = new BasicModel.ExitGate { Map = player.CurrentMap!.Definition, X1 = 120, X2 = 120, Y1 = 120, Y2 = 120 };
        return new BasicModel.WarpInfo { Index = 1, Costs = 2_000, LevelRequirement = 0, Gate = gate };
    }
}

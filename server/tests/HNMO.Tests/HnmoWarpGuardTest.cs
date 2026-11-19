// <copyright file="HnmoWarpGuardTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.TeleportNpc;

/// <summary>
/// HNMO's own warps (spouse warp, Teleport NPC) check what the game's warps check (audit 1.10).
/// </summary>
[TestFixture]
public class HnmoWarpGuardTest
{
    /// <summary>An alive player in the world may warp; a dead or trading one may not.</summary>
    [Test]
    public async Task AliveAndFreePlayersMayWarpAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var target = player.CurrentMap!.Definition;
        player.IsAlive = true;
        Assert.That(HnmoWarpGuard.Refusal(player, target), Is.Null);

        player.IsAlive = false;
        Assert.That(HnmoWarpGuard.Refusal(player, target)?.En, Does.Contain("dead"));
        player.IsAlive = true;

        await player.PlayerState.TryAdvanceToAsync(PlayerState.TradeRequested).ConfigureAwait(false);
        Assert.That(HnmoWarpGuard.Refusal(player, target)?.En, Does.Contain("trade"));
    }

    /// <summary>A player who didn't talk to a Teleport NPC isn't next to one.</summary>
    [Test]
    public async Task TheTeleportNpcMustBeNearbyAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        Assert.That(TeleportNpcPlugIn.IsNearTeleportNpc(player), Is.False);
    }
}

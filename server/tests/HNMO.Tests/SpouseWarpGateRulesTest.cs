// <copyright file="SpouseWarpGateRulesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.ComponentModel;
using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Marriage;

/// <summary>
/// The spouse warp applies the rules of the warp gate plug-ins (e.g. only Gens members into the Gens
/// battle zone, the closed Raklion boss room), like the warps of the M menu, /move and the gates.
/// </summary>
[TestFixture]
public class SpouseWarpGateRulesTest
{
    /// <summary>A warp gate rule which refuses the target map refuses the spouse warp too.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpouseWarpIsRefusedByTheGateRulesAsync()
    {
        var context = (GameContext)GameContextTestHelper.CreateGameContext();
        var rules = new RefusingGateRules();
        context.PlugInManager.RegisterPlugInAtPlugInPoint<IWarpGateEnteringPlugIn>(rules);

        var (warping, _, plugIn) = await CreateCoupleAsync(context).ConfigureAwait(false);

        await plugIn.TeleportToSpouseAsync(warping).ConfigureAwait(false);

        Assert.That(rules.Calls, Is.EqualTo(1));
        Assert.That(warping.CurrentMap, Is.Not.Null);
        Assert.That(warping.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
    }

    /// <summary>Without a refusing rule the spouse warp still works.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpouseWarpWorksWithoutRefusalAsync()
    {
        var context = (GameContext)GameContextTestHelper.CreateGameContext();
        var (warping, _, plugIn) = await CreateCoupleAsync(context).ConfigureAwait(false);

        await plugIn.TeleportToSpouseAsync(warping).ConfigureAwait(false);

        Assert.That(warping.PlayerState.CurrentState, Is.EqualTo(PlayerState.ChangingMap));
    }

    private static async Task<(Player Warping, Player Spouse, MarriagePlugIn PlugIn)> CreateCoupleAsync(GameContext context)
    {
        var romeo = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var juliet = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        romeo.SelectedCharacter!.Name = "Romeo";
        juliet.SelectedCharacter!.Name = "Juliet";
        await context.AddPlayerAsync(romeo).ConfigureAwait(false);
        await context.AddPlayerAsync(juliet).ConfigureAwait(false);

        var plugIn = new MarriagePlugIn { Configuration = new MarriageConfiguration { FileName = string.Empty, AllowSpouseWarp = true, RequireCeremonyLocation = false } };
        var engine = plugIn.GetEngine();
        var now = DateTime.UtcNow;
        Assume.That(engine.Propose("Romeo", "romeo", 400, "Juliet", 400, now), Is.EqualTo(MarriageActionResult.Success));
        Assume.That(engine.AcceptCeremony("Juliet", "juliet", 400, 400, true, now, out _), Is.EqualTo(MarriageActionResult.Success));
        return (romeo, juliet, plugIn);
    }

    /// <summary>A warp gate rule which refuses every warp, e.g. like the Gens battle zone for a player without Gens.</summary>
    [Guid("6C1F8E3A-2B7D-4E59-9A40-1D3C5B7E9F21")]
    private sealed class RefusingGateRules : IWarpGateEnteringPlugIn
    {
        public int Calls { get; private set; }

        public ValueTask WarpGateEnteringAsync(Player player, ExitGate targetGate, CancelEventArgs eventArgs)
        {
            this.Calls++;
            eventArgs.Cancel = true;
            return ValueTask.CompletedTask;
        }
    }
}

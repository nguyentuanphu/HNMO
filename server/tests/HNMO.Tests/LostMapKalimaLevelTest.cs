// <copyright file="LostMapKalimaLevelTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlugIns;
using HNMO.Pathfinding;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// A Lost Map opens Kalima with the same limits as the Kalima ticket: only up to the level of Kalima which
/// the character level allows, and not inside Kalima.
/// </summary>
[TestFixture]
public class LostMapKalimaLevelTest
{
    private const int KalimaSevenMapNumber = 36;
    private const short KalimaSevenGateNumber = 158;

    /// <summary>A level 50 character can't open Kalima 7 with a +7 Lost Map.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LowLevelCantOpenHighKalimaAsync()
    {
        var player = await CreatePlayerAsync(level: 50).ConfigureAwait(false);

        var args = await DropLostMapAsync(player, 7).ConfigureAwait(false);

        Assert.That(args.Success, Is.False);
        Assert.That(player.CurrentMap!.GetAttackablesInRange(player.Position, 5).OfType<GateNpc>(), Is.Empty);
    }

    /// <summary>A character of the level of Kalima 7 opens it.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HighLevelOpensKalimaAsync()
    {
        var player = await CreatePlayerAsync(level: 350).ConfigureAwait(false);

        var args = await DropLostMapAsync(player, 7).ConfigureAwait(false);

        Assert.That(args.Success, Is.True);
    }

    /// <summary>A Lost Map doesn't open a gate inside Kalima, like the ticket.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NoGateInsideKalimaAsync()
    {
        var player = await CreatePlayerAsync(level: 350).ConfigureAwait(false);
        var kalima = await player.GameContext.GetMapAsync(KalimaSevenMapNumber).ConfigureAwait(false);
        await player.CurrentMap!.RemoveAsync(player).ConfigureAwait(false);
        player.CurrentMap = kalima;

        var args = await DropLostMapAsync(player, 7).ConfigureAwait(false);

        Assert.That(args.Success, Is.False);
    }

    private static async Task<IItemDropPlugIn.ItemDropArguments> DropLostMapAsync(Player player, byte level)
    {
        var lostMap = new BasicModel.Item
        {
            Definition = new BasicModel.ItemDefinition { Group = KalimaConstants.LostMapGroup, Number = KalimaConstants.LostMapNumber },
            Level = level,
            Durability = 1,
        };
        var args = new IItemDropPlugIn.ItemDropArguments();
        await new LostMapDroppedPlugIn().HandleItemDropAsync(player, lostMap, new Point(150, 150), args).ConfigureAwait(false);
        return args;
    }

    private static async Task<Player> CreatePlayerAsync(int level)
    {
        var context = GameContextTestHelper.CreateGameContext();
        var configuration = context.Configuration;

        var kalima = new BasicModel.GameMapDefinition { Number = KalimaSevenMapNumber, TerrainData = new byte[ushort.MaxValue + 3] };
        kalima.ExitGates.Add(new BasicModel.ExitGate { Map = kalima, X1 = 20, X2 = 20, Y1 = 20, Y2 = 20, IsSpawnGate = true });
        configuration.Maps.Add(kalima);
        configuration.Monsters.Add(new BasicModel.MonsterDefinition { Number = KalimaSevenGateNumber, Designation = "Kalima 7 gate" });

        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Attributes![Stats.Level] = level;
        player.Position = new Point(150, 150);
        return player;
    }
}

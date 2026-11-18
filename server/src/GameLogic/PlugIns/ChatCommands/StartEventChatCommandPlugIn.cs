// <copyright file="StartEventChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.PlugIns;

/// <summary>GM command (<c>/startevent &lt;bc|cc|ds|it|kanturu&gt;</c>) force-starting a scheduled event.</summary>
[Guid("9E10C7A1-4F6D-4B58-902A-31E84A810F02")]
[PlugIn]
[Display(Name = "Start Event Command", Description = "Starts a mini-game event by short key (bc, cc, ds, it, kanturu).")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public sealed class StartEventChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/startevent";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var code = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;
        MiniGameType? type = code switch
        {
            "bc" or "bloodcastle" => MiniGameType.BloodCastle,
            "cc" or "chaoscastle" => MiniGameType.ChaosCastle,
            "ds" or "devilsquare" => MiniGameType.DevilSquare,
            "it" or "illusiontemple" => MiniGameType.IllusionTemple,
            "kanturu" => MiniGameType.Kanturu,
            _ => null,
        };

        if (type is not { } miniGame)
        {
            await player.ShowBlueMessageAsync("Usage: /startevent <bc|cc|ds|it|kanturu>").ConfigureAwait(false);
            return;
        }

        var result = await GameMasterEventActions.StartAsync(player.GameContext, miniGame, true, System.Threading.CancellationToken.None).ConfigureAwait(false);
        await player.ShowBlueMessageAsync(result.Succeeded
            ? $"Requested {miniGame} start."
            : (result.Error ?? $"Failed to start {miniGame}.")).ConfigureAwait(false);
    }
}

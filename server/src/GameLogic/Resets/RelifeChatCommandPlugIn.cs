// <copyright file="RelifeChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Resets;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.PlugIns;

/// <summary>
/// The <c>/relife</c> chat command: the grand reset of HNMO's reset options (<c>/relife info</c> tells what it needs).
/// </summary>
[Guid("5B8E2D14-C7A3-4F96-8D01-3A6E9C42F7B5")]
[PlugIn]
[Display(Name = nameof(PlugInResources.RelifeChatCommandPlugIn_Name), Description = nameof(PlugInResources.RelifeChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public class RelifeChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/relife";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (ResetOptionsPlugIn.Get(player.GameContext) is not { } options)
        {
            await player.ShowBlueMessageAsync(player.Culture.TwoLetterISOLanguageName == "vi" ? "Máy chủ này chưa bật relife." : "Relifes aren't on for this server.").ConfigureAwait(false);
            return;
        }

        var argument = command.Length > Command.Length ? command[Command.Length..].Trim().ToLowerInvariant() : string.Empty;
        if (argument is "info" or "?")
        {
            await options.InfoAsync(player).ConfigureAwait(false);
            return;
        }

        await options.RelifeAsync(player).ConfigureAwait(false);
    }
}

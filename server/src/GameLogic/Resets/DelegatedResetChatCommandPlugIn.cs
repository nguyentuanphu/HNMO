// <copyright file="DelegatedResetChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Resets;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.PlugIns;

/// <summary>
/// The <c>/autoreset on|off</c> chat command: switches the delegated reset of HNMO's reset options (the server resets
/// the character while it's offline leveling). Without an argument it tells whether it's on.
/// </summary>
[Guid("E2A47C91-3D58-4B60-A7F4-91C3D2E85B06")]
[PlugIn]
[Display(Name = nameof(PlugInResources.DelegatedResetChatCommandPlugIn_Name), Description = nameof(PlugInResources.DelegatedResetChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public class DelegatedResetChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/autoreset";

    /// <inheritdoc />
    public virtual string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <summary>
    /// Reads "on" or "off" (also "bat" / "tat").
    /// </summary>
    /// <param name="argument">The argument.</param>
    /// <returns>On, off, or <c>null</c> for anything else.</returns>
    public static bool? ParseSwitch(string argument) => argument.Trim().ToLowerInvariant() switch
    {
        "on" or "1" or "bat" or "bật" => true,
        "off" or "0" or "tat" or "tắt" => false,
        _ => null,
    };

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (ResetOptionsPlugIn.Get(player.GameContext) is not { } options)
        {
            await player.ShowBlueMessageAsync(player.Culture.TwoLetterISOLanguageName == "vi" ? "Máy chủ này chưa bật ủy thác reset." : "Delegated resets aren't on for this server.").ConfigureAwait(false);
            return;
        }

        var key = command.Split(' ', 2)[0];
        var argument = command.Length > key.Length ? command[key.Length..] : string.Empty;
        await options.SetDelegatedAsync(player, ParseSwitch(argument)).ConfigureAwait(false);
    }
}

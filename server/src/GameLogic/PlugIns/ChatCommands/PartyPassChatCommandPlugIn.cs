// <copyright file="PartyPassChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.PartyOptions;
using HNMO.PlugIns;

/// <summary>
/// <c>/partypass PASS</c> or <c>/partypass clear</c>: set a party password or join a passworded party without an invite.
/// </summary>
[Guid("1D9C4B60-3F2A-48E7-A112-8C6F5E4D2099")]
[PlugIn]
[Display(Name = "Party password command (/partypass)", Description = "Lets players set a party password or join a party by password with /partypass.")]
[ChatCommandHelp(Command, typeof(AccountSafetyChatCommandArgs), CharacterStatus.Normal)]
public class PartyPassChatCommandPlugIn : ChatCommandPlugInBase<AccountSafetyChatCommandArgs>
{
    private const string Command = "/partypass";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public override async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (PartyOptionsPlugIn.Get(player.GameContext) is not { Configuration: not null } plugIn)
        {
            return;
        }

        var space = command.IndexOf(' ');
        var arg = space >= 0 ? command[(space + 1)..].Trim() : string.Empty;
        await plugIn.HandlePartyPassCommandAsync(player, arg).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask DoHandleCommandAsync(Player player, AccountSafetyChatCommandArgs arguments) =>
        ValueTask.CompletedTask;
}

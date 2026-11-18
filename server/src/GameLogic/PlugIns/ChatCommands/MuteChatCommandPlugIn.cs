// <copyright file="MuteChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>GM command alias (<c>/mute &lt;character&gt; &lt;minutes&gt;</c>).</summary>
[Guid("A4E61D22-8C24-4B9E-8F9B-48DF1539A201")]
[PlugIn]
[Display(Name = "Mute Player Command", Description = "Mutes a target player's chat for the specified minutes.")]
[ChatCommandHelp(Command, typeof(ChatBanCharChatCommandArgs), CharacterStatus.GameMaster)]
public sealed class MuteChatCommandPlugIn : ChatBanCharChatCommandPlugIn
{
    private const string Command = "/mute";

    /// <inheritdoc />
    public override string Key => Command;
}

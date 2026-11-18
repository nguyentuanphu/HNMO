// <copyright file="KickChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>GM command alias (<c>/kick &lt;character&gt;</c>).</summary>
[Guid("B5F72E33-9D35-4CA0-90AC-59EF2640B302")]
[PlugIn]
[Display(Name = "Kick Player Command", Description = "Disconnects a target player immediately.")]
[ChatCommandHelp(Command, typeof(DisconnectChatCommandArgs), CharacterStatus.GameMaster)]
public sealed class KickChatCommandPlugIn : DisconnectChatCommandPlugIn
{
    private const string Command = "/kick";

    /// <inheritdoc />
    public override string Key => Command;
}

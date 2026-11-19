// <copyright file="GoToPlayerChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>GM command alias (<c>/goto &lt;character&gt;</c>).</summary>
[Guid("C6083F44-AE46-4DB1-A1BD-6AF03751C403")]
[PlugIn]
[Display(Name = "Go To Player Command", Description = "Moves the Game Master to a target player's map and coordinates.")]
[ChatCommandHelp(Command, typeof(TraceChatCommandArgs), CharacterStatus.GameMaster)]
public sealed class GoToPlayerChatCommandPlugIn : TraceChatCommandPlugIn
{
    private const string Command = "/goto";

    /// <inheritdoc />
    public override string Key => Command;
}

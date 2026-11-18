// <copyright file="SummonPlayerChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>GM command alias (<c>/summon &lt;character&gt;</c>).</summary>
[Guid("D7194055-BF57-4EC2-B2CE-7B014862D504")]
[PlugIn]
[Display(Name = "Summon Player Command", Description = "Brings a target player to the Game Master's coordinates.")]
[ChatCommandHelp(Command, typeof(TraceChatCommandArgs), CharacterStatus.GameMaster)]
public sealed class SummonPlayerChatCommandPlugIn : TrackChatCommandPlugIn
{
    private const string Command = "/summon";

    /// <inheritdoc />
    public override string Key => Command;
}

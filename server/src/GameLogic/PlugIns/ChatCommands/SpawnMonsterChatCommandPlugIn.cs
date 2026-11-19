// <copyright file="SpawnMonsterChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>GM command alias (<c>/spawn &lt;monsterNumber&gt;</c>).</summary>
[Guid("E82A5166-C068-4FD3-C3DF-8C125973E605")]
[PlugIn]
[Display(Name = "Spawn Monster Command", Description = "Spawns a monster or boss at the Game Master's coordinates.")]
[ChatCommandHelp(Command, typeof(CreateMonsterChatCommandArgs), CharacterStatus.GameMaster)]
internal sealed class SpawnMonsterChatCommandPlugIn : CreateMonsterChatCommand
{
    private const string Command = "/spawn";

    /// <inheritdoc />
    public override string Key => Command;
}

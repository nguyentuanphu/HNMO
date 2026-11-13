// <copyright file="WalkMonsterChatCommand.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>
/// Chat command to let a monster walk to specific coordinates.
/// </summary>
[Guid("1852FED5-8184-431E-8C5F-5131356D348F")]
[PlugIn]
[Display(Name = nameof(PlugInResources.WalkMonsterChatCommand_Name), Description = nameof(PlugInResources.WalkMonsterChatCommand_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(MoveMonsterCommandArgs), CharacterStatus.GameMaster)]
internal class WalkMonsterChatCommand : ChatCommandPlugInBase<MoveMonsterCommandArgs>
{
    private const string Command = "/walkmonster";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, MoveMonsterCommandArgs arguments)
    {
        var monster = gameMaster.ObservingBuckets.SelectMany(b => b).OfType<Monster>().FirstOrDefault(m => m.Id == arguments.Id);
        if (monster is null)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MonsterNotFoundById), arguments.Id).ConfigureAwait(false);
            return;
        }

        await monster.WalkToAsync(arguments.Coordinates).ConfigureAwait(false);
    }
}
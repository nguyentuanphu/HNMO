// <copyright file="ShowNpcIdsChatCommand.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Views.NPC;
using HNMO.PlugIns;

/// <summary>
/// Chat command to request the ids of all NPCs in the range of the player.
/// </summary>
[Guid("498D0205-388F-410A-A7C7-49069A64D3A9")]
[PlugIn]
[Display(ResourceType = typeof(PlugInResources), Name = "ShowNpcIdsChatCommand_ShowNpcIdsChatCommand_Name", Description = "ShowNpcIdsChatCommand_ShowNpcIdsChatCommand_Description")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
internal sealed class ShowNpcIdsChatCommand : IChatCommandPlugIn
{
    private const string Command = "/showids";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc/>
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc/>
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var monsters = player.ObservingBuckets.SelectMany(b => b).OfType<NonPlayerCharacter>().ToList();
        foreach (var monster in monsters)
        {
            await player.InvokeViewPlugInAsync<IShowMessageOfObjectPlugIn>(p => p.ShowMessageOfObjectAsync($"{monster.Id}", monster)).ConfigureAwait(false);
        }
    }
}
// <copyright file="ShowFireworksEffectChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.GameLogic.Views.Character;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// A chat command plugin which handles fireworks effect commands.
/// </summary>
[Guid("658F7F9D-B8FF-4D52-A835-5B3D658B6B9F")]
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowFireworksEffectChatCommandPlugIn_Name), Description = nameof(PlugInResources.ShowFireworksEffectChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(CoordinatesCommandArgs), CharacterStatus.GameMaster)]
public class ShowFireworksEffectChatCommandPlugIn : ChatCommandPlugInBase<CoordinatesCommandArgs>
{
    private const string Command = "/fireworks";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc/>
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, CoordinatesCommandArgs arguments)
    {
        var coordinates = arguments.X == 0 ? gameMaster.Position : new Point(arguments.X, arguments.Y);
        await gameMaster.ForEachWorldObserverAsync<IShowItemDropEffectPlugIn>(p => p.ShowEffectAsync(ItemDropEffect.Fireworks, coordinates), true).ConfigureAwait(false);
    }
}
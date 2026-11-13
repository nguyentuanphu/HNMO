// <copyright file="ResetChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Resets;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.PlugIns;

/// <summary>
/// A chat command plugin which handles reset command.
/// </summary>
/// <seealso cref="HNMO.GameLogic.PlugIns.ChatCommands.IChatCommandPlugIn" />
[Guid("90B35404-AADE-4F22-B5D2-4CD59B8BB4C8")]
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetChatCommandPlugIn_Name), Description = nameof(PlugInResources.ResetChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command)]
public class ResetChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/reset";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var resetAction = new ResetCharacterAction(player);
        await resetAction.ResetCharacterAsync().ConfigureAwait(false);
    }
}
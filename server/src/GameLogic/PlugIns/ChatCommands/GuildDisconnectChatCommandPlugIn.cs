// <copyright file="GuildDisconnectChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>
/// A chat command plugin which handles gm disconnect commands.
/// </summary>
[Guid("F23262E6-0D7C-4B9C-8CD5-7E44AF4EE469")]
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildDisconnectChatCommandPlugIn_Name), Description = nameof(PlugInResources.GuildDisconnectChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(GuildDisconnectChatCommandArgs), CharacterStatus.GameMaster)]
public class GuildDisconnectChatCommandPlugIn : ChatCommandPlugInBase<GuildDisconnectChatCommandArgs>
{
    private const string Command = "/guilddisconnect";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, GuildDisconnectChatCommandArgs arguments)
    {
        var guildId = await this.GetGuildIdByNameAsync(gameMaster, arguments.GuildName!).ConfigureAwait(false);
        if (guildId is null || gameMaster.GameContext is not IGameServerContext gameServerContext)
        {
            return;
        }

        await gameServerContext.ForEachGuildPlayerAsync(guildId.Value, async guildPlayer =>
        {
            await guildPlayer.DisconnectAsync().ConfigureAwait(false);

            if (!guildPlayer.Name.Equals(gameMaster.Name))
            {
                await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.GuildDisconnectResult), this.Key, guildPlayer.Name).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }
}
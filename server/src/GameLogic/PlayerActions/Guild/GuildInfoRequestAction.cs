// <copyright file="GuildInfoRequestAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Guild;

using HNMO.GameLogic.Views.Guild;

/// <summary>
/// Action to request the information (name, symbol) of a guild.
/// </summary>
public class GuildInfoRequestAction
{
    /// <summary>
    /// Requests the guild information.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="guildId">The guild identifier.</param>
    public async ValueTask RequestGuildInfoAsync(Player player, uint guildId)
    {
        await player.InvokeViewPlugInAsync<IShowGuildInfoPlugIn>(p => p.ShowGuildInfoAsync(guildId)).ConfigureAwait(false);
    }
}
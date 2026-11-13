// <copyright file="IShowGuildListPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Guild;

using HNMO.Interfaces;

/// <summary>
/// Interface of a view whose implementation informs about the previously requested list of guild players.
/// </summary>
public interface IShowGuildListPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the guild list.
    /// </summary>
    /// <param name="players">The players of the guild.</param>
    /// <param name="guild">The guild.</param>
    ValueTask ShowGuildListAsync(IReadOnlyCollection<GuildListEntry> players, Guild guild);
}
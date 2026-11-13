// <copyright file="IShowAllianceListPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Guild;

using HNMO.Interfaces;

/// <summary>
/// Interface of a view whose implementation sends the list of guilds in an alliance to the client.
/// </summary>
public interface IShowAllianceListPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the list of guilds in the alliance.
    /// </summary>
    /// <param name="guilds">The list of alliance guild entries.</param>
    ValueTask ShowListAsync(IEnumerable<AllianceGuildEntry> guilds);
}

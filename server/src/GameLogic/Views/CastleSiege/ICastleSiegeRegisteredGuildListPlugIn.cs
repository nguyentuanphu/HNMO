// <copyright file="ICastleSiegeRegisteredGuildListPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.CastleSiege;

using HNMO.DataModel.Entities;

/// <summary>
/// A view which shows the registered Castle Siege guilds.
/// </summary>
public interface ICastleSiegeRegisteredGuildListPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows all guild registrations of the current cycle.
    /// </summary>
    /// <param name="registrations">The guild registrations.</param>
    /// <returns>A task that represents the asynchronous show operation.</returns>
    ValueTask ShowRegisteredGuildListAsync(IReadOnlyCollection<CastleSiegeGuildRegistration> registrations);
}

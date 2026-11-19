// <copyright file="IHnmoTeleportNpcViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// View plug-in delivering the Teleport NPC destination list and warp outcomes to web clients.
/// </summary>
public interface IHnmoTeleportNpcViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the Teleport NPC destination snapshot JSON.</summary>
    /// <param name="json">Serialized state JSON.</param>
    /// <param name="requested">Whether triggered by client interaction or packet.</param>
    /// <returns>The task.</returns>
    ValueTask ShowTeleportNpcStateAsync(string json, bool requested = true);
}

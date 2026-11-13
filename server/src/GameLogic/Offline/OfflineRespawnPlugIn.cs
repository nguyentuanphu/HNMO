// <copyright file="OfflineRespawnPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Offline;

using System.Threading.Tasks;
using HNMO.GameLogic.Views.World;

/// <summary>
/// Simulates a respawn response for an offline character.
/// </summary>
internal sealed class OfflineRespawnPlugIn : IRespawnAfterDeathPlugIn
{
    /// <inheritdoc/>
    public ValueTask RespawnAsync() => ValueTask.CompletedTask;
}

// <copyright file="ImperialGuardianResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.MiniGames.ImperialGuardian;

/// <summary>
/// The result of a zone or of the imperial guardian event.
/// </summary>
public enum ImperialGuardianResult
{
    /// <summary>
    /// The event failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The zone has been cleared.
    /// </summary>
    ZoneCleared,

    /// <summary>
    /// The event has been completed.
    /// </summary>
    Success,
}

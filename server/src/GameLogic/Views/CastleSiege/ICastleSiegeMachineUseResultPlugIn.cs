// <copyright file="ICastleSiegeMachineUseResultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.CastleSiege;

using HNMO.GameLogic.CastleSiege;
using HNMO.Pathfinding;

/// <summary>
/// A view which reports the result of a Castle Siege warfare-machine shot.
/// </summary>
public interface ICastleSiegeMachineUseResultPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the result of a warfare-machine firing request.
    /// </summary>
    /// <param name="success">A value indicating whether the machine was fired.</param>
    /// <param name="machineId">The machine object identifier.</param>
    /// <param name="machineType">The machine type.</param>
    /// <param name="target">The impact point when the request succeeded.</param>
    ValueTask ShowMachineUseResultAsync(bool success, ushort machineId, CastleSiegeMachineType machineType, Point target);
}

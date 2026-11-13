// <copyright file="ICastleSiegeMachineInterfacePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.CastleSiege;

using HNMO.GameLogic.CastleSiege;

/// <summary>
/// A view which reports the result of opening a Castle Siege warfare-machine interface.
/// </summary>
public interface ICastleSiegeMachineInterfacePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the result of a request to open the warfare-machine interface.
    /// </summary>
    /// <param name="success">A value indicating whether the interface was opened.</param>
    /// <param name="machineType">The machine type.</param>
    /// <param name="machineId">The machine object identifier.</param>
    ValueTask ShowMachineInterfaceAsync(bool success, CastleSiegeMachineType machineType, ushort machineId);
}

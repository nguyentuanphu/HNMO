// <copyright file="IUpdateVaultStatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Vault;

/// <summary>
/// Interface of a view whose implementation updates the vault state on client side.
/// </summary>
public interface IUpdateVaultStatePlugIn : IViewPlugIn
{
    /// <summary>
    /// Updates the vault state on client side.
    /// </summary>
    ValueTask UpdateStateAsync();
}
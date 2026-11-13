// <copyright file="IShowVaultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Vault;

/// <summary>
/// Interface of a view whose implementation informs about an opened vault.
/// </summary>
public interface IShowVaultPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the vault.
    /// </summary>
    ValueTask ShowVaultAsync();
}
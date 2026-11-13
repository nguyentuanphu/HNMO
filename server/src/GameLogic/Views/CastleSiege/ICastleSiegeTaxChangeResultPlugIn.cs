// <copyright file="ICastleSiegeTaxChangeResultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.CastleSiege;

using HNMO.GameLogic.CastleSiege;

/// <summary>
/// A view which reports a Castle Siege tax-rate change result.
/// </summary>
public interface ICastleSiegeTaxChangeResultPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the tax-rate change result.
    /// </summary>
    /// <param name="result">The request result.</param>
    /// <param name="taxType">The requested tax type.</param>
    /// <param name="taxRate">The requested tax rate.</param>
    ValueTask ShowTaxChangeResultAsync(CastleSiegeRequestResult result, CastleSiegeTaxType taxType, uint taxRate);

    /// <summary>
    /// Updates a tax rate which affects all players.
    /// </summary>
    /// <param name="taxType">The tax type.</param>
    /// <param name="taxRate">The current tax rate.</param>
    ValueTask ShowTaxRateUpdateAsync(CastleSiegeTaxType taxType, byte taxRate);
}

// <copyright file="IEconomyExternalAssetProvider.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

using System.Threading;

/// <summary>
/// Optional extension hook allowing Jewel Bank or Item Market modules to report locked/listed balances
/// into <see cref="EconomyStore"/> snapshots.
/// </summary>
public interface IEconomyExternalAssetProvider
{
    /// <summary>Returns per-account external currency and item balances.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>External account balances.</returns>
    ValueTask<IReadOnlyList<EconomyAccountTotals>> GetExternalAssetsAsync(CancellationToken cancellationToken);
}

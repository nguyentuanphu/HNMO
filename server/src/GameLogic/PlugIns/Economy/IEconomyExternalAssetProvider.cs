// <copyright file="IEconomyExternalAssetProvider.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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

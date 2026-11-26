// <copyright file="EconomyDashboardOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.Economy;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;

/// <summary>
/// Admin operations for capturing, loading, and analyzing daily economy snapshots (ROADMAP 14.25).
/// </summary>
/// <param name="persistence">Persistence context provider.</param>
/// <param name="configurations">Configuration source.</param>
/// <param name="executor">Authorization and audit executor.</param>
/// <param name="timeProvider">Clock provider.</param>
/// <param name="externalProviders">Optional external asset providers (e.g. Jewel Bank, Item Market).</param>
public sealed class EconomyDashboardOperations(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    AdminOperationExecutor executor,
    TimeProvider timeProvider,
    IEnumerable<IEconomyExternalAssetProvider>? externalProviders = null)
{
    /// <summary>Loads recent daily economy snapshots for dashboard display.</summary>
    /// <param name="fileName">State key used to locate the row backend.</param>
    /// <param name="maxDays">Maximum days to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chronological snapshots (oldest to newest).</returns>
    [NotExposedToAgents("Admin panel economy dashboard UI only.")]
    public async ValueTask<IReadOnlyList<EconomyDailySnapshot>> LoadHistoryAsync(
        string? fileName,
        int maxDays,
        CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        return EconomyStore.Open(fileName).LoadSnapshots(maxDays);
    }

    /// <summary>
    /// Scans all non-bot accounts, external asset hooks, and today's activity log drops/sinks,
    /// persisting today's Sydney economy snapshot.
    /// </summary>
    /// <param name="config">Economy dashboard configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="jewelBank">The active Jewel Bank settings, if the bank is enabled.</param>
    /// <returns>The newly captured snapshot.</returns>
    [NotExposedToAgents("Admin panel economy dashboard UI only.")]
    public async ValueTask<EconomyDailySnapshot> CaptureNowAsync(
        EconomyConfiguration config,
        CancellationToken cancellationToken,
        JewelBankConfiguration? jewelBank = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);

        var owner = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(owner);
        var accounts = await LoadAllAccountsAsync(context, cancellationToken).ConfigureAwait(false);

        var external = new List<EconomyAccountTotals>();
        if (externalProviders is not null)
        {
            foreach (var provider in externalProviders)
            {
                var items = await provider.GetExternalAssetsAsync(cancellationToken).ConfigureAwait(false);
                external.AddRange(items);
            }
        }

        long? bankPieces = jewelBank is null ? null : await EconomyJewelActivity.SumBankPiecesAsync(context, accounts).ConfigureAwait(false);
        return EconomyStore.Open(config.FileName).CaptureSnapshot(
            accounts,
            external,
            config,
            timeProvider.GetUtcNow().UtcDateTime,
            owner,
            jewelBank,
            bankPieces);
    }

    private static async ValueTask<List<Account>> LoadAllAccountsAsync(IPlayerContext context, CancellationToken cancellationToken)
    {
        var result = new List<Account>();
        var skip = 0;
        const int batchSize = 500;
        while (true)
        {
            var page = (await context.GetAccountsOrderedByLoginNameAsync(skip, batchSize, cancellationToken).ConfigureAwait(false)).ToList();
            if (page.Count == 0)
            {
                break;
            }

            foreach (var summary in page)
            {
                if (await context.GetAccountByLoginNameAsync(summary.LoginName, cancellationToken).ConfigureAwait(false) is { } account)
                {
                    result.Add(account);
                }
            }
            if (page.Count < batchSize)
            {
                break;
            }

            skip += page.Count;
        }

        return result;
    }
}

// <copyright file="JewelBankOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;

/// <summary>
/// Admin panel operations for <see cref="JewelBankPlugIn"/>: recent database receipts
/// (<see cref="DataModel.Entities.AssetOperation"/>) and JSON-to-database import preview/execution.
/// </summary>
/// <param name="persistence">Persistence context provider.</param>
/// <param name="configurations">Game configuration source.</param>
/// <param name="executor">Authorization and audit executor.</param>
/// <param name="writeSettings">Optional read-only write protection settings.</param>
public sealed class JewelBankOperations(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    AdminOperationExecutor executor,
    AdminWriteSettings? writeSettings = null)
{
    /// <summary>Operation kinds written by Jewel Bank flows in game, website, and JSON import.</summary>
    private static readonly string[] JewelBankKinds =
    [
        "jewel-deposit",
        "jewel-withdraw",
        JewelBankPlugIn.WebsiteWithdrawKind,
        "jewel-delivery",
        JewelBankDatabaseImport.BalanceKind,
    ];

    /// <summary>Loads recent Jewel Bank database receipts, newest first.</summary>
    /// <param name="maximumCount">Maximum receipts to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent receipts from <c>AssetOperation</c>.</returns>
    [NotExposedToAgents("Admin panel Jewel Bank receipts UI only.")]
    public async ValueTask<IReadOnlyList<AssetOperationSummary>> LoadReceiptsAsync(
        int maximumCount = 100,
        CancellationToken cancellationToken = default)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var owner = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(owner);
        return await context.GetRecentAssetOperationsAsync(maximumCount, JewelBankKinds, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Previews what importing legacy Jewel Bank JSON entries into the database would do without writing anything.</summary>
    /// <param name="fileName">Jewel Bank state file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dry-run import report.</returns>
    [NotExposedToAgents("Admin panel Jewel Bank import dry-run UI only.")]
    public async ValueTask<JewelBankImportReport> PreviewImportAsync(
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var owner = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return await JewelBankDatabaseImport.RunAsync(persistence, owner, JewelBankStore.Open(fileName), dryRun: true).ConfigureAwait(false);
    }

    /// <summary>Imports legacy Jewel Bank JSON balances and pending withdrawals into the database idempotently.</summary>
    /// <param name="fileName">Jewel Bank state file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution import report.</returns>
    [NotExposedToAgents("Admin panel Jewel Bank import UI only.")]
    public async ValueTask<JewelBankImportReport> RunImportAsync(
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        if (writeSettings is not null)
        {
            await AdminChangeExecution.RequireAsync(executor, writeSettings, AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        }

        return await executor.ExecuteAsync(
            AdminRoles.Administrator,
            "jewelbank.import",
            new { fileName },
            async _ =>
            {
                var owner = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                return await JewelBankDatabaseImport.RunAsync(persistence, owner, JewelBankStore.Open(fileName), dryRun: false).ConfigureAwait(false);
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}

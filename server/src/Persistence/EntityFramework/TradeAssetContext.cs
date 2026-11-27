// <copyright file="TradeAssetContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using HNMO.Interfaces;
using HNMO.Persistence.EntityFramework.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Trade persistence context capable of committing item ownership, Zen updates, and an idempotent
/// <see cref="AssetOperation"/> receipt in a single database transaction.
/// </summary>
internal sealed class TradeAssetContext : CachingEntityFrameworkContext, IAtomicTradeContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TradeAssetContext"/> class.
    /// </summary>
    /// <param name="context">The trade database context.</param>
    /// <param name="repositoryProvider">The repository provider.</param>
    /// <param name="changeListener">The change listener.</param>
    /// <param name="logger">The logger.</param>
    public TradeAssetContext(
        DbContext context,
        IContextAwareRepositoryProvider repositoryProvider,
        IConfigurationChangeListener? changeListener,
        ILogger<CachingEntityFrameworkContext> logger)
        : base(context, repositoryProvider, changeListener, logger)
    {
    }

    /// <inheritdoc />
    public async ValueTask<AssetCommitStatus> SaveTradeWithAssetOperationAsync(
        AssetOperationRequest request,
        IReadOnlyList<TradeMoneyDelta> moneyChanges,
        CancellationToken cancellationToken = default)
    {
        var status = AssetCommitStatus.Refused;
        var added = new List<object>();

        async ValueTask<bool> WriteFirstAsync(EntityDataContext context, CancellationToken token)
        {
            if (await context.Set<AssetOperation>().AsNoTracking()
                    .FirstOrDefaultAsync(op => op.Id == request.OperationId, token).ConfigureAwait(false) is { } earlier)
            {
                status = earlier.PayloadHash == request.PayloadHash && earlier.AccountId == request.AccountId
                    ? AssetCommitStatus.AlreadyCommitted
                    : AssetCommitStatus.Conflict;
                return false;
            }

            var conn = context.Database.GetDbConnection();
            var tx = context.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var change in moneyChanges.Where(c => c.Delta != 0).OrderBy(c => c.StorageId))
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE data."ItemStorage"
                    SET "Money" = "Money" + @delta
                    WHERE "Id" = @id AND "Money" + @delta >= 0
                    """;
                var pDelta = cmd.CreateParameter();
                pDelta.ParameterName = "@delta";
                pDelta.Value = change.Delta;
                cmd.Parameters.Add(pDelta);
                var pId = cmd.CreateParameter();
                pId.ParameterName = "@id";
                pId.Value = change.StorageId;
                cmd.Parameters.Add(pId);
                if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1)
                {
                    status = AssetCommitStatus.Refused;
                    return false;
                }
            }

            var receipt = new AssetOperation { Id = request.OperationId };
            context.Add(receipt);
            receipt.AccountId = request.AccountId;
            receipt.Kind = request.Kind;
            receipt.PayloadHash = request.PayloadHash;
            receipt.Source = request.Source;
            receipt.Detail = request.Detail;
            receipt.PendingKey = request.PendingKey;
            receipt.PendingCount = Math.Max(0, request.PendingCount);
            receipt.CreatedAt = DateTime.UtcNow;
            added.Add(receipt);
            status = AssetCommitStatus.Committed;
            return true;
        }

        return await this.SaveChangesInTransactionAsync(WriteFirstAsync, added, cancellationToken).ConfigureAwait(false)
            ? AssetCommitStatus.Committed
            : status;
    }
}

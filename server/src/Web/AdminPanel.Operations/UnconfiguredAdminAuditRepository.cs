// <copyright file="UnconfiguredAdminAuditRepository.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Threading;
using HNMO.Persistence.AdminAuth;

/// <summary>Prevents actions when a host has not installed a durable audit repository.</summary>
internal sealed class UnconfiguredAdminAuditRepository : IAdminAuditRepository
{
    /// <inheritdoc />
    public ValueTask<AdminAuditEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Audit storage is not configured.");

    /// <inheritdoc />
    public ValueTask AddAsync(AdminAuditEntry entry, CancellationToken cancellationToken = default)
        => throw new AdminOperationException("Audit storage is not configured; no action was executed.", AdminOperationErrorKind.Unavailable);

    /// <inheritdoc />
    public ValueTask CompleteAsync(Guid id, string outcome, string resultJson, CancellationToken cancellationToken = default)
        => throw new AdminOperationException("Audit storage is not configured.", AdminOperationErrorKind.Unavailable);

    /// <inheritdoc />
    public ValueTask<(IReadOnlyList<AdminAuditEntry> Items, int Total)> ReadAsync(int offset, int limit, CancellationToken cancellationToken = default)
        => throw new AdminOperationException("Audit storage is not configured.", AdminOperationErrorKind.Unavailable);
}

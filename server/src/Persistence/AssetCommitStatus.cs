// <copyright file="AssetCommitStatus.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Persistence;

/// <summary>The result of <see cref="IPlayerContext.SaveChangesWithAssetOperationAsync"/>.</summary>
public enum AssetCommitStatus
{
    /// <summary>Committed now, together with the context's changes.</summary>
    Committed,

    /// <summary>
    /// An earlier attempt with the same identifier and payload was committed; nothing was written now and the context's
    /// changes are still pending (the caller takes them back, since they were already saved with the earlier attempt).
    /// </summary>
    AlreadyCommitted,

    /// <summary>The identifier was used by an operation with another payload; nothing was written.</summary>
    Conflict,

    /// <summary>A balance would leave 0..capacity, or fewer are pending than asked; nothing was written.</summary>
    Refused,
}

// <copyright file="IDbLogRetentionExporter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;

/// <summary>Exports expired database chat and admin audit logs before deleting them.</summary>
public interface IDbLogRetentionExporter
{
    /// <summary>
    /// Exports rows older than <paramref name="cutoffUtc"/> to a verified compressed file in
    /// <paramref name="archiveDirectory"/> and deletes those rows only after export succeeds.
    /// </summary>
    /// <param name="cutoffUtc">UTC cutoff timestamp.</param>
    /// <param name="archiveDirectory">Archive output directory.</param>
    /// <returns>Purge summary.</returns>
    DbLogPurgeResult ExportAndPurgeOlderThan(DateTime cutoffUtc, string archiveDirectory);
}

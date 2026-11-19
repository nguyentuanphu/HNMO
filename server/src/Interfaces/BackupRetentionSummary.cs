// <copyright file="BackupRetentionSummary.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// Result of applying daily/weekly/monthly retention across <c>save/backups</c>.
/// </summary>
/// <param name="TotalKept">Number of backups kept.</param>
/// <param name="TotalBytes">Combined byte size of remaining backups.</param>
/// <param name="RemovedCount">Number of older backups removed.</param>
public sealed record BackupRetentionSummary(int TotalKept, long TotalBytes, int RemovedCount);

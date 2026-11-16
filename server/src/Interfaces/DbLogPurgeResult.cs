// <copyright file="DbLogPurgeResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>Result of an export-and-purge database log retention run.</summary>
/// <param name="DeletedChatRows">Deleted chat rows.</param>
/// <param name="DeletedAuditRows">Deleted admin audit rows.</param>
/// <param name="ExportArchivePath">Path to compressed <c>.json.gz</c> export, or null if nothing expired.</param>
public sealed record DbLogPurgeResult(int DeletedChatRows, int DeletedAuditRows, string? ExportArchivePath);

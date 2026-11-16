// <copyright file="LogFileEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// Metadata for an active (<c>.log</c>) or compressed (<c>.log.gz</c>) log file in <c>save/logs</c>.
/// </summary>
/// <param name="FileName">File name inside <c>save/logs</c>.</param>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="ModifiedSydney">Last modification timestamp in Sydney time.</param>
/// <param name="Compressed">True if gzip-compressed.</param>
public sealed record LogFileEntry(string FileName, long SizeBytes, string ModifiedSydney, bool Compressed);

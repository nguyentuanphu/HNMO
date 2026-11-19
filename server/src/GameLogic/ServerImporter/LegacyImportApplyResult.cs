// <copyright file="LegacyImportApplyResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Result returned after applying a local import.</summary>
public sealed class LegacyImportApplyResult
{
    /// <summary>Gets or sets the created backup identifier used for undo.</summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>Gets or sets the backup file path on disk.</summary>
    public string BackupFilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets counts of created or modified entities.</summary>
    public int AppliedCount { get; set; }

    /// <summary>Gets or sets counts of skipped entities.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Gets or sets summary lines for admin display.</summary>
    public List<string> SummaryLines { get; set; } = [];
}

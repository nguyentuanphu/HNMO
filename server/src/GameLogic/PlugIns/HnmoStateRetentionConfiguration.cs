// <copyright file="HnmoStateRetentionConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Configuration for <see cref="HnmoStateRetentionPlugIn"/> controlling archival of finished seasons/races and expired gift code claims.
/// </summary>
public sealed class HnmoStateRetentionConfiguration
{
    /// <summary>Gets or sets shared HNMO state key.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets number of days after completion/expiry before rows are moved to the archive table.</summary>
    [Range(1, 3650)]
    public int ArchiveFinishedDataAfterDays { get; set; } = 30;
}

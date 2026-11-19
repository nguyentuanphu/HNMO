// <copyright file="HnmoStateRetentionMetadata.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>Summary metadata stored in <see cref="HnmoStateStore"/> under section <c>stateRetention</c>.</summary>
public sealed class HnmoStateRetentionMetadata
{
    /// <summary>Gets or sets UTC timestamp of last archival sweep.</summary>
    public DateTime? LastArchivedAtUtc { get; set; }

    /// <summary>Gets or sets number of rows archived in the last sweep.</summary>
    public int LastArchivedCount { get; set; }

    /// <summary>Gets or sets total lifetime archived rows.</summary>
    public long TotalArchivedCount { get; set; }
}

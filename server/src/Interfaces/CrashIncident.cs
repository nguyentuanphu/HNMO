// <copyright file="CrashIncident.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;

/// <summary>
/// Persisted crash watch record captured when the launched game server exits unexpectedly.
/// </summary>
public sealed class CrashIncident
{
    /// <summary>Gets or sets unique incident identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when the crash occurred.</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Gets or sets the formatted Sydney local timestamp.</summary>
    public string OccurredAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets the process exit code.</summary>
    public int ExitCode { get; set; }

    /// <summary>Gets or sets a value indicating whether an automatic restart was triggered.</summary>
    public bool AutoRestarted { get; set; }

    /// <summary>Gets or sets the restart backoff delay in seconds (<c>0</c> if not restarted).</summary>
    public int DelaySeconds { get; set; }

    /// <summary>Gets or sets the English notice summary.</summary>
    public string SummaryEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese notice summary.</summary>
    public string SummaryVi { get; set; } = string.Empty;

    /// <summary>Gets or sets trailing lines captured from <c>server.log</c>.</summary>
    public string TailExcerpt { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the admin dismissed/acknowledged this crash notice.</summary>
    public bool Acknowledged { get; set; }
}

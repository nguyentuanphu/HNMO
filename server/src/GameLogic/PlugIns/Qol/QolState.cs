// <copyright file="QolState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Qol;

using System.Collections.Generic;

/// <summary>
/// Persisted state payload for <see cref="QolStore"/> (<c>qol</c> section).
/// </summary>
public sealed class QolState
{
    /// <summary>Gets or sets total mining attempts completed.</summary>
    public long TotalMinesCompleted { get; set; }

    /// <summary>Gets or sets recent mining activity summaries (newest first).</summary>
    public List<string> RecentMineLogs { get; set; } = [];
}

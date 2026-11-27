// <copyright file="QolState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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

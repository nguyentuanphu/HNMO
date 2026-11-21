// <copyright file="TopUpHistoryEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>
/// A recorded top-up credit or milestone claim.
/// </summary>
public sealed class TopUpHistoryEntry
{
    /// <summary>Gets or sets when it happened (UTC).</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the kind (<c>topup</c> or <c>claim</c>).</summary>
    public string Kind { get; set; } = "topup";

    /// <summary>Gets or sets the VND delta (positive for top-ups, 0 for claims).</summary>
    public long AmountVnd { get; set; }

    /// <summary>Gets or sets the step id (for claims) or reference (for top-ups).</summary>
    public string Detail { get; set; } = string.Empty;
}

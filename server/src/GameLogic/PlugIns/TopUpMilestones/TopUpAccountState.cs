// <copyright file="TopUpAccountState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>
/// Per-account persisted top-up milestone state.
/// </summary>
public sealed class TopUpAccountState
{
    /// <summary>Gets or sets the cumulative VND topped up by this account.</summary>
    public long TotalVnd { get; set; }

    /// <summary>Gets or sets the unique transfer references already counted toward <see cref="TotalVnd"/>.</summary>
    public HashSet<string> CreditedReferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the milestone step ids already claimed by this account.</summary>
    public HashSet<string> ClaimedStepIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the bag rewards (items / zen) waiting for room on an account's character.</summary>
    public List<GiftCodeOwedReward> Owed { get; set; } = [];

    /// <summary>Gets or sets the recent top-up and claim entries (newest first).</summary>
    public List<TopUpHistoryEntry> History { get; set; } = [];
}

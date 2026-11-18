// <copyright file="S6ExchangeTier.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System.Collections.Generic;

/// <summary>
/// Stacked token exchange tier (e.g., 10 / 50 / 255 Cherry Blossoms or 10 / 20 / 30 Lucky Coins).
/// </summary>
public sealed class S6ExchangeTier
{
    /// <summary>Gets or sets stacked token quantity consumed.</summary>
    public int RequiredCount { get; set; } = 10;

    /// <summary>Gets or sets English tier label.</summary>
    public string LabelEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese tier label.</summary>
    public string LabelVi { get; set; } = string.Empty;

    /// <summary>Gets or sets weighted reward pool for this tier.</summary>
    public List<S6ExchangeRewardEntry> Rewards { get; set; } = [];
}

// <copyright file="CommunityOwedReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// Zen and items of an event which wait for the character to be online with room in the bag.
/// </summary>
public sealed class CommunityOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the event id.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English title of the event.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title of the event.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward (zen and items).</summary>
    public MissionReward Reward { get; set; } = new();
}

// <copyright file="MissionItemReward.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// An item of a reward.
/// </summary>
public class MissionItemReward
{
    /// <summary>Gets or sets the item group.</summary>
    [Range(0, 15)]
    public short Group { get; set; }

    /// <summary>Gets or sets the item number in its group.</summary>
    [Range(0, 511)]
    public short Number { get; set; }

    /// <summary>Gets or sets the item level.</summary>
    [Range(0, 15)]
    public byte Level { get; set; }

    /// <summary>Gets or sets how many of the item are given.</summary>
    [Range(1, 50)]
    public int Count { get; set; } = 1;
}

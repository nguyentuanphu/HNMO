// <copyright file="TopUpMilestoneItem.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using HNMO.DataModel.Configuration;

/// <summary>
/// One item in a top-up milestone reward.
/// </summary>
public sealed class TopUpMilestoneItem
{
    /// <summary>Gets or sets the item group.</summary>
    public short Group { get; set; }

    /// <summary>Gets or sets the item number within the group.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets the item level (0-15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets the option level (0-7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has Luck.</summary>
    public bool WithLuck { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has Skill.</summary>
    public bool WithSkill { get; set; }

    /// <summary>Gets or sets the stack count or number of pieces.</summary>
    public int Quantity { get; set; } = 1;
}

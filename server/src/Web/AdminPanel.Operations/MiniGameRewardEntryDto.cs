// <copyright file="MiniGameRewardEntryDto.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using HNMO.DataModel.Configuration;

/// <summary>
/// Editable row representing one <see cref="MiniGameReward"/> on a mini-game level (ROADMAP 14.7).
/// </summary>
public sealed class MiniGameRewardEntryDto
{
    /// <summary>Gets or sets the owning mini-game definition ID.</summary>
    public Guid MiniGameDefinitionId { get; set; }

    /// <summary>Gets or sets the mini-game type (Blood Castle, Devil Square, Chaos Castle, etc.).</summary>
    public MiniGameType Type { get; set; }

    /// <summary>Gets or sets the mini-game level (1..8).</summary>
    public byte GameLevel { get; set; }

    /// <summary>Gets or sets the event name.</summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward ID (<see cref="Guid.Empty"/> for newly added rows).</summary>
    public Guid RewardId { get; set; }

    /// <summary>Gets or sets reward rank (optional).</summary>
    public int? Rank { get; set; }

    /// <summary>Gets or sets reward type (Money, Experience, Item, etc.).</summary>
    public MiniGameRewardType RewardType { get; set; } = MiniGameRewardType.Money;

    /// <summary>Gets or sets reward quantity.</summary>
    public int RewardAmount { get; set; }

    /// <summary>Gets or sets required success flags.</summary>
    public MiniGameSuccessFlags RequiredSuccess { get; set; } = MiniGameSuccessFlags.Winner;

    /// <summary>Gets or sets optional linked <c>DropItemGroup</c> ID for item rewards.</summary>
    public Guid? ItemRewardGroupId { get; set; }

    /// <summary>Gets or sets linked <c>DropItemGroup</c> description (display only).</summary>
    public string ItemRewardDescription { get; set; } = string.Empty;
}

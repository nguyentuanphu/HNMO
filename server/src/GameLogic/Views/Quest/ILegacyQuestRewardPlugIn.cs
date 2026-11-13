// <copyright file="ILegacyQuestRewardPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Quest;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration.Quests;

/// <summary>
/// Interface of a view whose implementation informs about quest rewards after completion of a quest.
/// </summary>
/// <remarks>
/// Sends C1A4.
/// </remarks>
public interface ILegacyQuestRewardPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the quest reward for the specified player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="rewardType">Type of the reward.</param>
    /// <param name="value">The value.</param>
    /// <param name="attributeReward">The attribute reward.</param>
    ValueTask ShowAsync(Player player, QuestRewardType rewardType, int value, AttributeDefinition? attributeReward);
}
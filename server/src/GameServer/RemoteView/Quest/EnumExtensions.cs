// <copyright file="EnumExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.Quest;

using HNMO.DataModel.Configuration.Quests;
using HNMO.Network.Packets.ServerToClient;

/// <summary>
/// Extensions to convert enum values.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Converts the specified quest reward type.
    /// </summary>
    /// <param name="questRewardType">Type of the quest reward.</param>
    /// <returns>The converted value.</returns>
    public static RewardType Convert(this QuestRewardType questRewardType)
    {
        return questRewardType switch
        {
            QuestRewardType.Item => RewardType.Item,
            QuestRewardType.Experience => RewardType.Experience,
            QuestRewardType.Money => RewardType.Money,
            QuestRewardType.GensAttribution => RewardType.GensContribution,
            QuestRewardType.Undefined => RewardType.None,
            _ => throw new ArgumentException($"Unknown reward type {questRewardType}."),
        };
    }
}
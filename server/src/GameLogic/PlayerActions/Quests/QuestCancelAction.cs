// <copyright file="QuestCancelAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Quests;

using HNMO.GameLogic.Views.Quest;

/// <summary>
/// Cancels the specified quest for the player.
/// </summary>
public class QuestCancelAction
{
    /// <summary>
    /// Cancels the quest for the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="group">The group of the quest.</param>
    /// <param name="number">The number of the quest.</param>
    public async ValueTask CancelQuestAsync(Player player, short group, short number)
    {
        var questState = player.GetQuestState(group, number);
        if (questState?.ActiveQuest is null)
        {
            return;
        }

        var quest = questState.ActiveQuest;
        await questState.ClearAsync(player.PersistenceContext).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IQuestCancelledPlugIn>(p => p.QuestCancelledAsync(quest)).ConfigureAwait(false);
    }
}
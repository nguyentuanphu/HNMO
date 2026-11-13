// <copyright file="IQuestCancelledPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Quest;

using HNMO.DataModel.Configuration.Quests;

/// <summary>
/// Interface of a view whose implementation informs about the cancelled quest.
/// </summary>
/// <remarks>
/// Sends C1F60F.
/// </remarks>
public interface IQuestCancelledPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows if the specified quest was cancelled.
    /// </summary>
    /// <param name="quest">The quest.</param>
    ValueTask QuestCancelledAsync(QuestDefinition quest);
}
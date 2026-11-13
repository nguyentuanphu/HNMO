// <copyright file="IDuelEndedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Duel;

/// <summary>
/// Interface of a view whose implementation informs about a duel end.
/// </summary>
public interface IDuelEndedPlugIn : IViewPlugIn
{
    /// <summary>
    /// A duel has ended.
    /// </summary>
    ValueTask DuelEndedAsync();
}
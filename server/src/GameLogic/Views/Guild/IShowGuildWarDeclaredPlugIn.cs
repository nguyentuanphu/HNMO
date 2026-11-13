// <copyright file="IShowGuildWarDeclaredPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Guild;

/// <summary>
/// Interface of a view whose implementation informs about a declared guild war.
/// </summary>
public interface IShowGuildWarDeclaredPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows, that the guild war was declared.
    /// </summary>
    ValueTask ShowDeclaredAsync();
}
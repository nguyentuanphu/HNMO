// <copyright file="IHnmoBugReportViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>The view of HNMO's bug report window.</summary>
public interface IHnmoBugReportViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the bug report window's state.</summary>
    /// <param name="json">Plain-ASCII JSON state.</param>
    /// <returns>The task.</returns>
    ValueTask ShowBugReportsAsync(string json);
}

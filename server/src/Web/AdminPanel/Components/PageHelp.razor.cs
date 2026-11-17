// <copyright file="PageHelp.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components;

using Microsoft.AspNetCore.Components;

/// <summary>A reusable, accessible help disclosure for a host-facing page.</summary>
public partial class PageHelp
{
    private readonly string _id = $"page-help-{Guid.NewGuid():N}";
    private bool _expanded;

    /// <summary>Gets or sets localized plain-language purpose, changes and cautions for this page.</summary>
    [Parameter]
    [EditorRequired]
    public string Text { get; set; } = string.Empty;
}

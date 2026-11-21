// <copyright file="NavEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components.Layout;

using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

/// <summary>
/// A link of the sidebar.
/// </summary>
/// <param name="Caption">The caption.</param>
/// <param name="Href">The relative link.</param>
/// <param name="Icon">The icon class.</param>
/// <param name="Match">How the link matches the current page.</param>
public sealed record NavEntry(string Caption, string Href, string Icon, NavLinkMatch Match = NavLinkMatch.Prefix);

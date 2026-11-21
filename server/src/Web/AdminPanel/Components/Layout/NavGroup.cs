// <copyright file="NavGroup.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components.Layout;

using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

/// <summary>
/// A collapsible group of links of the sidebar.
/// </summary>
/// <param name="Key">The key of the group.</param>
/// <param name="Caption">The caption.</param>
/// <param name="Icon">The icon class.</param>
/// <param name="Entries">The links.</param>
public sealed record NavGroup(string Key, string Caption, string Icon, IReadOnlyList<NavEntry> Entries);

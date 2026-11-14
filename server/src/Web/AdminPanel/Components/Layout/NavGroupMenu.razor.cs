// <copyright file="NavGroupMenu.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using HNMO.Web.Shared.Services;

/// <summary>
/// Renders collapsible groups of links in the sidebar. A group which contains the current page is open,
/// unless the user closed it by hand.
/// </summary>
public partial class NavGroupMenu : IDisposable
{
    /// <summary>
    /// The groups which the user opened or closed by hand.
    /// </summary>
    private readonly HashSet<string> _toggledGroups = [];

    /// <summary>
    /// Gets or sets the groups.
    /// </summary>
    [Parameter]
    public IReadOnlyList<NavGroup> Groups { get; set; } = [];

    [Inject]
    private NavigationHistory NavigationHistory { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    /// <inheritdoc />
    public void Dispose()
    {
        this.NavigationManager.LocationChanged -= this.OnLocationChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        this.NavigationManager.LocationChanged += this.OnLocationChanged;
    }

    private bool IsExpanded(NavGroup group) => this.ContainsCurrentPage(group) != this._toggledGroups.Contains(group.Key);

    private bool ContainsCurrentPage(NavGroup group)
    {
        var relativePath = this.NavigationManager.ToBaseRelativePath(this.NavigationManager.Uri);
        var queryStart = relativePath.IndexOfAny(['?', '#']);
        if (queryStart >= 0)
        {
            relativePath = relativePath[..queryStart];
        }

        return group.Entries.Any(entry =>
            relativePath.Equals(entry.Href.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            || (entry.Href.EndsWith('/') && relativePath.StartsWith(entry.Href, StringComparison.OrdinalIgnoreCase)));
    }

    private void Toggle(NavGroup group)
    {
        if (!this._toggledGroups.Remove(group.Key))
        {
            this._toggledGroups.Add(group.Key);
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // After a navigation, the groups follow the current page again.
        this._toggledGroups.Clear();
        _ = this.InvokeAsync(this.StateHasChanged);
    }
}

/// <summary>
/// A link of the sidebar.
/// </summary>
/// <param name="Caption">The caption.</param>
/// <param name="Href">The relative link.</param>
/// <param name="Icon">The icon class.</param>
/// <param name="Match">How the link matches the current page.</param>
public sealed record NavEntry(string Caption, string Href, string Icon, NavLinkMatch Match = NavLinkMatch.Prefix);

/// <summary>
/// A collapsible group of links of the sidebar.
/// </summary>
/// <param name="Key">The key of the group.</param>
/// <param name="Caption">The caption.</param>
/// <param name="Icon">The icon class.</param>
/// <param name="Entries">The links.</param>
public sealed record NavGroup(string Key, string Caption, string Icon, IReadOnlyList<NavEntry> Entries);

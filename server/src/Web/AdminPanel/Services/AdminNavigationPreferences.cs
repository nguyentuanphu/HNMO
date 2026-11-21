// <copyright file="AdminNavigationPreferences.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// Holds per-circuit UI navigation preferences: pinned favourites, recent visited routes,
/// and Basic vs Advanced sidebar density mode.
/// </summary>
public sealed class AdminNavigationPreferences
{
    private const int MaxRecent = 6;
    private readonly List<string> _favourites = [];
    private readonly List<string> _recent = [];
    private bool _advancedMode = true;

    /// <summary>Raised whenever favourites, recents, or view mode change.</summary>
    public event Action? Changed;

    /// <summary>Gets a value indicating whether Advanced pages are shown in the navigation menu.</summary>
    public bool AdvancedMode => this._advancedMode;

    /// <summary>Gets ordered pinned favourite routes.</summary>
    public IReadOnlyList<string> Favourites => this._favourites;

    /// <summary>Gets most recently visited routes (newest first).</summary>
    public IReadOnlyList<string> Recent => this._recent;

    /// <summary>Normalizes a relative or absolute route to catalog key format (empty for home).</summary>
    /// <param name="route">Raw route or relative path.</param>
    /// <returns>Normalized route key.</returns>
    public static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return string.Empty;
        }

        var clean = route.Split('?', '#')[0].Trim().Trim('/');
        return clean.ToLowerInvariant();
    }

    /// <summary>Checks whether <paramref name="route"/> is currently starred.</summary>
    /// <param name="route">Route key.</param>
    /// <returns><c>true</c> if pinned.</returns>
    public bool IsFavourite(string? route)
    {
        var key = NormalizeRoute(route);
        return this._favourites.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Toggles a route in the favourites list.</summary>
    /// <param name="route">Route key.</param>
    public void ToggleFavourite(string? route)
    {
        var key = NormalizeRoute(route);
        if (this._favourites.RemoveAll(r => string.Equals(r, key, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            this._favourites.Add(key);
        }

        this.Changed?.Invoke();
    }

    /// <summary>Sets Basic vs Advanced navigation mode.</summary>
    /// <param name="advanced">True to show Advanced pages; false for Basic everyday pages only.</param>
    public void SetAdvancedMode(bool advanced)
    {
        if (this._advancedMode == advanced)
        {
            return;
        }

        this._advancedMode = advanced;
        this.Changed?.Invoke();
    }

    /// <summary>Records a navigation visit to update the Recent pages strip.</summary>
    /// <param name="route">Relative path.</param>
    public void RecordVisit(string? route)
    {
        var key = NormalizeRoute(route);
        this._recent.RemoveAll(r => string.Equals(r, key, StringComparison.OrdinalIgnoreCase));
        this._recent.Insert(0, key);
        if (this._recent.Count > MaxRecent)
        {
            this._recent.RemoveRange(MaxRecent, this._recent.Count - MaxRecent);
        }

        this.Changed?.Invoke();
    }
}

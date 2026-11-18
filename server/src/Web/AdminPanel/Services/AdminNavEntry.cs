// <copyright file="AdminNavEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;

/// <summary>
/// Describes a single navigable AdminPanel page in the unified grouped menu, quick search,
/// favourites, recent pages, and related-pages strip.
/// </summary>
public sealed record AdminNavEntry(
    string Route,
    string GroupId,
    string Icon,
    string TitleEn,
    string TitleVi,
    string DescriptionEn,
    string DescriptionVi,
    bool IsBasic,
    params string[] RelatedRoutes)
{
    /// <summary>Gets the localized title for the active UI culture.</summary>
    public string LocalizedTitle =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase)
            ? this.TitleVi
            : this.TitleEn;

    /// <summary>Gets the localized one-line description for the active UI culture.</summary>
    public string LocalizedDescription =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase)
            ? this.DescriptionVi
            : this.DescriptionEn;
}

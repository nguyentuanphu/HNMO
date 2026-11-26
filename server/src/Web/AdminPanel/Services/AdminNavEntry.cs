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
    /// <summary>Gets the optional German title.</summary>
    public string? TitleDe { get; init; }

    /// <summary>Gets the optional Simplified Chinese title.</summary>
    public string? TitleZh { get; init; }

    /// <summary>Gets the optional German description.</summary>
    public string? DescriptionDe { get; init; }

    /// <summary>Gets the optional Simplified Chinese description.</summary>
    public string? DescriptionZh { get; init; }

    /// <summary>Gets the localized title for the active UI culture.</summary>
    public string LocalizedTitle => Translate(this.TitleEn, this.TitleVi, this.TitleDe, this.TitleZh);

    /// <summary>Gets the localized one-line description for the active UI culture.</summary>
    public string LocalizedDescription =>
        Translate(this.DescriptionEn, this.DescriptionVi, this.DescriptionDe, this.DescriptionZh);

    private static string Translate(string en, string vi, string? de, string? zh) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "vi" => vi,
            "de" => de ?? en,
            "zh" => zh ?? en,
            _ => en,
        };
}

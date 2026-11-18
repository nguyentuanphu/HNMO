// <copyright file="AdminGuideArticle.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;

/// <summary>
/// A searchable host reference article or step-by-step operational recipe inside AdminPanel (<c>/admin-guide</c>).
/// </summary>
public sealed record AdminGuideArticle(
    string Id,
    string Category,
    bool IsRecipe,
    string TitleEn,
    string TitleVi,
    string SummaryEn,
    string SummaryVi,
    string BodyEn,
    string BodyVi,
    params string[] RelatedRoutes)
{
    /// <summary>Gets the localized article title.</summary>
    public string LocalizedTitle => IsVi() ? this.TitleVi : this.TitleEn;

    /// <summary>Gets the localized short summary.</summary>
    public string LocalizedSummary => IsVi() ? this.SummaryVi : this.SummaryEn;

    /// <summary>Gets the localized full guide text.</summary>
    public string LocalizedBody => IsVi() ? this.BodyVi : this.BodyEn;

    private static bool IsVi() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
}

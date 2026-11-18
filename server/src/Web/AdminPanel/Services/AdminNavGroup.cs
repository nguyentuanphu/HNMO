// <copyright file="AdminNavGroup.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;

/// <summary>
/// A logical task group in the AdminPanel sidebar menu.
/// </summary>
public sealed record AdminNavGroup(
    string Id,
    string Icon,
    string NameEn,
    string NameVi,
    IReadOnlyList<AdminNavEntry> Entries)
{
    /// <summary>Gets the localized group heading.</summary>
    public string LocalizedName =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase)
            ? this.NameVi
            : this.NameEn;
}

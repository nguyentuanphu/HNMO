// <copyright file="WebsiteBrandLabels.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Globalization;
using System.Resources;

/// <summary>Localized labels for the host's public website brand settings.</summary>
public static class WebsiteBrandLabels
{
    private static readonly ResourceManager Resources = new("HNMO.GameLogic.Properties.WebsiteBrand", typeof(WebsiteBrandLabels).Assembly);

    /// <summary>Gets the localized Caption text.</summary>
    public static string Caption => Resources.GetString(nameof(Caption), CultureInfo.CurrentUICulture) ?? nameof(Caption);

    /// <summary>Gets the localized Description text.</summary>
    public static string Description => Resources.GetString(nameof(Description), CultureInfo.CurrentUICulture) ?? nameof(Description);

    /// <summary>Gets the localized LogoCaption text.</summary>
    public static string LogoCaption => Resources.GetString(nameof(LogoCaption), CultureInfo.CurrentUICulture) ?? nameof(LogoCaption);

    /// <summary>Gets the localized LogoDescription text.</summary>
    public static string LogoDescription => Resources.GetString(nameof(LogoDescription), CultureInfo.CurrentUICulture) ?? nameof(LogoDescription);

    /// <summary>Gets the localized AccentCaption text.</summary>
    public static string AccentCaption => Resources.GetString(nameof(AccentCaption), CultureInfo.CurrentUICulture) ?? nameof(AccentCaption);

    /// <summary>Gets the localized AccentDescription text.</summary>
    public static string AccentDescription => Resources.GetString(nameof(AccentDescription), CultureInfo.CurrentUICulture) ?? nameof(AccentDescription);

    /// <summary>Gets the localized BackgroundCaption text.</summary>
    public static string BackgroundCaption => Resources.GetString(nameof(BackgroundCaption), CultureInfo.CurrentUICulture) ?? nameof(BackgroundCaption);

    /// <summary>Gets the localized BackgroundDescription text.</summary>
    public static string BackgroundDescription => Resources.GetString(nameof(BackgroundDescription), CultureInfo.CurrentUICulture) ?? nameof(BackgroundDescription);

    /// <summary>Gets the localized PanelCaption text.</summary>
    public static string PanelCaption => Resources.GetString(nameof(PanelCaption), CultureInfo.CurrentUICulture) ?? nameof(PanelCaption);

    /// <summary>Gets the localized PanelDescription text.</summary>
    public static string PanelDescription => Resources.GetString(nameof(PanelDescription), CultureInfo.CurrentUICulture) ?? nameof(PanelDescription);

    /// <summary>Gets the localized TextCaption text.</summary>
    public static string TextCaption => Resources.GetString(nameof(TextCaption), CultureInfo.CurrentUICulture) ?? nameof(TextCaption);

    /// <summary>Gets the localized TextDescription text.</summary>
    public static string TextDescription => Resources.GetString(nameof(TextDescription), CultureInfo.CurrentUICulture) ?? nameof(TextDescription);

    /// <summary>Gets the localized ColorValidation text.</summary>
    public static string ColorValidation => Resources.GetString(nameof(ColorValidation), CultureInfo.CurrentUICulture) ?? nameof(ColorValidation);

    /// <summary>Gets the localized LogoLengthValidation text.</summary>
    public static string LogoLengthValidation => Resources.GetString(nameof(LogoLengthValidation), CultureInfo.CurrentUICulture) ?? nameof(LogoLengthValidation);

    /// <summary>Gets the localized LogoFormatValidation text.</summary>
    public static string LogoFormatValidation => Resources.GetString(nameof(LogoFormatValidation), CultureInfo.CurrentUICulture) ?? nameof(LogoFormatValidation);
}

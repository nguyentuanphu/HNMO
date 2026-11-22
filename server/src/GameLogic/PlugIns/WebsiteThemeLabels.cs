// <copyright file="WebsiteThemeLabels.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Globalization;
using System.Resources;

/// <summary>Localized captions for the website appearance selector.</summary>
public static class WebsiteThemeLabels
{
    private static readonly ResourceManager Resources = new("HNMO.GameLogic.Properties.WebsiteTheme", typeof(WebsiteThemeLabels).Assembly);

    /// <summary>Gets the localized selector caption.</summary>
    public static string Caption => Resources.GetString(nameof(Caption), CultureInfo.CurrentUICulture) ?? nameof(Caption);

    /// <summary>Gets the localized description caption.</summary>
    public static string Description => Resources.GetString(nameof(Description), CultureInfo.CurrentUICulture) ?? nameof(Description);

    /// <summary>Gets the description of the single website-wide appearance setting.</summary>
    public static string GlobalDescription => Resources.GetString(nameof(GlobalDescription), CultureInfo.CurrentUICulture) ?? Description;

    /// <summary>Gets the localized classic caption.</summary>
    public static string Classic => Resources.GetString(nameof(Classic), CultureInfo.CurrentUICulture) ?? nameof(Classic);

    /// <summary>Gets the localized azure caption.</summary>
    public static string Azure => Resources.GetString(nameof(Azure), CultureInfo.CurrentUICulture) ?? nameof(Azure);

    /// <summary>Gets the localized ember caption.</summary>
    public static string Ember => Resources.GetString(nameof(Ember), CultureInfo.CurrentUICulture) ?? nameof(Ember);

    /// <summary>Gets the localized jade caption.</summary>
    public static string Jade => Resources.GetString(nameof(Jade), CultureInfo.CurrentUICulture) ?? nameof(Jade);

    /// <summary>Gets the localized violet caption.</summary>
    public static string Violet => Resources.GetString(nameof(Violet), CultureInfo.CurrentUICulture) ?? nameof(Violet);

    /// <summary>Gets the localized parchment caption.</summary>
    public static string Parchment => Resources.GetString(nameof(Parchment), CultureInfo.CurrentUICulture) ?? nameof(Parchment);
}

// <copyright file="WebsiteBrand.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

/// <summary>The bounded, public portion of the host's brand configuration.</summary>
/// <param name="LogoUrl">A safe logo URL, or null for the text brand.</param>
/// <param name="AccentColor">A valid highlight color, or null for the theme default.</param>
/// <param name="BackgroundColor">A valid background color, or null for the theme default.</param>
/// <param name="PanelColor">A valid panel color, or null for the theme default.</param>
/// <param name="TextColor">A valid text color, or null for the theme default.</param>
public sealed record WebsiteBrand(string? LogoUrl, string? AccentColor, string? BackgroundColor, string? PanelColor, string? TextColor)
{
    /// <summary>Projects only public fields, validating even manually edited configuration JSON.</summary>
    /// <param name="configuration">The host's saved brand settings.</param>
    /// <returns>The validated public brand.</returns>
    public static WebsiteBrand FromConfiguration(GameNameConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new WebsiteBrand(
            SafeLogo(configuration.LogoUrl),
            SafeColor(configuration.AccentColor),
            SafeColor(configuration.BackgroundColor),
            SafeColor(configuration.PanelColor),
            SafeColor(configuration.TextColor));
    }

    private static string? SafeColor(string? value)
    {
        const int HexColorLength = 7; // CSS #RRGGBB, not an operator setting.
        return value is { Length: HexColorLength } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit) ? value : null;
    }

    private static string? SafeLogo(string? value)
    {
        const int MaximumLogoLength = 500;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLogoLength || value.Any(char.IsWhiteSpace) || value.Contains('\\'))
        {
            return null;
        }

        if (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal))
        {
            return value;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri
            : null;
    }
}

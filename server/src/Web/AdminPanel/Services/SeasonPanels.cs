// <copyright file="SeasonPanels.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using HNMO.Interfaces;

/// <summary>One season's admin panel, as the home page's big buttons show it.</summary>
/// <param name="Season">The season key (<c>s6</c> or <c>s9</c>).</param>
/// <param name="Url">The panel's address, or <c>null</c> when the season is switched off.</param>
/// <param name="IsCurrent">Whether this panel is that season's.</param>
public sealed record SeasonPanel(string Season, string? Url, bool IsCurrent);

/// <summary>
/// Where each season's admin panel is (seasons plan, step 2). Each season runs as its own program with its own admin
/// panel: Season 6.3's on the configured port, Season 9's on the next one. Shared settings (accounts, coins,
/// website, bans, admin users, host settings) are edited in Season 6.3's.
/// </summary>
public static class SeasonPanels
{
    /// <summary>Both seasons' panels, Season 6.3 first.</summary>
    /// <param name="current">The season of this panel.</param>
    /// <param name="settings">The host settings (admin port, Season 9 switch).</param>
    /// <param name="scheme">The scheme of this request.</param>
    /// <param name="host">The host name of this request (the same computer serves both panels).</param>
    /// <returns>The two panels.</returns>
    public static IReadOnlyList<SeasonPanel> For(string current, HostSettings settings, string scheme, string host)
    {
        string Url(int port) => $"{scheme}://{host}:{port}/";
        var seasonNineOn = settings.SeasonNineEnabled || current == HnmoSeasonInstance.SeasonNine;
        return
        [
            new SeasonPanel(HnmoSeasonInstance.SeasonSix, Url(settings.AdminPort), current == HnmoSeasonInstance.SeasonSix),
            new SeasonPanel(HnmoSeasonInstance.SeasonNine, seasonNineOn ? Url(settings.AdminPort + 1) : null, current == HnmoSeasonInstance.SeasonNine),
        ];
    }

    /// <summary>The address of the shared settings: Season 6.3's panel, on its accounts page.</summary>
    /// <param name="panels">The panels from <see cref="For"/>.</param>
    /// <returns>The address.</returns>
    public static string SharedSettingsUrl(IReadOnlyList<SeasonPanel> panels) =>
        panels[0].IsCurrent ? "accounts" : panels[0].Url + "accounts";
}

// <copyright file="SeasonNineClientStateTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>
/// The Season 9 client settings the web game asks for (F5 58 / F5 59): the admin page's "Season 9 minimap" setting
/// reaches the web game, and only from an active Season 9 realm.
/// </summary>
[TestFixture]
public class SeasonNineClientStateTests
{
    /// <summary>The minimap setting is sent as it is set.</summary>
    /// <param name="minimap">The admin setting.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void TheMinimapSettingIsSent(bool minimap)
    {
        var plugIn = Create(enabled: true, seasonNine: true, minimap);
        Assert.That(plugIn.ClientStateJson(), Is.EqualTo($"{{\"v\":1,\"minimap\":{(minimap ? "true" : "false")}}}"));
    }

    /// <summary>Nothing is sent outside a Season 9 realm (Season 6.3, Arena), or with the systems off.</summary>
    /// <param name="enabled">Whether the Season 9 systems are on.</param>
    /// <param name="seasonNine">Whether the realm is a Season 9 realm.</param>
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void NothingIsSentOutsideAnActiveSeasonNineRealm(bool enabled, bool seasonNine)
    {
        Assert.That(Create(enabled, seasonNine, minimap: false).ClientStateJson(), Is.Null);
    }

    private static SeasonNineSystemsPlugIn Create(bool enabled, bool seasonNine, bool minimap) => new()
    {
        Configuration = new SeasonNineSystemsConfiguration { Enabled = enabled, SeasonNineMinimapEnabled = minimap },
        IsSeasonNineRealm = () => seasonNine,
    };
}

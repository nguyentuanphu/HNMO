// <copyright file="SeasonPanelsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Services;

/// <summary>The admin home page's big season buttons (<see cref="SeasonPanels"/>).</summary>
[TestFixture]
public class SeasonPanelsTests
{
    /// <summary>Season 6.3's panel: Season 9's is the next port when it's on, and off otherwise.</summary>
    [Test]
    public void SeasonSixPanelPointsToSeasonNineOnTheNextPort()
    {
        var settings = new HostSettings { AdminPort = 8095, SeasonNineEnabled = true };
        var panels = SeasonPanels.For("s6", settings, "http", "localhost");
        Assert.Multiple(() =>
        {
            Assert.That(panels[0], Is.EqualTo(new SeasonPanel("s6", "http://localhost:8095/", true)));
            Assert.That(panels[1], Is.EqualTo(new SeasonPanel("s9", "http://localhost:8096/", false)));
            Assert.That(SeasonPanels.SharedSettingsUrl(panels), Is.EqualTo("accounts"));
        });

        settings.SeasonNineEnabled = false;
        Assert.That(SeasonPanels.For("s6", settings, "http", "localhost")[1].Url, Is.Null, "Season 9 is off");
    }

    /// <summary>Season 9's panel points back to Season 6.3's, where the shared settings are.</summary>
    [Test]
    public void SeasonNinePanelPointsBackToSeasonSix()
    {
        var panels = SeasonPanels.For("s9", new HostSettings { AdminPort = 9000 }, "https", "host.tail");
        Assert.Multiple(() =>
        {
            Assert.That(panels[0], Is.EqualTo(new SeasonPanel("s6", "https://host.tail:9000/", false)));
            Assert.That(panels[1], Is.EqualTo(new SeasonPanel("s9", "https://host.tail:9001/", true)));
            Assert.That(SeasonPanels.SharedSettingsUrl(panels), Is.EqualTo("https://host.tail:9000/accounts"));
        });
    }
}

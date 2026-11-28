// <copyright file="S9EventsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Globalization;
using System.IO;
using System.Linq;
using HNMO.GameLogic.PlugIns.SeasonNineEvents;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;

/// <summary>
/// Admin page route, localization, and store persistence checks for <see cref="S9Events"/>.
/// </summary>
[TestFixture]
public class S9EventsPageTests
{
    /// <summary>Verifies <see cref="S9Events"/> route, 4-language resources, and owed-reward persistence.</summary>
    [Test]
    public void RegistersRouteLocalizesAndPersistsOwedRewards()
    {
        var routeAttr = typeof(S9Events)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/s9-events"));

        foreach (var cultureName in new[] { "en", "vi", "de", "zh-CN" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            Assert.That(Resources.ResourceManager.GetString("S9EventsTitle", culture), Is.Not.Null.And.Not.Empty, cultureName);
            Assert.That(Resources.ResourceManager.GetString("PageHelpTextS9Events", culture), Is.Not.Null.And.Not.Empty, cultureName);
        }

        var path = Path.Combine(Path.GetTempPath(), $"hnmo-s9page-{Guid.NewGuid():N}.json");
        try
        {
            var store = SeasonNineEventsStore.Open(path);
            store.QueueOwedReward(new S9EventOwedReward { LoginName = "acct1", Zen = 1_000_000, ReasonEn = "Acheron Guardian" });
            store.RecordHistory(new S9EventHistoryEntry { EventKey = "acheron", EventName = "Acheron Guardian", Summary = "Cleared", Count = 3 });
            var snap = store.Snapshot();
            Assert.That(snap.OwedRewards, Has.Count.EqualTo(1));
            Assert.That(snap.History, Has.Count.EqualTo(1));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

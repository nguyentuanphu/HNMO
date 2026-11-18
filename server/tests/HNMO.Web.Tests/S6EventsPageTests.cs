// <copyright file="S6EventsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.S6Events;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Admin page route and store persistence verification for <see cref="S6Events"/>.
/// </summary>
[TestFixture]
public class S6EventsPageTests
{
    /// <summary>Verifies <see cref="S6Events"/> exposes route <c>/s6-events</c> and default tiers.</summary>
    [Test]
    public void RegistersRouteAndPersistsOwedRewardsAndHistory()
    {
        var routeAttr = typeof(S6Events)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/s6-events"));

        var cfg = new S6EventsConfiguration();
        Assert.That(cfg.Enabled, Is.False);
        Assert.That(cfg.CherryBlossomTiers, Has.Count.EqualTo(3));
        Assert.That(cfg.LuckyCoinTiers, Has.Count.EqualTo(3));

        var path = Path.Combine(Path.GetTempPath(), $"hnmo-s6page-{Guid.NewGuid():N}.json");
        try
        {
            var store = S6EventsStore.Open(path);
            store.QueueOwedReward(new S6EventsOwedReward { LoginName = "acct1", Zen = 100_000, ReasonEn = "LMS" });
            store.RecordLmsWin(new S6EventsLmsWinEntry { CompletedAtUtc = DateTime.UtcNow, LoginName = "acct1", CharacterName = "Winner", Kills = 3 });
            var snap = store.Snapshot();
            Assert.That(snap.OwedRewards, Has.Count.EqualTo(1));
            Assert.That(snap.LmsHistory, Has.Count.EqualTo(1));
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

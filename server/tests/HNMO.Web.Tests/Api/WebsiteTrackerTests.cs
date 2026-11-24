// <copyright file="WebsiteTrackerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.API;
using Moq;

/// <summary>Privacy and response contract for the public tracker feed.</summary>
[TestFixture]
public sealed class WebsiteTrackerTests
{
    /// <summary>The raw payload remains private and opt-outs on either side apply on the next response.</summary>
    [Test]
    public void PublicProjectionUsesCurrentPreferencesAndAnAllowlist()
    {
        var privacy = new WebsiteTrackerPrivacyStore(new InMemoryHnmoPlayerStateRowBackend());
        var now = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
        ActivityLogEntry[] rows =
        [
            new() { Kind = ActivityKind.Trade, TimestampUtc = now, Account = "privateA", PartnerAccount = "privateB", Character = "Knight", PartnerCharacter = "Mage",
                ItemIds = "internal-item-id", Details = "private@example.test 127.0.0.1", ItemSummary = "private detail", Id = "not-a-public-id" },
            new() { Kind = ActivityKind.Administration, TimestampUtc = now, Account = "moderator", Character = "Secret" },
            new() { Kind = ActivityKind.Pickup, TimestampUtc = now, Account = "privateC", Character = "p@x.test" },
        ];

        var hidden = Projection(rows, false, privacy);
        Assert.That(hidden, Has.Count.EqualTo(2));
        Assert.That(hidden[0]!["character"], Is.Null);
        Assert.That(hidden[0]!["partner"], Is.Null);
        var visible = Projection(rows, true, privacy);
        Assert.That(visible[0]!["character"]!.GetValue<string>(), Is.EqualTo("Knight"));
        Assert.That(visible[0]!["partner"]!.GetValue<string>(), Is.EqualTo("Mage"));
        Assert.That(visible[0]!["time"]!.GetValue<DateTimeOffset>().Offset, Is.EqualTo(TimeSpan.FromHours(11)));
        Assert.That(visible[1]!["character"], Is.Null);
        var json = visible.ToJsonString();
        Assert.That(json, Does.Not.Contain("private").And.Not.Contain("127.0.0.1").And.Not.Contain("internal-item-id").And.Not.Contain("not-a-public-id"));
        privacy.Write("PRIVATEb", true, now);
        Assert.That(Projection(rows, true, privacy), Has.Count.EqualTo(1));
        privacy.Write("privateB", false, now);
        Assert.That(Projection(rows, true, privacy), Has.Count.EqualTo(2));
        privacy.Write("privateA", true, now);
        Assert.That(Projection(rows, false, privacy), Has.Count.EqualTo(1));
    }

    /// <summary>The history includes only the requested item's state and respects either player's opt-out.</summary>
    [Test]
    public void ItemHistoryFiltersOptOutsAndOtherItems()
    {
        var privacy = new WebsiteTrackerPrivacyStore(new InMemoryHnmoPlayerStateRowBackend());
        var now = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
        var serial = Guid.NewGuid();
        var before = new ActivityItemState(serial, 1, 2, 6, 1, 7, 0, false, "private-options", "private-description");
        var after = before with { Level = 7 };
        var other = before with { Serial = Guid.NewGuid(), Level = 15 };
        var row = new ActivityLogEntry
        {
            TimestampUtc = now,
            Kind = ActivityKind.Upgrade,
            Account = "secretAccount",
            PartnerAccount = "secondSecret",
            Character = "Knight",
            ItemSerials = $"{serial:N},{other.Serial:N}",
            BeforeItems = [other, before],
            AfterItems = [after],
        };
        var output = WebsiteTrackerController.ProjectItemHistory([row], serial, false, privacy);
        var json = JsonSerializer.SerializeToNode(output, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsArray().Single()!;
        Assert.That(json["kind"]!.GetValue<string>(), Is.EqualTo("upgrade"));
        Assert.That(json["before"]!["level"]!.GetValue<int>(), Is.EqualTo(6));
        Assert.That(json["after"]!["level"]!.GetValue<int>(), Is.EqualTo(7));
        Assert.That(json["character"], Is.Null);
        Assert.That(json.ToJsonString(), Does.Not.Contain("private").And.Not.Contain("secret"));
        privacy.Write("secondSecret", true, now);
        Assert.That(WebsiteTrackerController.ProjectItemHistory([row], serial, true, privacy), Is.Empty);
    }

    /// <summary>Even an oversized raw snapshot cannot create a large public response.</summary>
    [Test]
    public void PublicPageIsBoundedAndNeverUsesTheLoginAsTheName()
    {
        var privacy = new WebsiteTrackerPrivacyStore(new InMemoryHnmoPlayerStateRowBackend());
        var rows = Enumerable.Range(1, 300).Select(_ => new ActivityLogEntry
        {
            Kind = ActivityKind.Drop,
            Account = "account",
            Character = "Account",
            TimestampUtc = DateTime.UnixEpoch,
        });
        var projected = Projection(rows, true, privacy);
        Assert.That(projected, Has.Count.EqualTo(24));
        Assert.That(projected.All(row => row!["character"] is null), Is.True);
    }

    /// <summary>The tracker reads a bounded backend page and leaves expired or corrupt rows out.</summary>
    [Test]
    public void RecentActivityUsesTheBackendPageWithoutLoadingAllRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tracker-{Guid.NewGuid():N}.json");
        var now = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
        var stale = now.AddDays(-31);
        var backend = new Mock<IHnmoPlayerStateRowQueryBackend>();
        backend.Setup(value => value.LoadScopePage(ActivityLogStore.FeatureKey, string.Empty, 0, 48)).Returns(
        [
            new(ActivityLogStore.FeatureKey, "bad", "invalid", now),
            new(ActivityLogStore.FeatureKey, "recent", """{"id":"recent"}""", now),
            new(ActivityLogStore.FeatureKey, "stale", """{"id":"stale"}""", stale),
        ]);
        HnmoPlayerStateRowStoreRegistry.RegisterBackend(HnmoStateStore.ResolveStateFilePath(path)!, backend.Object);
        var result = ActivityLogStore.Open(path).QueryRecent(now.AddDays(-30), 48);
        Assert.That(result.Select(row => row.Id), Is.EqualTo(new[] { "recent" }));
        backend.Verify(value => value.LoadAll(It.IsAny<string>()), Times.Never);
    }

    /// <summary>The public tracker and public names both default to off, with localized captions.</summary>
    /// <param name="culture">The admin locale.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void HostSwitchesDefaultToOffAndHaveNativeCaptions(string culture)
    {
        var config = JsonSerializer.Deserialize<WebsiteConfiguration>("{}")!;
        Assert.That(config.TrackerEnabled, Is.False);
        Assert.That(config.TrackerShowCharacterNames, Is.False);
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Assert.That(WebsiteServerPageLabels.TrackerEnabled, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.TrackerEnabled)));
            Assert.That(WebsiteServerPageLabels.TrackerNamesDescription, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.TrackerNamesDescription)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static JsonArray Projection(IEnumerable<ActivityLogEntry> rows, bool showNames, WebsiteTrackerPrivacyStore privacy)
        => JsonSerializer.SerializeToNode(WebsiteTrackerController.Project(rows, showNames, privacy))!.AsArray();
}

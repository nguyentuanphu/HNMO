// <copyright file="WebsiteMissionSnapshotTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>Website reads must not reset live progress or invent usable referral codes.</summary>
[TestFixture]
public class WebsiteMissionSnapshotTests
{
    /// <summary>A new account's projected defaults do not create any state in the real engine.</summary>
    [Test]
    public void ReadsNewStateWithoutCreatingLiveRecords()
    {
        var store = new MissionStore();
        var engine = new MissionEngine(new MissionsConfiguration(), store);
        var board = WebsiteMissionSnapshot.Read(engine, "character", "ann", new BasicModel.GameConfiguration(), new DateTime(2026, 11, 17, 14, 0, 0, DateTimeKind.Utc));
        Assert.That(store.Data.Accounts, Is.Empty);
        Assert.That(store.Data.Characters, Is.Empty);
        Assert.That(board["invite"]!["code"], Is.Null);
        Assert.That(board["day"]!.GetValue<string>(), Is.EqualTo("2026-11-18"));
    }

    /// <summary>Rollover stays in the copy; referral counts disclose no other account names or addresses.</summary>
    [Test]
    public void KeepsLiveProgressAndReferralDetailsPrivate()
    {
        var store = new MissionStore();
        store.Data.Accounts["ann"] = new MissionAccountState { InviteCode = "REALCODE", DayKey = "2026-11-01", EarnedToday = 25 };
        store.Data.Accounts["private-friend-one"] = new MissionAccountState { InvitedBy = "ann", InviteRewarded = true, LastAddress = "203.0.113.255" };
        store.Data.Accounts["private-friend-two"] = new MissionAccountState { InvitedBy = "ann" };
        var before = JsonSerializer.Serialize(store.Data);
        var board = WebsiteMissionSnapshot.Read(new MissionEngine(new MissionsConfiguration(), store), "character", "ann", new BasicModel.GameConfiguration(), new DateTime(2026, 11, 17, 14, 0, 0, DateTimeKind.Utc));
        Assert.That(JsonSerializer.Serialize(store.Data), Is.EqualTo(before));
        Assert.That(board["invite"]!["code"]!.GetValue<string>(), Is.EqualTo("REALCODE"));
        Assert.That(board["invite"]!["invitedCount"]!.GetValue<int>(), Is.EqualTo(2));
        Assert.That(board["invite"]!["qualifiedCount"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(board.ToJsonString(), Does.Not.Contain("private-friend").And.Not.Contain("203.0.113.255"));
    }
}

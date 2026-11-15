// <copyright file="CommunityEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Community;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Tests for HNMO's community: the one-time Discord and Zalo coins (once per account, Discord also once per Discord
/// user), the community events (codes once per account while the event runs, listed winners once each, with Sydney
/// wall-clock dates incl. daylight saving), and the automatic posts (rare drop filter, texts, highlights).
/// </summary>
[TestFixture]
public class CommunityEngineTest
{
    /// <summary>
    /// The Discord coins need a linked account, come once per account, and a Discord user can't get them for a second account.
    /// </summary>
    [Test]
    public void DiscordCoinsArePaidOncePerAccountAndUser()
    {
        var configuration = new CommunityConfiguration();
        configuration.Discord.Coins = 70;
        configuration.Discord.CoinType = CashShopCoinType.WCoinP;
        var engine = new CommunityEngine(configuration, new CommunityStore());
        var now = Utc(2026, 11, 16, 2, 0);

        Assert.That(engine.ClaimDiscord("test1", null, now).Result, Is.EqualTo(CommunityClaimResult.NotLinked));
        var (result, grant) = engine.ClaimDiscord("Test1", "1001", now);
        Assert.That(result, Is.EqualTo(CommunityClaimResult.Ok));
        Assert.That(grant, Is.EqualTo(new CoinGrant("Test1", CashShopCoinType.WCoinP, 70, "Community: Discord account linked", "hnmo-community:discord:test1")));
        Assert.That(engine.ClaimDiscord("test1", "1001", now).Result, Is.EqualTo(CommunityClaimResult.AlreadyPaid), "Same account, any case.");
        Assert.That(engine.ClaimDiscord("test1", "2002", now).Result, Is.EqualTo(CommunityClaimResult.AlreadyPaid), "Unlinked and linked to another user: still once.");
        Assert.That(engine.ClaimDiscord("test2", "1001", now).Result, Is.EqualTo(CommunityClaimResult.DiscordUserUsed), "The user moved the link to another account.");
        Assert.That(engine.ClaimDiscord("test2", "3003", now).Result, Is.EqualTo(CommunityClaimResult.Ok));

        configuration.Discord.Enabled = false;
        Assert.That(engine.ClaimDiscord("test3", "4004", now).Result, Is.EqualTo(CommunityClaimResult.Off));
    }

    /// <summary>
    /// A Zalo claim gets a code which stays while it waits; the admin's approval pays once; a rejected claim can be made again.
    /// </summary>
    [Test]
    public void ZaloClaimsWaitForTheAdmin()
    {
        var configuration = new CommunityConfiguration();
        var engine = new CommunityEngine(configuration, new CommunityStore());
        var now = Utc(2026, 11, 16, 2, 0);

        var (result, code) = engine.RequestZalo("test1", "Ann", "Ann Nguyen", now);
        Assert.That(result, Is.EqualTo(CommunityClaimResult.Ok));
        Assert.That(code, Does.Match("^Z-[A-Z2-9]{6}$"));
        var again = engine.RequestZalo("TEST1", "Ann", null, now.AddMinutes(1));
        Assert.That(again, Is.EqualTo((CommunityClaimResult.Pending, code)), "The waiting claim keeps its code.");
        Assert.That(engine.PendingZalo().Single().ZaloName, Is.EqualTo("Ann Nguyen"));
        Assert.That(engine.ZaloToPay(), Is.Empty);

        Assert.That(engine.DecideZalo("test1", false, now), Is.True);
        Assert.That(engine.DecideZalo("test1", true, now), Is.False, "Only a waiting claim is decided.");
        var (second, newCode) = engine.RequestZalo("test1", "Ann", "Ann N.", now.AddHours(1));
        Assert.That(second, Is.EqualTo(CommunityClaimResult.Ok));
        Assert.That(newCode, Is.Not.EqualTo(code));

        Assert.That(engine.DecideZalo("test1", true, now.AddHours(2)), Is.True);
        var grant = engine.ZaloToPay().Single();
        Assert.That((grant.LoginName, grant.Amount, grant.Reference), Is.EqualTo(("test1", 50, "hnmo-community:zalo:test1")));
        engine.MarkZaloPaid("test1", now.AddHours(2));
        Assert.That(engine.ZaloToPay(), Is.Empty);
        Assert.That(engine.RequestZalo("test1", "Ann", null, now.AddHours(3)).Result, Is.EqualTo(CommunityClaimResult.AlreadyPaid));
    }

    /// <summary>
    /// An event's code counts once per account and only while the event runs (Sydney wall-clock dates).
    /// </summary>
    [Test]
    public void CodesCountOncePerAccountWhileTheEventRuns()
    {
        var configuration = new CommunityConfiguration();
        configuration.Events.Add(new CommunityEventDefinition
        {
            Id = "summer",
            TitleEn = "Summer party",
            Start = "2026-12-01 18:00",
            End = "2026-12-02 18:00",
            Code = "Sun-Shine",
            Reward = new MissionReward { Coins = 15, Zen = 1000, Items = { new MissionItemReward { Group = 14, Number = 13, Count = 1 } } },
        });
        var engine = new CommunityEngine(configuration, new CommunityStore());

        // 18:00 on 1 December in Sydney (daylight saving, +11) is 07:00 UTC.
        Assert.That(engine.EnterCode("test1", "Ann", "sunshine", Utc(2026, 12, 1, 6, 59)).Result, Is.EqualTo(CommunityCodeResult.NotStarted));
        Assert.That(engine.EnterCode("test1", "Ann", "nothing", Utc(2026, 12, 1, 7, 0)).Result, Is.EqualTo(CommunityCodeResult.Unknown));
        Assert.That(engine.EnterCode("test1", "Ann", " sun shine ", Utc(2026, 12, 1, 7, 0)).Result, Is.EqualTo(CommunityCodeResult.Ok), "Case and separators don't matter.");
        Assert.That(engine.EnterCode("test1", "Bob", "SUNSHINE", Utc(2026, 12, 1, 8, 0)).Result, Is.EqualTo(CommunityCodeResult.AlreadyEntered), "Another character of the same account.");
        Assert.That(engine.EnterCode("test2", "Cid", "SUNSHINE", Utc(2026, 12, 2, 7, 0)).Result, Is.EqualTo(CommunityCodeResult.Ended));

        var (entry, grant) = engine.CoinsToPay().Single();
        Assert.That((grant.LoginName, grant.Amount, grant.Reference), Is.EqualTo(("test1", 15, "hnmo-community:event:summer:code:test1")));
        engine.MarkCoinsPaid(entry);
        Assert.That(engine.CoinsToPay(), Is.Empty);
        var owed = engine.TakeOwed("ann");
        Assert.That(owed.Single().Reward.Zen, Is.EqualTo(1000));
        engine.ReturnOwed("Ann", owed);
        Assert.That(engine.Owed("Ann"), Has.Count.EqualTo(1), "A reward which didn't fit waits.");
        Assert.That(engine.Took("summer", "TEST1", "Ann"), Is.EqualTo((true, false)));
    }

    /// <summary>
    /// Listed winners are recorded once the event started, each once, with their own references.
    /// </summary>
    [Test]
    public void WinnersArePaidOnceAfterTheStart()
    {
        var configuration = new CommunityConfiguration();
        var definition = new CommunityEventDefinition { Id = "screens", Start = "2026-11-20 12:00", Winners = { "Ann", " ann ", "Bob", "" } };
        configuration.Events.Add(definition);
        var engine = new CommunityEngine(configuration, new CommunityStore());

        Assert.That(engine.WinnersToRecord(Utc(2026, 11, 20, 0, 59)), Is.Empty, "12:00 Sydney is 01:00 UTC.");
        var winners = engine.WinnersToRecord(Utc(2026, 11, 20, 1, 0));
        Assert.That(winners.Select(w => w.Character), Is.EqualTo(new[] { "Ann", "Bob" }));
        Assert.That(engine.RecordWinner(definition, "Ann", "test1", Utc(2026, 11, 20, 1, 0)), Is.True);
        Assert.That(engine.RecordWinner(definition, "ANN", "test1", Utc(2026, 11, 20, 1, 1)), Is.False);
        Assert.That(engine.WinnersToRecord(Utc(2026, 11, 20, 1, 2)).Single().Character, Is.EqualTo("Bob"));
        var grant = engine.CoinsToPay().Single().Grant;
        Assert.That(grant.Reference, Is.EqualTo("hnmo-community:event:screens:winner:ann"));
        Assert.That(grant.Amount, Is.EqualTo(20), "The default reward.");
    }

    /// <summary>
    /// Sydney wall-clock times: a time skipped when the clocks go forward moves past the gap, a repeated one is its first occurrence.
    /// </summary>
    [Test]
    public void EventDatesAreSydneyTimes()
    {
        var engine = new CommunityEngine(new CommunityConfiguration(), new CommunityStore());
        Assert.That(engine.ToUtc("2026-07-01 10:00"), Is.EqualTo(Utc(2026, 7, 1, 0, 0)), "Winter: +10.");
        Assert.That(engine.ToUtc("2026-12-25T10:00"), Is.EqualTo(Utc(2026, 12, 24, 23, 0)), "Summer: +11.");
        Assert.That(engine.ToUtc("2026-10-04 02:30"), Is.EqualTo(Utc(2026, 10, 3, 16, 0)), "02:30 doesn't exist on 4 October 2026; 03:00 (+11) does.");
        Assert.That(engine.ToUtc("2026-04-05 02:30"), Is.EqualTo(Utc(2026, 4, 4, 15, 30)), "02:30 happens twice on 5 April 2026; the first is +11.");
        Assert.That(engine.ToUtc(string.Empty), Is.Null);
        Assert.That(engine.ToUtc("tomorrow"), Is.Null);
        var open = new CommunityEventDefinition { Id = "a" };
        Assert.That(engine.Phase(open, Utc(2026, 1, 1, 0, 0)), Is.EqualTo(CommunityEventPhase.Open), "No dates: always open.");
    }

    /// <summary>
    /// The rare drop filter: excellent, ancient, a level, listed items, limited to maps when maps are set.
    /// </summary>
    [Test]
    public void TheDropFilterPicksTheRareOnes()
    {
        var configuration = new CommunityConfiguration();
        configuration.Drops.Excellent = true;
        configuration.Drops.Ancient = false;
        configuration.Drops.MinimumLevel = 9;
        configuration.Drops.Items.Add("14 13");
        var engine = new CommunityEngine(configuration, new CommunityStore());
        Assert.That(engine.IsRareDrop(0, 1, 0, true, false, 0), Is.True, "Excellent.");
        Assert.That(engine.IsRareDrop(0, 1, 0, false, true, 0), Is.False, "Ancient is off.");
        Assert.That(engine.IsRareDrop(0, 1, 9, false, false, 0), Is.True, "+9.");
        Assert.That(engine.IsRareDrop(0, 1, 8, false, false, 0), Is.False);
        Assert.That(engine.IsRareDrop(14, 13, 0, false, false, 0), Is.True, "A listed item.");
        configuration.Drops.Maps.Add(7);
        Assert.That(engine.IsRareDrop(14, 13, 0, false, false, 0), Is.False, "Not on a listed map.");
        Assert.That(engine.IsRareDrop(14, 13, 0, false, false, 7), Is.True);
        Assert.That(CommunityEngine.ParseItem("14,14"), Is.EqualTo((14, 14)));
        Assert.That(CommunityEngine.ParseItem("x"), Is.Null);

        var now = Utc(2026, 11, 16, 2, 0);
        Assert.That(engine.TryTakeDropSlot("Ann", now), Is.True);
        Assert.That(engine.TryTakeDropSlot("ann", now.AddSeconds(10)), Is.False, "Within the cooldown.");
        Assert.That(engine.TryTakeDropSlot("Ann", now.AddSeconds(31)), Is.True);
    }

    /// <summary>
    /// Posts fill their texts in both languages; the website's highlights keep only the kinds that go there, newest first,
    /// and a kind which is off makes no post.
    /// </summary>
    [Test]
    public void PostsFillTheTextsAndKeepTheHighlights()
    {
        var configuration = new CommunityConfiguration();
        configuration.GuildLadderWinner.ToWebsite = false;
        configuration.CastleSiegeWinner.Enabled = false;
        var engine = new CommunityEngine(configuration, new CommunityStore());
        var now = Utc(2026, 11, 16, 2, 0);
        var post = engine.Post(CommunityPostKind.RareDrop, new Dictionary<string, string> { ["player"] = "Ann", ["item"] = "Excellent Sword", ["monster"] = "Goblin", ["map"] = "Lorencia" }, new Dictionary<string, string> { ["player"] = "Ann", ["item"] = "Kiếm (hoàn hảo)", ["monster"] = "Goblin", ["map"] = "Lorencia" }, now);
        Assert.That(post!.TextEn, Is.EqualTo("Ann found Excellent Sword (Goblin, Lorencia)!"));
        Assert.That(post.TextVi, Is.EqualTo("Ann đã nhặt được Kiếm (hoàn hảo) (Goblin, Lorencia)!"));
        engine.Post(CommunityPostKind.GuildLevelUp, new Dictionary<string, string> { ["guild"] = "Alpha", ["level"] = "3" }, null, now.AddMinutes(1));
        Assert.That(engine.Post(CommunityPostKind.GuildLadderWinner, new Dictionary<string, string> { ["guild"] = "Alpha" }, null, now), Is.Not.Null, "Still posted (to Discord).");
        Assert.That(engine.Post(CommunityPostKind.CastleSiegeWinner, new Dictionary<string, string> { ["guild"] = "Alpha" }, null, now), Is.Null);

        var highlights = engine.Highlights(10);
        Assert.That(highlights.Select(h => h.Kind), Is.EqualTo(new[] { CommunityPostKind.GuildLevelUp, CommunityPostKind.RareDrop }));
        Assert.That(highlights[0].TextVi, Is.EqualTo("Bang hội Alpha đã lên cấp 3!"));
        for (var i = 0; i < 120; i++)
        {
            engine.Post(CommunityPostKind.GuildLevelUp, new Dictionary<string, string> { ["guild"] = "G" + i, ["level"] = "2" }, null, now);
        }

        Assert.That(engine.Highlights(1000), Has.Count.EqualTo(CommunityEngine.HighlightsKept));
    }

    /// <summary>
    /// The state outlasts a restart (the file), and the client and website JSON are plain ASCII without the codes.
    /// </summary>
    [Test]
    public void TheStateIsSavedAndWrittenAsAscii()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-community-{Guid.NewGuid():N}.json");
        try
        {
            var configuration = new CommunityConfiguration();
            configuration.Events.Add(new CommunityEventDefinition { Id = "e1", TitleEn = "Party", TitleVi = "Tiệc", Code = "SECRET" });
            var store = new CommunityStore(file);
            var engine = new CommunityEngine(configuration, store);
            var now = Utc(2026, 11, 16, 2, 0);
            engine.ClaimDiscord("test1", "1001", now);
            engine.EnterCode("test1", "Ann", "secret", now);
            engine.Post(CommunityPostKind.GuildLevelUp, new Dictionary<string, string> { ["guild"] = "Ồ", ["level"] = "2" }, null, now);
            lock (store.Lock)
            {
                store.SaveIfChanged();
            }

            var reloaded = new CommunityEngine(configuration, new CommunityStore(file));
            Assert.That(reloaded.ClaimDiscord("TEST1", "1001", now).Result, Is.EqualTo(CommunityClaimResult.AlreadyPaid));
            Assert.That(reloaded.EnterCode("test1", "Ann", "SECRET", now).Result, Is.EqualTo(CommunityCodeResult.AlreadyEntered));

            var state = CommunityStateWriter.WriteState(reloaded, new CommunityPlayerFacts("test1", "Ann", true, true), now, new CommunityStateEvent("code", "Ok", 20, "e1"));
            Assert.That(state.All(c => c < 128), Is.True);
            using var json = JsonDocument.Parse(state);
            Assert.That(json.RootElement.GetProperty("discord").GetProperty("paid").GetBoolean(), Is.True);
            Assert.That(json.RootElement.GetProperty("events")[0].GetProperty("entered").GetBoolean(), Is.True);
            Assert.That(json.RootElement.GetProperty("events")[0].GetProperty("titleVi").GetString(), Is.EqualTo("Tiệc"));
            Assert.That(state, Does.Not.Contain("SECRET"));

            var website = CommunityStateWriter.WritePublic(reloaded, now);
            Assert.That(website.All(c => c < 128), Is.True);
            Assert.That(website, Does.Not.Contain("SECRET").And.Not.Contain("test1"));
            using var publicJson = JsonDocument.Parse(website);
            Assert.That(publicJson.RootElement.GetProperty("highlights")[0].GetProperty("textEn").GetString(), Is.EqualTo("The guild Ồ reached level 2!"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}

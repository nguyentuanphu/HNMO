// <copyright file="BanManagerEnforcementTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.PlayerActions.Character;
using HNMO.GameLogic.PlayerActions.Chat;
using HNMO.GameLogic.PlugIns.Moderation;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Login;
using HNMO.Interfaces;
using HNMO.Persistence;
using Moq;

/// <summary>End-to-end verification of salted network/device bans, login/character/chat enforcement, Sydney time notices, and fail-closed salt rules.</summary>
[TestFixture]
public class BanManagerEnforcementTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);

    /// <summary>Network and device bans never persist raw IP/device tokens and fail closed if salt vanishes.</summary>
    [Test]
    public void SaltedNetworkAndDeviceBansNeverStorePlaintextAndFailClosedWithoutSalt()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var service = new BanManagerService(backend);

        var netBan = service.Issue(
            BanTargetKind.NetworkBan,
            "::ffff:203.0.113.42",
            "Suspicious traffic",
            "Lưu lượng đáng ngờ",
            "host",
            Now,
            Now.AddHours(2));
        var devBan = service.Issue(
            BanTargetKind.DeviceBan,
            "DEVICE-HWID-998877",
            "Repeated abuse",
            "Vi phạm nhiều lần",
            "host",
            Now,
            null);

        Assert.That(netBan.TargetKey, Does.Not.Contain("203.0.113.42"));
        Assert.That(netBan.TargetDisplay, Does.Not.Contain("203.0.113.42"));
        Assert.That(devBan.TargetKey, Does.Not.Contain("998877"));

        var serialized = string.Join('\n', backend.LoadAll(BanManagerService.BanFeature).Select(r => r.PayloadJson));
        Assert.That(serialized, Does.Not.Contain("203.0.113.42"));
        Assert.That(serialized, Does.Not.Contain("998877"));

        Assert.That(service.FindActiveLoginBlock("any", "203.0.113.42", null, Now)?.Id, Is.EqualTo(netBan.Id));
        Assert.That(service.FindActiveLoginBlock("any", null, "device-hwid-998877", Now)?.Id, Is.EqualTo(devBan.Id));
        Assert.That(service.FindActiveLoginBlock("any", "203.0.113.42", null, Now.AddHours(3)), Is.Null);

        backend.UpsertBatch([], [(BanManagerService.SaltFeature, "identity")]);
        Assert.Throws<InvalidDataException>(() => service.FindActiveLoginBlock("any", "203.0.113.42", null, Now));
    }

    /// <summary>Refused login sends structured EN/VI ban notice with Sydney expiry and blocks entry.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LoginActionRefusesAccountAndNetworkBanWithNoticeAsync()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var service = new BanManagerService(backend);
        var expires = DateTime.UtcNow.AddHours(3);
        service.Issue(BanTargetKind.AccountBan, "ann", "Exploit abuse", "Lạm dụng lỗi game", "host", DateTime.UtcNow, expires);

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var action = new LoginAction(service);
        await action.LoginAsync(player, "ann", "pass").ConfigureAwait(false);

        var noticeMock = Mock.Get(player.ViewPlugIns.GetPlugIn<IHnmoBanNoticeViewPlugIn>()!);
        noticeMock.Verify(v => v.SendBanNoticeAsync(It.Is<string>(json => HasBanDetails(json, "Exploit abuse", "Lạm dụng lỗi game"))), Times.Once);

        var loginMock = Mock.Get(player.ViewPlugIns.GetPlugIn<IShowLoginResultPlugIn>()!);
        loginMock.Verify(v => v.ShowLoginResultAsync(LoginResult.TemporaryBlocked), Times.Once);
    }

    /// <summary>Character bans refuse entering the world in SelectCharacterAction.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task SelectCharacterActionRefusesBannedCharacterWithSydneyNoticeAsync()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var service = new BanManagerService(backend);
        service.Issue(BanTargetKind.CharacterBan, "AnnHero", "Trading bug", "Lỗi giao dịch", "host", DateTime.UtcNow, null);

        var player = new SelectTestPlayer(GameContextTestHelper.CreateGameContext([]));
        var account = new HNMO.Persistence.BasicModel.Account { LoginName = "ann" };
        var character = new HNMO.Persistence.BasicModel.Character { Name = "AnnHero" };
        account.Characters.Add(character);
        player.Account = account;
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);

        var action = new SelectCharacterAction(service);
        await action.SelectCharacterAsync(player, "AnnHero").ConfigureAwait(false);

        Assert.That(player.SelectedCharacter, Is.Null);
        Assert.That(player.Disconnected, Is.True);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!).Verify(
            v => v.ShowMessageAsync(It.Is<string>(s => s.Contains("Trading bug") && s.Contains("Permanent")), MessageType.BlueNormal),
            Times.Once);
    }

    /// <summary>Chat mutes block normal chat and display localized EN/VI reason and Sydney end time.</summary>
    /// <param name="culture">Player culture.</param>
    /// <returns>The test task.</returns>
    [TestCase("en")]
    [TestCase("vi")]
    public async Task ChatProcessorBlocksMutedPlayerWithLocalizedReasonAndSydneyTimeAsync(string culture)
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var service = new BanManagerService(backend);
        service.Issue(
            BanTargetKind.CharacterMute,
            "MutedDk",
            "Spamming world chat",
            "Spam kênh chat thế giới",
            "host",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(45));

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Culture = CultureInfo.GetCultureInfo(culture);
        player.SelectedCharacter!.Name = "MutedDk";

        var processor = new ChatMessageNormalProcessor { BanServiceOverride = service };
        await processor.ProcessMessageAsync(player, ("hello everyone", "MutedDk")).ConfigureAwait(false);

        var expectedSnippet = culture == "vi" ? "Spam kênh chat thế giới" : "Spamming world chat";
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!).Verify(
            v => v.ShowMessageAsync(It.Is<string>(s => s.Contains(expectedSnippet, StringComparison.Ordinal) && s.Contains("(Sydney)", StringComparison.Ordinal)), MessageType.BlueNormal),
            Times.Once);
    }

    /// <summary>Expired timed character (earlier kind) and account bans allow entry even when persisted status still reflects the expired ban.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ExpiredTimedBansAllowLoginAndCharacterSelectionAsync()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var service = new BanManagerService(backend);
        var issuedAt = DateTime.UtcNow.AddHours(-2);
        var expiredAt = DateTime.UtcNow.AddMinutes(-5);
        service.Issue(BanTargetKind.AccountBan, "timedacc", "Cooldown", "Tạm khóa", "host", issuedAt, expiredAt);
        var timed = service.Issue(BanTargetKind.CharacterBan, "TimedChar", "Cooldown", "Tạm khóa", "host", issuedAt, expiredAt);
        Assert.That(timed.LeavesCharacterStatus, Is.True);

        // A timed ban of the earlier kind set the character's status: its record has no "leaves status" mark.
        var row = backend.LoadAll(BanManagerService.BanFeature).Single(r => r.ScopeId == timed.Id);
        var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(row.PayloadJson)!.AsObject();
        legacyJson.Remove("leavesCharacterStatus");
        backend.UpsertBatch([new HnmoFeatureRow(BanManagerService.BanFeature, timed.Id, legacyJson.ToJsonString(), DateTime.UtcNow)], []);

        Assert.That(service.HasExpiredAccountBanOnly("timedacc", DateTime.UtcNow), Is.True);
        Assert.That(service.HasExpiredAccountBanOnly("manualacc", DateTime.UtcNow), Is.False);
        Assert.That(service.HasExpiredCharacterBanOnly("TimedChar", DateTime.UtcNow), Is.True);

        var gameContext = GameContextTestHelper.CreateGameContext([]);
        var templateChar = PlayerTestHelper.CreateCharacter(gameContext);
        var player = new SelectTestPlayer(gameContext);
        var account = new HNMO.Persistence.BasicModel.Account { LoginName = "timedacc", State = AccountState.TemporarilyBanned };
        var character = new HNMO.Persistence.BasicModel.Character
        {
            Name = "TimedChar",
            CharacterStatus = CharacterStatus.Banned,
            CharacterClass = templateChar.CharacterClass,
            CurrentMap = templateChar.CurrentMap,
            Inventory = new HNMO.Persistence.BasicModel.ItemStorage(),
        };
        account.Characters.Add(character);
        player.Account = account;
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);

        var action = new SelectCharacterAction(service);
        await action.SelectCharacterAsync(player, "TimedChar").ConfigureAwait(false);

        Assert.That(player.Disconnected, Is.False);
        Assert.That(player.SelectedCharacter, Is.SameAs(character));
    }

    /// <summary>
    /// A character banned by hand stays banned after an unrelated timed ban of it ran out: a timed ban no longer sets the
    /// status, so the status is the hand-set ban.
    /// </summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task HandSetCharacterBanOutlastsAnExpiredTimedBanAsync()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var service = new BanManagerService(backend);
        service.Issue(BanTargetKind.CharacterBan, "HandBanned", "Cooldown", "Tạm khóa", "host", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-5));
        Assert.That(service.HasExpiredCharacterBanOnly("HandBanned", DateTime.UtcNow), Is.False);

        var gameContext = GameContextTestHelper.CreateGameContext([]);
        var templateChar = PlayerTestHelper.CreateCharacter(gameContext);
        var player = new SelectTestPlayer(gameContext);
        var account = new HNMO.Persistence.BasicModel.Account { LoginName = "handacc", State = AccountState.Normal };
        var character = new HNMO.Persistence.BasicModel.Character
        {
            Name = "HandBanned",
            CharacterStatus = CharacterStatus.Banned,
            CharacterClass = templateChar.CharacterClass,
            CurrentMap = templateChar.CurrentMap,
            Inventory = new HNMO.Persistence.BasicModel.ItemStorage(),
        };
        account.Characters.Add(character);
        player.Account = account;
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);

        await new SelectCharacterAction(service).SelectCharacterAsync(player, "HandBanned").ConfigureAwait(false);

        Assert.That(player.Disconnected, Is.True);
        Assert.That(player.SelectedCharacter, Is.Null);
    }

    private static bool HasBanDetails(string json, string expectedEn, string expectedVi)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return root.GetProperty("reasonEn").GetString() == expectedEn
            && root.GetProperty("reasonVi").GetString() == expectedVi
            && (root.GetProperty("sydneyEn").GetString()?.Contains("(Sydney)", StringComparison.Ordinal) ?? false);
    }

    private sealed class SelectTestPlayer(IGameContext gameContext)
        : Player(gameContext)
    {
        public bool Disconnected { get; private set; }

        protected override HNMO.PlugIns.ICustomPlugInContainer<HNMO.GameLogic.Views.IViewPlugIn> CreateViewPlugInContainer()
            => new MockViewPlugInContainer();

        protected override ValueTask InternalDisconnectAsync()
        {
            this.Disconnected = true;
            return base.InternalDisconnectAsync();
        }
    }
}

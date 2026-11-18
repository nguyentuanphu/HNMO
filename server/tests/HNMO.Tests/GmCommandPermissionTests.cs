// <copyright file="GmCommandPermissionTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Chat;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.GmManager;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Verifies per-GM command permission enforcement and auditing across all GM command categories (ROADMAP 14.17 + 14.11).</summary>
[TestFixture]
public sealed class GmCommandPermissionTests
{
    private string _tempDir = null!;
    private string _stateFile = null!;
    private string _activityFile = null!;

    /// <summary>Creates isolated state and activity log paths per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-gmtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
        this._stateFile = Path.Combine(this._tempDir, "gm-state.json");
        this._activityFile = Path.Combine(this._tempDir, "activity.json");
    }

    /// <summary>Deletes temporary test directory.</summary>
    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(this._tempDir))
            {
                Directory.Delete(this._tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore temp lock cleanup error.
        }
    }

    /// <summary>Verifies per-GM permissions are enforced in ChatMessageCommandProcessor and every attempt is audited.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task EnforcesPerGmPermissionsAndAuditsEveryInvocationAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        var plugIns = context.PlugInManager;
        plugIns.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new NoticeChatCommandPlugIn());
        plugIns.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new InspectInventoryChatCommandPlugIn());
        plugIns.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new MuteChatCommandPlugIn());
        plugIns.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new KickChatCommandPlugIn());
        plugIns.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new GoToPlayerChatCommandPlugIn());
        plugIns.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new SummonPlayerChatCommandPlugIn());
        plugIns.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new SpawnMonsterChatCommandPlugIn());
        plugIns.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new StartEventChatCommandPlugIn());

        var gmChar = PlayerTestHelper.CreateCharacter(context);
        gmChar.Name = "PartialGM";
        gmChar.CharacterStatus = CharacterStatus.GameMaster;
        var gm = new GmTestPlayer(context);
        await context.AddPlayerAsync(gm).ConfigureAwait(false);
        await PlayerTestHelper.EnterWorldAsync(gm, gmChar).ConfigureAwait(false);
        gm.Account!.LoginName = "gmacc";

        var gmMessages = new List<string>();
        Mock.Get(gm.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Setup(m => m.ShowMessageAsync(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Callback<string, MessageType>((text, _) => gmMessages.Add(text))
            .Returns(ValueTask.CompletedTask);

        var targetChar = PlayerTestHelper.CreateCharacter(context);
        targetChar.Name = "TargetP1";
        targetChar.Inventory!.Money = 54321;
        var target = new GmTestPlayer(context);
        await context.AddPlayerAsync(target).ConfigureAwait(false);
        await PlayerTestHelper.EnterWorldAsync(target, targetChar).ConfigureAwait(false);
        target.Account!.LoginName = "p1acc";

        var store = new GmPermissionStore(this._stateFile, this._activityFile);
        store.UpsertAssignment(new GmAssignment
        {
            CharacterName = "PartialGM",
            AccountLogin = "gmacc",
            Permissions = GmPermission.Announce | GmPermission.InspectInventory,
            Notes = "Event moderator",
        });

        var processor = new ChatMessageCommandProcessor(store);

        // Allowed: Announce + InspectInventory
        await processor.ProcessMessageAsync(gm, ("/goldnotice Welcome!", gm.Name)).ConfigureAwait(false);
        await processor.ProcessMessageAsync(gm, ("/inspectinv TargetP1", gm.Name)).ConfigureAwait(false);
        Assert.That(gmMessages.Any(m => m.Contains("Zen: 54,321", StringComparison.Ordinal)), Is.True, $"Messages received: {string.Join(" | ", gmMessages)}");

        // Denied: Mute, Kick, GoTo, Summon, Spawn, StartEvent
        await processor.ProcessMessageAsync(gm, ("/mute TargetP1 5", gm.Name)).ConfigureAwait(false);
        await processor.ProcessMessageAsync(gm, ("/kick TargetP1", gm.Name)).ConfigureAwait(false);
        await processor.ProcessMessageAsync(gm, ("/goto TargetP1", gm.Name)).ConfigureAwait(false);
        await processor.ProcessMessageAsync(gm, ("/summon TargetP1", gm.Name)).ConfigureAwait(false);
        await processor.ProcessMessageAsync(gm, ("/spawn 0", gm.Name)).ConfigureAwait(false);
        await processor.ProcessMessageAsync(gm, ("/startevent bc", gm.Name)).ConfigureAwait(false);

        Assert.That(target.Account.ChatBanUntil, Is.Null, "Denied /mute must not mute the target.");
        Assert.That(gmMessages.Count(m => m.Contains("denied", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(6));

        var audits = store.ListAuditEntries(20);
        Assert.That(audits, Has.Count.EqualTo(8));
        Assert.That(audits.Count(a => a.Allowed), Is.EqualTo(2));
        Assert.That(audits.Count(a => !a.Allowed), Is.EqualTo(6));

        var activityEntries = ActivityLogStore.Open(this._activityFile).Query(new ActivityLogQuery { Kind = ActivityKind.Administration, MaxResults = 20 });
        Assert.That(activityEntries, Has.Count.EqualTo(8));
    }

    /// <summary>Verifies strict allowlist denies unlisted GameMasters when enabled.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task StrictMode_DeniesUnlistedGameMasterAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        context.PlugInManager.RegisterPlugInAtPlugInPoint<IChatCommandPlugIn>(new NoticeChatCommandPlugIn());

        var gmChar = PlayerTestHelper.CreateCharacter(context);
        gmChar.Name = "UnlistedGM";
        gmChar.CharacterStatus = CharacterStatus.GameMaster;
        var gm = new GmTestPlayer(context);
        await PlayerTestHelper.EnterWorldAsync(gm, gmChar).ConfigureAwait(false);

        var gmMessages = new List<string>();
        Mock.Get(gm.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Setup(m => m.ShowMessageAsync(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Callback<string, MessageType>((text, _) => gmMessages.Add(text))
            .Returns(ValueTask.CompletedTask);

        var store = new GmPermissionStore(this._stateFile, this._activityFile)
        {
            RequireListedForGmCommands = true,
        };

        var processor = new ChatMessageCommandProcessor(store);
        await processor.ProcessMessageAsync(gm, ("/goldnotice Blocked", gm.Name)).ConfigureAwait(false);

        Assert.That(gmMessages.Any(m => m.Contains("denied", StringComparison.OrdinalIgnoreCase)), Is.True);
        var audits = store.ListAuditEntries(10);
        Assert.That(audits, Has.Count.EqualTo(1));
        Assert.That(audits[0].Allowed, Is.False);
    }

    private sealed class GmTestPlayer(IGameContext gameContext) : Player(gameContext)
    {
        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer()
            => new MockViewPlugInContainer();
    }
}

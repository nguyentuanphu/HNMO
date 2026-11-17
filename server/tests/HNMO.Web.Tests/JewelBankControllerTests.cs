// <copyright file="JewelBankControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Integration tests for <see cref="WebsiteJewelBankController"/>: public rules, website jewel withdrawal,
/// and offline master skill point clearing.
/// </summary>
[TestFixture]
public class JewelBankControllerTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated state directory per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-jb-web-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes temp directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Public GET lists enabled jewels; website withdraw queues pending delivery; clear-master refunds points.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task PublicGetWithdrawAndClearMasterFlow()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var gameConfig = new GameConfiguration();
        var configSource = new Mock<IDataSource<GameConfiguration>>();
        configSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(gameConfig);

        var loginServer = new Mock<ILoginServer>();
        loginServer.Setup(l => l.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte>());
        loginServer.Setup(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);

        var serverProvider = new Mock<IServerProvider>();
        serverProvider.Setup(s => s.Servers).Returns([]);

        var jbCfg = new JewelBankConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            AllowWebsite = true,
            ClearMasterZen = 1000,
        };
        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(JewelBankPlugIn).GUID;
            row.IsActive = true;
            row.SetConfiguration(jbCfg, null);
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        using (var acctCtx = persistence.CreateNewPlayerContext(gameConfig))
        {
            var account = acctCtx.CreateNew<Account>();
            account.LoginName = "hero1";
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123");

            var character = acctCtx.CreateNew<Character>();
            character.Name = "BladeMaster";
            character.MasterLevelUpPoints = 5;
            character.Inventory = acctCtx.CreateNew<ItemStorage>();
            character.Inventory.Money = 5000;

            var mSkill = acctCtx.CreateNew<Skill>();
            mSkill.Number = 335;
            mSkill.MasterDefinition = acctCtx.CreateNew<MasterSkillDefinition>();
            var entry = acctCtx.CreateNew<SkillEntry>();
            entry.Skill = mSkill;
            entry.Level = 10;
            character.LearnedSkills.Add(entry);
            account.Characters.Add(character);
            await acctCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var store = JewelBankStore.Open(jbCfg.FileName);
        var engine = new JewelBankEngine(jbCfg, store, enabled: true);
        engine.Deposit("hero1", "bless", 7, "game", DateTime.UtcNow);

        var controller = new WebsiteJewelBankController(
            persistence,
            configSource.Object,
            loginServer.Object,
            serverProvider.Object,
            NullLogger<WebsiteJewelBankController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var pubResult = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(pubResult.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("jewels").GetArrayLength(), Is.EqualTo(7));
        }

        var wdResult = (ContentResult)await controller.WithdrawAsync(new JewelBankWithdrawRequest("hero1", "pass123", "bless", 3)).ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(wdResult.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("state").GetProperty("event").GetProperty("result").GetString(), Is.EqualTo("Queued"));
            Assert.That(engine.GetBalances("hero1")["bless"], Is.EqualTo(4));
            Assert.That(engine.PeekPending("hero1").Sum(p => p.Quantity), Is.EqualTo(3));
        }

        var cmResult = (ContentResult)await controller.ClearMasterAsync(new JewelBankClearMasterRequest("hero1", "pass123", "BladeMaster")).ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(cmResult.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("state").GetProperty("event").GetProperty("result").GetString(), Is.EqualTo("ClearedMaster"));
            var ch = doc.RootElement.GetProperty("characters")[0];
            Assert.That(ch.GetProperty("masterFreePoints").GetInt32(), Is.EqualTo(15));
            Assert.That(ch.GetProperty("masterSpentPoints").GetInt32(), Is.EqualTo(0));
            Assert.That(ch.GetProperty("zen").GetInt32(), Is.EqualTo(4000));
        }
    }
}

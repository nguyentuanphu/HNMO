// <copyright file="MountChainControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.MountChain;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Integration tests for <see cref="WebsiteMountsController"/>.
/// </summary>
[TestFixture]
public class MountChainControllerTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates temp directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-mc-web-{Guid.NewGuid():N}");
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

    /// <summary>Disabled returns enabled=false; active returns tiers and optional account unlocked list.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task PublicEndpointReturnsTiersAndAccountUnlocked()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var controller = new WebsiteMountsController(persistence)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(MountChainPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var off = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(off.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
        }

        var statePath = Path.Combine(this._tempDir, "hnmo-state.json");
        var cfg = new MountChainConfiguration { FileName = statePath };
        var engine = new MountChainEngine(cfg, MountChainStore.Open(statePath), enabled: true);
        engine.AwardShards("hero", 5);
        engine.CompleteCraft("hero", cfg.Tiers[0], DateTime.UtcNow);

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = (await plugCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false)).First(p => p.TypeId == typeof(MountChainPlugIn).GUID);
            row.SetConfiguration(cfg, null);
            row.IsActive = true;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var onPublic = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(onPublic.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("totalCollectors").GetInt32(), Is.EqualTo(1));
        }

        var onAccount = (ContentResult)await controller.GetAsync("hero").ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(onAccount.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("accountShards").GetInt32(), Is.EqualTo(2));
            Assert.That(doc.RootElement.GetProperty("accountUnlocked")[0].GetString(), Is.EqualTo("horn-uniria"));
        }
    }
}

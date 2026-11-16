// <copyright file="PartyOptionsControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.PartyOptions;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Integration tests for <see cref="WebsitePartySpotsController"/>.
/// </summary>
[TestFixture]
public class PartyOptionsControllerTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates temp directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-po-web-{Guid.NewGuid():N}");
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

    /// <summary>Disabled returns enabled=false; active returns advertised spot listings without exposing passwords.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task PublicEndpointReturnsOpenSpotsWithoutPasswords()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var controller = new WebsitePartySpotsController(persistence)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(PartyOptionsPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var off = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(off.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
        }

        var statePath = Path.Combine(this._tempDir, "hnmo-state.json");
        var cfg = new PartyOptionsConfiguration { FileName = statePath };
        var engine = new PartyOptionsEngine(cfg, PartyOptionsStore.Open(statePath), enabled: true);
        engine.SetAdvertisement(new PartyAdvertisement
        {
            LeaderName = "WizardTop",
            MapName = "Icarus",
            SpotName = "Icarus Blue",
            Password = "hiddenpass",
            MemberCount = 2,
            MaxSize = 5,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = (await plugCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false)).First(p => p.TypeId == typeof(PartyOptionsPlugIn).GUID);
            row.SetConfiguration(cfg, null);
            row.IsActive = true;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var on = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        Assert.That(on.Content, Does.Not.Contain("hiddenpass"));
        using (var doc = JsonDocument.Parse(on.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("ads")[0].GetProperty("spotName").GetString(), Is.EqualTo("Icarus Blue"));
            Assert.That(doc.RootElement.GetProperty("ads")[0].GetProperty("hasPassword").GetBoolean(), Is.True);
        }
    }
}

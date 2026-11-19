// <copyright file="SeasonPlanControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.SeasonPlan;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Integration tests for <see cref="WebsiteSeasonPlanController"/>.
/// </summary>
[TestFixture]
public class SeasonPlanControllerTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates temp directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-sp-web-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Removes temp directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Disabled plug-in returns enabled=false; active returns full timeline &amp; gates.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task PublicTimelineReturnsStagesAndGates()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var controller = new WebsiteSeasonPlanController(persistence)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(SeasonPlanPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var off = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(off.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
        }

        var cfg = new SeasonPlanConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
        };
        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var rows = await plugCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false);
            var row = rows.First(r => r.TypeId == typeof(SeasonPlanPlugIn).GUID);
            row.IsActive = true;
            row.SetConfiguration(cfg, null);
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var on = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(on.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("stages").GetArrayLength(), Is.EqualTo(7));
            Assert.That(doc.RootElement.GetProperty("gates").GetProperty("Wing3").GetBoolean(), Is.False);
        }
    }
}

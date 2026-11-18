// <copyright file="CatchupCapControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.CatchupCap;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Integration tests for <see cref="WebsiteCatchupController"/>.
/// </summary>
[TestFixture]
public class CatchupCapControllerTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates temp folder.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-cu-web-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Removes temp folder.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Returns disabled when off and today's cap when active.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReturnsDisabledThenActiveCapJson()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var controller = new WebsiteCatchupController(persistence)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        // Inactive initially
        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(CatchupCapPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var off = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(off.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
        }

        // Enable with InitialLevelCap=250
        var cuCfg = new CatchupCapConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            InitialLevelCap = 250,
            LevelsPerDay = 20,
            HardMaxLevelCap = 400,
        };
        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var rows = await plugCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false);
            var row = rows.First(r => r.TypeId == typeof(CatchupCapPlugIn).GUID);
            row.IsActive = true;
            row.SetConfiguration(cuCfg, null);
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var on = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(on.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("levelCap").GetProperty("todayCap").GetInt32(), Is.EqualTo(250));
        }
    }
}

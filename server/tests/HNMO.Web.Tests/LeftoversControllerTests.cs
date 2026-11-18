// <copyright file="LeftoversControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.CombiningLeftovers;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Integration tests for <see cref="WebsiteLeftoversController"/>.
/// </summary>
[TestFixture]
public class LeftoversControllerTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates temp directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-lo-web-{Guid.NewGuid():N}");
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

    /// <summary>Disabled plug-in returns enabled=false; active returns recipes and exchange offers.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task PublicEndpointReturnsRecipesAndOffersWhenActive()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var controller = new WebsiteLeftoversController(persistence)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = plugCtx.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(CombiningLeftoversPlugIn).GUID;
            row.IsActive = false;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var off = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(off.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
        }

        using (var plugCtx = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = (await plugCtx.GetAsync<PlugInConfiguration>().ConfigureAwait(false)).First(p => p.TypeId == typeof(CombiningLeftoversPlugIn).GUID);
            var cfg = new CombiningLeftoversConfiguration
            {
                FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
                MaxBoxLevel = 4,
            };
            row.SetConfiguration(cfg, null);
            row.IsActive = true;
            await plugCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var on = (ContentResult)await controller.GetAsync().ConfigureAwait(false);
        using (var doc = JsonDocument.Parse(on.Content!))
        {
            Assert.That(doc.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("effectiveMaxBoxLevel").GetInt32(), Is.EqualTo(4));
            Assert.That(doc.RootElement.GetProperty("recipes").GetArrayLength(), Is.GreaterThanOrEqualTo(4));
            Assert.That(doc.RootElement.GetProperty("offers").GetArrayLength(), Is.GreaterThanOrEqualTo(2));
        }
    }
}

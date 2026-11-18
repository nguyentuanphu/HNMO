// <copyright file="ItemDefinitionsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.ItemDefinitions;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Integration checks for the Item Definitions Admin page, public website override feed, and ZIP export controller.
/// </summary>
[TestFixture]
public class ItemDefinitionsPageTests
{
    private string? _statePath;

    /// <summary>Initializes isolated <see cref="HNMO.GameLogic.PlugIns.HnmoStateStore"/> storage.</summary>
    [SetUp]
    public void SetUp()
    {
        this._statePath = Path.Combine(Path.GetTempPath(), $"hnmo-items-web-{Guid.NewGuid():N}.json");
        ItemDefinitionStateStore.FilePath = this._statePath;
    }

    /// <summary>Removes temporary test state.</summary>
    [TearDown]
    public void TearDown()
    {
        ItemDefinitionStateStore.FilePath = ItemDefinitionStateStore.DefaultFilePath;
        if (this._statePath is not null && File.Exists(this._statePath))
        {
            File.Delete(this._statePath);
        }
    }

    /// <summary>Verifies <see cref="ItemDefinitions"/> declares route <c>/item-definitions</c> and <see cref="WebsiteItemsController"/> exposes stored overrides.</summary>
    [Test]
    public void ExposesRouteAndWebsiteFeed()
    {
        var routeAttr = typeof(ItemDefinitions).GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/item-definitions"));

        ItemDefinitionStateStore.Upsert(new ItemClientMetadata
        {
            Group = 0,
            Number = 50,
            NameEn = "Test Halberd",
            NameVi = "Kích Thử Nghiệm",
            Width = 2,
            Height = 4,
            DropLevel = 75,
            StrengthRequirement = 400,
            GlowColor = "azure",
            TooltipTextEn = "Test note EN",
            TooltipTextVi = "Ghi chú VI",
        });

        var controller = new WebsiteItemsController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
        var result = controller.Get() as OkObjectResult;
        Assert.That(result, Is.Not.Null);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result!.Value));
        Assert.That(doc.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(1));
        var first = doc.RootElement.GetProperty("items")[0];
        Assert.That(first.GetProperty("name").GetProperty("vi").GetString(), Is.EqualTo("Kích Thử Nghiệm"));
        Assert.That(first.GetProperty("glowColor").GetString(), Is.EqualTo("azure"));
        Assert.That(first.GetProperty("tooltip").GetProperty("en").GetString(), Is.EqualTo("Test note EN"));

        var zipBytes = ItemClientDataSync.BuildExportZip(null, ItemDefinitionStateStore.GetAll());
        using var ms = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(zip.GetEntry("Data/Items/hnmo-item-overrides.json"), Is.Not.Null);
    }
}

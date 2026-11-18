// <copyright file="WorldEditorTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.TerrainEditor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Verification tests for ROADMAP 14.24 3D World Editor routes, authorization, asset unwrapping, and dual Server+Web terrain sync.
/// </summary>
[TestFixture]
public class WorldEditorTests
{
    private string _tempRoot = string.Empty;

    /// <summary>Creates an isolated temp workspace before each test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempRoot = Path.Combine(Path.GetTempPath(), "hnmo-world-editor-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._tempRoot);
    }

    /// <summary>Cleans up the temp workspace after each test.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempRoot))
        {
            Directory.Delete(this._tempRoot, recursive: true);
        }
    }

    /// <summary>Verifies <see cref="WorldEditor"/> and <see cref="WorldEditorController"/> expose administrator-protected routes.</summary>
    [Test]
    public void ExposesAuthorizedRoutes()
    {
        var pageRoutes = typeof(WorldEditor)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(r => r.Template)
            .ToList();
        Assert.That(pageRoutes, Does.Contain("/world-editor"));
        Assert.That(pageRoutes, Does.Contain("/world-editor/{SelectedMapId:guid}"));

        var pageAuthPolicies = typeof(WorldEditor)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();
        Assert.That(pageAuthPolicies, Does.Contain(AdminPolicies.Administrator));

        var apiRoute = typeof(WorldEditorController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(apiRoute?.Template, Is.EqualTo("api/world-editor"));
    }

    /// <summary>Verifies shared folder number resolution matches the web game's WorldN folder conventions.</summary>
    [Test]
    public void ResolvesSharedWorldFolderNumbers()
    {
        Assert.That(WorldEditorDataService.GetWorldFolderNumber(0), Is.EqualTo(1));
        Assert.That(WorldEditorDataService.GetWorldFolderNumber(2), Is.EqualTo(3));
        Assert.That(WorldEditorDataService.GetWorldFolderNumber(9), Is.EqualTo(10));
        Assert.That(WorldEditorDataService.GetWorldFolderNumber(11), Is.EqualTo(12));
        Assert.That(WorldEditorDataService.GetWorldFolderNumber(18), Is.EqualTo(19));
        Assert.That(WorldEditorDataService.GetWorldFolderNumber(24), Is.EqualTo(25));
        Assert.That(WorldEditorDataService.GetWorldFolderNumber(45), Is.EqualTo(47));
    }

    /// <summary>Verifies <see cref="WorldEditorDataService.WriteClientAttributes"/> writes valid encrypted EncTerrainN.att files to both web and server directories.</summary>
    [Test]
    public void WriteClientAttributes_SyncsWebAndServerAttRoundTrip()
    {
        var serverDir = Path.Combine(this._tempRoot, "serverData");
        var webDir = Path.Combine(this._tempRoot, "webData");
        var service = new WorldEditorDataService(serverDir, webDir);

        var attrs = new byte[TerrainFileConverter.TileCount];
        attrs[100] = 0x01; // Safezone
        attrs[200] = 0x04; // Blocked
        attrs[300] = 0x10; // Water

        var written = service.WriteClientAttributes(0, attrs);
        Assert.That(written, Has.Count.EqualTo(2));

        var webAtt = Path.Combine(webDir, "World1", "EncTerrain1.att");
        var serverAtt = Path.Combine(serverDir, "World1", "EncTerrain1.att");
        Assert.That(File.Exists(webAtt), Is.True);
        Assert.That(File.Exists(serverAtt), Is.True);

        var ok = TerrainFileConverter.TryReadFile(File.ReadAllBytes(webAtt), out var decoded);
        Assert.That(ok, Is.True);
        Assert.That(decoded![100], Is.EqualTo(0x01));
        Assert.That(decoded[200], Is.EqualTo(0x04));
        Assert.That(decoded[300], Is.EqualTo(0x10));
    }

    /// <summary>Verifies asset reading unwraps OZJ/OZT images and rejects path traversal attempts.</summary>
    /// <returns>Asynchronous task.</returns>
    [Test]
    public async Task TryReadDataBytesAsync_UnwrapsOzjAndBlocksTraversalAsync()
    {
        var serverDir = Path.Combine(this._tempRoot, "serverData");
        var webDir = Path.Combine(this._tempRoot, "webData");
        Directory.CreateDirectory(Path.Combine(serverDir, "World1"));

        // 24-byte OZJ prefix followed by synthetic JPEG marker bytes
        var ozjBytes = new byte[28];
        ozjBytes[24] = 0xFF;
        ozjBytes[25] = 0xD8;
        ozjBytes[26] = 0xFF;
        ozjBytes[27] = 0xE0;
        await File.WriteAllBytesAsync(Path.Combine(serverDir, "World1", "TileGrass01.ozj"), ozjBytes);

        var service = new WorldEditorDataService(serverDir, webDir);
        var loaded = await service.TryReadDataBytesAsync("World1/TileGrass01.jpg", CancellationToken.None);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Value.ContentType, Is.EqualTo("image/jpeg"));
        Assert.That(loaded.Value.Bytes, Is.EqualTo(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));

        var traversal = await service.TryReadDataBytesAsync("../secret.txt", CancellationToken.None);
        Assert.That(traversal, Is.Null);
    }
}

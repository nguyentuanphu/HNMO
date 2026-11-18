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

    /// <summary>Verifies <see cref="WorldEditorDataService.WriteClientObjects"/>, <see cref="WorldEditorDataService.WriteClientMinimap"/>, and <see cref="WorldEditorDataService.RebuildWorldPack"/> produce valid client binaries and HPK1 packs.</summary>
    /// <returns>Asynchronous task.</returns>
    [Test]
    public async Task PartB_ObjectsMinimapAndHpkRebuildRoundTripAsync()
    {
        var serverDir = Path.Combine(this._tempRoot, "serverData");
        var webDir = Path.Combine(this._tempRoot, "webData");
        var service = new WorldEditorDataService(serverDir, webDir);

        // 1. Write EncTerrain1.obj (header 4 bytes + 1 record 30 bytes = 34 bytes)
        var rawObj = new byte[34];
        rawObj[1] = 0; // map 0
        rawObj[2] = 1; // 1 object
        var objWritten = service.WriteClientObjects(0, rawObj);
        Assert.That(objWritten, Has.Count.EqualTo(2));
        Assert.That(File.Exists(Path.Combine(webDir, "World1", "EncTerrain1.obj")), Is.True);

        // 2. Write mini_map.tga + .ozt (4x4 RGBA -> 18 + 64 = 82-byte TGA)
        var rgba = Enumerable.Repeat((byte)0x80, 4 * 4 * 4).ToArray();
        var mapWritten = service.WriteClientMinimap(0, rgba, 4, 4);
        Assert.That(mapWritten, Has.Count.EqualTo(2));
        var tgaBytes = File.ReadAllBytes(Path.Combine(webDir, "World1", "mini_map.tga"));
        Assert.That(tgaBytes.Length, Is.EqualTo(18 + 64));
        Assert.That(tgaBytes[2], Is.EqualTo(2)); // uncompressed TGA
        Assert.That(tgaBytes[16], Is.EqualTo(32)); // 32-bit BGRA

        // 3. Rebuild world1.hpk and read an entry back via TryReadDataBytesAsync
        var (packPath, entryCount) = service.RebuildWorldPack(0);
        Assert.That(File.Exists(packPath), Is.True);
        Assert.That(entryCount, Is.GreaterThanOrEqualTo(2));

        // Verify HPK1 pack can serve packed entry even if loose dir doesn't contain it
        var packOnlyDir = Path.Combine(this._tempRoot, "packOnlyServer");
        Directory.CreateDirectory(Path.Combine(packOnlyDir, "packs"));
        File.Copy(packPath, Path.Combine(packOnlyDir, "packs", "world1.hpk"));
        var packReader = new WorldEditorDataService(packOnlyDir, Path.Combine(this._tempRoot, "emptyWeb"));
        var readFromPack = await packReader.TryReadDataBytesAsync("World1/EncTerrain1.obj", CancellationToken.None);
        Assert.That(readFromPack, Is.Not.Null);
        Assert.That(readFromPack!.Value.Bytes.Length, Is.EqualTo(34));
    }
}

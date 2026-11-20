// <copyright file="ControlServiceTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.Interfaces;

/// <summary>
/// Unit tests for <see cref="ControlOperations"/>.
/// </summary>
[TestFixture]
public class ControlServiceTests
{
    private string _tempDir = string.Empty;

    /// <summary>
    /// Creates a temporary save directory for each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-control-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>
    /// Cleans up the temporary save directory after each test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies that backups include save files + SQL dump, exclude logs/backups/database,
    /// use Sydney time in the filename, and restore cleanly.
    /// </summary>
    [Test]
    public void BackupAndRestoreRoundTrip()
    {
        var saveDir = Path.Combine(this._tempDir, "save");
        Directory.CreateDirectory(Path.Combine(saveDir, "logs"));
        Directory.CreateDirectory(Path.Combine(saveDir, "database"));
        File.WriteAllText(Path.Combine(saveDir, "hnmo-settings.json"), """{"WebPort":8080}""");
        File.WriteAllText(Path.Combine(saveDir, "logs", "server.log"), "ignored-log");
        File.WriteAllText(Path.Combine(saveDir, "database", "PG_VERSION"), "16");

        var sqlDump = Path.Combine(this._tempDir, "dump.sql");
        File.WriteAllText(sqlDump, "-- sql dump");

        // 2026-11-16T00:00:00Z = 2026-11-16 11:00:00 AEDT (Sydney)
        var utc = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        var backup = ControlOperations.CreateBackupZip(saveDir, sqlDump, utc);

        Assert.That(backup.FileName, Is.EqualTo("hnmo-backup-20261116-110000.zip"));
        Assert.That(backup.CreatedSydney, Is.EqualTo("2026-11-16 11:00:00"));

        var list = ControlOperations.ListBackups(saveDir);
        Assert.That(list, Has.Count.EqualTo(1));

        File.WriteAllText(Path.Combine(saveDir, "hnmo-settings.json"), """{"WebPort":9999}""");
        var restoredSql = ControlOperations.RestoreBackupZip(saveDir, backup.FileName);

        Assert.That(File.ReadAllText(Path.Combine(saveDir, "hnmo-settings.json")), Does.Contain("8080"));
        Assert.That(restoredSql, Is.Not.Null);
        Assert.That(File.ReadAllText(restoredSql!), Is.EqualTo("-- sql dump"));
    }

    /// <summary>
    /// Verifies that the hub's release JSON is parsed with Sydney time and compared against local build stamp, and that
    /// no link is made up when the release has none.
    /// </summary>
    [Test]
    public void ParseReleaseDetectsNewerVersion()
    {
        var json = """
            {
              "tag_name": "hnmo-20261120",
              "published_at": "2026-11-20T01:00:00Z",
              "body": "Bug fixes",
              "html_url": "https://downloads.example/HNMO.zip"
            }
            """;

        var result = ControlOperations.ParseRelease(json, "HNMO-001-20261116");
        Assert.That(result.LatestTag, Is.EqualTo("hnmo-20261120"));
        Assert.That(result.IsUpdateAvailable, Is.True);
        Assert.That(result.PublishedAtSydney, Is.EqualTo("2026-11-20 12:00:00"));
        Assert.That(result.DownloadUrl, Is.EqualTo("https://downloads.example/HNMO.zip"));
        Assert.That(ControlOperations.ParseRelease("""{"tag_name":"v1"}""", null).DownloadUrl, Is.Empty, "no link of its own");
    }
}

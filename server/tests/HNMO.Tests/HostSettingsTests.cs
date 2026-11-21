// <copyright file="HostSettingsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.Interfaces;

/// <summary>
/// Unit tests for <see cref="HostSettingsStore"/> and <see cref="HostSettingsValidator"/>.
/// </summary>
[TestFixture]
public class HostSettingsTests
{
    private string _tempDir = string.Empty;

    /// <summary>
    /// Creates a temporary directory for each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-host-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>
    /// Deletes the temporary directory after each test.
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
    /// Verifies that migrating from legacy <c>appsettings.json</c> and <c>ConnectionSettings.xml</c>
    /// preserves existing values and writes <c>hnmo-settings.json</c>.
    /// </summary>
    [Test]
    public void LoadOrMigrateReadsLegacyFilesAndPersistsJson()
    {
        var appSettingsPath = Path.Combine(this._tempDir, "appsettings.json");
        File.WriteAllText(appSettingsPath, """
            {
              "Urls": "http://0.0.0.0:9090",
              "HNMO": { "WebRoot": "www", "DataPath": "../GameData", "VendorPath": "libs" },
              "Relay": { "AllowedHosts": [ "10.0.0.5" ], "AllowLoopback": false, "ConnectTimeoutSeconds": 15 },
              "Launcher": {
                "Enabled": true,
                "DatabasePort": 55444,
                "DatabasePassword": "secret-legacy-password",
                "AdminUrl": "http://127.0.0.1:8195",
                "ConnectServerPort": 44416,
                "OpenBrowserUrl": "https://play.example.com/"
              }
            }
            """);

        var xmlPath = Path.Combine(this._tempDir, "ConnectionSettings.xml");
        File.WriteAllText(xmlPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <ConnectionSettings xmlns="https://github.com/nguyentuanphu/HNMO/ConnectionSettings">
              <Connections>
                <ConnectionSetting>
                  <ContextTypeName>HNMO.Persistence.EntityFramework.EntityContext</ContextTypeName>
                  <ConnectionString>Server=127.0.0.1;Port=55444;Database=hnmo_custom;User Id=hnmo_user;Password=xml-pw;</ConnectionString>
                </ConnectionSetting>
              </Connections>
            </ConnectionSettings>
            """);

        var targetPath = Path.Combine(this._tempDir, "save", "hnmo-settings.json");
        var loaded = HostSettingsStore.LoadOrMigrate(targetPath, appSettingsPath, xmlPath);

        Assert.That(File.Exists(targetPath), Is.True);
        Assert.That(loaded.WebPort, Is.EqualTo(9090));
        Assert.That(loaded.AdminPort, Is.EqualTo(8195));
        Assert.That(loaded.ConnectServerPort, Is.EqualTo(44416));
        Assert.That(loaded.DatabasePort, Is.EqualTo(55444));
        Assert.That(loaded.PublicAddress, Is.EqualTo("play.example.com"));
        Assert.That(loaded.WebRoot, Is.EqualTo("www"));
        Assert.That(loaded.DataPath, Is.EqualTo("../GameData"));
        Assert.That(loaded.VendorPath, Is.EqualTo("libs"));
        Assert.That(loaded.RelayAllowLoopback, Is.False);
        Assert.That(loaded.RelayConnectTimeoutSeconds, Is.EqualTo(15));
        Assert.That(loaded.LauncherEnabled, Is.True);
        Assert.That(loaded.DatabaseName, Is.EqualTo("hnmo_custom"));
        Assert.That(loaded.DatabaseUser, Is.EqualTo("hnmo_user"));
        Assert.That(loaded.DatabasePassword, Is.EqualTo("xml-pw"));
    }

    /// <summary>
    /// Verifies that validation catches out-of-range ports, duplicate ports, and invalid URLs.
    /// </summary>
    [Test]
    public void ValidatorDetectsInvalidPortsAndUrls()
    {
        var settings = new HostSettings
        {
            WebPort = 8080,
            AdminPort = 8080,
            ConnectServerPort = 70000,
            DatabasePort = 0,
            OpenBrowserUrl = "not-a-url",
            RelayConnectTimeoutSeconds = 0,
        };

        var errors = HostSettingsValidator.Validate(settings);
        Assert.That(errors.Any(e => e.Contains("8080", StringComparison.Ordinal)), Is.True);
        Assert.That(errors.Any(e => e.Contains("ConnectServerPort", StringComparison.Ordinal)), Is.True);
        Assert.That(errors.Any(e => e.Contains("DatabasePort", StringComparison.Ordinal)), Is.True);
        Assert.That(errors.Any(e => e.Contains("OpenBrowserUrl", StringComparison.Ordinal)), Is.True);
        Assert.That(errors.Any(e => e.Contains("RelayConnectTimeoutSeconds", StringComparison.Ordinal)), Is.True);
    }

    /// <summary>
    /// Verifies that default settings pass validation.
    /// </summary>
    [Test]
    public void DefaultSettingsAreValid()
    {
        var errors = HostSettingsValidator.Validate(new HostSettings());
        Assert.That(errors, Is.Empty);
    }

    /// <summary>
    /// Folder paths must stay inside the HNMO folder and the server program must be a bare name: the admin panel
    /// can be reached from the website, and these paths decide where files are written and what is started.
    /// </summary>
    [Test]
    public void PathsOutsideTheHnmoFolderAreRefused()
    {
        Assert.That(HostSettingsValidator.IsInsideInstallFolder("../save"), Is.True);
        Assert.That(HostSettingsValidator.IsInsideInstallFolder("license.hnmo"), Is.True);
        Assert.That(HostSettingsValidator.IsInsideInstallFolder("../postgres/bin"), Is.True);
        Assert.That(HostSettingsValidator.IsInsideInstallFolder("../../outside"), Is.False);
        Assert.That(HostSettingsValidator.IsInsideInstallFolder("a/../../../etc"), Is.False);
        Assert.That(HostSettingsValidator.IsInsideInstallFolder("/etc/cron.d"), Is.False);
        Assert.That(HostSettingsValidator.IsInsideInstallFolder("C:\\Windows"), Is.False);
        Assert.That(HostSettingsValidator.IsInsideInstallFolder(string.Empty), Is.False);

        var settings = new HostSettings { PostgresPath = "/usr/bin", ServerProgram = "../evil", LicenseFilePath = "../../x.hnmo" };
        var errors = HostSettingsValidator.Validate(settings, checkPortAvailability: false);
        Assert.That(errors, Has.Some.Contains(nameof(HostSettings.PostgresPath)));
        Assert.That(errors, Has.Some.Contains(nameof(HostSettings.ServerProgram)));
        Assert.That(errors, Has.Some.Contains(nameof(HostSettings.LicenseFilePath)));
    }
}

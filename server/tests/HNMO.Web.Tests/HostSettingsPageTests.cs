// <copyright file="HostSettingsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using NUnit.Framework;

/// <summary>
/// bUnit tests for <see cref="HostSettingsPage"/>.
/// </summary>
[TestFixture]
public class HostSettingsPageTests
{
    /// <summary>
    /// Verifies that the page renders settings, hides the database password, toggles help,
    /// validates and saves changes, and triggers a restart when confirmed.
    /// </summary>
    [Test]
    public void RendersSavesAndRestarts()
    {
        using var ctx = new BunitContext();
        var fakeService = new FakeHostSettingsService();
        var fakeRestart = new FakeRestartService();
        ctx.Services.AddSingleton<IHostSettingsService>(fakeService);
        ctx.Services.AddSingleton<IServerRestartService>(fakeRestart);
        ctx.Services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(HostRequest(forwarded: false));

        var cut = ctx.Render<HostSettingsPage>();

        // Password is never shown in full in the rendered markup
        Assert.That(cut.Markup, Does.Not.Contain("super-secret-db-pw"));
        Assert.That(cut.Find("#db-password-masked").GetAttribute("value"), Is.EqualTo("••••••••"));

        // Help box toggles on ? click
        Assert.That(cut.FindAll("#host-settings-help"), Is.Empty);
        cut.Find("#host-settings-help-btn").Click();
        Assert.That(cut.FindAll("#host-settings-help"), Has.Count.EqualTo(1));

        // Update WebPort and save
        cut.Find("#web-port").Change("8088");
        cut.Find("#save-host-settings-btn").Click();
        Assert.That(fakeService.SavedSettings?.WebPort, Is.EqualTo(8088));
        Assert.That(cut.FindAll("#host-settings-status"), Has.Count.EqualTo(1));

        // Restart HNMO requires confirmation then invokes IServerRestartService
        cut.Find("#restart-hnmo-btn").Click();
        cut.Find("#confirm-restart-hnmo-btn").Click();
        Assert.That(fakeRestart.Requested, Is.True);
    }

    /// <summary>
    /// Through the website's admin path (a forwarded request), the page shows no settings and saves nothing: only
    /// the host computer changes ports, folders and the database password (audit 2.5). The licence card is there:
    /// an admin may paste or ask for a licence from afar (owner, 2026-11-21).
    /// </summary>
    [Test]
    public void ThroughTheWebsiteNothingIsShownOrSaved()
    {
        using var ctx = new BunitContext();
        var fakeService = new FakeHostSettingsService();
        ctx.Services.AddSingleton<IHostSettingsService>(fakeService);
        ctx.Services.AddSingleton<IServerRestartService>(new FakeRestartService());
        ctx.Services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(HostRequest(forwarded: true));

        var cut = ctx.Render<HostSettingsPage>();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("#host-settings-host-only"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("#save-host-settings-btn"), Is.Empty);
            Assert.That(cut.FindAll("#web-port"), Is.Empty);
            Assert.That(cut.FindAll("#db-password-new"), Is.Empty);
            Assert.That(cut.FindAll("#restart-hnmo-btn"), Is.Empty);
            Assert.That(cut.FindAll("#license-card"), Has.Count.EqualTo(1), "the licence from afar");
            Assert.That(fakeService.SavedSettings, Is.Null);
        });
    }

    /// <summary>
    /// A pasted license is checked before it is saved: a bad one is refused and nothing is written; a good one is saved
    /// and used at once, and the page shows whom it is for and how many players it allows. The same on the host computer
    /// and through the website's admin path (owner, 2026-11-21).
    /// </summary>
    /// <param name="forwarded">Whether the page is opened through the website.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void ALicenseIsCheckedBeforeItIsSavedAndUsedAtOnce(bool forwarded)
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var fakeService = new FakeHostSettingsService();
        ctx.Services.AddSingleton<IHostSettingsService>(fakeService);
        ctx.Services.AddSingleton<IServerRestartService>(new FakeRestartService());
        ctx.Services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(HostRequest(forwarded));
        var cut = ctx.Render<HostSettingsPage>();
        Assert.That(cut.Find("#machine-id").TextContent, Is.EqualTo("TEST-MACHINE"));

        cut.Find("#license-text").Change("not a license");
        cut.Find("#license-paste-save").Click();
        Assert.That(fakeService.SavedLicense, Is.Null, "a bad license is not written");
        Assert.That(cut.Markup, Does.Contain("broken"));

        cut.Find("#license-text").Change("GOOD");
        cut.Find("#license-paste-save").Click();
        Assert.That(fakeService.SavedLicense, Is.EqualTo("GOOD"u8.ToArray()));
        Assert.That(fakeService.Applied, Is.True);
        Assert.That(cut.Find("#license-status").TextContent, Does.Contain("Friends server").And.Contain("50"));
    }

    /// <summary>The host computer's own request, or the same one forwarded by the website's admin path.</summary>
    private static Microsoft.AspNetCore.Http.HttpContextAccessor HostRequest(bool forwarded)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost", 8095);
        if (forwarded)
        {
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        }

        return new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = context };
    }

    private sealed class FakeHostSettingsService : IHostSettingsService
    {
        private readonly HostSettings _current = new() { DatabasePassword = "super-secret-db-pw" };

        public string SettingsFilePath => "save/hnmo-settings.json";

        public HostSettings? SavedSettings { get; private set; }

        public HostSettings Load() => this._current;

        public IReadOnlyList<string> Validate(HostSettings settings, IReadOnlySet<int>? currentlyBoundPorts = null) =>
            HostSettingsValidator.Validate(settings, checkPortAvailability: false, currentlyBoundPorts: currentlyBoundPorts);

        public Task SaveAsync(HostSettings settings, string? newDatabasePassword, CancellationToken cancellationToken = default)
        {
            this.SavedSettings = settings;
            return Task.CompletedTask;
        }

        public byte[]? SavedLicense { get; private set; }

        public bool Applied { get; private set; }

        public LicenseStatusInfo GetLicenseStatus(string? configuredPath) => this.Applied
            ? new(50, "Licensed.", "TEST-MACHINE", "license.hnmo", true, true, "Friends server")
            : new(10, "Default license", "TEST-MACHINE", "license.hnmo", false);

        public LicenseStatusInfo CheckLicense(byte[] content) => System.Text.Encoding.UTF8.GetString(content) == "GOOD"
            ? new(50, "Licensed.", "TEST-MACHINE", string.Empty, true, true, "Friends server")
            : new(10, "The license file is broken.", "TEST-MACHINE", string.Empty, true);

        public LicenseStatusInfo ApplyLicense(string? configuredPath)
        {
            this.Applied = true;
            return this.GetLicenseStatus(configuredPath);
        }

        public Task<string> SaveLicenseFileAsync(string? configuredPath, byte[] content, CancellationToken cancellationToken = default)
        {
            this.SavedLicense = content;
            return Task.FromResult("license.hnmo");
        }
    }

    private sealed class FakeRestartService : IServerRestartService
    {
        public bool IsSupported => true;

        public bool Requested { get; private set; }

        public Task RequestRestartAsync(CancellationToken cancellationToken = default)
        {
            this.Requested = true;
            return Task.CompletedTask;
        }
    }
}

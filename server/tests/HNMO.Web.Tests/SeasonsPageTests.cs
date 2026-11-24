// <copyright file="SeasonsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

/// <summary>
/// bUnit tests for <see cref="SeasonsPage"/> across all four UI languages and host-computer security checks.
/// </summary>
[TestFixture]
public class SeasonsPageTests
{
    /// <summary>
    /// Verifies Season 6.3, Season 9, and Arena appear on one page, toggles save cleanly,
    /// restart confirmation works, and forwarded website requests stay read-only.
    /// </summary>
    [Test]
    public void RendersAllWorldsSavesTogglesAndRespectsHostBoundary()
    {
        using var ctx = new BunitContext();
        var fakeService = new FakeHostSettingsService();
        var fakeRestart = new FakeRestartService();
        ctx.Services.AddSingleton<IHostSettingsService>(fakeService);
        ctx.Services.AddSingleton<IServerRestartService>(fakeRestart);
        ctx.Services.AddSingleton<IHttpContextAccessor>(HostRequest(forwarded: false));
        ctx.Services.AddSingleton<NavigationHistory>();

        var cut = ctx.Render<SeasonsPage>();
        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("#season-card-s6"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("#season-card-s9"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("#season-card-arena"), Has.Count.EqualTo(1));
            Assert.That(cut.Find("#season-s6-status").TextContent, Does.Contain("Primary"));
            Assert.That(cut.Find("#season-s9-status").TextContent, Does.Contain("Disabled"));
            Assert.That(cut.Find("#season-arena-status").TextContent, Does.Contain("Disabled"));
        });

        cut.Find("#season-nine-enabled").Change(true);
        cut.Find("#arena-enabled").Change(true);
        cut.Find("#save-seasons-btn").Click();

        Assert.Multiple(() =>
        {
            Assert.That(fakeService.SavedSettings?.SeasonNineEnabled, Is.True);
            Assert.That(fakeService.SavedSettings?.ArenaEnabled, Is.True);
            Assert.That(cut.FindAll("#seasons-status"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("#seasons-restart-banner"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("#season-s9-open"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("#season-arena-open"), Has.Count.EqualTo(1));
        });

        cut.Find("#restart-seasons-btn").Click();
        cut.Find("#confirm-restart-seasons-btn").Click();
        Assert.That(fakeRestart.Requested, Is.True);

        // Forwarded website requests cannot save or trigger restart.
        using var forwardedCtx = new BunitContext();
        var forwardedSettings = new FakeHostSettingsService();
        forwardedCtx.Services.AddSingleton<IHostSettingsService>(forwardedSettings);
        forwardedCtx.Services.AddSingleton<IServerRestartService>(new FakeRestartService());
        forwardedCtx.Services.AddSingleton<IHttpContextAccessor>(HostRequest(forwarded: true));
        forwardedCtx.Services.AddSingleton<NavigationHistory>();

        var roCut = forwardedCtx.Render<SeasonsPage>();
        Assert.Multiple(() =>
        {
            Assert.That(roCut.FindAll("#seasons-host-only"), Has.Count.EqualTo(1));
            Assert.That(roCut.FindAll("#save-seasons-btn"), Is.Empty);
            Assert.That(roCut.FindAll("#restart-seasons-btn"), Is.Empty);
            Assert.That(roCut.Find("#season-nine-enabled").HasAttribute("disabled"), Is.True);
            Assert.That(roCut.Find("#arena-enabled").HasAttribute("disabled"), Is.True);
        });
    }

    /// <summary>
    /// Verifies that every visible string on <see cref="SeasonsPage"/> is localized in EN, VI, DE, and zh-CN.
    /// </summary>
    /// <param name="cultureName">Target UI culture.</param>
    /// <param name="expectedHeading">Expected page heading in that culture.</param>
    /// <param name="expectedPrimary">Expected primary badge in that culture.</param>
    [TestCase("en", "Seasons & Worlds", "Primary")]
    [TestCase("vi", "Các Season & Thế giới", "Chính")]
    [TestCase("de", "Seasons & Welten", "Primär")]
    [TestCase("zh-CN", "赛季与大区设置", "主大区")]
    public void RendersLocalizedTextsInFourLanguages(string cultureName, string expectedHeading, string expectedPrimary)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            using var ctx = new BunitContext();
            ctx.Services.AddSingleton<IHostSettingsService>(new FakeHostSettingsService());
            ctx.Services.AddSingleton<IServerRestartService>(new FakeRestartService());
            ctx.Services.AddSingleton<IHttpContextAccessor>(HostRequest(forwarded: false));
            ctx.Services.AddSingleton<NavigationHistory>();

            var cut = ctx.Render<SeasonsPage>();
            Assert.Multiple(() =>
            {
                Assert.That(cut.Find("h1").TextContent, Is.EqualTo(expectedHeading));
                Assert.That(cut.Find("#season-s6-status").TextContent, Does.Contain(expectedPrimary));
                if (cultureName != "en")
                {
                    Assert.That(cut.Markup, Does.Not.Contain("Enable or disable Season 9 and the Arena"));
                    Assert.That(cut.Markup, Does.Not.Contain("Save season settings"));
                }
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static HttpContextAccessor HostRequest(bool forwarded)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        context.Request.Host = new HostString("localhost", 8095);
        if (forwarded)
        {
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        }

        return new HttpContextAccessor { HttpContext = context };
    }

    private sealed class FakeHostSettingsService : IHostSettingsService
    {
        private readonly HostSettings _current = new() { DatabasePassword = "pw" };

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

        public LicenseStatusInfo GetLicenseStatus(string? configuredPath) => new(10, "Default license", "TEST-MACHINE", "license.hnmo", false);

        public Task<string> SaveLicenseFileAsync(string? configuredPath, byte[] content, CancellationToken cancellationToken = default) => Task.FromResult("license.hnmo");
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

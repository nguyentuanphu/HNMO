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

        public LicenseStatusInfo GetLicenseStatus(string? configuredPath) =>
            new(10, "Default license", "TEST-MACHINE", "license.hnmo", false);

        public Task<string> SaveLicenseFileAsync(string? configuredPath, byte[] content, CancellationToken cancellationToken = default) =>
            Task.FromResult("license.hnmo");
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

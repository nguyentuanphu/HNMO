// <copyright file="DbLogRetentionPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System;
using System.Globalization;
using System.IO;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using HNMO.Interfaces;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// bUnit tests for <see cref="DbLogRetention"/> (<c>/db-log-retention</c>).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class DbLogRetentionPageTests
{
    private BunitContext _context = null!;
    private CultureInfo _prevCulture = null!;
    private string _saveDir = null!;

    /// <summary>Sets up bUnit context and isolated save folder.</summary>
    [SetUp]
    public void SetUp()
    {
        this._prevCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._saveDir = Path.Combine(Path.GetTempPath(), $"hnmo-dbr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._saveDir);
        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Disposes bUnit context and temp directory.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._prevCulture;
        if (Directory.Exists(this._saveDir))
        {
            Directory.Delete(this._saveDir, recursive: true);
        }
    }

    /// <summary>Edits and persists retention settings into <c>host-settings.json</c>.</summary>
    [Test]
    public void SavesRetentionSettingsAndTogglesHelp()
    {
        var cut = this._context.Render<DbLogRetention>(p => p.Add(x => x.SaveDirectory, this._saveDir));
        cut.Find("#dbr-db-days").Change("45");
        cut.Find("#dbr-log-days").Change("21");
        cut.Find("#dbr-log-mb").Change("500");
        cut.Find("#dbr-save").Click();

        var loaded = MaintenanceSettings.Load(this._saveDir);
        Assert.That(loaded.DbLogRetentionDays, Is.EqualTo(45));
        Assert.That(loaded.MaxLogDays, Is.EqualTo(21));
        Assert.That(loaded.MaxLogTotalMb, Is.EqualTo(500));

        cut.Find("button.btn-outline-info").Click();
        Assert.That(cut.FindAll("#db-retention-help").Count, Is.EqualTo(1));
    }

    /// <summary>Verifies English and Vietnamese labels render properly.</summary>
    /// <param name="culture">Culture code.</param>
    /// <param name="expectedHeading">Expected heading text.</param>
    [TestCase("en", "Database log retention & export")]
    [TestCase("vi", "Lưu trữ & Xuất nhật ký CSDL")]
    public void RendersEnglishAndVietnamese(string culture, string expectedHeading)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var cut = this._context.Render<DbLogRetention>(p => p.Add(x => x.SaveDirectory, this._saveDir));
        Assert.That(cut.Find("h1").TextContent, Does.Contain(expectedHeading));
    }
}

// <copyright file="ApiKeysPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using Bunit;
using HNMO.Persistence.AdminAuth;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Tests.NetworkAnalyzer;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>
/// Tests the API keys page's localized help and action hints.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ApiKeysPageTests
{
    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;

    /// <summary>
    /// Sets up a host with an enabled key and a disabled key.
    /// </summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        var users = new InMemoryAdminUserRepository();
        await users.AddAsync(AdminPanelSetupModeTests.CreateUser("admin")).ConfigureAwait(false);
        var keys = new InMemoryApiKeyRepository();
        await keys.AddAsync(new ApiKey { Id = Guid.NewGuid(), Name = "Website", KeyPrefix = "enabled", Roles = "Viewer" }).ConfigureAwait(false);
        await keys.AddAsync(new ApiKey { Id = Guid.NewGuid(), Name = "Bot", KeyPrefix = "disabled", Roles = "Viewer", IsDisabled = true }).ConfigureAwait(false);
        var setupMode = AdminPanelSetupModeTests.CreateSetupMode(users, keys);
        var toasts = Mock.Of<IToastService>();
        var service = new ApiKeyManagementService(keys, new TestModalService(), toasts, setupMode);

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton(service);
        this._context.Services.AddSingleton(setupMode);
        this._context.Services.AddSingleton(toasts);
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>
    /// Disposes the page and restores the test culture.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
    }

    /// <summary>
    /// Help is initially hidden and can be opened and closed in either language.
    /// </summary>
    /// <param name="culture">The selected language.</param>
    /// <param name="caption">The expected localized help button caption.</param>
    [TestCase("en", "How to use this page")]
    [TestCase("vi", "Hướng dẫn sử dụng trang này")]
    public void HelpOpensAndCloses(string culture, string caption)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var cut = this._context.Render<ApiKeys>();
        var button = cut.Find("#api-keys-help-toggle");
        Assert.Multiple(() =>
        {
            Assert.That(button.TextContent.Trim(), Is.EqualTo(caption));
            Assert.That(button.GetAttribute("aria-expanded"), Is.EqualTo("false"));
            Assert.That(cut.FindAll("#api-keys-help"), Is.Empty);
        });

        button.Click();
        var help = cut.Find("#api-keys-help.alert.alert-info");
        Assert.Multiple(() =>
        {
            Assert.That(button.GetAttribute("aria-expanded"), Is.EqualTo("true"));
            Assert.That(help.TextContent, Does.Contain(Resources.ApiKeysHelpWhat));
            Assert.That(help.TextContent, Does.Contain(Resources.ApiKeysHelpWhen));
            Assert.That(help.TextContent, Does.Contain(Resources.ApiKeysHelpCreate));
            Assert.That(help.TextContent, Does.Contain(Resources.ApiKeysHelpButtons));
            Assert.That(help.TextContent, Does.Contain(Resources.ApiKeysHelpSafety));
        });

        button.Click();
        Assert.That(cut.FindAll("#api-keys-help"), Is.Empty);
    }

    /// <summary>
    /// Every action has a localized hint, including copy and close after a key is created.
    /// </summary>
    /// <param name="culture">The selected language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    public void AllActionsHaveHints(string culture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var cut = this._context.Render<ApiKeys>();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("tbody tr"), Has.Count.EqualTo(2)));
        var create = cut.Find($"button[title='{Resources.ApiKeysHintCreate}']");
        create.Click();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll(".alert-warning input[readonly]"), Has.Count.EqualTo(1)));
        var hints = cut.FindAll("button").Select(button => button.GetAttribute("title")).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(hints.All(hint => !string.IsNullOrWhiteSpace(hint)), Is.True);
            Assert.That(hints, Does.Contain(Resources.ApiKeysHintHelp));
            Assert.That(hints, Does.Contain(Resources.ApiKeysHintCreate));
            Assert.That(hints, Does.Contain(Resources.ApiKeysHintChange));
            Assert.That(hints, Does.Contain(Resources.ApiKeysHintEnable));
            Assert.That(hints, Does.Contain(Resources.ApiKeysHintDisable));
            Assert.That(hints, Does.Contain(Resources.ApiKeysHintDelete));
            Assert.That(hints, Does.Contain(Resources.ApiKeysHintCopy));
            Assert.That(hints, Does.Contain(Resources.ApiKeysHintClose));
        });

        cut.Find($"button[title='{Resources.ApiKeysHintClose}']").Click();
        Assert.That(cut.FindAll(".alert-warning input[readonly]"), Is.Empty);
    }
}

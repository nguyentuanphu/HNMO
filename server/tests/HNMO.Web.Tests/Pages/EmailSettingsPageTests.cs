// <copyright file="EmailSettingsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using Bunit;
using HNMO.GameLogic.PlugIns;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks the mail settings form, localized instructions and administrator route boundary.</summary>
[TestFixture]
[NonParallelizable]
public class EmailSettingsPageTests
{
    /// <summary>Both languages explain SMTP and the form never reveals a saved credential.</summary>
    /// <param name="language">The admin language.</param>
    /// <returns>The test task.</returns>
    [TestCase("en")]
    [TestCase("vi")]
    public async Task SavesSettingsWithHelpAndHintsAsync(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        var previousResourceCulture = Resources.Culture;
        var directory = Directory.CreateTempSubdirectory("hnmo-mail-page-");
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            Resources.Culture = CultureInfo.CurrentUICulture;
            await using var context = new BunitContext();
            var store = new WebsiteMailSettingsStore(new HnmoStateStore(Path.Combine(directory.FullName, HnmoStateStore.StateFileName)), DataProtectionProvider.Create(directory));
            context.Services.AddSingleton(store);
            context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
            var page = context.Render<EmailSettings>();
            Assert.That(page.Find("h1").TextContent, Is.EqualTo(language == "vi" ? "Gửi email" : "Email delivery"));
            Assert.That(page.FindAll(".alert-info"), Is.Empty);
            page.Find(".page-help button").Click();
            Assert.That(page.Find(".alert-info").TextContent, Does.Contain("STARTTLS").And.Contain("587"));
            Assert.That(page.FindAll("input,button").All(c => !string.IsNullOrWhiteSpace(c.GetAttribute("title"))), Is.True);
            page.Find("#mail-enabled").Change(true);
            page.Find("#mail-host").Change("smtp.example.test");
            page.Find("#mail-from").Change("host@example.test");
            page.Find("#mail-user").Change("host");
            page.Find("#mail-password").Change("mail-page-test-secret");
            page.Find("form").Submit();
            Assert.That(store.Read().Enabled, Is.True);
            Assert.That(store.Read().HasPassword, Is.True);
            Assert.That(page.Markup, Does.Not.Contain("mail-page-test-secret").And.Contain(Resources.WebsiteMailSaved));
            Assert.That(File.ReadAllText(store.StatePath), Does.Not.Contain("mail-page-test-secret"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Resources.Culture = previousResourceCulture;
            directory.Delete(true);
        }
    }

    /// <summary>The settings route requires the administrator policy.</summary>
    [Test]
    public void RequiresAdministrator()
    {
        var policies = typeof(EmailSettings).GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>();
        Assert.That(policies.Any(p => p.Policy == AdminPolicies.Administrator), Is.True);
    }
}

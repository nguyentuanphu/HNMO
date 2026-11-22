// <copyright file="WebsiteThemeSettingsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>Public appearance settings are host-owned and expose no private configuration.</summary>
[TestFixture]
public class WebsiteThemeSettingsTests
{
    /// <summary>One saved appearance and its public brand are independent of a visitor's world or query.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ExposesOnlyTheGlobalHostThemeAndValidatedBrandAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewContext();
        var website = context.CreateNew<PlugInConfiguration>();
        website.TypeId = typeof(WebsitePlugIn).GUID;
        website.IsActive = true;
        website.CustomConfiguration = JsonSerializer.Serialize(new WebsiteConfiguration { Theme = WebsiteTheme.Ember, AdminPath = "private-admin-location" });
        var brand = context.CreateNew<PlugInConfiguration>();
        brand.TypeId = typeof(GameNamePlugIn).GUID;
        brand.IsActive = true;
        brand.CustomConfiguration = JsonSerializer.Serialize(new GameNameConfiguration { LogoUrl = "/images/host.png", AccentColor = "#a1B2c3", BackgroundColor = "#112233" });
        await context.SaveChangesAsync().ConfigureAwait(false);
        var controller = new WebsiteSettingsController(provider)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.Request.QueryString = new QueryString("?theme=jade&serverId=99");
        var result = (JsonResult)await controller.PublicAsync().ConfigureAwait(false);
        var json = JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Multiple(() =>
        {
            Assert.That(json.GetProperty("theme").GetString(), Is.EqualTo("ember"));
            Assert.That(json.GetProperty("brand").GetProperty("logoUrl").GetString(), Is.EqualTo("/images/host.png"));
            Assert.That(json.GetProperty("brand").GetProperty("accentColor").GetString(), Is.EqualTo("#a1B2c3"));
            Assert.That(json.GetRawText(), Does.Not.Contain("private-admin-location"));
            Assert.That(json.TryGetProperty("adminPath", out _), Is.False);
        });
    }

    /// <summary>Older saved settings need no migration and retain Classic and no brand override.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task OldSettingsKeepClassicAndNoBrandOverridesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var controller = new WebsiteSettingsController(provider)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        var result = (JsonResult)await controller.PublicAsync().ConfigureAwait(false);
        var json = JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(json.GetProperty("theme").GetString(), Is.EqualTo("classic"));
        Assert.That(json.GetProperty("brand").EnumerateObject().All(property => property.Value.ValueKind == JsonValueKind.Null), Is.True);
    }

    /// <summary>Unsafe and malformed stored colors or logo URLs never become public presentation data.</summary>
    /// <param name="logo">The invalid logo URL.</param>
    [TestCase("javascript:alert(1)")]
    [TestCase("data:image/svg+xml,test")]
    [TestCase("//example.test/logo.png")]
    [TestCase("/\\example.test/logo.png")]
    [TestCase("http://example.test/logo.png")]
    [TestCase("https://private:secret@example.test/logo.png")]
    public void RejectsUnsafeBrandValues(string logo)
    {
        var result = WebsiteBrand.FromConfiguration(new GameNameConfiguration { LogoUrl = logo, AccentColor = "red;display:none", PanelColor = "#12345g" });
        Assert.Multiple(() =>
        {
            Assert.That(result.LogoUrl, Is.Null);
            Assert.That(result.AccentColor, Is.Null);
            Assert.That(result.PanelColor, Is.Null);
        });
    }

    /// <summary>Hosts see translated brand settings and a global-theme explanation in each required language.</summary>
    /// <param name="language">The host's UI language.</param>
    /// <param name="caption">The translated brand caption.</param>
    [TestCase("en", "Brand")]
    [TestCase("vi", "Thương hiệu")]
    [TestCase("de", "Marke")]
    [TestCase("zh-CN", "品牌")]
    public void LocalizesTheHostSettings(string language, string caption)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            Assert.Multiple(() =>
            {
                Assert.That(WebsiteBrandLabels.Caption, Is.EqualTo(caption));
                Assert.That(WebsiteBrandLabels.LogoDescription, Is.Not.Empty);
                Assert.That(WebsiteBrandLabels.AccentDescription, Is.Not.Empty);
                Assert.That(WebsiteThemeLabels.GlobalDescription, Is.Not.EqualTo(nameof(WebsiteThemeLabels.GlobalDescription)));
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}

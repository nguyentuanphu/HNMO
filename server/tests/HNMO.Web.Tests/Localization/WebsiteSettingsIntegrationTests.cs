// <copyright file="WebsiteSettingsIntegrationTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Localization;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns;
using HNMO.PlugIns;

/// <summary>Checks the admin contract shared by the public website settings.</summary>
[TestFixture]
public sealed class WebsiteSettingsIntegrationTests
{
    private static readonly PropertyInfo[] Settings = new[] { typeof(WebsiteConfiguration), typeof(GameNameConfiguration) }
        .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        .Where(property => property.CanRead && property.CanWrite)
        .ToArray();

    /// <summary>Every newly shipped website control is discoverable by the admin editor.</summary>
    [Test]
    public void IntegratedWebsiteControlsAreDiscoverable()
    {
        string[] expected =
        [
            nameof(WebsiteConfiguration.Theme),
            nameof(WebsiteConfiguration.RequireWebsiteLogin),
            nameof(GameNameConfiguration.LogoUrl),
            nameof(GameNameConfiguration.AccentColor),
            nameof(GameNameConfiguration.BackgroundColor),
            nameof(GameNameConfiguration.PanelColor),
            nameof(GameNameConfiguration.TextColor),
            nameof(WebsiteConfiguration.TrackerEnabled),
            nameof(WebsiteConfiguration.TrackerShowCharacterNames),
            nameof(WebsiteConfiguration.ClassGuideEnabled),
            nameof(WebsiteConfiguration.EventGuideEnabled),
            nameof(WebsiteConfiguration.ItemGuideEnabled),
            nameof(WebsiteConfiguration.CombatGuideEnabled),
            nameof(WebsiteConfiguration.PublishBannedCharacters),
            nameof(WebsiteConfiguration.ContentRoadmapEn),
            nameof(WebsiteConfiguration.ContentRoadmapVi),
            nameof(WebsiteConfiguration.ContentAboutEn),
            nameof(WebsiteConfiguration.ContentAboutVi),
            nameof(WebsiteConfiguration.ContentTermsEn),
            nameof(WebsiteConfiguration.ContentTermsVi),
            nameof(WebsiteConfiguration.ContentPrivacyEn),
            nameof(WebsiteConfiguration.ContentPrivacyVi),
        ];
        Assert.That(Settings.Select(property => property.Name), Is.SupersetOf(expected));
        foreach (var property in Settings)
        {
            Assert.That(property.GetCustomAttribute<DisplayAttribute>(), Is.Not.Null, property.Name);
        }
    }

    /// <summary>Resolves captions and help from each installed language without silently using fallback resources.</summary>
    /// <param name="locale">The admin language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void WebsiteLabelsMatchTheAdminDisplay(string locale)
    {
        var previous = CultureInfo.CurrentUICulture;
        var managers = new Dictionary<string, ResourceManager>(StringComparer.Ordinal);
        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);
            CultureInfo.CurrentUICulture = culture;
            var exactCulture = locale == "en" ? CultureInfo.InvariantCulture : culture;
            foreach (var property in Settings)
            {
                var display = ReadDisplay(property);
                var resources = GetResources(display, exactCulture);
                var caption = resources.GetString(display.Name!);
                var help = resources.GetString(display.Description!);
                Assert.That(caption, Is.Not.Null.And.Not.Empty, property.Name);
                Assert.That(help, Is.Not.Null.And.Not.Empty, property.Name);
                Assert.That(display.GetName(), Is.EqualTo(caption), property.Name);
                Assert.That(display.GetDescription(), Is.EqualTo(help), property.Name);
            }

            foreach (var theme in typeof(WebsiteTheme).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var display = ReadDisplay(theme);
                var caption = GetResources(display, exactCulture).GetString(display.Name!);
                Assert.That(caption, Is.Not.Null.And.Not.Empty, theme.Name);
                Assert.That(display.GetName(), Is.EqualTo(caption), theme.Name);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            foreach (var manager in managers.Values)
            {
                manager.ReleaseAllResources();
            }
        }

        ResourceSet GetResources(DisplayAttribute display, CultureInfo culture)
        {
            var resourceType = display.ResourceType!;
            var baseName = resourceType.Name switch
            {
                nameof(WebsiteServerPageLabels) => "WebsiteServerPage",
                nameof(WebsiteThemeLabels) => "WebsiteTheme",
                nameof(WebsiteBrandLabels) => "WebsiteBrand",
                _ => resourceType.Name,
            };
            if (!managers.TryGetValue(baseName, out var manager))
            {
                manager = new ResourceManager($"HNMO.GameLogic.Properties.{baseName}", resourceType.Assembly);
                managers.Add(baseName, manager);
            }

            var resources = manager.GetResourceSet(culture, true, false);
            Assert.That(resources, Is.Not.Null, $"{locale}/{baseName}");
            return resources!;
        }
    }

    /// <summary>Website toggles and multiline settings survive the existing admin save format.</summary>
    [Test]
    public void WebsiteSettingsRoundTripInAdminStoreFormat()
    {
        var expected = new WebsiteConfiguration
        {
            TrackerEnabled = true,
            PublishBannedCharacters = true,
            Theme = WebsiteTheme.Jade,
            ContentRoadmapVi = "Dòng một\nDòng hai",
        };
        var row = new PlugInConfiguration { TypeId = typeof(WebsitePlugIn).GUID, IsActive = true };
        row.SetConfiguration(expected, ReferenceHandler.Preserve);
        var stored = row.GetConfiguration<WebsiteConfiguration>(ReferenceHandler.Preserve);
        Assert.That(stored, Is.Not.Null);
        foreach (var property in Settings.Where(property => property.DeclaringType == typeof(WebsiteConfiguration)))
        {
            Assert.That(property.GetValue(stored), Is.EqualTo(property.GetValue(expected)), property.Name);
        }
    }

    private static DisplayAttribute ReadDisplay(MemberInfo member)
    {
        var result = member.GetCustomAttribute<DisplayAttribute>();
        Assert.That(result?.ResourceType, Is.Not.Null, member.Name);
        Assert.That(result!.Name, Is.Not.Null.And.Not.Empty, member.Name);
        if (member is PropertyInfo)
        {
            Assert.That(result.Description, Is.Not.Null.And.Not.Empty, member.Name);
        }

        return result;
    }
}

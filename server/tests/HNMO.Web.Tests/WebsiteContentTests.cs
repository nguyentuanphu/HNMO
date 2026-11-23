// <copyright file="WebsiteContentTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.GameLogic.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>Checks which settings become public and how the existing editor validates them.</summary>
[TestFixture]
public sealed class WebsiteContentTests
{
    /// <summary>Publishes only the four allowlisted fields and keeps the languages independent.</summary>
    [Test]
    public void ProjectionPublishesOnlyAuthoredLanguageFields()
    {
        var settings = new WebsiteConfiguration
        {
            AdminPath = "private-admin-path",
            GoogleClientId = "private-test-value",
            ContentRoadmapEn = "  First line.\n\nSecond line.  ",
            ContentPrivacyVi = "Chính sách thử nghiệm",
            ContentTermsEn = new string('T', WebsiteConfiguration.MaximumPublicContentLength + 1),
        };
        var result = JsonSerializer.SerializeToNode(WebsiteContentController.Build(settings))!.AsObject();
        Assert.That(result.Select(entry => entry.Key), Is.EquivalentTo(new[] { "roadmap", "about", "terms", "privacy" }));
        Assert.That(result["roadmap"]!["en"]!.GetValue<string>(), Is.EqualTo("First line.\n\nSecond line."));
        Assert.That(result["roadmap"]!["vi"]!.GetValue<string>(), Is.Empty);
        Assert.That(result["privacy"]!["en"]!.GetValue<string>(), Is.Empty);
        Assert.That(result["privacy"]!["vi"]!.GetValue<string>(), Is.EqualTo(settings.ContentPrivacyVi));
        Assert.That(result["terms"]!["en"]!.GetValue<string>(), Has.Length.EqualTo(WebsiteConfiguration.MaximumPublicContentLength));
    }

    /// <summary>All eight Website settings use bounded multiline text and a native resource label.</summary>
    /// <param name="locale">The supported admin locale.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void HostEditorFieldsAreLocalizedAndBounded(string locale)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
            var fields = typeof(WebsiteConfiguration).GetProperties().Where(field => field.Name.StartsWith("Content", StringComparison.Ordinal)).ToArray();
            Assert.That(fields, Has.Length.EqualTo(8));
            foreach (var field in fields)
            {
                var display = field.GetCustomAttribute<DisplayAttribute>()!;
                Assert.That(display.GetName(), Is.Not.EqualTo(field.Name));
                Assert.That(display.GetDescription(), Is.EqualTo(WebsiteServerPageLabels.ContentDescription));
                Assert.That(field.GetCustomAttribute<DataTypeAttribute>()?.DataType, Is.EqualTo(DataType.MultilineText));
                Assert.That(field.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength, Is.EqualTo(WebsiteConfiguration.MaximumPublicContentLength));
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}

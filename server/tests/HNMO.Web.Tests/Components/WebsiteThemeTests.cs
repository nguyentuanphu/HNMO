// <copyright file="WebsiteThemeTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Components;

using System.Globalization;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using HNMO.GameLogic.PlugIns;
using HNMO.Web.Shared.Components.Form;
using HNMO.Web.Shared.Services;

/// <summary>Checks the persisted theme contract and the admin selector's Vietnamese labels.</summary>
[TestFixture]
[NonParallelizable]
public sealed class WebsiteThemeTests
{
    /// <summary>Existing configuration keeps its appearance; a saved choice round trips.</summary>
    [Test]
    public void SavedChoiceRoundTripsAndLegacyConfigurationKeepsClassic()
    {
        var legacy = JsonSerializer.Deserialize<WebsiteConfiguration>("{}");
        var saved = JsonSerializer.Serialize(new WebsiteConfiguration { Theme = WebsiteTheme.Parchment });
        Assert.Multiple(() =>
        {
            Assert.That(legacy?.Theme, Is.EqualTo(WebsiteTheme.Classic));
            Assert.That(JsonSerializer.Deserialize<WebsiteConfiguration>(saved)?.Theme, Is.EqualTo(WebsiteTheme.Parchment));
        });
    }

    /// <summary>The generic admin enum editor lists every appearance in Vietnamese.</summary>
    [Test]
    public void AdminSelectorShowsVietnameseThemeNames()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
            using var context = new BunitContext();
            context.Services.AddSingleton(Mock.Of<IChangeNotificationService>());
            var configuration = new WebsiteConfiguration();
            var selector = context.Render<EnumSelect<WebsiteTheme>>(parameters => parameters
                .Add(component => component.Value, configuration.Theme)
                .Add(component => component.ValueExpression, () => configuration.Theme));
            var labels = selector.FindAll("option").Select(option => option.TextContent).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(labels, Is.EquivalentTo(new[] { "Cổ điển", "Biển xanh", "Lửa hồng", "Ngọc bích", "Tím huyền", "Giấy cổ" }));
                Assert.That(WebsiteThemeLabels.Caption, Is.EqualTo("Giao diện website"));
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}

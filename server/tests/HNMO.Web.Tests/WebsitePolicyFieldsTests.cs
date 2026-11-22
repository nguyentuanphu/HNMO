// <copyright file="WebsitePolicyFieldsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Globalization;
using Bunit;
using HNMO.GameLogic.PlugIns;
using HNMO.Web.Shared.Components.Form;
using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks hosts can edit public policy lines in every supported administration language.</summary>
[TestFixture]
[NonParallelizable]
public sealed class WebsitePolicyFieldsTests
{
    /// <summary>Checks localized labels and lossless multiline editing through the existing field component.</summary>
    /// <param name="language">The administration language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void PublicPolicyUsesLocalizedMultilineField(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            using var context = new BunitContext();
            context.Services.AddSingleton<IChangeNotificationService, ChangeNotificationService>();
            var settings = new WebsiteConfiguration();
            var component = context.Render<CascadingValue<EditContext>>(parameters => parameters
                .Add(cascade => cascade.Value, new EditContext(settings))
                .AddChildContent<TextField>(field => field
                    .Add(value => value.Value, settings.ServerClientRulesEn)
                    .Add(value => value.ValueExpression, () => settings.ServerClientRulesEn)
                    .Add(value => value.ValueChanged, value => settings.ServerClientRulesEn = value)));
            Assert.That(component.Find("label").TextContent.Trim(), Is.EqualTo(WebsiteServerPageLabels.ClientRulesEn));
            Assert.That(component.FindAll("input"), Is.Empty);
            component.Find("textarea").Change("First rule.\nSecond rule.");
            Assert.That(settings.ServerClientRulesEn, Is.EqualTo("First rule.\nSecond rule."));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Checks unannotated settings keep the existing single-line editor.</summary>
    [Test]
    public void OrdinaryTextFieldKeepsItsOriginalInput()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IChangeNotificationService, ChangeNotificationService>();
        var settings = new WebsiteConfiguration();
        var component = context.Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(cascade => cascade.Value, new EditContext(settings))
            .AddChildContent<TextField>(field => field
                .Add(value => value.Value, settings.GoogleClientId)
                .Add(value => value.ValueExpression, () => settings.GoogleClientId)));
        Assert.That(component.FindAll("textarea"), Is.Empty);
        Assert.That(component.Find("input").GetAttribute("type"), Is.EqualTo("text"));
    }
}

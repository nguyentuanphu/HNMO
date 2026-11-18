// <copyright file="PageHelpTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Collections;
using System.Globalization;
using Bunit;
using HNMO.Web.AdminPanel.Components;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;

/// <summary>Localized disclosure behavior and resource coverage for every scoped admin-page explanation.</summary>
[TestFixture]
[NonParallelizable]
public class PageHelpTests
{
    /// <summary>Gets every page explanation in both supported interface languages.</summary>
    public static IEnumerable<TestCaseData> HelpCases => HelpKeys.SelectMany(key => new[] { "en", "vi" }
        .Select(language => new TestCaseData(key, language).SetName($"PageHelp_{key}_{language}")));

    private static IEnumerable<string> HelpKeys => Resources.ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!
        .Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Where(key => key.StartsWith("PageHelpText", StringComparison.Ordinal));

    /// <summary>Every scoped page's explanation starts closed, opens with its button and closes again.</summary>
    /// <param name="key">The resource key for this page.</param>
    /// <param name="language">The interface language.</param>
    [TestCaseSource(nameof(HelpCases))]
    public void EveryExplanationCanBeOpenedAndClosed(string key, string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            using var context = new BunitContext();
            var text = Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)!;
            var cut = context.Render<PageHelp>(parameters => parameters.Add(component => component.Text, text));
            var button = cut.Find("button");
            Assert.That(button.TextContent.Trim(), Is.EqualTo(Resources.PageHelpTitle));
            Assert.That(button.GetAttribute("title"), Is.EqualTo(Resources.PageHelpToggleHint));
            Assert.That(button.GetAttribute("aria-expanded"), Is.EqualTo("false"));
            Assert.That(cut.FindAll(".alert-info"), Is.Empty);
            button.Click();
            Assert.That(cut.Find(".alert-info").TextContent, Is.EqualTo(text));
            Assert.That(cut.Find(".alert-info").Id, Is.EqualTo(button.GetAttribute("aria-controls")));
            Assert.That(cut.Find("button").GetAttribute("aria-expanded"), Is.EqualTo("true"));
            cut.Find("button").Click();
            Assert.That(cut.FindAll(".alert-info"), Is.Empty);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Help and hover hints have explicit English and Vietnamese entries rather than silent fallback.</summary>
    [Test]
    public void EveryHelpAndHintHasBothLanguages()
    {
        foreach (var manager in new[] { Resources.ResourceManager, HNMO.Web.Shared.Properties.Resources.ResourceManager })
        {
            var english = manager.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!;
            var vietnamese = manager.GetResourceSet(CultureInfo.GetCultureInfo("vi"), true, false)!;
            var enHelpKeys = english.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Where(key => key.StartsWith("PageHelp", StringComparison.Ordinal)).ToList();
            var viHelpKeys = vietnamese.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Where(key => key.StartsWith("PageHelp", StringComparison.Ordinal)).ToList();

            foreach (var key in enHelpKeys)
            {
                Assert.That(english.GetString(key), Is.Not.Null.And.Not.Empty, $"Missing EN string for {key}");
                Assert.That(vietnamese.GetString(key), Is.Not.Null.And.Not.Empty, $"Missing VI string for {key}");
            }

            foreach (var key in viHelpKeys)
            {
                Assert.That(english.GetString(key), Is.Not.Null.And.Not.Empty, $"Orphaned VI key {key} missing EN string");
            }
        }
    }

    /// <summary>The error and access pages render the reusable help in their actual page markup.</summary>
    [Test]
    public void StatusPagesRenderHelp()
    {
        using var context = new BunitContext();
        Assert.That(context.Render<Error>().FindComponents<PageHelp>(), Has.Count.EqualTo(1));
        Assert.That(context.Render<AccessDenied>().FindComponents<PageHelp>(), Has.Count.EqualTo(1));
        Assert.That(context.Render<NotFound>().FindComponents<PageHelp>(), Has.Count.EqualTo(1));
    }
}

// <copyright file="ArenaSettingsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using HNMO.GameLogic.PlugIns.Arena;
using HNMO.Web.AdminPanel.Pages;

/// <summary>The admin page "Arena characters &amp; battles": its route, the gear text and the checks before saving.</summary>
[TestFixture]
public class ArenaSettingsPageTests
{
    /// <summary>The page answers at /arena.</summary>
    [Test]
    public void RegistersRoute()
    {
        var route = typeof(ArenaSettings).GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>().FirstOrDefault();
        Assert.That(route?.Template, Is.EqualTo("/arena"));
    }

    /// <summary>The default gear goes to text and back unchanged.</summary>
    [Test]
    public void GearTextRoundTrips()
    {
        foreach (var template in ArenaConfiguration.DefaultTemplates())
        {
            var text = ArenaSettings.ItemsText(template.Items);
            Assert.That(ArenaSettings.TryParseItems(text, out var items, out var error), Is.True, error);
            Assert.That(ArenaSettings.ItemsText(items), Is.EqualTo(text), template.Name);
        }
    }

    /// <summary>Short lines take defaults; wrong lines and values out of range are refused with the line.</summary>
    [Test]
    public void GearTextIsChecked()
    {
        Assert.That(ArenaSettings.TryParseItems("0, 0, 16\n# a comment\n\n12 14 3 0 0 0 0 0 100", out var items, out _), Is.True);
        Assert.That(items, Has.Count.EqualTo(2));
        Assert.That(items[1].Durability, Is.EqualTo(100));
        Assert.That(ArenaSettings.TryParseItems("0, 0", out _, out var error), Is.False);
        Assert.That(error, Does.Contain("line 1"));
        Assert.That(ArenaSettings.TryParseItems("0, 16, 1", out _, out error), Is.False, "group 16 doesn't exist");
    }

    /// <summary>Valid edits are taken over; wrong ones are refused and leave the templates as they were.</summary>
    [Test]
    public void ApplyChecksEverything()
    {
        var configuration = new ArenaConfiguration();
        var templates = ArenaConfiguration.DefaultTemplates();
        templates[1].DamagePercent = 90;
        var rows = templates.Select(t => (t, ArenaSettings.ItemsText(t.Items))).ToList();
        Assert.That(ArenaSettings.Apply(configuration, rows), Is.Empty);
        Assert.That(configuration.Templates[1].DamagePercent, Is.EqualTo(90));

        var bad = new ArenaConfiguration { RoyaleTimes = "25:99" };
        var wrong = ArenaConfiguration.DefaultTemplates();
        wrong[0].DefensePercent = 5;
        var errors = ArenaSettings.Apply(bad, wrong.Select(t => (t, "0, 0")).ToList());
        Assert.That(errors, Has.Count.GreaterThanOrEqualTo(3));
        Assert.That(bad.Templates[0].DefensePercent, Is.EqualTo(100), "nothing taken over");
    }
}

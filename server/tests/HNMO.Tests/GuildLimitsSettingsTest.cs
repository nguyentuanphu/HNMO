// <copyright file="GuildLimitsSettingsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>
/// The guild creation level can be edited in the admin panel (Plug-ins, "Guild limits", configure): the generic
/// plug-in configuration editor shows public settable properties with their localized caption and description.
/// </summary>
[TestFixture]
public class GuildLimitsSettingsTest
{
    /// <summary>The level is a public settable number with a caption and a description in every language.</summary>
    /// <param name="culture">The language of the admin panel.</param>
    [TestCase("en")]
    [TestCase("de")]
    [TestCase("vi")]
    [TestCase("zh-CN")]
    public void MinimumCreateLevelIsEditable(string culture)
    {
        var property = typeof(GuildLimitsConfiguration).GetProperty(nameof(GuildLimitsConfiguration.MinimumCreateLevel))!;
        var display = property.GetCustomAttribute<DisplayAttribute>();

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(property.PropertyType, Is.EqualTo(typeof(int)));
                Assert.That(property.SetMethod?.IsPublic, Is.True);
                Assert.That(display, Is.Not.Null);
                Assert.That(display!.GetName(), Is.Not.Empty.And.Not.Contain("_Caption"));
                Assert.That(display.GetDescription(), Is.Not.Empty.And.Not.Contain("_Description"));
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}

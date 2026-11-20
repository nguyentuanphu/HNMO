// <copyright file="WebsiteAdminPathTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// A new save's website admin path is random, hard to guess and allowed by the setting's own rule.
/// </summary>
[TestFixture]
public class WebsiteAdminPathTest
{
    /// <summary>
    /// The default settings of a new save have a random path which the setting accepts; two saves differ.
    /// </summary>
    [Test]
    public void ANewSaveGetsARandomValidPath()
    {
        var plugIn = new WebsitePlugIn();
        var first = ((WebsiteConfiguration)plugIn.CreateDefaultConfig()).AdminPath;
        var second = ((WebsiteConfiguration)plugIn.CreateDefaultConfig()).AdminPath;

        Assert.That(first, Does.StartWith("admin-").And.Length.EqualTo(22));
        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(first, Is.Not.EqualTo(WebsiteConfiguration.DefaultAdminPath));

        var rule = typeof(WebsiteConfiguration).GetProperty(nameof(WebsiteConfiguration.AdminPath))!.GetCustomAttributes<ValidationAttribute>().ToList();
        Assert.That(rule, Is.Not.Empty);
        Assert.That(rule.All(r => r.IsValid(first)), Is.True);
    }

    /// <summary>
    /// Settings read without a saved path (older saves) keep the old path, so their address still works.
    /// </summary>
    [Test]
    public void OlderSettingsKeepTheirPath()
    {
        Assert.That(new WebsiteConfiguration().AdminPath, Is.EqualTo(WebsiteConfiguration.DefaultAdminPath));
    }
}

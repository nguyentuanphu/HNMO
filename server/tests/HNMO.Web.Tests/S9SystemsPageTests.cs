// <copyright file="S9SystemsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Globalization;
using System.Linq;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;

/// <summary>
/// Admin page route and 4-language localization checks for <see cref="S9Systems"/>.
/// </summary>
[TestFixture]
public class S9SystemsPageTests
{
    /// <summary>Verifies <see cref="S9Systems"/> exposes route <c>/s9-systems</c> and 4-language captions.</summary>
    [Test]
    public void RegistersRouteAndLocalizesAllFourLanguages()
    {
        var routeAttr = typeof(S9Systems)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/s9-systems"));

        foreach (var cultureName in new[] { "en", "vi", "de", "zh-CN" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            Assert.That(Resources.ResourceManager.GetString("S9SystemsTitle", culture), Is.Not.Null.And.Not.Empty, cultureName);
            Assert.That(Resources.ResourceManager.GetString("PageHelpTextS9Systems", culture), Is.Not.Null.And.Not.Empty, cultureName);
        }

        var cfg = new SeasonNineSystemsConfiguration();
        Assert.That(cfg.Enabled, Is.False);
        Assert.That(cfg.MasterLevelMaxCap, Is.EqualTo(330));
    }
}

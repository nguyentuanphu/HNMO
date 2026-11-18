// <copyright file="QolSettingsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using HNMO.GameLogic.PlugIns.Qol;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Admin page route and configuration defaults check for <see cref="QolSettings"/>.
/// </summary>
[TestFixture]
public class QolSettingsPageTests
{
    /// <summary>Verifies <see cref="QolSettings"/> exposes route <c>/qol-settings</c> and disabled defaults.</summary>
    [Test]
    public void RegistersRouteAndDefaults()
    {
        var routeAttr = typeof(QolSettings)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/qol-settings"));

        var cfg = new QolConfiguration();
        Assert.That(cfg.Enabled, Is.False);
        Assert.That(cfg.MiningEnabled, Is.False);
        Assert.That(cfg.MiningRewards, Is.Not.Empty);
    }
}

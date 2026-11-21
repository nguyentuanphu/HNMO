// <copyright file="RateTuningPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Admin page route and configuration defaults check for <see cref="RateTuning"/>.
/// </summary>
[TestFixture]
public class RateTuningPageTests
{
    /// <summary>Verifies <see cref="RateTuning"/> exposes route <c>/rate-tuning</c> and default 7 class lines.</summary>
    [Test]
    public void RegistersRouteAndDefaultClassEntries()
    {
        var routeAttr = typeof(RateTuning)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/rate-tuning"));

        var cfg = new RateTuningConfiguration();
        Assert.That(cfg.Enabled, Is.False);
        Assert.That(cfg.MaxLifeOptionLevel, Is.EqualTo(7), "the Jewel of Life goes up to +28 by default");
        Assert.That(cfg.ClassDamageRates, Has.Count.EqualTo(7));
    }
}

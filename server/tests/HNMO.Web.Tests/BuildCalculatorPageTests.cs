// <copyright file="BuildCalculatorPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Linq;
using HNMO.Web.AdminPanel.API;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Route and anonymous-access verification for <see cref="WebsiteBuildCalculatorController"/> (ROADMAP 14.21).
/// </summary>
[TestFixture]
public class BuildCalculatorPageTests
{
    /// <summary>Verifies public route mapping and AllowAnonymous access.</summary>
    [Test]
    public void ExposesPublicRouteAndMetadataActions()
    {
        var route = typeof(WebsiteBuildCalculatorController)
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>()
            .FirstOrDefault();
        Assert.That(route?.Template, Is.EqualTo("api/website/build-calculator"));

        var anon = typeof(WebsiteBuildCalculatorController).GetCustomAttributes(typeof(AllowAnonymousAttribute), false);
        Assert.That(anon, Is.Not.Empty);
        Assert.That(typeof(WebsiteBuildCalculatorController).GetMethod("GetMetadataAsync"), Is.Not.Null);
        Assert.That(typeof(WebsiteBuildCalculatorController).GetMethod("CalculateAsync"), Is.Not.Null);
    }
}

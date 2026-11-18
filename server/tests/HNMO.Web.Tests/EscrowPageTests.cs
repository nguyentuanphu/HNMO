// <copyright file="EscrowPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Linq;
using HNMO.GameLogic.PlugIns.Escrow;
using HNMO.Web.AdminPanel.Pages;

/// <summary>
/// Admin page route and configuration defaults check for <see cref="EscrowContracts"/>.
/// </summary>
[TestFixture]
public class EscrowPageTests
{
    /// <summary>Verifies <see cref="EscrowContracts"/> exposes route <c>/escrow-contracts</c> and safe defaults.</summary>
    [Test]
    public void RegistersRouteAndDefaults()
    {
        var routeAttr = typeof(EscrowContracts)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .FirstOrDefault();
        Assert.That(routeAttr?.Template, Is.EqualTo("/escrow-contracts"));

        var cfg = new EscrowConfiguration();
        Assert.That(cfg.Enabled, Is.False);
        Assert.That(cfg.DirectVndEnabled, Is.False);
        Assert.That(cfg.DirectVndComplianceAcknowledged, Is.False);
        Assert.That(cfg.HostVndEnabled, Is.False);
        Assert.That(cfg.HostVndComplianceAcknowledged, Is.False);
        Assert.That(cfg.RequireSecondPassword, Is.True);
        Assert.That(cfg.BannedItems, Is.Not.Empty);
        Assert.That(cfg.AllowedCurrencies, Does.Contain(EscrowCurrency.WCoinC));
        Assert.That(cfg.AllowedCurrencies, Does.Contain(EscrowCurrency.Zen));

        var webhookMethod = typeof(HNMO.Web.AdminPanel.API.WebsiteEscrowController).GetMethod("DirectWebhookAsync");
        Assert.That(webhookMethod, Is.Not.Null);
    }
}

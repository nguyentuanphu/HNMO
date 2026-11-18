// <copyright file="VipActivityTextTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;

/// <summary>Checks that privileged VIP log details are readable in both languages while retaining host-authored data.</summary>
[TestFixture]
[NonParallelizable]
public class VipActivityTextTests
{
    /// <summary>Grant/removal labels and outcomes are localized, not raw machine JSON.</summary>
    /// <param name="language">The viewer's language.</param>
    /// <param name="remove">Whether the activity is a removal.</param>
    [TestCase("en", false)]
    [TestCase("vi", false)]
    [TestCase("en", true)]
    [TestCase("vi", true)]
    public void FormatsAttributedActions(string language, bool remove)
    {
        var previous = CultureInfo.CurrentUICulture;
        var previousResource = Resources.Culture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            Resources.Culture = CultureInfo.CurrentUICulture;
            var operationId = Guid.NewGuid();
            var entry = new ActivityLogEntry
            {
                Kind = ActivityKind.Administration,
                PartnerAccount = "Host",
                Details = JsonSerializer.Serialize(new { operation = remove ? "vip_remove" : "vip_grant", operationId, status = "Applied", reason = "Host-authored reason" }),
            };
            var text = VipActivityText.Format(entry);
            Assert.That(text, Does.Contain(language == "vi" ? "đã áp dụng" : "applied"));
            Assert.That(text, Does.Contain(remove ? (language == "vi" ? "thu hồi" : "removal") : (language == "vi" ? "cấp" : "grant")));
            Assert.That(text, Does.Contain("Host-authored reason").And.Contain("Host").And.Contain(operationId.ToString()));
            Assert.That(text, Does.Not.Contain("vip_grant").And.Not.Contain("vip_remove").And.Not.Contain("{0}"));
            entry.Kind = ActivityKind.Trade;
            Assert.That(VipActivityText.Format(entry), Is.EqualTo(entry.Details));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Resources.Culture = previousResource;
        }
    }
}

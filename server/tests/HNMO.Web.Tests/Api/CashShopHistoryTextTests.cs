// <copyright file="CashShopHistoryTextTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;

/// <summary>Checks that admin history presents bilingual descriptions and Sydney dates instead of metadata.</summary>
[TestFixture]
[NonParallelizable]
public class CashShopHistoryTextTests
{
    /// <summary>Every balance source has a real label in both languages and retains legacy host-written reasons.</summary>
    /// <param name="language">The supported admin language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    public void FormatsEveryBalanceSource(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        var previousResource = Resources.Culture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            Resources.Culture = CultureInfo.CurrentUICulture;
            var provider = new InMemoryPersistenceContextProvider();
            using var setup = provider.CreateNewContext();
            using var context = provider.CreateNewPlayerContext(setup.CreateNew<GameConfiguration>());
            var account = context.CreateNew<Account>();
            foreach (var reason in Enum.GetValues<CashShopBalanceReason>())
            {
                var receipt = CashShopBalanceReceipt.Record(context, account, CashShopCoinType.WCoinC, -5, reason, "Other");
                var label = CashShopHistoryText.Format(receipt!);
                Assert.That(label, Is.Not.Empty);
                Assert.That(label, Does.Not.Contain("account-balance-v1").And.Not.Contain("{0}"));
                Assert.That(label, Is.Not.EqualTo(reason.ToString()));
                if (reason == CashShopBalanceReason.ExtraVault)
                {
                    Assert.That(label, Is.EqualTo(language == "vi" ? "Mua thêm rương" : "Extra vault purchase"));
                }
            }

            var legacy = context.CreateNew<CashShopCoinGrant>();
            legacy.Reason = "Host-written compensation";
            Assert.That(CashShopHistoryText.Format(legacy), Is.EqualTo(legacy.Reason));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Resources.Culture = previousResource;
        }
    }

    /// <summary>UTC dates cross the Sydney day boundary even if the host itself runs on UTC.</summary>
    [Test]
    public void UsesSydneyTime()
        => Assert.That(CashShopHistoryText.Sydney(new DateTime(2026, 11, 15, 14, 30, 0, DateTimeKind.Utc)), Is.EqualTo("16/11/2026 01:30"));
}

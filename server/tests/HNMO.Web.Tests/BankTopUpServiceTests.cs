// <copyright file="BankTopUpServiceTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using HNMO.Web.AdminPanel.Services;

/// <summary>
/// The parts of the bank top-up which turn a transfer message and amount into an account and coins.
/// </summary>
[TestFixture]
public class BankTopUpServiceTests
{
    /// <summary>The account name is found however the bank wrote the message.</summary>
    /// <param name="message">The transfer message.</param>
    /// <param name="expected">The expected account name.</param>
    [TestCase("HNMO test2", "test2")]
    [TestCase("hnmo TEST2", "TEST2")]
    [TestCase("MBVCB.3278907687.HNMO.test2.CT tu 0123456789 NGUYEN VAN A", "test2")]
    [TestCase("FT24012345678 HNMO-test300 chuyen tien", "test300")]
    [TestCase("NGUYEN VAN A chuyen tien HNMO:abc123", "abc123")]
    [TestCase("chuyen tien", null)]
    [TestCase("XHNMO test2", null)]
    [TestCase("HNMO ab", null)]
    [TestCase("", null)]
    public void FindsTheAccountName(string message, string? expected)
    {
        Assert.That(BankTopUpService.FindAccountName(message, "HNMO"), Is.EqualTo(expected));
    }

    /// <summary>Whole coins only, at the configured rate.</summary>
    [Test]
    public void CountsWholeCoins()
    {
        Assert.That(BankTopUpService.CoinsFor(50_000, 100), Is.EqualTo(500));
        Assert.That(BankTopUpService.CoinsFor(50_050, 100), Is.EqualTo(500));
        Assert.That(BankTopUpService.CoinsFor(0, 100), Is.EqualTo(0));
        Assert.That(BankTopUpService.CoinsFor(-5, 100), Is.EqualTo(0));
    }

    /// <summary>The same bank transaction reported by Casso and by SePay has one reference.</summary>
    [Test]
    public void OneReferencePerBankTransaction()
    {
        var casso = new BankTransfer("casso", "6785", "ft24012345678", 50_000, "HNMO test2");
        var sepay = new BankTransfer("sepay", "92704", "FT24012345678", 50_000, "HNMO test2");
        Assert.That(BankTopUpService.ReferenceOf(casso), Is.EqualTo(BankTopUpService.ReferenceOf(sepay)));
        Assert.That(BankTopUpService.ReferenceOf(new BankTransfer("sepay", "1", null, 1, string.Empty)), Is.EqualTo("topup:sepay:1"));
    }
}

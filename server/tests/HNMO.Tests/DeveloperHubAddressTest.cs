// <copyright file="DeveloperHubAddressTest.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Tests;

using HNMO.Interfaces;

/// <summary>
/// HNMO_DEVELOPER_HUB: unset is the real hub (a host's folder); empty, "off" or "none" turn it off (test runs; Windows
/// drops an empty variable, so "off" is how a Windows test run turns it off); anything else is another hub.
/// </summary>
[TestFixture]
public class DeveloperHubAddressTest
{
    /// <summary>The value gives the hub address.</summary>
    /// <param name="value">The variable's value.</param>
    /// <param name="expected">The address.</param>
    [TestCase(null, DeveloperHubAddress.Default)]
    [TestCase("", "")]
    [TestCase("   ", "")]
    [TestCase("off", "")]
    [TestCase("OFF", "")]
    [TestCase(" none ", "")]
    [TestCase("https://hub.example/", "https://hub.example")]
    [TestCase("http://127.0.0.1:9999", "http://127.0.0.1:9999")]
    public void TheValueGivesTheAddress(string? value, string expected)
    {
        Assert.That(DeveloperHubAddress.Resolve(value), Is.EqualTo(expected));
    }
}

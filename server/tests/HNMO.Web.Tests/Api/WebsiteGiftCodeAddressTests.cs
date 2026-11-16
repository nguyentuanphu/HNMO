// <copyright file="WebsiteGiftCodeAddressTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Net;
using Microsoft.AspNetCore.Http;
using HNMO.Web.AdminPanel.API;

/// <summary>Gift-code forwarding headers are trusted only at the local bridge boundary.</summary>
[TestFixture]
public class WebsiteGiftCodeAddressTests
{
    /// <summary>Direct internet peers cannot choose another address; only the local bridge may forward one.</summary>
    /// <param name="peer">The observed TCP peer.</param>
    /// <param name="forwarded">The supplied forwarding header.</param>
    /// <param name="expected">The address used by gift-code limits.</param>
    [TestCase("198.51.100.2", "203.0.113.7", "198.51.100.2")]
    [TestCase("::ffff:198.51.100.2", "203.0.113.7", "198.51.100.2")]
    [TestCase("127.0.0.1", "203.0.113.7", "203.0.113.7")]
    [TestCase("::1", "::ffff:203.0.113.7", "203.0.113.7")]
    [TestCase("::ffff:127.0.0.1", "203.0.113.7", "203.0.113.7")]
    [TestCase("127.0.0.1", "203.0.113.7, 198.51.100.2", "127.0.0.1")]
    [TestCase("127.0.0.1", "invalid", "127.0.0.1")]
    public void ForwardingRequiresALoopbackPeer(string peer, string forwarded, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Headers["X-HNMO-Client-Address"] = forwarded;
        context.Request.Headers["X-Forwarded-For"] = "192.0.2.99";
        Assert.That(WebsiteGiftCodeAddress.Read(context), Is.EqualTo(expected));
    }

    /// <summary>An absent connection address cannot be supplied by an arbitrary header.</summary>
    [Test]
    public void AbsentPeerRemainsUnknown()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-HNMO-Client-Address"] = "203.0.113.7";
        Assert.That(WebsiteGiftCodeAddress.Read(context), Is.Null);
    }
}

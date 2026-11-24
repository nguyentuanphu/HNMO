using System.Net;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

[TestFixture]
public class HostControlFrameTests
{
    [TestCase("http://127.0.0.1:8095/", "iframe", false, true)]
    [TestCase("http://localhost:8095/", "iframe", false, true)]
    [TestCase("http://127.0.0.1:8096/", "iframe", false, false)]
    [TestCase("http://127.0.0.1:8095/", "document", false, false)]
    [TestCase("http://127.0.0.1:8095/", "iframe", true, false)]
    [TestCase("https://example.test/", "iframe", false, false)]
    public void RequiresPrimaryLocalAdminOrigin(string referer, string destination, bool forwarded, bool permitted)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Host = new HostString("127.0.0.1", 8080);
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.Referer = referer;
        context.Request.Headers["Sec-Fetch-Dest"] = destination;
        if (forwarded)
        {
            context.Request.Headers["X-Forwarded-For"] = "192.0.2.1";
        }

        Assert.That(HostOnly.IsAdminFrame(context, new Uri("http://127.0.0.1:8095")), Is.EqualTo(permitted));
    }
}

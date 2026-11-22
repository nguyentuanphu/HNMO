using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace HNMO.Launcher.Tests;

/// <summary>Website sessions bind existing password-verified handlers without browser credential storage.</summary>
[TestFixture]
public sealed class WebsiteSessionTests
{
    /// <summary>Opaque sessions expire, rotate, remain bounded and never print credentials.</summary>
    [Test]
    public void ExpiryCapacityAndOpaqueIdentity()
    {
        var clock = new Clock(); var store = new WebsiteSessions(clock, 1);
        var token = store.Create("ann", "private-password")!;
        Assert.That(token.Length, Is.EqualTo(64));
        Assert.That(token, Does.Not.Contain("private-password"));
        Assert.That(store.TryGet(token, out var session), Is.True);
        Assert.That(session!.ToString(), Does.Not.Contain("private-password"));
        Assert.That(store.Create("other", "secret"), Is.Null);
        clock.Advance(TimeSpan.FromHours(1));
        Assert.That(store.TryGet(token, out _), Is.False);
        var renewed = store.Create("ann", "private-password")!;
        Assert.That(renewed, Is.Not.EqualTo(token));
        for (var i = 0; i < 15; i++) { clock.Advance(TimeSpan.FromMinutes(30)); Assert.That(store.TryGet(renewed, out _), Is.True); }
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.That(store.TryGet(renewed, out _), Is.False, "Activity cannot extend the absolute eight-hour limit.");
    }

    /// <summary>Explicit revocation invalidates the same token.</summary>
    [Test]
    public void RevocationCannotBeReplayed()
    {
        var store = new WebsiteSessions(new Clock()); var token = store.Create("ann", "secret");
        store.Remove(token);
        Assert.That(store.TryGet(token, out _), Is.False);
        Assert.That(store.TryGet("not-a-token", out _), Is.False);
    }

    /// <summary>Origin and opt-in header protect cookie-authenticated operations.</summary>
    /// <param name="origin">The browser origin.</param>
    /// <param name="site">Fetch-site metadata.</param>
    /// <param name="marker">Explicit cookie authentication opt-in.</param>
    /// <param name="allowed">Whether it is accepted.</param>
    [TestCase("https://site.test", "same-origin", "1", true)]
    [TestCase("https://site.test:443", "same-origin", "1", true)]
    [TestCase("https://other.test", "same-origin", "1", false)]
    [TestCase("https://site.test", "cross-site", "1", false)]
    [TestCase("https://site.test", "same-site", "1", false)]
    [TestCase("https://site.test", "same-origin", "", false)]
    [TestCase("http://site.test", "same-origin", "1", false)]
    public void RequiresSameOrigin(string origin, string site, string marker, bool allowed)
    {
        var context = new DefaultHttpContext(); context.Request.Scheme = "https"; context.Request.Host = new HostString("site.test");
        context.Request.Headers.Origin = origin; context.Request.Headers["Sec-Fetch-Site"] = site; context.Request.Headers["X-HNMO-Session"] = marker;
        Assert.That(WebsiteSessions.IsSameOrigin(context), Is.EqualTo(allowed));
    }

    /// <summary>Existing handlers receive only the cookie account, never a browser-selected substitute.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AdaptsLegacyHandlersAndBoundsInputAsync()
    {
        var store = new WebsiteSessions(new Clock()); var token = store.Create("ann", "actual-secret")!;
        var context = Context(token, "{\"LoginName\":\"victim\",\"password\":\"fake\",\"code\":\"GIFT\"}");
        await store.InvokeAsync(context, async () =>
        {
            using var payload = await JsonDocument.ParseAsync(context.Request.Body);
            Assert.That(payload.RootElement.GetProperty("loginName").GetString(), Is.EqualTo("ann"));
            Assert.That(payload.RootElement.GetProperty("password").GetString(), Is.EqualTo("actual-secret"));
            Assert.That(payload.RootElement.GetProperty("code").GetString(), Is.EqualTo("GIFT"));
            Assert.That(payload.RootElement.TryGetProperty("LoginName", out _), Is.False);
        });
        Assert.That(context.Request.Body.Position, Is.GreaterThan(0), "Original stream is restored, not retained credential-bearing injected JSON.");
        var large = Context(token, new string('x', 65537));
        await store.InvokeAsync(large, () => throw new AssertionException("Oversized body reached the handler."));
        Assert.That(large.Response.StatusCode, Is.EqualTo(413));
        var malformed = Context(token, "[");
        await store.InvokeAsync(malformed, () => throw new AssertionException("Malformed JSON reached the handler."));
        Assert.That(malformed.Response.StatusCode, Is.EqualTo(400));
        var missing = Context(new string('a', 64), "{}");
        await store.InvokeAsync(missing, () => throw new AssertionException("Missing identity reached the handler."));
        Assert.That(missing.Response.StatusCode, Is.EqualTo(401));
    }

    /// <summary>The browser receives only an opaque HttpOnly, same-site, bounded cookie.</summary>
    [Test]
    public void CookieIsNotAJavaScriptCredential()
    {
        var context = new DefaultHttpContext(); context.Request.Scheme = "https";
        WebsiteSessions.SetCookie(context, new string('A', 64));
        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.That(cookie, Does.Contain("httponly").And.Contain("secure").And.Contain("samesite=strict").And.Contain("max-age=28800").And.Contain("path=/"));
    }

    /// <summary>Real bridge endpoints verify login first, restore across requests, reject forgery and revoke after logout or server rejection.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RealPortalCookieRoundTripAndFreshPasswordChecksAsync()
    {
        var folder = Path.Combine(Path.GetTempPath(), "hnmo-session-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "key"), "operator-key");
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("per-visitor", limiter => { limiter.PermitLimit = 1000; limiter.Window = TimeSpan.FromMinutes(1); }));
        await using var app = builder.Build();
        var backend = new Backend(); using var api = new HttpClient(backend);
        app.UseRouting(); app.UseRateLimiter(); Portal.Map(app, new Portal(new PortalOptions { ApiBaseUrl = "http://api.test/", ApiKeyFile = "key" }, folder, api, NullLogger<Portal>.Instance));
        try
        {
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() }; using var browser = new HttpClient(handler) { BaseAddress = new Uri(address) };
            browser.DefaultRequestHeaders.Add("X-HNMO-Session", "1"); browser.DefaultRequestHeaders.Add("Origin", address);
            var wrong = await browser.PostAsJsonAsync("/portal/session", new { loginName = "ann", password = "wrong" });
            Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized)); Assert.That(wrong.Headers.Contains("Set-Cookie"), Is.False);
            var login = await browser.PostAsJsonAsync("/portal/session", new { loginName = "ann", password = "secret" });
            Assert.That(login.IsSuccessStatusCode, Is.True); Assert.That(await login.Content.ReadAsStringAsync(), Does.Not.Contain("secret"));
            Assert.That(await browser.GetStringAsync("/portal/session"), Does.Contain("true"));
            var restored = await browser.PostAsJsonAsync("/portal/account/overview", new { loginName = "victim", password = "forged", page = 0 });
            Assert.That(restored.IsSuccessStatusCode, Is.True); Assert.That(backend.LastLogin, Is.EqualTo("ann")); Assert.That(backend.LastPassword, Is.EqualTo("secret"));
            browser.DefaultRequestHeaders.Remove("Origin"); browser.DefaultRequestHeaders.Add("Origin", "https://attacker.test");
            Assert.That((await browser.PostAsJsonAsync("/portal/account/overview", new { })).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            browser.DefaultRequestHeaders.Remove("Origin"); browser.DefaultRequestHeaders.Add("Origin", address);
            Assert.That((await browser.DeleteAsync("/portal/session")).IsSuccessStatusCode, Is.True);
            Assert.That(await browser.GetStringAsync("/portal/session"), Does.Contain("false"));
            Assert.That((await browser.PostAsJsonAsync("/portal/account/overview", new { })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            await browser.PostAsJsonAsync("/portal/session", new { loginName = "ann", password = "secret" }); backend.Valid = false;
            Assert.That((await browser.PostAsJsonAsync("/portal/account/overview", new { })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(await browser.GetStringAsync("/portal/session"), Does.Contain("false"), "Server password/ban rejection revokes the bridge session.");
        }
        finally { await app.StopAsync(); Directory.Delete(folder, true); }
    }

    private static DefaultHttpContext Context(string token, string body)
    {
        var context = new DefaultHttpContext(); context.Request.Path = "/portal/gift-codes/redeem"; context.Request.Headers["X-HNMO-Session"] = "1";
        context.Request.Headers.Cookie = WebsiteSessions.CookieName + "=" + token;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body)); return context;
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => this._now;
        internal void Advance(TimeSpan value) => this._now += value;
    }

    private sealed class Backend : HttpMessageHandler
    {
        internal bool Valid { get; set; } = true;
        internal string? LastLogin { get; private set; }
        internal string? LastPassword { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            this.LastLogin = doc.RootElement.GetProperty("loginName").GetString(); this.LastPassword = doc.RootElement.GetProperty("password").GetString();
            return new HttpResponseMessage(this.Valid && this.LastLogin == "ann" && this.LastPassword == "secret" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
            { Content = JsonContent.Create(new { loginName = "ann", page = 0 }) };
        }
    }
}

// Read-only server-guide forwarding and the existing visitor budget, without a live game backend.
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace HNMO.Launcher.Tests;

/// <summary>Checks the real public bridge route, upstream credential isolation and request limits.</summary>
[TestFixture]
public sealed class WebsiteServerGuideForwardingTests
{
    /// <summary>Checks only GET forwards, no visitor credentials cross upstream and the route uses its visitor policy.</summary>
    [Test]
    public async Task ReadOnlyRouteUsesVisitorBudgetWithoutForwardingCredentialsAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddFixedWindowLimiter("per-visitor", limiter => { limiter.PermitLimit = 2; limiter.Window = TimeSpan.FromHours(1); });
        });
        await using var app = builder.Build();
        app.UseRateLimiter();
        using var handler = new PublicHandler();
        using var upstream = new HttpClient(handler);
        Website.Map(app, new PortalOptions { ApiBaseUrl = "http://source.invalid/" }, new FixedClientFactory(upstream));
        await app.StartAsync().ConfigureAwait(false);
        using var visitor = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        visitor.DefaultRequestHeaders.Add("Cookie", "private-session=not-for-upstream");
        visitor.DefaultRequestHeaders.Authorization = new("Bearer", "not-for-upstream");
        using var first = await visitor.GetAsync("/website/server-guide").ConfigureAwait(false);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(first.Headers.CacheControl?.NoStore, Is.True);
        using var rejectedMethod = await visitor.PostAsync("/website/server-guide", new StringContent("{}")).ConfigureAwait(false);
        Assert.That(rejectedMethod.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        using var second = await visitor.GetAsync("/website/server-guide").ConfigureAwait(false);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var limited = await visitor.GetAsync("/website/server-guide").ConfigureAwait(false);
        Assert.That(limited.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(handler.Count, Is.EqualTo(2));
        await app.StopAsync().ConfigureAwait(false);
    }

    private sealed class FixedClientFactory(HttpClient client) : IHttpClientFactory
    {
        /// <inheritdoc />
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class PublicHandler : HttpMessageHandler
    {
        /// <summary>Gets the number of upstream reads.</summary>
        public int Count { get; private set; }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.RequestUri?.AbsolutePath, Is.EqualTo("/api/website/server-guide"));
            Assert.That(request.Headers.Contains("Cookie"), Is.False);
            Assert.That(request.Headers.Contains("Authorization"), Is.False);
            this.Count++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"servers\":[]}") });
        }
    }
}

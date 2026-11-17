// <copyright file="WebsiteMissionsHttpTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using HNMO.Dapr.Common;
using HNMO.GameServer.Host;
using HNMO.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>Protects the internal mission read/claim channel from callers which bypass the trusted sidecar.</summary>
[TestFixture]
public class WebsiteMissionsHttpTests
{
    private const string Token = "hnmo-missions-test-token";

    /// <summary>Missing, wrong, duplicate or unconfigured tokens never reach either action.</summary>
    /// <param name="configured">Whether the host has a token.</param>
    /// <param name="supplied">The supplied token.</param>
    /// <param name="duplicate">Whether the header is duplicated.</param>
    /// <returns>The test task.</returns>
    [TestCase(true, null, false)]
    [TestCase(true, "wrong", false)]
    [TestCase(true, Token, true)]
    [TestCase(false, Token, false)]
    [TestCase(true, Token, false)]
    public async Task GuardsReadAndClaimAsync(bool configured, string? supplied, bool duplicate)
    {
        var server = new Mock<IGameServer>();
        server.SetupGet(s => s.Id).Returns(4);
        var account = server.As<IWebsiteAccountGameServer>();
        account.Setup(s => s.ReadWebsiteMissionsAsync(It.IsAny<WebsiteMissionSubject>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WebsiteMissionsReply("ok", "{}", false));
        account.Setup(s => s.ClaimWebsiteMissionsAsync(It.IsAny<WebsiteMissionSubject>(), "calendar", string.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(new WebsiteMissionsReply("ok", "{}", true));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["APP_API_TOKEN"] = configured ? Token : null });
        builder.Services.AddSingleton(server.Object);
        builder.Services.AddSingleton<DaprAppTokenAuthorizationFilter>();
        builder.Services.AddControllers().AddApplicationPart(typeof(WebsiteMissionsController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync().ConfigureAwait(false);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        if (supplied is not null)
        {
            http.DefaultRequestHeaders.TryAddWithoutValidation("dapr-api-token", duplicate ? [supplied, supplied] : [supplied]);
        }

        var subject = new WebsiteMissionSubject(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ann");
        var accepted = configured && supplied == Token && !duplicate;
        foreach (var operation in new[] { "read", "claim" })
        {
            using var body = JsonContent.Create(new WebsiteMissionsInvocation(4, subject, "calendar", string.Empty));
            using var response = await http.PostAsync($"/website-account/missions/{operation}", body).ConfigureAwait(false);
            Assert.That(response.StatusCode, Is.EqualTo(accepted ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
        }

        account.Verify(s => s.ReadWebsiteMissionsAsync(subject, It.IsAny<CancellationToken>()), accepted ? Times.Once() : Times.Never());
        account.Verify(s => s.ClaimWebsiteMissionsAsync(subject, "calendar", string.Empty, It.IsAny<CancellationToken>()), accepted ? Times.Once() : Times.Never());
    }
}

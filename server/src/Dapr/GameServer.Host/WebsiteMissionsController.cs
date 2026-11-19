// <copyright file="WebsiteMissionsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.Host;

using System.Threading;
using HNMO.Dapr.Common;
using HNMO.Interfaces;
using Microsoft.AspNetCore.Mvc;

/// <summary>Protected internal account-feature endpoints; these are not public website routes.</summary>
/// <param name="server">The addressed game server.</param>
[ApiController]
[Route("website-account/missions")]
[ServiceFilter(typeof(DaprAppTokenAuthorizationFilter))]
public sealed class WebsiteMissionsController(IGameServer server) : ControllerBase
{
    /// <summary>Reads one account's mission state.</summary>
    /// <param name="request">The verified subject and target server.</param>
    /// <param name="cancellationToken">The request cancellation.</param>
    /// <returns>The board or availability status.</returns>
    [HttpPost("read")]
    public ValueTask<WebsiteMissionsReply> ReadAsync(WebsiteMissionsInvocation request, CancellationToken cancellationToken)
        => this.Resolve(request).ReadWebsiteMissionsAsync(request.Subject, cancellationToken);

    /// <summary>Invokes an existing game mission action.</summary>
    /// <param name="request">The verified subject and selected action.</param>
    /// <param name="cancellationToken">The cancellation before gameplay begins.</param>
    /// <returns>The result and refreshed board.</returns>
    [HttpPost("claim")]
    public ValueTask<WebsiteMissionsReply> ClaimAsync(WebsiteMissionsInvocation request, CancellationToken cancellationToken)
        => this.Resolve(request).ClaimWebsiteMissionsAsync(request.Subject, request.Action ?? string.Empty, request.Argument ?? string.Empty, cancellationToken);

    private IWebsiteAccountGameServer Resolve(WebsiteMissionsInvocation request)
        => request.ServerId == server.Id && server is IWebsiteAccountGameServer accountServer
            ? accountServer : throw new ArgumentException("The account-feature request does not address this server.");
}

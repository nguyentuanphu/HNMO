// <copyright file="WebsiteAccountMissionsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Net.Http;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Own-account mission reads/actions through the trusted bridge and authoritative game server.</summary>
/// <param name="persistence">The account persistence.</param>
/// <param name="configurations">The default website world configuration.</param>
/// <param name="servers">The known game-server channels.</param>
/// <param name="loginServer">The current account/server mapping.</param>
/// <param name="logger">The diagnostic logger, which never receives credentials.</param>
[ApiController]
[Route("api/website/account/missions")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
public sealed class WebsiteAccountMissionsController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    IServerProvider servers,
    ILoginServer loginServer,
    ILogger<WebsiteAccountMissionsController> logger) : ControllerBase
{
    /// <summary>Reads only the authenticated account's selected character.</summary>
    /// <param name="request">The credentials and owned character.</param>
    /// <returns>The mission board or an availability status.</returns>
    [HttpPost("state")]
    public ValueTask<IActionResult> StateAsync([FromBody] WebsiteMissionRequest request) => this.HandleAsync(request, false);

    /// <summary>Performs an explicit action using the same reward rules as the live game.</summary>
    /// <param name="request">The credentials, owned character and action.</param>
    /// <returns>The result and refreshed board.</returns>
    [HttpPost("claim")]
    public ValueTask<IActionResult> ClaimAsync([FromBody] WebsiteMissionRequest request) => this.HandleAsync(request, true);

    private async ValueTask<IActionResult> HandleAsync(WebsiteMissionRequest request, bool claim)
    {
        this.Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(request.LoginName) || request.LoginName.Length > 10 || request.Password is not { Length: > 0 and <= 20 })
        {
            return this.Unauthorized(new { error = "login" });
        }

        if (request.CharacterName is not { Length: > 0 and <= 10 }
            || (claim && (request.Action is not ("mission" or "streak" or "calendar" or "invite") || request.Argument?.Length > 64)))
        {
            return this.BadRequest(new { error = "request" });
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var configuration = await configurations.GetOwnerAsync(Guid.Empty, timeout.Token).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        var account = await context.GetAccountByLoginNameAsync(request.LoginName.Trim(), request.Password, timeout.Token).ConfigureAwait(false);
        if (account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var character = account.Characters.FirstOrDefault(c => string.Equals(c.Name, request.CharacterName, StringComparison.Ordinal));
        if (character is null)
        {
            return this.BadRequest(new { error = "character" });
        }

        var subject = new WebsiteMissionSubject(configuration.GetId(), account.GetId(), character.GetId(), account.LoginName);
        return this.Ok(await this.QueryServersAsync(subject, request, claim, timeout.Token).ConfigureAwait(false));
    }

    private async ValueTask<WebsiteMissionsReply> QueryServersAsync(WebsiteMissionSubject subject, WebsiteMissionRequest request, bool claim, CancellationToken cancellationToken)
    {
        var online = await loginServer.GetSnapshotAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        var candidates = servers.Servers.Where(s => s.Type == ServerType.GameServer && s is IWebsiteAccountGameServer)
            .OrderByDescending(s => online.TryGetValue(subject.LoginName, out var id) && s.Id == id).ToList();
        WebsiteMissionsReply? fallback = null;
        foreach (var server in candidates)
        {
            try
            {
                var channel = (IWebsiteAccountGameServer)server;
                var result = claim
                    ? await channel.ClaimWebsiteMissionsAsync(subject, request.Action!, request.Argument ?? string.Empty, cancellationToken).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false)
                    : await channel.ReadWebsiteMissionsAsync(subject, cancellationToken).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
                if (result.Status is "wrong-world" or "missing")
                {
                    continue;
                }

                if (result.Status == "ok" && result.CharacterOnline)
                {
                    return result;
                }

                if (fallback is null || result.BoardJson is not null)
                {
                    fallback = result;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TimeoutException)
            {
                logger.LogWarning(exception, "Account mission channel {ServerId} is unavailable.", server.Id);
            }
            catch (OperationCanceledException) when (!this.HttpContext.RequestAborted.IsCancellationRequested)
            {
                break;
            }
        }

        return fallback ?? new WebsiteMissionsReply("unavailable", null, false);
    }
}

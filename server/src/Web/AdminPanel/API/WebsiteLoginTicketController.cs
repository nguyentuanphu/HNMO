// <copyright file="WebsiteLoginTicketController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlayerActions;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;

/// <summary>
/// Issues short-lived, single-use login tickets for players signed in on the website so "Play now"
/// can open the web game at server selection without typing the password again.
/// </summary>
[ApiController]
[Route("api/website/login-ticket")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
public sealed class WebsiteLoginTicketController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource) : ControllerBase
{
    /// <summary>
    /// Verifies the account's password and issues a one-time login ticket.
    /// </summary>
    /// <param name="request">The login name and password.</param>
    /// <returns>The login name, ticket, and lifetime in seconds; 401 on wrong password, 403 if blocked.</returns>
    [HttpPost]
    public async Task<IActionResult> IssueAsync([FromBody] LoginTicketRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        var loginName = request.LoginName?.Trim() ?? string.Empty;
        if (loginName.Length is < 3 or > 10 || string.IsNullOrEmpty(request.Password))
        {
            return this.Unauthorized(new { error = "login" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        var state = await context.AuthenticateAsync(loginName, request.Password, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return this.Unauthorized(new { error = "login" });
        }

        if (state is AccountState.Banned or AccountState.TemporarilyBanned)
        {
            return this.StatusCode(403, new { error = "blocked" });
        }

        var (ticket, _) = WebLoginTicketStore.Shared.Issue(loginName);
        this.Response.Headers.CacheControl = "no-store";
        return this.Ok(new
        {
            loginName,
            ticket,
            expiresInSeconds = (int)WebLoginTicketStore.TicketLifetime.TotalSeconds,
        });
    }

    /// <summary>
    /// Issues a one-time login ticket for an account already verified by the website (e.g. Google sign-in).
    /// </summary>
    /// <param name="request">The verified login name.</param>
    /// <returns>The login name, ticket, and lifetime in seconds; 404 if missing, 403 if blocked.</returns>
    [HttpPost("verified")]
    public async Task<IActionResult> IssueVerifiedAsync([FromBody] VerifiedLoginTicketRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        var loginName = request.LoginName?.Trim() ?? string.Empty;
        if (loginName.Length is < 3 or > 10)
        {
            return this.BadRequest(new { error = "login" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        var account = await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return this.NotFound(new { error = "missing" });
        }

        if (account.State is AccountState.Banned or AccountState.TemporarilyBanned)
        {
            return this.StatusCode(403, new { error = "blocked" });
        }

        var (ticket, _) = WebLoginTicketStore.Shared.Issue(account.LoginName);
        this.Response.Headers.CacheControl = "no-store";
        return this.Ok(new
        {
            loginName = account.LoginName,
            ticket,
            expiresInSeconds = (int)WebLoginTicketStore.TicketLifetime.TotalSeconds,
        });
    }

    /// <summary>Request body for <see cref="IssueAsync"/>.</summary>
    /// <param name="LoginName">The account login name.</param>
    /// <param name="Password">The account password.</param>
    public sealed record LoginTicketRequest(string? LoginName, string? Password);

    /// <summary>Request body for <see cref="IssueVerifiedAsync"/>.</summary>
    /// <param name="LoginName">The verified account login name.</param>
    public sealed record VerifiedLoginTicketRequest(string? LoginName);
}

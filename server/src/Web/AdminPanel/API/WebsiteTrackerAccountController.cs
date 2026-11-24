// <copyright file="WebsiteTrackerAccountController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Reads or changes tracker privacy for the verified account only.</summary>
/// <param name="persistence">Account persistence.</param>
/// <param name="configurations">The website's world configuration.</param>
/// <param name="clock">The host clock.</param>
[ApiController]
[Route("api/website/account/tracker")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
public sealed class WebsiteTrackerAccountController(IPersistenceContextProvider persistence, IDataSource<GameConfiguration> configurations, TimeProvider clock) : ControllerBase
{
    /// <summary>Reads the account's choice or replaces it when a new choice is supplied.</summary>
    /// <param name="request">The session credentials and optional new preference.</param>
    /// <returns>The current opt-out status.</returns>
    [HttpPost("privacy")]
    public async ValueTask<IActionResult> PrivacyAsync([FromBody] WebsiteTrackerPreferenceRequest request)
    {
        this.Response.Headers.CacheControl = "no-store";
        if (request.LoginName is not { Length: > 0 and <= 10 } login || request.Password is not { Length: > 0 and <= 20 })
        {
            return this.Unauthorized(new { error = "login" });
        }

        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await configurations.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var auth = persistence.CreateNewPlayerContext(configuration);
        var account = await auth.GetAccountByLoginNameAsync(login.Trim(), request.Password, cancellationToken).ConfigureAwait(false);
        if (account is not { IsBot: false } || account.State is AccountState.Banned or AccountState.TemporarilyBanned)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var store = WebsiteTrackerPrivacyStore.Open();
        if (request.OptedOut is { } optedOut)
        {
            store.Write(account.LoginName, optedOut, clock.GetUtcNow().UtcDateTime);
            return this.Ok(new { optedOut });
        }

        return this.Ok(new { optedOut = store.ReadOptOuts([account.LoginName]).Count != 0 });
    }
}

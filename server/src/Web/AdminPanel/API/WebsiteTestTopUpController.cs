// <copyright file="WebsiteTestTopUpController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.IO;
using System.Net.Http;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>Test-only website coin package catalogue and own-account orders.</summary>
/// <param name="services">The primary admin services.</param>
/// <param name="persistence">Player persistence.</param>
/// <param name="configurations">Active game configuration.</param>
[ApiController]
[Route("api/website/test-topup")]
[RequestSizeLimit(2048)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WebsiteTestTopUpController(
    IServiceProvider services,
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations) : ControllerBase
{
    private WebsiteStripeTestCheckout Stripe => (WebsiteStripeTestCheckout)services.GetService(typeof(WebsiteStripeTestCheckout))!;

    private WebsiteTestTopUpStore Store => (WebsiteTestTopUpStore)services.GetService(typeof(WebsiteTestTopUpStore))!;

    /// <summary>Gets configured packages without revealing any account or order.</summary>
    /// <returns>The catalogue and test availability.</returns>
    [AllowAnonymous]
    [HttpGet("catalogue")]
    public IActionResult Catalogue()
    {
        var bank = this.Store.PublicSettings.BankSimulationEnabled;
        var stripe = this.Stripe.Enabled;
        return this.Ok(new { enabled = bank || stripe, bank, stripe, packages = bank || stripe ? this.Store.Settings.Packages : [] });
    }

    /// <summary>Creates or resumes a simulated transfer for the authenticated account.</summary>
    /// <param name="request">The package selection, operation ID and account credentials resolved by the website bridge.</param>
    /// <returns>The opaque order reference and its locked package amount.</returns>
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    [HttpPost("orders")]
    public async Task<IActionResult> CreateAsync([FromBody] TestTopUpOrderRequest request)
    {
        if (request.Provider == "bank" && !this.Store.PublicSettings.BankSimulationEnabled
            || request.Provider == "stripe" && !this.Stripe.Enabled
            || request.Provider is not ("bank" or "stripe"))
        {
            return this.StatusCode(StatusCodes.Status403Forbidden, new { error = "disabled" });
        }

        if (request.LoginName is not { Length: > 0 and <= 10 } login
            || request.Password is not { Length: > 0 and <= 20 } password)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var cancellationToken = this.HttpContext.RequestAborted;
        var game = await configurations.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var players = persistence.CreateNewPlayerContext(game);
        if (await players.GetAccountByLoginNameAsync(login, cancellationToken).ConfigureAwait(false) is not { IsBot: false } account
            || account.State is AccountState.Banned or AccountState.TemporarilyBanned
            || string.IsNullOrEmpty(account.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
        {
            return this.Unauthorized(new { error = "login" });
        }

        if (this.Store.TryCreate(account.GetId(), request.OperationId, request.PackageId, request.Provider, this.Stripe.Enabled) is not { } order)
        {
            return this.BadRequest(new { error = "order" });
        }

        if (order.Provider == "stripe" && !order.CreditedAt.HasValue)
        {
            try
            {
                var target = await this.Stripe.CreateAsync(order, cancellationToken).ConfigureAwait(false);
                return target is not null
                    ? this.Ok(new { checkoutUrl = target.AbsoluteUri })
                    : this.BadRequest(new { error = "checkout" });
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
            {
                return this.BadRequest(new { error = "checkout" });
            }
        }

        return this.Ok(new { id = order.Id, amount = order.Package.PriceVnd, reference = order.Reference, credited = order.CreditedAt.HasValue });
    }

    /// <summary>Accepts only signed Stripe test events for website coin orders.</summary>
    /// <returns>The webhook delivery status.</returns>
    [AllowAnonymous]
    [HttpPost("stripe/webhook")]
    [RequestSizeLimit(128 * 1024)]
    public async Task<IActionResult> StripeWebhookAsync()
    {
        var signature = this.Request.Headers["Stripe-Signature"].ToString();
        if (signature.Length is 0 or > 4096)
        {
            return this.BadRequest();
        }

        using var buffer = new MemoryStream();
        await this.Request.Body.CopyToAsync(buffer, this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (buffer.Length > 128 * 1024)
        {
            return this.BadRequest();
        }

        var status = await this.Stripe.HandleWebhookAsync(buffer.ToArray(), signature, this.HttpContext.RequestAborted).ConfigureAwait(false);
        return this.StatusCode(status);
    }
}

/// <summary>Account-owned test top-up selection.</summary>
/// <param name="PackageId">Configured package ID.</param>
/// <param name="OperationId">Opaque retry ID.</param>
/// <param name="LoginName">Account name.</param>
/// <param name="Password">Account password resolved by the website bridge.</param>
/// <param name="Provider">Enabled test payment provider.</param>
public sealed record TestTopUpOrderRequest(string PackageId, Guid OperationId, string LoginName, string Password, string Provider = "bank");

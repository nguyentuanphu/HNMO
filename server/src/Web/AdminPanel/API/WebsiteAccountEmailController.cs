// <copyright file="WebsiteAccountEmailController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Confirms mailbox ownership before changing an offline account, without returning or logging its code.</summary>
/// <param name="persistence">The account persistence.</param>
/// <param name="configurations">The website's world configuration.</param>
/// <param name="login">The authoritative account reservation service.</param>
/// <param name="settings">The off-by-default mail settings.</param>
/// <param name="challenges">The protected challenge rows.</param>
/// <param name="mail">The SMTP sender.</param>
/// <param name="clock">The UTC clock.</param>
/// <param name="logger">The security event logger, which receives no email, code or password.</param>
[ApiController]
[Route("api/website/account/email")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
public sealed class WebsiteAccountEmailController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    ILoginServer login,
    WebsiteMailSettingsStore settings,
    WebsiteEmailChallengeStore challenges,
    IWebsiteConfirmationMail mail,
    TimeProvider clock,
    ILogger<WebsiteAccountEmailController> logger) : ControllerBase
{
    /// <summary>Reads the player's own current email and whether confirmations are configured.</summary>
    /// <param name="request">The current credentials.</param>
    /// <returns>The secret-free settings.</returns>
    [HttpPost("state")]
    public ValueTask<IActionResult> StateAsync([FromBody] WebsiteEmailRequest request) => this.HandleAsync(request, "state");

    /// <summary>Sends a one-use code to the requested mailbox.</summary>
    /// <param name="request">The credentials and new address.</param>
    /// <returns>The expiry only; never the code.</returns>
    [HttpPost("request")]
    public ValueTask<IActionResult> RequestAsync([FromBody] WebsiteEmailRequest request) => this.HandleAsync(request, "request");

    /// <summary>Checks a mailed code and saves only the verified address.</summary>
    /// <param name="request">The credentials and mailed code.</param>
    /// <returns>A secret-free outcome.</returns>
    [HttpPost("confirm")]
    public ValueTask<IActionResult> ConfirmAsync([FromBody] WebsiteEmailRequest request) => this.HandleAsync(request, "confirm");

    private static bool Allowed([NotNullWhen(true)] Account? account) => account is { IsBot: false } && account.State is not (AccountState.Banned or AccountState.TemporarilyBanned);

    /// <summary>
    /// Tells whether another account already has this address. Google sign-in finds an account by its address, so
    /// an address must belong to one account only.
    /// </summary>
    private async ValueTask<bool> IsAddressTakenAsync(Account account, string address, CancellationToken cancellationToken)
    {
        var configuration = await configurations.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        var (accounts, _) = await context.SearchAccountPageAsync(address, null, 0, 100, cancellationToken).ConfigureAwait(false);
        return (accounts ?? []).Any(other => !string.Equals(other.LoginName, account.LoginName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(other.EMail?.Trim(), address, StringComparison.OrdinalIgnoreCase));
    }

    private async ValueTask<IActionResult> HandleAsync(WebsiteEmailRequest request, string operation)
    {
        this.Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(request.LoginName) || request.LoginName.Length > 10 || request.Password is not { Length: > 0 and <= 20 })
        {
            return this.Unauthorized(new { error = "login" });
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var configuration = await configurations.GetOwnerAsync(Guid.Empty, timeout.Token).ConfigureAwait(false);
        using var authentication = persistence.CreateNewPlayerContext(configuration);
        var account = await authentication.GetAccountByLoginNameAsync(request.LoginName.Trim(), request.Password, timeout.Token).ConfigureAwait(false);
        if (!Allowed(account))
        {
            return this.Unauthorized(new { error = "login" });
        }

        if (operation == "state")
        {
            return this.Ok(new { enabled = settings.Read().Enabled, email = account!.EMail });
        }

        if (!settings.Read().Enabled)
        {
            return this.StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "mail-off" });
        }

        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        if (!await login.TryReserveAccountAsync(account!.LoginName, reservation).ConfigureAwait(false))
        {
            return this.Conflict(new { error = "online" });
        }

        try
        {
            return await this.ChangeUnderReservationAsync(configuration, request, operation, timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            await login.ReleaseAccountReservationAsync(account.LoginName, reservation).ConfigureAwait(false);
        }
    }

    private async ValueTask<IActionResult> ChangeUnderReservationAsync(GameConfiguration configuration, WebsiteEmailRequest request, string operation, CancellationToken cancellationToken)
    {
        using var context = persistence.CreateNewPlayerContext(configuration);
        var account = await context.GetAccountByLoginNameAsync(request.LoginName!.Trim(), request.Password!, cancellationToken).ConfigureAwait(false);
        if (!Allowed(account))
        {
            return this.Unauthorized(new { error = "login" });
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var current = challenges.Read(account!.GetId());
        return operation == "request"
            ? await this.SendAsync(account, request, current, now, cancellationToken).ConfigureAwait(false)
            : await this.ApplyAsync(context, account, request, current, now, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IActionResult> SendAsync(Account account, WebsiteEmailRequest request, WebsiteEmailChallenge? current, DateTime now, CancellationToken cancellationToken)
    {
        var address = request.NewEmail?.Trim() ?? string.Empty;
        if (!WebsiteMailSettingsStore.IsAddress(address) || string.Equals(address, account.EMail, StringComparison.OrdinalIgnoreCase))
        {
            return this.BadRequest(new { error = "email" });
        }

        if (await this.IsAddressTakenAsync(account, address, cancellationToken).ConfigureAwait(false))
        {
            return this.BadRequest(new { error = "email-taken" });
        }

        if (current is not null && now < current.NextSendAtUtc)
        {
            return this.StatusCode(StatusCodes.Status429TooManyRequests, new { error = "mail-wait" });
        }

        var (challenge, code) = challenges.Create(account, address, now);
        try
        {
            await mail.SendAsync(address, code, request.Language == "vi" ? "vi" : "en", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is System.Net.Mail.SmtpException or OperationCanceledException or CryptographicException)
        {
            challenges.Write(account.GetId(), challenge with { State = "failed" }, now);
            logger.LogWarning("Email confirmation delivery failed for account {AccountId}.", account.GetId());
            return this.StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "mail-send" });
        }

        challenges.Write(account.GetId(), challenge with { State = "pending" }, now);
        return this.Ok(new { sent = true, expiresAt = challenge.ExpiresAtUtc });
    }

    private async ValueTask<IActionResult> ApplyAsync(IPlayerContext context, Account account, WebsiteEmailRequest request, WebsiteEmailChallenge? current, DateTime now, CancellationToken cancellationToken)
    {
        var email = current is null ? null : challenges.Verify(account, current, request.Code?.Trim() ?? string.Empty, now);
        if (email is null)
        {
            if (current is { State: "pending" or "applying", Attempts: < 5 })
            {
                challenges.Write(account.GetId(), current with { Attempts = current.Attempts + 1 }, now);
            }

            return this.BadRequest(new { error = "code" });
        }

        // Another account may have confirmed the same address since the code was sent.
        if (account.EMail != email && await this.IsAddressTakenAsync(account, email, cancellationToken).ConfigureAwait(false))
        {
            return this.BadRequest(new { error = "email-taken" });
        }

        // Persist recovery intent first. A save with a lost response can then be finalized without changing the email twice.
        var applying = current! with { State = "applying" };
        challenges.Write(account.GetId(), applying, now);
        if (account.EMail != email)
        {
            var previous = account.EMail;
            account.EMail = email;
            if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
            {
                account.EMail = previous;
                return this.StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "server" });
            }
        }

        challenges.Write(account.GetId(), applying with { State = "consumed" }, now);
        logger.LogInformation("Account {AccountId} confirmed its new email address.", account.GetId());
        return this.Ok(new { changed = true });
    }
}

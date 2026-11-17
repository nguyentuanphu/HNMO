// <copyright file="WebsiteAccountController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads and updates the player's own account through the trusted website bridge. Both an operator API key and the
/// player's password are required; knowing another account's name does not authorize reading it.
/// </summary>
/// <param name="persistence">The account persistence provider.</param>
/// <param name="configurations">The game configuration source.</param>
/// <param name="loginServer">The authoritative online state and account reservations.</param>
/// <param name="servers">The read channels of the known game servers.</param>
/// <param name="logger">The security-event logger, which never receives passwords.</param>
[ApiController]
[Route("api/website/account")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
public sealed class WebsiteAccountController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    ILoginServer loginServer,
    IServerProvider servers,
    ILogger<WebsiteAccountController> logger) : ControllerBase
{
    private const int PageSize = 20;

    /// <summary>Reads a password-authenticated account overview without secrets or other accounts' grants.</summary>
    /// <param name="request">The player's credentials and history page.</param>
    /// <returns>A persisted snapshot, or a machine-readable refusal for the bilingual website.</returns>
    [HttpPost("overview")]
    public async ValueTask<IActionResult> OverviewAsync([FromBody] WebsiteAccountRequest request)
    {
        this.Response.Headers.CacheControl = "no-store";
        if (request.Page < 0 || request.Page > (int.MaxValue - PageSize) / PageSize)
        {
            return this.BadRequest(new { error = "request" });
        }

        if (string.IsNullOrWhiteSpace(request.LoginName) || request.LoginName.Length > 10
            || string.IsNullOrEmpty(request.Password) || request.Password.Length > 20)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await configurations.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        var account = await context.GetAccountByLoginNameAsync(request.LoginName.Trim(), request.Password, cancellationToken).ConfigureAwait(false);
        if (account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var offset = request.Page * PageSize;
        var grants = await context.GetCashShopCoinHistoryAsync(account.GetId(), offset, PageSize + 1, cancellationToken).ConfigureAwait(false);
        var online = await loginServer.GetSnapshotAsync().ConfigureAwait(false);
        var presence = await WebsiteAccountPresence.ReadAsync(account, online, servers, logger, cancellationToken).ConfigureAwait(false);
        return this.Ok(new
        {
            account.LoginName,
            accountOnline = online.ContainsKey(account.LoginName),
            balances = new { account.WCoinC, account.WCoinP, account.GoblinPoints },
            vipLevel = (int)account.GetAccountAttributeValue(Stats.IsVip, DateTime.UtcNow),
            characters = account.Characters.OrderBy(c => c.CharacterSlot).Select(c => new
            {
                c.Name,
                online = presence[c.GetId()],
                className = c.CharacterClass?.Name.ValueInNeutralLanguage ?? string.Empty,
                classNameVi = c.CharacterClass?.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? string.Empty,
                level = (int)(c.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.Level.Id)?.Value ?? 0),
                resets = (int)(c.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.Resets.Id)?.Value ?? 0),
                map = c.CurrentMap?.Number,
            }).ToList(),
            history = grants.Take(PageSize).Select(g => new
            {
                g.Id,
                coinType = g.CoinType.ToString(),
                g.Amount,
                g.Reason,
                purchase = CashShopPurchaseReceipt.Read(g.Reason),
                createdAt = DateTime.SpecifyKind(g.CreatedAt, DateTimeKind.Utc),
                pending = g.AppliedAt is null,
            }).ToList(),
            page = request.Page,
            hasMore = grants.Count > PageSize,
        });
    }

    /// <summary>Changes an offline account's password while reserving it against concurrent game logins.</summary>
    /// <param name="request">The current credentials and the new password.</param>
    /// <returns>A secret-free success or refusal code for the bilingual website.</returns>
    [HttpPost("password")]
    public async ValueTask<IActionResult> ChangePasswordAsync([FromBody] WebsitePasswordChangeRequest request)
    {
        this.Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(request.LoginName) || request.LoginName.Length > 10
            || string.IsNullOrEmpty(request.Password) || request.Password.Length > 20)
        {
            return this.Unauthorized(new { error = "login" });
        }

        if (request.NewPassword is not { Length: >= 3 and <= 20 } nextPassword || nextPassword.Any(c => c is < ' ' or > '~'))
        {
            return this.BadRequest(new { error = "password" });
        }

        // Keep persistence work well inside the login server's five-minute reservation lease.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(this.HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var configuration = await configurations.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var authentication = persistence.CreateNewPlayerContext(configuration);
        var account = await authentication.GetAccountByLoginNameAsync(request.LoginName.Trim(), request.Password, cancellationToken).ConfigureAwait(false);
        if (account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        if (!await loginServer.TryReserveAccountAsync(account.LoginName, reservation).ConfigureAwait(false))
        {
            return this.Conflict(new { error = "online" });
        }

        try
        {
            return await this.WritePasswordAsync(configuration, account.LoginName, request.Password, nextPassword, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await loginServer.ReleaseAccountReservationAsync(account.LoginName, reservation).ConfigureAwait(false);
        }
    }

    private async ValueTask<IActionResult> WritePasswordAsync(GameConfiguration configuration, string loginName, string password, string nextPassword, CancellationToken cancellationToken)
    {
        using var context = persistence.CreateNewPlayerContext(configuration);

        // Re-read and authenticate under the reservation: an earlier password change must invalidate old credentials.
        var account = await context.GetAccountByLoginNameAsync(loginName, password, cancellationToken).ConfigureAwait(false);
        if (account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned)
        {
            return this.Unauthorized(new { error = "login" });
        }

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(nextPassword);
        if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
        {
            return this.StatusCode(StatusCodes.Status500InternalServerError, new { error = "server" });
        }

        logger.LogInformation("The account {LoginName} changed its password on the website.", account.LoginName);
        return this.Ok(new { changed = true });
    }
}

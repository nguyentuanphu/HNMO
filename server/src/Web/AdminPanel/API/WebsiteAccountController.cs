// <copyright file="WebsiteAccountController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Reads the player's own saved data through the trusted website bridge. Both an operator API key and the
/// player's password are required; knowing another account's name does not authorize reading it.
/// </summary>
/// <param name="persistence">The account persistence provider.</param>
/// <param name="configurations">The game configuration source.</param>
/// <param name="loginServer">The authoritative account online snapshot.</param>
[ApiController]
[Route("api/website/account")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
public sealed class WebsiteAccountController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> configurations,
    ILoginServer loginServer) : ControllerBase
{
    private const int PageSize = 20;
    private const int MaximumHistory = 1000;

    /// <summary>Reads a password-authenticated account overview without secrets or other accounts' grants.</summary>
    /// <param name="request">The player's credentials and history page.</param>
    /// <returns>A persisted snapshot, or a machine-readable refusal for the bilingual website.</returns>
    [HttpPost("overview")]
    public async ValueTask<IActionResult> OverviewAsync([FromBody] WebsiteAccountRequest request)
    {
        this.Response.Headers.CacheControl = "no-store";
        if (request.Page is < 0 or >= MaximumHistory / PageSize)
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
        var grants = await context.GetLatestCashShopCoinGrantsAsync(account.GetId(), Math.Min(offset + PageSize + 1, MaximumHistory), cancellationToken).ConfigureAwait(false);
        var online = await loginServer.GetSnapshotAsync().ConfigureAwait(false);
        return this.Ok(new
        {
            account.LoginName,
            accountOnline = online.ContainsKey(account.LoginName),
            balances = new { account.WCoinC, account.WCoinP, account.GoblinPoints },
            vipLevel = (int)account.GetAccountAttributeValue(Stats.IsVip, DateTime.UtcNow),
            characters = account.Characters.OrderBy(c => c.CharacterSlot).Select(c => new
            {
                c.Name,
                className = c.CharacterClass?.Name.ValueInNeutralLanguage ?? string.Empty,
                classNameVi = c.CharacterClass?.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? string.Empty,
                level = (int)(c.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.Level.Id)?.Value ?? 0),
                resets = (int)(c.Attributes.FirstOrDefault(a => a.Definition?.Id == Stats.Resets.Id)?.Value ?? 0),
                map = c.CurrentMap?.Number,
            }).ToList(),
            history = grants.Skip(offset).Take(PageSize).Select(g => new
            {
                g.Id,
                coinType = g.CoinType.ToString(),
                g.Amount,
                g.Reason,
                createdAt = DateTime.SpecifyKind(g.CreatedAt, DateTimeKind.Utc),
                pending = g.AppliedAt is null,
            }).ToList(),
            page = request.Page,
            hasMore = offset + PageSize < MaximumHistory && grants.Count > offset + PageSize,
        });
    }
}

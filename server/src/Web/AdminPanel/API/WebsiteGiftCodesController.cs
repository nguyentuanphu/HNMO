// <copyright file="WebsiteGiftCodesController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;

/// <summary>
/// The website's gift codes: the public codes (read-only, no account data), and, for the account page through the
/// web bridge (which holds the API key), the characters of an account and the redemption of a code. Both of these
/// check the account's password; the bridge limits how often a visitor may call them, and the gift codes limit the
/// wrong codes per account.
/// </summary>
[ApiController]
[Route("api/website/gift-codes")]
public sealed class WebsiteGiftCodesController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteGiftCodesController> logger) : ControllerBase
{
    /// <summary>Gets the public gift codes.</summary>
    /// <returns>The JSON; "enabled": false when the plug-in is off.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { } engine)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        return this.Content(GiftCodeStateWriter.WritePublic(engine, DateTime.UtcNow, await this.ItemNamesAsync(cancellationToken).ConfigureAwait(false)), "application/json");
    }

    /// <summary>Gets the characters of an account, after checking its password.</summary>
    /// <param name="request">The login name and password.</param>
    /// <returns>The characters; 401 when the name or password is wrong; 403 when the website may not enter codes.</returns>
    [HttpPost("characters")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> CharactersAsync([FromBody] GiftCodeAccountRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { Configuration.AllowWebsite: true })
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        if (await this.AccountAsync(configuration, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        return this.Ok(new
        {
            loginName = account.LoginName,
            characters = account.Characters.OrderBy(c => c.CharacterSlot).Select(c => new
            {
                name = c.Name,
                level = Level(c),
                resets = Resets(c),
                cls = c.CharacterClass?.Number ?? 0,
                className = c.CharacterClass?.Name.ValueInNeutralLanguage ?? string.Empty,
            }),
        });
    }

    /// <summary>Enters a gift code for a character of an account, after checking its password.</summary>
    /// <param name="request">The login name, password, character and code.</param>
    /// <returns>The result (see <see cref="GiftCodeStateWriter.WriteRedeemAnswer"/>); 401 when the name or password is wrong.</returns>
    [HttpPost("redeem")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> RedeemAsync([FromBody] GiftCodeRedeemRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { Configuration.AllowWebsite: true } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        if (await this.AccountAsync(configuration, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var character = account.Characters.FirstOrDefault(c => string.Equals(c.Name, request.Character, StringComparison.Ordinal));
        if (character is null)
        {
            return this.BadRequest(new { error = "character" });
        }

        var who = new GiftCodeRedeemer(account.LoginName, character.Id.ToString(), character.Name, Level(character), Resets(character), character.CharacterClass?.Number ?? 0);
        var outcome = engine.Redeem(request.Code, who, DateTime.UtcNow, "website");
        if (outcome.Result == GiftCodeResult.Ok)
        {
            logger.LogInformation("Gift codes: {Login} used {Code} on the website for {Character} ({Reference}).", account.LoginName, outcome.Entry?.Code, character.Name, outcome.Entry?.Reference);
            if (outcome.Coins is { } coins)
            {
                await this.GiveCoinsAsync(engine, configuration, coins).ConfigureAwait(false);
            }
        }
        else if (outcome.Result == GiftCodeResult.TooManyTries)
        {
            logger.LogWarning("Gift codes: {Login} entered too many wrong codes on the website.", account.LoginName);
        }

        lock (engine.Store.Lock)
        {
            engine.Store.SaveIfChanged();
        }

        return this.Content(GiftCodeStateWriter.WriteRedeemAnswer(engine, outcome, character.Name, GiftCodesPlugIn.ItemNames(configuration)), "application/json");
    }

    private static int Level(Character character) => (int)(character.Attributes.FirstOrDefault(a => a.Definition == Stats.Level)?.Value ?? 0);

    private static int Resets(Character character) => (int)(character.Attributes.FirstOrDefault(a => a.Definition == Stats.Resets)?.Value ?? 0);

    private async Task GiveCoinsAsync(GiftCodeEngine engine, GameConfiguration configuration, GiftCodePendingCoins coins)
    {
        // A game server of this process gives them (and applies them at once when the player is in the game); without
        // one, the grant is saved and applied when the player opens the cash shop. Coins not saved now are tried
        // again by the game server.
        foreach (var context in GiftCodesPlugIn.KnownContexts())
        {
            if (GiftCodesPlugIn.Get(context) is { } plugIn)
            {
                await plugIn.GiveCoinsAsync(context, coins).ConfigureAwait(false);
                return;
            }
        }

        if (await GiftCodeRewards.GrantCoinsAsync(persistence, configuration, coins, logger).ConfigureAwait(false) != GiftCodeCoinsResult.Failed)
        {
            engine.CoinsGiven(coins.Reference);
        }
    }

    private async Task<Account?> AccountAsync(GameConfiguration configuration, string? loginName, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrEmpty(password) || loginName.Length > 10 || password.Length > 20)
        {
            return null;
        }

        using var context = persistence.CreateNewPlayerContext(configuration);
        var account = await context.GetAccountByLoginNameAsync(loginName.Trim(), password, cancellationToken).ConfigureAwait(false);
        return account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned ? null : account;
    }

    private async Task<GiftCodeEngine?> EngineAsync(CancellationToken cancellationToken)
    {
        var configuration = await PlugInSettings.ReadActiveAsync<GiftCodesConfiguration>(persistence, typeof(GiftCodesPlugIn), () => new GiftCodesConfiguration(), cancellationToken).ConfigureAwait(false);
        return configuration is null ? null : new GiftCodeEngine(configuration, GiftCodeStore.Open(configuration.FileName));
    }

    private async Task<Func<short, short, string?>?> ItemNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return GiftCodesPlugIn.ItemNames(await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The item names for the website's gift codes weren't read.");
            return null;
        }
    }
}

/// <summary>
/// An account's login for the website's gift codes.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
public sealed record GiftCodeAccountRequest(string? LoginName, string? Password);

/// <summary>
/// A gift code entered on the website.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
/// <param name="Character">The character which gets the zen and items.</param>
/// <param name="Code">The code.</param>
public sealed record GiftCodeRedeemRequest(string? LoginName, string? Password, string? Character, string? Code);

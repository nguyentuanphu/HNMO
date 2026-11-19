// <copyright file="WebsiteAccountSafetyController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// The account page's account safety, through the web bridge (which holds the API key and tells the visitor's
/// address): the second password (set, change, clear), the login history (addresses masked), the trade history,
/// and the items of the account's characters and vault with their locks (lock; unlock with the second password,
/// after the wait). Every call checks the account's password and counts as a website login in the history.
/// </summary>
[ApiController]
[Route("api/website/account-safety")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
public sealed class WebsiteAccountSafetyController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILoginServer loginServer,
    IServerProvider serverProvider,
    ILogger<WebsiteAccountSafetyController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Changes the second password of an account: on the live account when it's in a game of this process (so the game
    /// doesn't save the old one over it), else in the database while the login server holds the account.
    /// </summary>
    /// <param name="persistence">The persistence.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="loginServer">The login server.</param>
    /// <param name="contexts">The game contexts of this process.</param>
    /// <param name="login">The login name.</param>
    /// <param name="stored">The new stored value (a hash, or empty to clear it).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>"ok", "online" (in a game this process can't reach) or "missing".</returns>
    public static async Task<string> WritePinAsync(IPersistenceContextProvider persistence, GameConfiguration configuration, ILoginServer loginServer, IEnumerable<IGameContext> contexts, string login, string stored, CancellationToken cancellationToken)
    {
        if (await AccountSafetyPlugIn.FindPlayerAsync(contexts, login).ConfigureAwait(false) is { Account: { } live } player)
        {
            live.VaultPassword = stored;
            AccountSafetyPlugIn.ForgetEntered(player);
            player.IsVaultLocked = AccountSafetyEngine.HasPin(stored);
            await player.SaveProgressAsync().ConfigureAwait(false);
            return "ok";
        }

        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        if (!await loginServer.TryReserveAccountAsync(login, reservation).ConfigureAwait(false))
        {
            return "online";
        }

        try
        {
            using var context = persistence.CreateNewPlayerContext(configuration);
            if (await context.GetAccountByLoginNameAsync(login, cancellationToken).ConfigureAwait(false) is not { } account)
            {
                return "missing";
            }

            account.VaultPassword = stored;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return "ok";
        }
        finally
        {
            await loginServer.ReleaseAccountReservationAsync(login, reservation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Describes where an inventory slot is.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>"equipped", "bag", "extension" or "store".</returns>
    public static string PlaceOf(int slot) => slot switch
    {
        < 12 => "equipped",
        < 76 => "bag",
        < 204 => "extension",
        _ => "store",
    };

    /// <summary>Gets the account's safety state, after checking its password.</summary>
    /// <param name="request">The login.</param>
    /// <returns>The state; 401 when the name or password is wrong; 403 when the plug-in is off.</returns>
    [HttpPost("account")]
    public async Task<IActionResult> AccountAsync([FromBody] SafetyLoginRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await this.AuthenticateAsync(engine, context, request.LoginName, request.Password, request.Address, true, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        return this.Content(await this.StateAsync(engine, account, null, cancellationToken).ConfigureAwait(false), "application/json");
    }

    /// <summary>Sets, changes or clears the second password, after checking the account's password and the old one.</summary>
    /// <param name="request">The login, the old and the new second password (empty clears it).</param>
    /// <returns>The result and the state.</returns>
    [HttpPost("pin")]
    public async Task<IActionResult> PinAsync([FromBody] SafetyPinRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await this.AuthenticateAsync(engine, context, request.LoginName, request.Password, request.Address, false, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        string result;
        if (!engine.Configuration.AllowWebsitePinChange)
        {
            result = "notAllowed";
        }
        else
        {
            var clear = string.IsNullOrEmpty(request.NewPin);
            var newPin = clear ? null : AccountSafetyEngine.NormalizePin(request.NewPin);
            var current = await this.CurrentPinAsync(account).ConfigureAwait(false);
            var check = engine.CheckPin(account.LoginName, current, AccountSafetyEngine.NormalizePin(request.OldPin), DateTime.UtcNow);
            if (!clear && newPin is null)
            {
                result = "invalid";
            }
            else if (check.Result is PinCheckResult.Wrong or PinCheckResult.LockedOut)
            {
                result = check.Result == PinCheckResult.Wrong ? "wrong" : "lockedOut";
            }
            else if (clear && check.Result == PinCheckResult.NoPin)
            {
                result = "noPin";
            }
            else
            {
                var stored = clear ? string.Empty : AccountSafetyEngine.HashPin(newPin!);
                result = await WritePinAsync(persistence, configuration, loginServer, this.Contexts(), account.LoginName, stored, cancellationToken).ConfigureAwait(false);
                if (result == "ok")
                {
                    engine.PinChanged(account.LoginName, DateTime.UtcNow);
                    account.VaultPassword = stored;
                    logger.LogInformation("Account safety: {Login} {Action} the second password on the website.", account.LoginName, clear ? "cleared" : "set");
                }
            }
        }

        Save(engine);
        return this.Content(await this.StateAsync(engine, account, result, cancellationToken).ConfigureAwait(false), "application/json");
    }

    /// <summary>Locks an item of the account, or asks its unlock (with the second password).</summary>
    /// <param name="request">The login, the item's id, whether to unlock, and the second password for an unlock.</param>
    /// <returns>The result and the state.</returns>
    [HttpPost("item")]
    public async Task<IActionResult> ItemAsync([FromBody] SafetyItemRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await this.AuthenticateAsync(engine, context, request.LoginName, request.Password, request.Address, false, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var found = Items(account).FirstOrDefault(i => i.Item.GetId() == request.ItemId);
        var now = DateTime.UtcNow;
        string result;
        string? until = null;
        if (found.Item is null)
        {
            result = "noItem";
        }
        else if (!request.Unlock)
        {
            result = engine.Lock(account.LoginName, request.ItemId, AccountSafetyPlugIn.Describe(found.Item), found.Character, now).ToString();
        }
        else
        {
            var check = engine.CheckPin(account.LoginName, await this.CurrentPinAsync(account).ConfigureAwait(false), AccountSafetyEngine.NormalizePin(request.Pin), now);
            if (check.Result is PinCheckResult.Wrong or PinCheckResult.LockedOut)
            {
                result = check.Result == PinCheckResult.Wrong ? "wrong" : "lockedOut";
            }
            else
            {
                var (unlock, at) = engine.Unlock(account.LoginName, request.ItemId, now);
                result = unlock.ToString();
                until = Iso(at);
            }
        }

        Save(engine);
        await this.TellPlayerAsync(account.LoginName).ConfigureAwait(false);
        return this.Content(await this.StateAsync(engine, account, result, cancellationToken, until).ConfigureAwait(false), "application/json");
    }

    private static IEnumerable<(Item Item, string Character, string Place, int Slot)> Items(Account account)
    {
        foreach (var character in account.Characters.OrderBy(c => c.CharacterSlot))
        {
            foreach (var item in character.Inventory?.Items ?? Enumerable.Empty<Item>())
            {
                yield return (item, character.Name, PlaceOf(item.ItemSlot), item.ItemSlot);
            }
        }

        foreach (var item in account.Vault?.Items ?? Enumerable.Empty<Item>())
        {
            yield return (item, string.Empty, "vault", item.ItemSlot);
        }
    }

    private static string? Iso(DateTime? utc) => utc is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : null;

    private static void Save(AccountSafetyEngine engine)
    {
        lock (engine.Store.Lock)
        {
            engine.Store.SaveIfChanged();
        }
    }

    private IEnumerable<IGameContext> Contexts() =>
        serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).Concat(AccountSafetyPlugIn.KnownContexts()).Distinct();

    private async Task<string> CurrentPinAsync(Account account) =>
        await AccountSafetyPlugIn.FindPlayerAsync(this.Contexts(), account.LoginName).ConfigureAwait(false) is { Account: { } live } ? live.VaultPassword : account.VaultPassword;

    private async Task TellPlayerAsync(string login)
    {
        try
        {
            if (await AccountSafetyPlugIn.FindPlayerAsync(this.Contexts(), login).ConfigureAwait(false) is { } player && AccountSafetyPlugIn.Get(player.GameContext) is { } safety)
            {
                await safety.SendStateAsync(player, false).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The game wasn't told about a changed item lock.");
        }
    }

    private async Task<Account?> AuthenticateAsync(AccountSafetyEngine engine, IPlayerContext context, string? loginName, string? password, string? address, bool recordSuccess, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrEmpty(password) || loginName.Length > 10 || password.Length > 20)
        {
            return null;
        }

        var login = loginName.Trim();
        var account = await context.GetAccountByLoginNameAsync(login, password, cancellationToken).ConfigureAwait(false);
        var ok = account is not null && !account.IsBot && account.State is not (AccountState.Banned or AccountState.TemporarilyBanned);
        if (!ok || recordSuccess)
        {
            engine.RecordLogin(login, string.IsNullOrWhiteSpace(address) ? null : address.Trim(), "website", ok, DateTime.UtcNow);
            Save(engine);
        }

        return ok ? account : null;
    }

    private async Task<string> StateAsync(AccountSafetyEngine engine, Account account, string? result, CancellationToken cancellationToken, string? until = null)
    {
        var configuration = engine.Configuration;
        var now = DateTime.UtcNow;
        var locks = engine.Locks(account.LoginName, now).ToDictionary(l => l.ItemId);
        var online = await AccountSafetyPlugIn.FindPlayerAsync(this.Contexts(), account.LoginName).ConfigureAwait(false) is not null;
        var current = await this.CurrentPinAsync(account).ConfigureAwait(false);
        var state = new
        {
            v = 1,
            loginName = account.LoginName,
            result,
            until,
            online,
            hasPin = AccountSafetyEngine.HasPin(current),
            pinChangedAt = Iso(engine.PinChangedAt(account.LoginName)),
            lockedOutUntil = Iso(engine.LockedOutUntil(account.LoginName, now)),
            options = new
            {
                vault = configuration.RequireForVault,
                trade = configuration.RequireForTrade,
                store = configuration.RequireForPersonalStore,
                valuable = configuration.RequireForValuableItems,
                valuableLevel = configuration.ValuableMinimumLevel,
                pinChange = configuration.AllowWebsitePinChange,
                itemLock = configuration.ItemLockEnabled,
                waitHours = configuration.UnlockWaitHours,
                maxTries = configuration.MaxWrongTries,
                lockoutMinutes = configuration.LockoutMinutes,
                loginDays = configuration.LoginHistoryDays,
                tradeDays = configuration.TradeHistoryDays,
            },
            logins = engine.Logins(account.LoginName, configuration.LoginsShown)
                .Select(l => new { at = Iso(l.AtUtc), address = AccountSafetyEngine.Mask(l.Address), source = l.Source, success = l.Success }).ToList(),
            trades = engine.Trades(account.LoginName, configuration.TradesShown)
                .Select(t => new { at = Iso(t.AtUtc), kind = t.Kind, character = t.Character, partner = t.Partner, gave = t.Gave, got = t.Got, zenGave = t.ZenGave, zenGot = t.ZenGot }).ToList(),
            items = Items(account)
                .Select(i => (i, id: i.Item.GetId(), locked: locks.GetValueOrDefault(i.Item.GetId())))
                .Select(x => new
                {
                    id = x.id,
                    character = x.i.Character,
                    place = x.i.Place,
                    slot = x.i.Slot,
                    name = AccountSafetyPlugIn.Describe(x.i.Item),
                    locked = x.locked is not null,
                    unlockAt = Iso(x.locked?.UnlockAtUtc),
                    valuable = engine.IsValuable(x.i.Item.Level, x.i.Item.IsExcellent(), x.i.Item.IsAncient()),
                })
                .ToList(),
        };
        return JsonSerializer.Serialize(state, JsonOptions);
    }

    private async Task<AccountSafetyEngine?> EngineAsync(CancellationToken cancellationToken)
    {
        var configuration = await PlugInSettings.ReadActiveAsync<AccountSafetyConfiguration>(persistence, typeof(AccountSafetyPlugIn), () => new AccountSafetyConfiguration(), cancellationToken).ConfigureAwait(false);
        return configuration is null ? null : new AccountSafetyEngine(configuration, AccountSafetyStore.Open(configuration.FileName));
    }
}

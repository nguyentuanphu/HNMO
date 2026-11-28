// <copyright file="LoginAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using HNMO.GameLogic.PlugIns.Moderation;
using HNMO.GameLogic.Views.Login;
using HNMO.Interfaces;
using Nito.AsyncEx;

/// <summary>
/// Action to log in a player to the game.
/// </summary>
public class LoginAction
{
    private static readonly ConditionalWeakTable<IGameServerContext, ConcurrentDictionary<string, AsyncLock>> LoginGates = new();

    private static int _templateCounter;

    private readonly BanManagerService? _banService;

    /// <summary>Initializes a new instance of the <see cref="LoginAction"/> class.</summary>
    public LoginAction()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LoginAction"/> class with an explicit moderation store.</summary>
    /// <param name="banService">Isolated moderation service for testing.</param>
    internal LoginAction(BanManagerService banService)
    {
        this._banService = banService;
    }

    private BanManagerService BanService => this._banService ?? BanManagerService.Default;

    /// <summary>
    /// Logins the specified player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public async ValueTask LoginAsync(Player player, string username, string password)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());

        // Password guessing: too many wrong passwords lock the account (or address) for a while.
        var address = (player as IHasIpAddress)?.IpAddress;
        if (await this.RefuseIfModerationBlockedAsync(player, username, address, null).ConfigureAwait(false))
        {
            return;
        }

        if (LoginAttemptGuard.IsLocked(username, address))
        {
            player.Logger.LogWarning("Login of [{Username}] refused: too many wrong passwords recently.", username);
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionClosed3Fails)).ConfigureAwait(false);
            return;
        }

        await MirrorSharedAccountAsync(player, username).ConfigureAwait(false);
        var state = await this.AuthenticateAsync(player, username, password).ConfigureAwait(false);
        if (state is null)
        {
            PlugIns.AccountSafety.AccountSafetyPlugIn.RecordFailedLogin(player, username);
            if (LoginAttemptGuard.RecordFailure(username, address))
            {
                player.Logger.LogWarning("Logins of [{Username}] (address {Address}) locked for {Minutes} minutes after too many wrong passwords.", username, address, LoginAttemptGuard.LockTime.TotalMinutes);
            }

            return;
        }

        LoginAttemptGuard.RecordSuccess(username);

        if (!await this.ValidateAccountStateAsync(player, state.Value, username).ConfigureAwait(false))
        {
            return;
        }

        var (success, account) = await this.TryEstablishSessionAsync(player, username, validateAccountStateAfterLoad: false).ConfigureAwait(false);
        await this.CompleteLoginAsync(player, username, success, account).ConfigureAwait(false);
    }

    /// <summary>
    /// Logs in the specified player using a one-time website login ticket.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="username">The username.</param>
    /// <param name="ticket">The one-time login ticket.</param>
    /// <param name="ticketStore">Optional ticket store (defaults to <see cref="WebLoginTicketStore.Shared"/>).</param>
    public async ValueTask LoginWithTicketAsync(Player player, string username, string ticket, WebLoginTicketStore? ticketStore = null)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());

        var address = (player as IHasIpAddress)?.IpAddress;
        if (await this.RefuseIfModerationBlockedAsync(player, username, address, null).ConfigureAwait(false))
        {
            return;
        }

        if (LoginAttemptGuard.IsLocked(username, address))
        {
            player.Logger.LogWarning("Ticket login of [{Username}] refused: too many wrong attempts recently.", username);
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionClosed3Fails)).ConfigureAwait(false);
            return;
        }

        // Without a given store, the website's tickets may come from the shared store (both seasons' programs).
        var redeemed = ticketStore is not null
            ? ticketStore.TryRedeem(username, ticket)
            : await RedeemWebTicketAsync(player, username, ticket).ConfigureAwait(false);
        if (!redeemed)
        {
            player.Logger.LogInformation("Invalid, expired, or already used login ticket for [{Username}].", username);
            PlugIns.AccountSafety.AccountSafetyPlugIn.RecordFailedLogin(player, username);
            if (LoginAttemptGuard.RecordFailure(username, address))
            {
                player.Logger.LogWarning("Logins of [{Username}] (address {Address}) locked for {Minutes} minutes after too many failed attempts.", username, address, LoginAttemptGuard.LockTime.TotalMinutes);
            }

            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.InvalidPassword)).ConfigureAwait(false);
            return;
        }

        LoginAttemptGuard.RecordSuccess(username);

        await MirrorSharedAccountAsync(player, username).ConfigureAwait(false);
        var (success, account) = await this.TryEstablishSessionAsync(player, username, validateAccountStateAfterLoad: true).ConfigureAwait(false);
        await this.CompleteLoginAsync(player, username, success, account).ConfigureAwait(false);
    }

    private static async ValueTask<bool> RedeemWebTicketAsync(Player player, string username, string ticket)
    {
        try
        {
            return await WebLoginTicketStore.Shared.TryRedeemAsync(username, ticket, HNMO.Persistence.SharedLoginTicketStore.Current).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't check the website login ticket of [{Username}].", username);
            return false;
        }
    }

    /// <summary>
    /// In a Season 9 program, copies the shared account (login, state, coins) into this season's database first, so
    /// one sign-up plays both seasons (<see cref="Seasons.SeasonSharedAccounts"/>). Nothing happens in Season 6.3.
    /// </summary>
    private static async ValueTask MirrorSharedAccountAsync(Player player, string username)
    {
        if (HNMO.Persistence.SharedAccountStore.Current is not { } store)
        {
            return;
        }

        try
        {
            await Seasons.SeasonSharedAccounts.MirrorAsync(
                store,
                () => player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration),
                username).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The shared database is out of reach: the login goes on with what this season's database has.
            player.Logger.LogWarning(ex, "Couldn't read the shared account [{Username}].", username);
        }
    }

    private static async ValueTask ReleaseLoginServerRegistrationAsync(Player player)
    {
        if (player.ReleaseLoginServerRegistration() is not { } loginName
            || player.GameContext is not IGameServerContext gameServerContext)
        {
            return;
        }

        try
        {
            await gameServerContext.LoginServer.LogOffAsync(loginName, gameServerContext.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't log off [{Username}] at the login server after the failed login.", loginName);
        }
    }

    /// <summary>
    /// Reserves a place for the player on the game server, see <see cref="IGameContext.TryReserveLoginPlace"/>.
    /// </summary>
    /// <param name="player">The player which logs in. It's not logged in yet, so it isn't counted.</param>
    /// <param name="gameServerContext">The game server context.</param>
    /// <param name="username">The username.</param>
    /// <param name="replacedPlayer">The logged in player whose place the player takes over, e.g. its offline player.</param>
    /// <returns>The reservation, or <c>null</c> when the game server is full.</returns>
    /// <remarks>
    /// Only logged in players and logins in progress take a place, so connections which stay at the
    /// login screen don't.
    /// </remarks>
    private static IDisposable? TryReservePlace(Player player, IGameServerContext gameServerContext, string username, Player? replacedPlayer)
    {
        var maximumPlayers = gameServerContext.ServerConfiguration.MaximumPlayers;
        var (reservation, occupiedPlaces) = gameServerContext.TryReserveLoginPlace(maximumPlayers, replacedPlayer);
        if (reservation is not null)
        {
            return reservation;
        }

        player.Logger.LogInformation(
            "Login of [{Username}] refused, because the game server is full: {OccupiedPlaces} of {MaximumPlayers} players are logged in or logging in.",
            username,
            occupiedPlaces,
            maximumPlayers);
        return null;
    }

    private static bool TryAdmitLicensed(Player player, string username)
    {
        var currentMaximum = LicensedPlayerLimit.ReevaluateAndEnforce(DateTime.UtcNow);
        if (LicensedPlayerLimit.TryAdmit(player, username))
        {
            return true;
        }

        if (FolderSeat.Status is { IsBlocked: true } seat)
        {
            player.Logger.LogWarning(
                "Login of [{Username}] refused: this HNMO folder has no seat on this computer (id {MachineId}); the licence lets {Allowed} HNMO folder(s) run at once and these already do: {Holders}.",
                username,
                seat.MachineId,
                seat.Allowed,
                string.Join("; ", seat.OtherHolders));
            return false;
        }

        player.Logger.LogWarning(
            "Login of [{Username}] refused: the licensed limit of {Maximum} players at once is reached (license.hnmo raises it).",
            username,
            currentMaximum);
        return false;
    }

    /// <summary>
    /// Refuses the login, because the game server is full. The connection stays at the login screen.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="context">The context of the state change to <see cref="PlayerState.Authenticated"/>, which doesn't happen.</param>
    /// <returns>The result of the session establishment.</returns>
    private static async ValueTask<(bool Success, Account? Account)> RefuseBecauseFullAsync(Player player, StateMachine.StateChangeContext context)
    {
        context.Allowed = false;
        await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ServerIsFull)).ConfigureAwait(false);
        return (false, null);
    }

    private static AsyncLock GetLoginGate(IGameServerContext gameServerContext, string username)
    {
        var serverGates = LoginGates.GetValue(
            gameServerContext,
            static _ => new ConcurrentDictionary<string, AsyncLock>(StringComparer.OrdinalIgnoreCase));
        return serverGates.GetOrAdd(username, static _ => new AsyncLock());
    }

    private static async ValueTask<bool> TryAcquireLoginServerRegistrationAsync(Player player, IGameServerContext gameServerContext, string username)
    {
        using var gate = await GetLoginGate(gameServerContext, username).LockAsync().ConfigureAwait(false);
        if (player.PlayerState.CurrentState.IsDisconnectedOrFinished())
        {
            return false;
        }

        if (await gameServerContext.LoginServer.TryLoginAsync(username, gameServerContext.Id).ConfigureAwait(false))
        {
            // Remembered while the state change is in progress, which a disconnect waits for.
            player.SetLoginServerRegistration(username);
            return true;
        }

        // Safety net: if the login server says the account is logged in on THIS game server,
        // verify under the per-account login gate whether any live player (or active offline player)
        // on this server owns it. If none does, the registration is orphaned and can be safely freed.
        var snapshot = await gameServerContext.LoginServer.GetSnapshotAsync().ConfigureAwait(false);
        if (snapshot is not null
            && snapshot.TryGetValue(username, out var registeredServerId)
            && registeredServerId == gameServerContext.Id
            && !gameServerContext.OfflinePlayerManager.IsActive(username))
        {
            var players = await gameServerContext.GetPlayersAsync().ConfigureAwait(false);
            var hasLiveOwner = players.Any(p =>
                p != player
                && (string.Equals(p.Account?.LoginName, username, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.LoginServerRegistration, username, StringComparison.OrdinalIgnoreCase)));
            if (!hasLiveOwner)
            {
                player.Logger.LogWarning(
                    "Account [{Username}] was marked logged in on server {ServerId}, but no live player owns it. Releasing the stale login registration.",
                    username,
                    gameServerContext.Id);
                await gameServerContext.LoginServer.LogOffAsync(username, gameServerContext.Id).ConfigureAwait(false);
                if (await gameServerContext.LoginServer.TryLoginAsync(username, gameServerContext.Id).ConfigureAwait(false))
                {
                    player.SetLoginServerRegistration(username);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Notifies the game servers and external systems, that somebody tried to log into an account which is already logged in.
    /// </summary>
    private static async ValueTask NotifyAlreadyLoggedInAsync(IGameServerContext gameServerContext, string username)
    {
        await gameServerContext.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext.Id, username).ConfigureAwait(false);
        await gameServerContext.EventPublisher.GameEventAsync(new AccountLoginBlockedEvent(gameServerContext.Id, DateTime.UtcNow, username)).ConfigureAwait(false);
    }

    private async ValueTask CompleteLoginAsync(Player player, string username, bool success, Account? account)
    {
        if (success && account is { })
        {
            if (player.PlayerState.CurrentState.IsDisconnectedOrFinished())
            {
                await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
                return;
            }

            try
            {
                await this.FinishLoginAsync(player, username, account).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Failed to finish login for [{Username}].", username);
                await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);

                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<AccountState?> AuthenticateAsync(Player player, string username, string password)
    {
        try
        {
            var state = await player.PersistenceContext.AuthenticateAsync(username, password).ConfigureAwait(false);
            if (state is null)
            {
                player.Logger.LogInformation("Account not found or invalid password, username: [{Username}].", username);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.InvalidPassword)).ConfigureAwait(false);
            }

            return state;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Login Failed.");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return null;
        }
    }

    private async ValueTask<bool> RefuseIfModerationBlockedAsync(Player player, string username, string? address, string? deviceId)
    {
        var active = this.BanService.FindActiveLoginBlock(username, address, deviceId, DateTime.UtcNow);
        if (active is null)
        {
            return false;
        }

        await player.InvokeViewPlugInAsync<IHnmoBanNoticeViewPlugIn>(v => v.SendBanNoticeAsync(BanManagerService.BuildNoticeJson(active))).ConfigureAwait(false);
        var code = active.ExpiresAtUtc.HasValue ? LoginResult.TemporaryBlocked : LoginResult.AccountBlocked;
        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(code)).ConfigureAwait(false);
        return true;
    }

    private async ValueTask<bool> ValidateAccountStateAsync(Player player, AccountState state, string? username = null)
    {
        var now = DateTime.UtcNow;
        if (state == AccountState.Banned)
        {
            // A permanent ban never ends because an older timed ban ran out: only the temporary state below does.
            var active = username is null ? null : this.BanService.FindActiveLoginBlock(username, null, null, now);
            if (active is not null)
            {
                await player.InvokeViewPlugInAsync<IHnmoBanNoticeViewPlugIn>(v => v.SendBanNoticeAsync(BanManagerService.BuildNoticeJson(active))).ConfigureAwait(false);
            }

            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountBlocked)).ConfigureAwait(false);
            return false;
        }

        if (state == AccountState.TemporarilyBanned)
        {
            var active = username is null ? null : this.BanService.FindActiveLoginBlock(username, null, null, now);
            if (active is null && username is not null && this.BanService.HasExpiredAccountBanOnly(username, now))
            {
                return true;
            }

            if (active is not null)
            {
                await player.InvokeViewPlugInAsync<IHnmoBanNoticeViewPlugIn>(v => v.SendBanNoticeAsync(BanManagerService.BuildNoticeJson(active))).ConfigureAwait(false);
            }

            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.TemporaryBlocked)).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async ValueTask<(bool Success, Account? Account)> TryEstablishSessionAsync(Player player, string username, bool validateAccountStateAfterLoad)
    {
        // Kept until the state change of the context below is done, see IGameContext.TryReserveLoginPlace.
        IDisposable? reservedPlace = null;
        try
        {
            var previousState = player.PlayerState.CurrentState;
            await using var context = await player.PlayerState.TryBeginAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
            if (!context.Allowed)
            {
                if (!player.PlayerState.CurrentState.IsDisconnectedOrFinished())
                {
                    await this.HandleAlreadyConnectedAsync(player, username).ConfigureAwait(false);
                }

                return (false, null);
            }

            // Default to disallowed so any exception or early return never advances a failed login
            // to PlayerState.Authenticated; only set to true once the session is established.
            context.Allowed = false;

            // In-game states also allow transitioning to Authenticated (for LogoutAction), so guard
            // against a duplicate login packet overwriting an active session's registration.
            if (previousState != PlayerState.LoginScreen || player.Account is not null || player.LoginServerRegistration is not null)
            {
                await this.HandleAlreadyConnectedAsync(player, username).ConfigureAwait(false);
                return (false, null);
            }

            if (player.GameContext is not IGameServerContext gameServerContext)
            {
                return (false, null);
            }

            if (player.GameContext.OfflinePlayerManager.TryGetPlayer(username, out var offlinePlayer))
            {
                // The player takes over the place of its offline player. It needs a free place only when the
                // offline player isn't logged in anymore, e.g. because its session ended in the meantime.
                // The place is reserved before the offline session gets stopped, so it's kept when it fails.
                reservedPlace = TryReservePlace(player, gameServerContext, username, offlinePlayer);
                if (reservedPlace is null)
                {
                    return await RefuseBecauseFullAsync(player, context).ConfigureAwait(false);
                }

                var isTemplateOffline = offlinePlayer!.IsTemplatePlayer;
                if (!isTemplateOffline)
                {
                    if (!await TryAcquireLoginServerRegistrationAsync(player, gameServerContext, username).ConfigureAwait(false))
                    {
                        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);
                        await NotifyAlreadyLoggedInAsync(gameServerContext, username).ConfigureAwait(false);
                        return (false, null);
                    }
                }

                // Checked before the offline session is stopped, so that it keeps running when the account is rejected.
                if (offlinePlayer.Account is { } runningAccount
                    && !await this.CheckJoinRequirementsAsync(player, runningAccount, gameServerContext).ConfigureAwait(false))
                {
                    await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
                    return (false, null);
                }

                // The folder's licensed player limit, before the offline session is stopped (see LicensedPlayerLimit).
                if (!offlinePlayer.IsTemplatePlayer && offlinePlayer.Account is not { IsBot: true } && !TryAdmitLicensed(player, username))
                {
                    return await RefuseBecauseFullAsync(player, context).ConfigureAwait(false);
                }

                var offlineAccount = await this.HandleOfflineSessionHandoverAsync(player, username).ConfigureAwait(false);
                if (offlineAccount is null)
                {
                    await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
                    return (false, null);
                }

                context.Allowed = true;
                return (true, offlineAccount);
            }

            // The account is reserved at the login server BEFORE it's loaded. The old session of an
            // account saves its progress and only then logs off (see Player.DisconnectAsync), so a
            // reservation which succeeds guarantees that the load below sees that save. Loading first
            // let a quick reconnect (the client resumes about 2 seconds after a lost connection) load
            // the account while the old session was still saving, and reserve it right after the old
            // session logged off: two diverging copies of the character, and the database ended up
            // with both of them - duplicated items, and items on top of each other.
            var reserved = await TryAcquireLoginServerRegistrationAsync(player, gameServerContext, username).ConfigureAwait(false);
            if (reserved)
            {
                // A full game server refuses the login before the account gets loaded. When the account
                // couldn't be reserved, it's loaded first, so that an account which is logged in
                // somewhere else is still reported as such (unless it's a template account).
                reservedPlace = TryReservePlace(player, gameServerContext, username, null);
                if (reservedPlace is null)
                {
                    return await RefuseBecauseFullAsync(player, context).ConfigureAwait(false);
                }
            }

            var loadedAccount = await player.PersistenceContext.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
            if (loadedAccount is null)
            {
                await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
                player.Logger.LogError("Failed to load account {Username} after authentication.", username);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                return (false, null);
            }

            if (!await this.CheckJoinRequirementsAsync(player, loadedAccount, gameServerContext).ConfigureAwait(false))
            {
                await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
                return (false, null);
            }

            if (validateAccountStateAfterLoad
                && !await this.ValidateAccountStateAsync(player, loadedAccount.State, loadedAccount.LoginName).ConfigureAwait(false))
            {
                await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
                context.Allowed = false;
                return (false, null);
            }

            if (loadedAccount.IsTemplate)
            {
                // Template accounts may be logged in several times at once, so they never hold the reservation.
                await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
            }

            if (!reserved && !loadedAccount.IsTemplate)
            {
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);
                await NotifyAlreadyLoggedInAsync(gameServerContext, username).ConfigureAwait(false);
                return (false, null);
            }

            reservedPlace ??= TryReservePlace(player, gameServerContext, username, null);
            if (reservedPlace is null)
            {
                return await RefuseBecauseFullAsync(player, context).ConfigureAwait(false);
            }

            // The folder's licensed player limit (see LicensedPlayerLimit); bots and templates don't count.
            if (!loadedAccount.IsBot && !loadedAccount.IsTemplate && !TryAdmitLicensed(player, username))
            {
                return await RefuseBecauseFullAsync(player, context).ConfigureAwait(false);
            }

            context.Allowed = true;
            return (true, loadedAccount);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error during login through login server.");
            await ReleaseLoginServerRegistrationAsync(player).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return (false, null);
        }
        finally
        {
            // After the state change: the context is disposed when the try block is left.
            reservedPlace?.Dispose();
        }
    }

    /// <summary>
    /// Checks if the account fulfills the <see cref="IGameServerContext.JoinRequirements"/> of the game server,
    /// and shows the login result to the player if it doesn't.
    /// </summary>
    /// <remarks>
    /// Game masters can join every game server, so that they can look after the players.
    /// </remarks>
    /// <returns><c>true</c>, if the account may join the game server.</returns>
    private async ValueTask<bool> CheckJoinRequirementsAsync(Player player, Account account, IGameServerContext gameServerContext)
    {
        if (account.State is AccountState.GameMaster or AccountState.GameMasterInvisible)
        {
            return true;
        }

        if (account.GetFirstUnfulfilledRequirement(gameServerContext.JoinRequirements, DateTime.UtcNow) is not { } requirement)
        {
            return true;
        }

        player.Logger.LogInformation("Account {Username} doesn't fulfill the join requirement [{Requirement}] of game server {ServerId}.", account.LoginName, requirement, gameServerContext.Id);

        // When the account has an expired grant of the attribute, its subscription is over.
        // Without any grant of the attribute, the account never had a subscription.
        var result = account.AttributeGrants?.Any(grant => grant.Attribute == requirement.Attribute) is true
            ? LoginResult.SubscriptionTermOver
            : LoginResult.NoChargeInfo;
        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(result)).ConfigureAwait(false);
        return false;
    }

    private async ValueTask<Account?> HandleOfflineSessionHandoverAsync(Player player, string username)
    {
        player.Logger.LogInformation("Account {Username} has an active offline session. Stopping it and reloading account data.", username);

        try
        {
            await player.GameContext.OfflinePlayerManager.StopAsync(username).ConfigureAwait(false);
            var account = await player.PersistenceContext.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
            if (account is null)
            {
                player.Logger.LogError("Failed to reload account {Username} after stopping offline session.", username);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            }

            return account;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Failed to reload account {Username} after stopping offline session.", username);
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return null;
        }
    }

    private async ValueTask HandleAlreadyConnectedAsync(Player player, string username)
    {
        var result = player.LoginResultOverride ?? LoginResult.AccountAlreadyConnected;
        player.LoginResultOverride = null;
        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(result)).ConfigureAwait(false);
        if (player.GameContext is IGameServerContext gameServerContext)
        {
            await NotifyAlreadyLoggedInAsync(gameServerContext, username).ConfigureAwait(false);
        }
    }

    private async ValueTask FinishLoginAsync(Player player, string username, Account account)
    {
        await player.SetAccountAsync(account).ConfigureAwait(false);
        player.Logger.LogDebug("Login successful, username: [{Username}].", username);

        if (player.IsTemplatePlayer)
        {
            foreach (var character in account.Characters)
            {
                var counter = Interlocked.Increment(ref _templateCounter);
                character.Name = $"_{counter}";
            }
        }

        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.Ok)).ConfigureAwait(false);
    }
}
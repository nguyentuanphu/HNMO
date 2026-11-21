// <copyright file="WebsiteMissionActions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Threading;
using HNMO.GameLogic.Offline;
using HNMO.Interfaces;
using HNMO.Persistence;

/// <summary>Routes account-page actions to the authoritative engine and the existing live reward rules.</summary>
/// <param name="context">The game context which owns the mission state.</param>
public sealed class WebsiteMissionActions(IGameContext context)
{
    /// <summary>Reads one verified subject without altering the live mission state.</summary>
    /// <param name="subject">The subject stamped by the authenticated website API.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The board or an availability status.</returns>
    public async ValueTask<WebsiteMissionsReply> ReadAsync(WebsiteMissionSubject subject, CancellationToken cancellationToken)
    {
        if (context.Configuration.GetId() != subject.ConfigurationId)
        {
            return new("wrong-world", null, false);
        }

        if (!await this.OwnsCharacterAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            return new("missing", null, false);
        }

        if (MissionsPlugIn.Get(context) is not { } missions)
        {
            return new("off", null, false);
        }

        var player = await this.FindLivePlayerAsync(subject, cancellationToken).ConfigureAwait(false);
        return this.Board(missions, subject, player is not null);
    }

    /// <summary>Collects through the same live-character rules as the game, serialized with player persistence.</summary>
    /// <param name="subject">The verified subject.</param>
    /// <param name="action">Mission, streak, calendar or invite.</param>
    /// <param name="argument">The mission identifier or invitation code.</param>
    /// <param name="cancellationToken">The cancellation token before starting the action.</param>
    /// <returns>The result and refreshed board.</returns>
    public async ValueTask<WebsiteMissionsReply> ClaimAsync(WebsiteMissionSubject subject, string action, string argument, CancellationToken cancellationToken)
    {
        var current = await this.ReadAsync(subject, cancellationToken).ConfigureAwait(false);
        if (current.Status != "ok")
        {
            return current;
        }

        if (action is not ("mission" or "streak" or "calendar" or "invite") || argument.Length > 64)
        {
            return new("invalid", current.BoardJson, current.CharacterOnline);
        }

        var player = await this.FindLivePlayerAsync(subject, cancellationToken).ConfigureAwait(false);
        if (player is null)
        {
            return current with { Status = "offline", CharacterOnline = false };
        }

        if (MissionsPlugIn.Get(context) is not { } missions)
        {
            return new("off", null, false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        async ValueTask<WebsiteMissionsReply> ClaimAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The character may have changed while the request waited for the player lock.
            if (!player.IsConnected || player.SelectedCharacter?.Id != subject.CharacterId || player.Account?.GetId() != subject.AccountId)
            {
                return new WebsiteMissionsReply("offline", null, false);
            }

            var result = action switch
            {
                "mission" => (await missions.ClaimMissionAsync(player, argument).ConfigureAwait(false)).ToString(),
                "streak" => (await missions.ClaimStreakAsync(player).ConfigureAwait(false)).ToString(),
                "calendar" => (await missions.ClaimCalendarAsync(player).ConfigureAwait(false)).ToString(),
                _ => (await missions.EnterInviteAsync(player, argument).ConfigureAwait(false)).ToString(),
            };
            return this.Board(missions, subject, true, new MissionBoardEvent(action, result));
        }

        return await player.RunPersistenceExclusiveAsync(ClaimAsync, cancellationToken).ConfigureAwait(false);
    }

    private WebsiteMissionsReply Board(MissionsPlugIn missions, WebsiteMissionSubject subject, bool online, MissionBoardEvent? boardEvent = null)
    {
        var board = WebsiteMissionSnapshot.Read(missions.GetEngine(), subject.CharacterId.ToString(), subject.LoginName, context.Configuration, DateTime.UtcNow, boardEvent).ToJsonString();
        return board.Length <= 131072 ? new("ok", board, online) : new("too-large", null, online);
    }

    private async ValueTask<bool> OwnsCharacterAsync(WebsiteMissionSubject subject, CancellationToken cancellationToken)
    {
        if (subject.LoginName is not { Length: > 0 and <= 10 } || subject.AccountId == Guid.Empty || subject.CharacterId == Guid.Empty)
        {
            return false;
        }

        using var persistence = context.PersistenceContextProvider.CreateNewPlayerContext(context.Configuration);
        var account = await persistence.GetAccountByLoginNameAsync(subject.LoginName, cancellationToken).ConfigureAwait(false);
        return account is { IsBot: false } && account.State is not (AccountState.Banned or AccountState.TemporarilyBanned)
            && account.GetId() == subject.AccountId && account.Characters.Any(c => c.GetId() == subject.CharacterId);
    }

    private async ValueTask<Player?> FindLivePlayerAsync(WebsiteMissionSubject subject, CancellationToken cancellationToken)
        => (await context.GetPlayersAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(p => p.IsConnected && p is not OfflinePlayer && !p.IsTemplatePlayer
            && p.Account?.GetId() == subject.AccountId && p.SelectedCharacter?.Id == subject.CharacterId);
}

// <copyright file="WebsiteAccountPresence.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.Extensions.Logging;

/// <summary>Reads only the authenticated account's character presence, with a bounded best-effort server query.</summary>
internal static class WebsiteAccountPresence
{
    /// <summary>Reads current presence without treating an unavailable game server as proof that a character is offline.</summary>
    /// <param name="account">The authenticated account.</param>
    /// <param name="accounts">The login server's current account/server snapshot.</param>
    /// <param name="servers">The known server channels.</param>
    /// <param name="logger">The diagnostic logger.</param>
    /// <param name="cancellationToken">The request cancellation.</param>
    /// <returns>True/false for confirmed presence, or null when the live server cannot be checked.</returns>
    internal static async ValueTask<IReadOnlyDictionary<Guid, bool?>> ReadAsync(
        Account account,
        IReadOnlyDictionary<string, byte> accounts,
        IServerProvider servers,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var connected = accounts.TryGetValue(account.LoginName, out var serverId);
        var result = account.Characters.ToDictionary(c => c.GetId(), _ => connected ? (bool?)null : false);
        if (!connected || servers.Servers.FirstOrDefault(s => s.Type == ServerType.GameServer && s.Id == serverId) is not IGameServerAdministration server)
        {
            return result;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            foreach (var character in account.Characters)
            {
                var live = await server.GetPlayerAsync(character.Name, timeout.Token).AsTask().WaitAsync(timeout.Token).ConfigureAwait(false);
                if (live is null)
                {
                    result[character.GetId()] = false;
                }
                else if (live.CharacterId == character.GetId() && string.Equals(live.Account, account.LoginName, StringComparison.OrdinalIgnoreCase))
                {
                    result[character.GetId()] = true;
                }
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Live character status is unavailable for the account {LoginName}.", account.LoginName);
        }

        return result;
    }
}

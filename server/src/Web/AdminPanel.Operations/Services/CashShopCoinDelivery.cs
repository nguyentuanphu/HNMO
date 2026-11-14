// <copyright file="CashShopCoinDelivery.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic.Administration;
using HNMO.Interfaces;

/// <summary>
/// Lets the game server apply the pending coin grants of an account which is in the game at once
/// (<see cref="CashShopCoinGrantsAdminOperationPlugIn"/>), instead of the next time the player opens the cash shop.
/// </summary>
public class CashShopCoinDelivery
{
    private readonly ILoginServer _loginServer;
    private readonly IServerProvider _serverProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CashShopCoinDelivery"/> class.
    /// </summary>
    /// <param name="loginServer">The login server, which knows the accounts which are online.</param>
    /// <param name="serverProvider">The server provider, which holds the game servers.</param>
    public CashShopCoinDelivery(ILoginServer loginServer, IServerProvider serverProvider)
    {
        this._loginServer = loginServer;
        this._serverProvider = serverProvider;
    }

    /// <summary>
    /// Applies the pending coin grants of the account, when it's in the game.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the grants were applied, or why not.</returns>
    public async ValueTask<CashShopCoinDeliveryStatus> ApplyNowAsync(string loginName, CancellationToken cancellationToken = default)
    {
        var snapshot = await this._loginServer.GetSnapshotAsync().ConfigureAwait(false);
        var entry = snapshot.FirstOrDefault(e => string.Equals(e.Key, loginName, StringComparison.OrdinalIgnoreCase));
        if (entry.Key is null)
        {
            return CashShopCoinDeliveryStatus.Offline;
        }

        if (this._serverProvider.Servers.OfType<IGameServer>().FirstOrDefault(s => s.Id == entry.Value) is not { ServerState: ServerState.Started } server
            || server is not IGameServerAdministration administration)
        {
            return CashShopCoinDeliveryStatus.Pending;
        }

        var plugInId = typeof(CashShopCoinGrantsAdminOperationPlugIn).GUID;
        var arguments = new JsonObject { ["loginName"] = entry.Key }.ToJsonString();
        try
        {
            var preview = await administration.PreviewAdminOperationAsync(plugInId, CashShopCoinGrantsAdminOperationPlugIn.OperationName, arguments, cancellationToken).ConfigureAwait(false);
            await administration.ExecuteAdminOperationAsync(plugInId, CashShopCoinGrantsAdminOperationPlugIn.OperationName, arguments, preview, cancellationToken).ConfigureAwait(false);
            return CashShopCoinDeliveryStatus.Applied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // E.g. the account is logged in, but its character is not in the game yet. The grant was saved already,
            // so it's applied the next time the player opens the cash shop.
            return CashShopCoinDeliveryStatus.Pending;
        }
    }
}

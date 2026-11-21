// <copyright file="GameServer.WebsiteAccounts.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer;

using System.Threading;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Interfaces;

/// <summary>The authenticated website's account-feature channel.</summary>
public sealed partial class GameServer : IWebsiteAccountGameServer
{
    /// <inheritdoc />
    public ValueTask<WebsiteMissionsReply> ReadWebsiteMissionsAsync(WebsiteMissionSubject subject, CancellationToken cancellationToken)
        => new WebsiteMissionActions(this._gameContext).ReadAsync(subject, cancellationToken);

    /// <inheritdoc />
    public ValueTask<WebsiteMissionsReply> ClaimWebsiteMissionsAsync(WebsiteMissionSubject subject, string action, string argument, CancellationToken cancellationToken)
        => new WebsiteMissionActions(this._gameContext).ClaimAsync(subject, action, argument, cancellationToken);
}

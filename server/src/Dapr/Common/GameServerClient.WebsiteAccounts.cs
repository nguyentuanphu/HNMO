// <copyright file="GameServerClient.WebsiteAccounts.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Dapr.Common;

using System.Threading;
using HNMO.Interfaces;

/// <summary>Invokes the protected account-feature channel of one game server.</summary>
public sealed partial class GameServerClient : IWebsiteAccountGameServer
{
    /// <inheritdoc />
    public ValueTask<WebsiteMissionsReply> ReadWebsiteMissionsAsync(WebsiteMissionSubject subject, CancellationToken cancellationToken)
        => this.InvokeWebsiteMissionsAsync("read", new(this.Id, subject), cancellationToken);

    /// <inheritdoc />
    public ValueTask<WebsiteMissionsReply> ClaimWebsiteMissionsAsync(WebsiteMissionSubject subject, string action, string argument, CancellationToken cancellationToken)
        => this.InvokeWebsiteMissionsAsync("claim", new(this.Id, subject, action, argument), cancellationToken);

    private async ValueTask<WebsiteMissionsReply> InvokeWebsiteMissionsAsync(string operation, WebsiteMissionsInvocation request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return await this.DaprClient.InvokeMethodAsync<WebsiteMissionsInvocation, WebsiteMissionsReply>(this.AdministrationAppId, $"website-account/missions/{operation}", request, timeout.Token).ConfigureAwait(false);
    }
}

// <copyright file="IWebsiteAccountGameServer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System.Threading;

/// <summary>Internal account-page operations. The website API must authenticate and stamp the subject itself.</summary>
public interface IWebsiteAccountGameServer
{
    /// <summary>Reads owned mission/calendar/referral state without mutating gameplay state.</summary>
    /// <param name="subject">The verified account and character.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current authoritative board or availability status.</returns>
    ValueTask<WebsiteMissionsReply> ReadWebsiteMissionsAsync(WebsiteMissionSubject subject, CancellationToken cancellationToken);

    /// <summary>Invokes existing mission actions for the verified live character under its persistence lock.</summary>
    /// <param name="subject">The verified account and character.</param>
    /// <param name="action">Mission, streak, calendar or invite.</param>
    /// <param name="argument">The mission identifier or invitation code, where required.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result and refreshed board.</returns>
    ValueTask<WebsiteMissionsReply> ClaimWebsiteMissionsAsync(WebsiteMissionSubject subject, string action, string argument, CancellationToken cancellationToken);
}

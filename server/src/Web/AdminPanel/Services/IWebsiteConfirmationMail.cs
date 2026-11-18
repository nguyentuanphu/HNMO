// <copyright file="IWebsiteConfirmationMail.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Threading;

/// <summary>Sends only an explicitly requested account email confirmation.</summary>
public interface IWebsiteConfirmationMail
{
    /// <summary>Sends the code to the new mailbox; no login password belongs in the message.</summary>
    /// <param name="address">The new mailbox.</param>
    /// <param name="code">The random one-use confirmation code.</param>
    /// <param name="language">English or Vietnamese.</param>
    /// <param name="cancellationToken">The bounded request lifetime.</param>
    /// <returns>The delivery task.</returns>
    Task SendAsync(string address, string code, string language, CancellationToken cancellationToken);
}

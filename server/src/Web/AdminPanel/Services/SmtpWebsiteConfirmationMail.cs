// <copyright file="SmtpWebsiteConfirmationMail.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Threading;
using HNMO.Web.AdminPanel.Properties;

/// <summary>Sends confirmation codes over SMTP submission with STARTTLS.</summary>
/// <param name="settings">The administrator's protected outgoing-mail configuration.</param>
public sealed class SmtpWebsiteConfirmationMail(WebsiteMailSettingsStore settings) : IWebsiteConfirmationMail
{
    /// <inheritdoc />
    public async Task SendAsync(string address, string code, string language, CancellationToken cancellationToken)
    {
        var culture = CultureInfo.GetCultureInfo(language == "vi" ? "vi" : "en");
        using var client = settings.CreateClient();
        using var message = new MailMessage(settings.Read().From, address)
        {
            Subject = Resources.ResourceManager.GetString(nameof(Resources.WebsiteMailConfirmationSubject), culture)!,
            Body = string.Format(culture, Resources.ResourceManager.GetString(nameof(Resources.WebsiteMailConfirmationBody), culture)!, code),
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

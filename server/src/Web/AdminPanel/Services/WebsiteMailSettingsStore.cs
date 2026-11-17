// <copyright file="WebsiteMailSettingsStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Net;
using System.Net.Mail;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using Microsoft.AspNetCore.DataProtection;

/// <summary>Persists mail settings without exposing SMTP secrets to account endpoints or settings forms.</summary>
/// <param name="state">The same atomic state file used by the other server features.</param>
/// <param name="protection">The server's existing persisted data-protection key ring.</param>
public sealed class WebsiteMailSettingsStore(HnmoStateStore state, IDataProtectionProvider protection)
{
    private const string Section = "websiteMail";
    private readonly object _gate = new();
    private readonly IDataProtector _protector = protection.CreateProtector("HNMO.Website.SmtpPassword.v1");

    /// <summary>Gets the unified state path used to resolve the existing per-account row backend.</summary>
    public string StatePath => state.FilePath;

    /// <summary>Reads a detached form model with no stored credential.</summary>
    /// <returns>The public settings and password-exists flag.</returns>
    public WebsiteMailSettings Read()
    {
        lock (this._gate)
        {
            var stored = this.ReadStored();
            return stored with { ProtectedPassword = string.Empty, HasPassword = !string.IsNullOrEmpty(stored.ProtectedPassword) };
        }
    }

    /// <summary>Validates a plain mailbox address without display names or newline injection.</summary>
    /// <param name="address">The input address.</param>
    /// <returns>Whether it is a single plain address.</returns>
    public static bool IsAddress(string address) => address.Length <= 254 && !address.Any(char.IsControl)
        && MailAddress.TryCreate(address, out var parsed) && parsed.DisplayName.Length == 0 && parsed.Address == address;

    /// <summary>Saves validated settings; a blank replacement keeps the previous protected password.</summary>
    /// <param name="settings">The form settings.</param>
    /// <param name="password">An optional new SMTP password, never saved in plaintext.</param>
    public void Save(WebsiteMailSettings settings, string? password)
    {
        var host = settings.Host.Trim();
        var from = settings.From.Trim();
        var user = settings.UserName.Trim();
        if (host.Length > 253 || host.Any(char.IsControl) || user.Length > 254 || user.Any(char.IsControl)
            || password?.Length > 1024 || settings.Port is < 1 or > 65535
            || (settings.Enabled && (Uri.CheckHostName(host) == UriHostNameType.Unknown || !IsAddress(from))))
        {
            throw new ArgumentException("Invalid SMTP settings.");
        }

        lock (this._gate)
        {
            var cipher = string.IsNullOrEmpty(password) ? this.ReadStored().ProtectedPassword : this._protector.Protect(password);
            var next = settings with { Host = host, From = from, UserName = user, ProtectedPassword = user.Length == 0 ? string.Empty : cipher, HasPassword = false };
            state.WriteSection(Section, next, JsonSerializerOptions.Default);
        }
    }

    /// <summary>Creates one disposable SMTP client; remote delivery always requires STARTTLS.</summary>
    /// <returns>The configured client.</returns>
    public SmtpClient CreateClient()
    {
        lock (this._gate)
        {
            var settings = this.ReadStored();
            if (!settings.Enabled)
            {
                throw new InvalidOperationException("Email delivery is disabled.");
            }

            var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = true, UseDefaultCredentials = false };
            if (settings.UserName.Length > 0)
            {
                client.Credentials = new NetworkCredential(settings.UserName, settings.ProtectedPassword.Length == 0 ? string.Empty : this._protector.Unprotect(settings.ProtectedPassword));
            }

            return client;
        }
    }

    private WebsiteMailSettings ReadStored() => state.ReadSection<WebsiteMailSettings>(Section, JsonSerializerOptions.Default) ?? new();
}

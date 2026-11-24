// <copyright file="WebsiteStripeTestSettings.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using Microsoft.AspNetCore.DataProtection;

internal sealed record StripeSettingsForm(bool Enabled, string Origin, bool HasApiKey = false, bool HasWebhookKey = false);

internal sealed record StripeTestCredentials(string Origin, string ApiKey, string WebhookKey);

internal sealed class WebsiteStripeTestSettings(HnmoStateStore state, IDataProtectionProvider protection)
{
    private const string Section = "websiteStripeTestCheckout";
    private readonly object _gate = new();
    private readonly IDataProtector _protector = protection.CreateProtector("HNMO.Website.StripeTest.v1");

    public StripeSettingsForm Read()
    {
        var saved = this.ReadStored();
        return new(saved.Enabled, saved.Origin, saved.ApiKey.Length > 0, saved.WebhookKey.Length > 0);
    }

    public bool CanCheckout => this.Read() is { Enabled: true, HasApiKey: true, HasWebhookKey: true };

    public StripeTestCredentials? ReadCredentials(bool allowDisabled = false)
    {
        var saved = this.ReadStored();
        if ((!saved.Enabled && !allowDisabled) || saved.ApiKey.Length == 0 || saved.WebhookKey.Length == 0 || !IsOrigin(saved.Origin))
        {
            return null;
        }

        var apiKey = this._protector.Unprotect(saved.ApiKey);
        var webhookKey = this._protector.Unprotect(saved.WebhookKey);
        return IsApiKey(apiKey) && IsWebhookKey(webhookKey) ? new(saved.Origin, apiKey, webhookKey) : null;
    }

    public void Save(bool enabled, string origin, string apiKey, string webhookKey)
    {
        origin = origin.Trim();
        apiKey = apiKey.Trim();
        webhookKey = webhookKey.Trim();
        if ((enabled || origin.Length > 0) && !IsOrigin(origin)
            || apiKey.Length > 0 && !IsApiKey(apiKey)
            || webhookKey.Length > 0 && !IsWebhookKey(webhookKey))
        {
            throw new ArgumentException("Invalid Stripe test settings.");
        }

        lock (this._gate)
        {
            var previous = this.ReadStored();
            var next = new StoredSettings(enabled, origin,
                apiKey.Length == 0 ? previous.ApiKey : this._protector.Protect(apiKey),
                webhookKey.Length == 0 ? previous.WebhookKey : this._protector.Protect(webhookKey));
            if (enabled && (next.ApiKey.Length == 0 || next.WebhookKey.Length == 0))
            {
                throw new ArgumentException("Both Stripe test credentials are required.");
            }

            state.WriteSection(Section, next, JsonSerializerOptions.Default);
        }
    }

    private static bool IsApiKey(string value)
        => IsToken(value, "sk_test_") || IsToken(value, "rk_test_");

    private static bool IsWebhookKey(string value) => IsToken(value, "whsec_");

    private static bool IsOrigin(string origin) => origin.Length <= 240
        && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && uri.UserInfo.Length == 0 && origin == uri.GetLeftPart(UriPartial.Authority);

    private static bool IsToken(string value, string prefix) => value.Length > prefix.Length && value.Length <= 300
        && value.StartsWith(prefix, StringComparison.Ordinal) && value[prefix.Length..].All(char.IsAsciiLetterOrDigit);

    private StoredSettings ReadStored()
        => state.ReadSection<StoredSettings>(Section, JsonSerializerOptions.Default) ?? new(false, string.Empty, string.Empty, string.Empty);

    private sealed record StoredSettings(bool Enabled, string Origin, string ApiKey, string WebhookKey);
}

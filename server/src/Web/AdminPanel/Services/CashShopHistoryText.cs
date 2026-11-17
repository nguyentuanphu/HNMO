// <copyright file="CashShopHistoryText.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;
using HNMO.GameLogic.CashShop;
using HNMO.Web.AdminPanel.Properties;

/// <summary>Formats coin receipt metadata for people, with all history dates in Sydney time.</summary>
public static class CashShopHistoryText
{
    private static readonly TimeZoneInfo SydneyZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "AUS Eastern Standard Time" : "Australia/Sydney");

    /// <summary>Localizes known receipts while retaining ordinary host-written reasons.</summary>
    /// <param name="reason">The history entry reason.</param>
    /// <returns>The localized description.</returns>
    public static string Format(string? reason)
    {
        var culture = CultureInfo.CurrentUICulture;
        if (CashShopPurchaseReceipt.Read(reason) is { } purchase)
        {
            var name = culture.TwoLetterISOLanguageName == "vi" ? purchase.NameVi : purchase.NameEn;
            return purchase.GiftRecipient is { Length: > 0 } recipient
                ? string.Format(culture, Resources.AccountCoinHistoryGift, recipient, name)
                : string.Format(culture, Resources.AccountCoinHistoryCashShop, name);
        }

        if (CashShopBalanceReceipt.Read(reason) is not { } change)
        {
            return reason ?? string.Empty;
        }

        var key = $"AccountCoinHistory{change.Action}";
        var text = Resources.ResourceManager.GetString(key, culture)!;
        var other = string.IsNullOrWhiteSpace(change.OtherCharacter) ? Resources.AccountCoinHistoryOtherCharacter : change.OtherCharacter;
        return string.Format(culture, text, other);
    }

    /// <summary>Formats a persisted UTC instant in Sydney regardless of the server's local zone.</summary>
    /// <param name="utc">The stored UTC instant.</param>
    /// <returns>The Sydney date and time.</returns>
    public static string Sydney(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SydneyZone)
        .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}

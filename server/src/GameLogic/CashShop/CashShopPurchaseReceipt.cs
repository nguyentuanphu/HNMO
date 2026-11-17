// <copyright file="CashShopPurchaseReceipt.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.CashShop;

using System.Globalization;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>Records an already-applied debit in the same persistence save as its cash-shop purchase.</summary>
public static class CashShopPurchaseReceipt
{
    private const string Kind = "cash-shop-purchase-v1";

    /// <summary>Creates a receipt without applying or scheduling another debit.</summary>
    /// <param name="context">The purchase's existing persistence context.</param>
    /// <param name="account">The paying account.</param>
    /// <param name="package">The bought package.</param>
    /// <param name="price">The amount already deducted.</param>
    /// <param name="giftRecipient">The optional gift recipient.</param>
    /// <returns>The receipt, which the caller must delete if the purchase save fails.</returns>
    public static CashShopCoinGrant Create(IPlayerContext context, Account account, CashShopPackage package, int price, string? giftRecipient)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        var now = DateTime.UtcNow;
        var receipt = context.CreateNew<CashShopCoinGrant>();
        receipt.AccountId = account.GetId();
        receipt.CoinType = package.CoinType;
        receipt.Amount = -price;
        receipt.CreatedAt = now;
        receipt.AppliedAt = now;
        receipt.Reference = $"cashshop:{Guid.NewGuid():N}";
        receipt.Reason = JsonSerializer.Serialize(new CashShopPurchaseDetails(Kind, package.Name.ValueInNeutralLanguage, package.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? package.Name.ValueInNeutralLanguage, giftRecipient));
        return receipt;
    }

    /// <summary>Reads only recognized receipt metadata; ordinary host-written reasons stay ordinary text.</summary>
    /// <param name="reason">The stored reason.</param>
    /// <returns>The purchase details, or null for a legacy reason or malformed metadata.</returns>
    public static CashShopPurchaseDetails? Read(string? reason)
    {
        if (reason is null || reason.Length > 16384 || !reason.StartsWith('{'))
        {
            return null;
        }

        try
        {
            var details = JsonSerializer.Deserialize<CashShopPurchaseDetails>(reason);
            return details?.Kind == Kind ? details : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

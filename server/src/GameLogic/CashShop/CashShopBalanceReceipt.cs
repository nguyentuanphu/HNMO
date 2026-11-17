// <copyright file="CashShopBalanceReceipt.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.CashShop;

using System.Text.Json;
using HNMO.Persistence;

/// <summary>Audits direct balance mutations in their existing context without performing or scheduling another payment.</summary>
public static class CashShopBalanceReceipt
{
    private const string Kind = "account-balance-v1";

    /// <summary>Tracks an already-applied delta for the same save as its account.</summary>
    /// <param name="context">The account's existing persistence context.</param>
    /// <param name="account">The account whose balance changed.</param>
    /// <param name="coinType">The affected currency.</param>
    /// <param name="amount">The actual signed change; zero produces no receipt.</param>
    /// <param name="reason">The change source.</param>
    /// <param name="otherCharacter">The optional public character name.</param>
    /// <returns>The tracked receipt; remove it if the caller rolls back the balance.</returns>
    public static CashShopCoinGrant? Record(IContext context, Account account, CashShopCoinType coinType, int amount, CashShopBalanceReason reason, string? otherCharacter = null)
    {
        if (!Enum.IsDefined(coinType) || !Enum.IsDefined(reason))
        {
            throw new ArgumentException("Invalid balance receipt currency or reason.");
        }

        if (amount == 0)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var receipt = context.CreateNew<CashShopCoinGrant>();
        receipt.AccountId = account.GetId();
        receipt.CoinType = coinType;
        receipt.Amount = amount;
        receipt.CreatedAt = now;
        receipt.AppliedAt = now;
        receipt.Reference = $"balance:{Guid.NewGuid():N}";
        receipt.Reason = JsonSerializer.Serialize(new CashShopBalanceDetails(Kind, reason.ToString(), otherCharacter));
        return receipt;
    }

    /// <summary>Removes only the receipt when an existing payment path restores its balance.</summary>
    /// <param name="context">The same payment context.</param>
    /// <param name="receipt">The tracked receipt, if there was a nonzero delta.</param>
    /// <returns>The cleanup task.</returns>
    public static async ValueTask RemoveAsync(IContext context, CashShopCoinGrant? receipt)
    {
        if (receipt is not null)
        {
            await context.DeleteAsync(receipt).ConfigureAwait(false);
        }
    }

    /// <summary>Reads only this known format and action set, leaving ordinary host-written reasons untouched.</summary>
    /// <param name="reason">The persisted reason.</param>
    /// <returns>The receipt metadata, or null.</returns>
    public static CashShopBalanceDetails? Read(string? reason)
    {
        if (reason is null || reason.Length > 16384 || !reason.StartsWith('{'))
        {
            return null;
        }

        try
        {
            var details = JsonSerializer.Deserialize<CashShopBalanceDetails>(reason);
            return details?.Kind == Kind && Enum.TryParse<CashShopBalanceReason>(details.Action, out var action) && Enum.IsDefined(action) ? details : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

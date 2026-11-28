// <copyright file="HubLicencePackage.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>A licence package of the HNMO developer's hub (<c>GET /api/licence-packages</c>).</summary>
/// <param name="Id">The package id.</param>
/// <param name="Kind">plan, trial or addon.</param>
/// <param name="NameEn">The name in English.</param>
/// <param name="NameVi">The name in Vietnamese.</param>
/// <param name="DescriptionEn">The description in English.</param>
/// <param name="DescriptionVi">The description in Vietnamese.</param>
/// <param name="MaxPlayers">The players at once it allows (plans and trials).</param>
/// <param name="AddPlayers">The players an add-on adds.</param>
/// <param name="Seasons">The seasons it plays (s6, s9, arena).</param>
/// <param name="Billing">month, year, lifetime or once.</param>
/// <param name="DurationDays">How long it lasts; null: for good.</param>
/// <param name="Prices">The prices by currency (vnd in whole đồng, aud and usd in cents).</param>
/// <param name="Highlight">Whether it is the suggested one.</param>
/// <param name="Order">Its place in the list.</param>
/// <param name="Addon">For an add-on: players, upgrade, move, second-machine (move and second-machine need the other
/// computer's id, upgrade the package to move up to).</param>
public sealed record HubLicencePackage(
    string Id,
    string Kind,
    string NameEn,
    string NameVi,
    string DescriptionEn,
    string DescriptionVi,
    int MaxPlayers,
    int AddPlayers,
    IReadOnlyList<string> Seasons,
    string Billing,
    int? DurationDays,
    IReadOnlyDictionary<string, long> Prices,
    bool Highlight,
    int Order,
    string Addon = "");

/// <summary>The hub's packages and which payment ways it offers.</summary>
/// <param name="Ok">Whether the hub answered.</param>
/// <param name="Packages">The packages, in order.</param>
/// <param name="Stripe">Whether card payment (Stripe) is offered.</param>
/// <param name="PayOs">Whether VN bank transfer through PayOS is offered.</param>
/// <param name="Error">Why there are none, when the hub didn't answer.</param>
/// <param name="Bank">Whether a plain bank transfer to the developer (VietQR drawn by the server) is offered.</param>
public sealed record HubLicenceCatalogue(bool Ok, IReadOnlyList<HubLicencePackage> Packages, bool Stripe, bool PayOs, string Error, bool Bank = false);

/// <summary>A bank transfer the host makes for an order (VietQR); the licence comes once the developer confirms it.</summary>
/// <param name="Bin">The bank's BIN.</param>
/// <param name="BankName">The bank's name, when the hub gave one.</param>
/// <param name="Account">The account number.</param>
/// <param name="AccountName">The account holder.</param>
/// <param name="Amount">The exact amount in whole đồng.</param>
/// <param name="Note">The transfer note the order is found by.</param>
/// <param name="Order">The hub's order id.</param>
public sealed record HubBankTransfer(string Bin, string BankName, string Account, string AccountName, long Amount, string Note, string Order);

/// <summary>What starting a purchase gave.</summary>
/// <param name="Url">The payment page to open (cards, PayOS).</param>
/// <param name="Bank">The bank transfer to make (provider bank).</param>
/// <param name="En">The answer in English.</param>
/// <param name="Vi">The answer in Vietnamese.</param>
public sealed record HubCheckout(string? Url, HubBankTransfer? Bank, string En, string Vi)
{
    /// <summary>Gets a value indicating whether the purchase started.</summary>
    public bool Started => this.Url is not null || this.Bank is not null;
}

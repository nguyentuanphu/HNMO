// <copyright file="CashShopPurchaseDetails.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.CashShop;

/// <summary>Saved bilingual purchase metadata for the account's coin history.</summary>
/// <param name="Kind">The versioned receipt discriminator.</param>
/// <param name="NameEn">The package name at purchase time.</param>
/// <param name="NameVi">Its Vietnamese name at purchase time.</param>
/// <param name="GiftRecipient">The gift recipient, or null for an ordinary purchase.</param>
public sealed record CashShopPurchaseDetails(string Kind, string NameEn, string NameVi, string? GiftRecipient);

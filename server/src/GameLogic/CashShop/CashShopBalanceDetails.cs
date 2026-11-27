// <copyright file="CashShopBalanceDetails.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.CashShop;

/// <summary>Versioned receipt metadata; labels are localized by the website/admin interface.</summary>
/// <param name="Kind">The receipt format marker.</param>
/// <param name="Action">The recognized balance-change reason.</param>
/// <param name="OtherCharacter">The optional public character name, never another player's account login.</param>
public sealed record CashShopBalanceDetails(string Kind, string Action, string? OtherCharacter);

// <copyright file="CashShopCoinDeliveryStatus.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// Whether the pending coin grants of an account were applied at once.
/// </summary>
public enum CashShopCoinDeliveryStatus
{
    /// <summary>
    /// The account is offline; the grants are applied the next time the player opens the cash shop.
    /// </summary>
    Offline,

    /// <summary>
    /// The account is in the game and its balance was changed at once.
    /// </summary>
    Applied,

    /// <summary>
    /// The account is online, but the grants couldn't be applied now (e.g. it's in the character selection);
    /// they're applied the next time the player opens the cash shop.
    /// </summary>
    Pending,
}

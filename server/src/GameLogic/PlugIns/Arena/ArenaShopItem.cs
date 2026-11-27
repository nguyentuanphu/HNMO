// <copyright file="ArenaShopItem.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>An item of the Arena points shop (made new on the home server when bought).</summary>
public sealed class ArenaShopItem
{
    /// <summary>Gets or sets the category shown in the shop: "jewels", "charms", "tickets" or "cosmetic".</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the item group.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets the item level.</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets the price in arena points.</summary>
    public int Price { get; set; }

    /// <summary>Gets or sets the English name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    public string NameVi { get; set; } = string.Empty;
}

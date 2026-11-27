// <copyright file="ItemStackKind.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemStacks;

/// <summary>A kind of items which stack: its items, how many fit one slot, and whether it's free or unlocked with coins.</summary>
public sealed class ItemStackKind
{
    /// <summary>Gets or sets the key (unique, used to store an account's unlocks), e.g. <c>jewels</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether every account stacks these items (otherwise after an unlock).</summary>
    public bool Free { get; set; }

    /// <summary>Gets or sets the most pieces in one slot (2 to 255).</summary>
    public int Limit { get; set; } = 255;

    /// <summary>Gets or sets the coins one unlock costs (0: can't be bought).</summary>
    public int UnlockPrice { get; set; } = 500;

    /// <summary>Gets or sets the items: <c>group:number</c> or <c>group:from-to</c>, e.g. <c>14:13</c>, <c>14:35-40</c>.</summary>
    public List<string> Items { get; set; } = [];
}

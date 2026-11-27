// <copyright file="DuplicateItemCaseEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.DuplicateItems;

/// <summary>One of the items of a <see cref="DuplicateItemCase"/>, as the scan found it.</summary>
public sealed class DuplicateItemCaseEntry
{
    /// <summary>Gets or sets the item's database id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets where it was: "inventory", "vault" or "held" (market, escrow or a vault page not in use).</summary>
    public string Place { get; set; } = string.Empty;

    /// <summary>Gets or sets the character whose bag held it.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the account whose bag or vault held it.</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the item's name.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets the item's level.</summary>
    public int Level { get; set; }
}

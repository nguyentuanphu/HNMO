// <copyright file="EconomyAccountTotals.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

/// <summary>
/// Per-account aggregate holdings across inventories, vaults, and optional external stores.
/// </summary>
public sealed class EconomyAccountTotals
{
    /// <summary>Gets or sets the account login name.</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets total zen across characters and vault.</summary>
    public long Zen { get; set; }

    /// <summary>Gets or sets total cash shop coins (WCoinC + WCoinP + GoblinPoints + external).</summary>
    public long Coins { get; set; }

    /// <summary>Gets or sets total jewel piece count (inventory + vault + external jewel bank).</summary>
    public int Jewels { get; set; }

    /// <summary>Gets or sets count of non-jewel valuable items.</summary>
    public int ValuableItems { get; set; }
}

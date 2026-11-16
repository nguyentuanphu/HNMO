// <copyright file="MultiVaultConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration.Items;

/// <summary>
/// Configuration for <see cref="MultiVaultPlugIn"/> (ROADMAP 14.5). Off until enabled by host.
/// </summary>
public sealed class MultiVaultConfiguration
{
    /// <summary>Gets or sets a value indicating whether extra vaults and <c>/vault</c> switching are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets number of unlocked vaults for every standard account (minimum 1 = vault #0).</summary>
    [Range(1, 20)]
    public int DefaultVaultCount { get; set; } = 1;

    /// <summary>Gets or sets hard maximum vaults any single account may hold.</summary>
    [Range(1, 30)]
    public int MaxVaultsPerAccount { get; set; } = 5;

    /// <summary>Gets or sets bonus vaults granted while the account has active VIP status.</summary>
    [Range(0, 20)]
    public int VipExtraVaults { get; set; } = 1;

    /// <summary>Gets or sets coin cost to permanently unlock one extra vault with <c>/vault buy</c> (0 = purchase disabled).</summary>
    [Range(0, 1000000)]
    public int PurchaseExtraVaultCost { get; set; } = 100;

    /// <summary>Gets or sets coin type charged when buying extra vaults.</summary>
    public CashShopCoinType PurchaseCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets shared HNMO state key used to locate the PostgreSQL row backend.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;
}

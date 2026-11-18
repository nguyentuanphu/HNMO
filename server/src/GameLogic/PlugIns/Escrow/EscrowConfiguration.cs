// <copyright file="EscrowConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System.Collections.Generic;

/// <summary>
/// Configuration for <see cref="EscrowContractsPlugIn"/> (ROADMAP 16.1 + 16.3, disabled by default until configured).
/// </summary>
public sealed class EscrowConfiguration
{
    /// <summary>Gets or sets a value indicating whether in-game escrow contracts are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets escrow state file path.</summary>
    public string FileName { get; set; } = "save/hnmo-escrow.json";

    /// <summary>Gets or sets Jewel Bank state file path for escrow jewel transfers.</summary>
    public string JewelBankFileName { get; set; } = "save/hnmo-state.json";

    /// <summary>Gets or sets Activity Log state file path for origin lookups.</summary>
    public string ActivityLogFileName { get; set; } = "save/hnmo-state.json";

    /// <summary>Gets or sets host fee percentage charged per completed contract (0..25).</summary>
    public double FeePercent { get; set; } = 2.5;

    /// <summary>Gets or sets minimum fee charged when <see cref="FeePercent"/> &gt; 0.</summary>
    public long MinFee { get; set; } = 1;

    /// <summary>Gets or sets maximum fee cap (0 = uncapped).</summary>
    public long MaxFee { get; set; }

    /// <summary>Gets or sets who pays the escrow fee.</summary>
    public EscrowFeePayer FeePayer { get; set; } = EscrowFeePayer.Seller;

    /// <summary>Gets or sets allowed pricing currencies.</summary>
    public List<EscrowCurrency> AllowedCurrencies { get; set; } =
    [
        EscrowCurrency.Zen,
        EscrowCurrency.WCoinC,
        EscrowCurrency.WCoinP,
        EscrowCurrency.GoblinPoints,
        EscrowCurrency.JewelBless,
        EscrowCurrency.JewelSoul,
        EscrowCurrency.JewelChaos,
        EscrowCurrency.JewelCreation,
        EscrowCurrency.JewelLife,
    ];

    /// <summary>Gets or sets maximum simultaneous open contracts per account.</summary>
    public int MaxOpenContractsPerAccount { get; set; } = 10;

    /// <summary>Gets or sets default contract deadline in hours.</summary>
    public int DefaultDeadlineHours { get; set; } = 24;

    /// <summary>Gets or sets maximum allowed deadline in hours.</summary>
    public int MaxDeadlineHours { get; set; } = 168;

    /// <summary>Gets or sets banned item specifications (<c>group:number</c> or exact display name).</summary>
    public List<string> BannedItems { get; set; } = ["13:20"];

    /// <summary>Gets or sets minimum account age in days before using escrow (0 = no wait).</summary>
    public int MinAccountAgeDays { get; set; } = 3;

    /// <summary>Gets or sets holding cooldown hours before escrow-received items can be traded/escrowed again (0 = none).</summary>
    public int PostEscrowTradeCooldownHours { get; set; } = 24;

    /// <summary>Gets or sets a value indicating whether second password verification is required to create or accept contracts.</summary>
    public bool RequireSecondPassword { get; set; } = true;

    /// <summary>Gets or sets coin value threshold requiring explicit high-value confirmation (0 = disabled).</summary>
    public long HighValueThresholdCoins { get; set; } = 1000;

    /// <summary>Gets or sets Zen value threshold requiring explicit high-value confirmation (0 = disabled).</summary>
    public long HighValueThresholdZen { get; set; } = 100_000_000;

    /// <summary>Gets or sets jewel quantity threshold requiring explicit high-value confirmation (0 = disabled).</summary>
    public long HighValueThresholdJewels { get; set; } = 50;
}

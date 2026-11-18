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

    /// <summary>Gets or sets a value indicating whether Direct P2P VND real-money contracts are enabled (OFF by default).</summary>
    public bool DirectVndEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether host ticked acknowledgement to check Vietnamese online-game/payment regulations before enabling Direct P2P VND.</summary>
    public bool DirectVndComplianceAcknowledged { get; set; }

    /// <summary>Gets or sets buyer payment window minutes upon accepting a Direct P2P VND contract (default 30 min).</summary>
    public int DirectVndPaymentWindowMinutes { get; set; } = 30;

    /// <summary>Gets or sets one-time extension minutes granted if buyer extends payment window.</summary>
    public int DirectVndExtendMinutes { get; set; } = 15;

    /// <summary>Gets or sets hours after buyer marks paid before auto-escalating an unconfirmed Direct P2P VND contract to Disputed.</summary>
    public int DirectVndAutoDisputeHours { get; set; } = 2;

    /// <summary>Gets or sets coin currency charged for Direct P2P VND contract service fee.</summary>
    public EscrowCurrency DirectVndServiceFeeCurrency { get; set; } = EscrowCurrency.WCoinC;

    /// <summary>Gets or sets flat coin service fee on Direct P2P VND contracts (combined with <see cref="DirectVndServiceFeePercent"/>).</summary>
    public long DirectVndServiceFeeCoins { get; set; } = 10;

    /// <summary>Gets or sets percentage fee converted into coins per 1000 VND (optional, 0 = flat fee only).</summary>
    public double DirectVndServiceFeePercent { get; set; }

    /// <summary>Gets or sets who pays the Direct P2P VND coin service fee.</summary>
    public EscrowFeePayer DirectVndFeePayer { get; set; } = EscrowFeePayer.Seller;

    /// <summary>Gets or sets maximum optional seller security bond in coins (0 = bonds disabled).</summary>
    public int DirectVndMaxSellerBondCoins { get; set; } = 10_000;

    /// <summary>Gets or sets maximum receipt image byte size (default 512 KB).</summary>
    public int DirectVndMaxReceiptBytes { get; set; } = 524_288;

    /// <summary>Gets or sets a value indicating whether Host-mediated VND contracts are enabled (OFF by default).</summary>
    public bool HostVndEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether host ticked acknowledgement to check Vietnamese rules before enabling Host-mediated VND.</summary>
    public bool HostVndComplianceAcknowledged { get; set; }

    /// <summary>Gets or sets host bank 6-digit NAPAS BIN for receiving buyer VietQR payments.</summary>
    public string HostVndBankBin { get; set; } = "970436";

    /// <summary>Gets or sets host bank name for buyer VietQR display.</summary>
    public string HostVndBankName { get; set; } = "Vietcombank";

    /// <summary>Gets or sets host receiving bank account number.</summary>
    public string HostVndBankAccountNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets host receiving bank account holder name.</summary>
    public string HostVndBankAccountName { get; set; } = string.Empty;

    /// <summary>Gets or sets host commission fee % on Host-mediated VND sales (0..25%).</summary>
    public double HostVndFeePercent { get; set; } = 5.0;

    /// <summary>Gets or sets minimum VND host fee when fee % &gt; 0.</summary>
    public long HostVndMinFeeVnd { get; set; } = 5000;

    /// <summary>Gets or sets maximum VND host fee cap (0 = uncapped).</summary>
    public long HostVndMaxFeeVnd { get; set; }

    /// <summary>Gets or sets buyer payment window minutes when reserving a Host VND contract.</summary>
    public int HostVndPaymentWindowMinutes { get; set; } = 30;

    /// <summary>Gets or sets days allowed for admin to execute queued seller payouts.</summary>
    public int HostVndPayoutDays { get; set; } = 3;

    /// <summary>Gets or sets post-delivery dispute window hours before seller payout clears.</summary>
    public int HostVndDisputeWindowHours { get; set; } = 24;

    /// <summary>Gets or sets a value indicating whether convertible Trade Credit currency is enabled (OFF by default, highest risk).</summary>
    public bool TradeCreditEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether host ticked highest-risk Vietnamese legal compliance acknowledgement for Trade Credit.</summary>
    public bool TradeCreditComplianceAcknowledged { get; set; }

    /// <summary>Gets or sets fixed VND value per 1 Trade Credit unit (e.g. 1000 VND = 1 TC).</summary>
    public long TradeCreditVndPerUnit { get; set; } = 1000;

    /// <summary>Gets or sets normal WCoinC granted per 1 Trade Credit converted one-way (0 = disallow coin conversion).</summary>
    public int TradeCreditToCoinRate { get; set; } = 1;

    /// <summary>Gets or sets hours sellers must hold earned cash-sale proceeds before cashing out (default 72h).</summary>
    public int TradeCreditCashOutHoldHours { get; set; } = 72;

    /// <summary>Gets or sets cash-out fee percentage (0..25%).</summary>
    public double TradeCreditCashOutFeePercent { get; set; } = 5.0;

    /// <summary>Gets or sets minimum cash-out fee in Trade Credit units.</summary>
    public long TradeCreditCashOutMinFeeUnits { get; set; } = 5;

    /// <summary>Gets or sets daily Trade Credit cash-out cap per account (0 = unlimited).</summary>
    public long TradeCreditCashOutDailyLimitUnits { get; set; } = 5000;

    /// <summary>Gets or sets monthly Trade Credit cash-out cap per account (0 = unlimited).</summary>
    public long TradeCreditCashOutMonthlyLimitUnits { get; set; } = 20_000;
}

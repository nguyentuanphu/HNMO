// <copyright file="EconomyDailySnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

/// <summary>
/// Daily economy health snapshot stored per Sydney calendar date in <c>hnmo.player_feature_state</c>.
/// </summary>
public sealed class EconomyDailySnapshot
{
    /// <summary>Gets or sets Sydney calendar date (<c>yyyy-MM-dd</c>).</summary>
    public string DateSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC capture timestamp.</summary>
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>Gets or sets number of player accounts scanned.</summary>
    public int AccountCount { get; set; }

    /// <summary>Gets or sets total zen across all player accounts and external pools.</summary>
    public long TotalZen { get; set; }

    /// <summary>Gets or sets total coins across all player accounts and external pools.</summary>
    public long TotalCoins { get; set; }

    /// <summary>Gets or sets total jewel pieces across inventories, vaults, and external banks.</summary>
    public int TotalJewels { get; set; }

    /// <summary>Gets or sets total non-jewel valuable items.</summary>
    public int TotalValuableItems { get; set; }

    /// <summary>Gets or sets valuable item pickups logged for this Sydney date.</summary>
    public int DropsPickedPerDay { get; set; }

    /// <summary>Gets or sets Chaos Machine mixes logged for this Sydney date.</summary>
    public int ChaosMixesPerDay { get; set; }

    /// <summary>Gets or sets zen spent at NPC merchants (sink) during this Sydney date.</summary>
    public long NpcBuyZenSinkPerDay { get; set; }

    /// <summary>Gets or sets zen generated via NPC sales (faucet) during this Sydney date.</summary>
    public long NpcSellZenDropPerDay { get; set; }

    /// <summary>Gets or sets top account holders ranked by wealth.</summary>
    public List<EconomyAccountTotals> TopHolders { get; set; } = [];

    /// <summary>Gets or sets tracked account snapshot map (for anomaly delta detection).</summary>
    public List<EconomyAccountTotals> AccountSamples { get; set; } = [];

    /// <summary>Gets or sets anomalies detected against the previous snapshot.</summary>
    public List<EconomyAnomaly> Anomalies { get; set; } = [];
}

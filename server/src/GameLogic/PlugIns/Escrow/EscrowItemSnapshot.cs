// <copyright file="EscrowItemSnapshot.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Full portable snapshot of an item held in an escrow contract, including options and Activity Log origin.
/// </summary>
public sealed class EscrowItemSnapshot
{
    /// <summary>Gets or sets item group (0..15).</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number (0..511).</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets upgrade level (0..15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability/stack count.</summary>
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether item has Skill.</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether item has Luck.</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets Jewel of Life option level (0..7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets Excellent option bitmask.</summary>
    public byte ExcellentOptions { get; set; }

    /// <summary>Gets or sets English item display name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese item display name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets item origin provenance looked up from Activity Log.</summary>
    public string OriginSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item storage which holds the real item while the contract has it (<see cref="HeldItems"/>);
    /// empty for contracts made before, whose item is made again from this record.
    /// </summary>
    public Guid StorageId { get; set; }
}

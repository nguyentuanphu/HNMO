// <copyright file="MarketItemSnapshot.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.IO;
using System.Text.Json;

/// <summary>
/// Full serializable snapshot of an escrowed item.
/// </summary>
public sealed class MarketItemSnapshot
{
    /// <summary>Gets or sets the item group.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets upgrade level (+0..+15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability/stack count.</summary>
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has skill.</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has luck.</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets the additional option level (0..7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets the excellent bitmask (bits 0..5).</summary>
    public byte ExcellentOptions { get; set; }

    /// <summary>Gets or sets the English item display name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese item display name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item storage which holds the real item while the market has it (<see cref="HeldItems"/>);
    /// empty for listings made before, whose item is made again from this record.
    /// </summary>
    public Guid StorageId { get; set; }
}

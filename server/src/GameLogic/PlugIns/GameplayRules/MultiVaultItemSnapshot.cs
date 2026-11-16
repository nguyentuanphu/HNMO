// <copyright file="MultiVaultItemSnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

/// <summary>
/// Serialized item snapshot for secondary vault storage (ROADMAP 14.5).
/// </summary>
public sealed class MultiVaultItemSnapshot
{
    /// <summary>Gets or sets item group.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item level.</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability.</summary>
    public double Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has skill.</summary>
    public bool HasSkill { get; set; }

    /// <summary>Gets or sets socket count.</summary>
    public int SocketCount { get; set; }

    /// <summary>Gets or sets slot index in vault.</summary>
    public byte Slot { get; set; }

    /// <summary>Gets or sets item options.</summary>
    public List<MultiVaultOptionSnapshot> Options { get; set; } = [];
}

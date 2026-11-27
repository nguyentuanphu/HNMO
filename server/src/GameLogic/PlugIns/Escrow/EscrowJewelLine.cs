// <copyright file="EscrowJewelLine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Jewel Bank quantity line locked inside an escrow contract.
/// </summary>
public sealed class EscrowJewelLine
{
    /// <summary>Gets or sets canonical Jewel Bank currency/key (<c>JewelBless</c>, <c>JewelSoul</c>, etc.).</summary>
    public string JewelKey { get; set; } = string.Empty;

    /// <summary>Gets or sets escrowed count.</summary>
    public int Quantity { get; set; }
}

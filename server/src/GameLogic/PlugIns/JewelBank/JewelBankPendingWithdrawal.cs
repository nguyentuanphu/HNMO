// <copyright file="JewelBankPendingWithdrawal.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.IO;
using System.Text.Json;

/// <summary>
/// A queued website jewel withdrawal awaiting claim in-game if an account's offline bag had insufficient room or was online.
/// </summary>
public sealed class JewelBankPendingWithdrawal
{
    /// <summary>Gets or sets the unique delivery identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipient login.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the jewel key.</summary>
    public string JewelKey { get; set; } = string.Empty;

    /// <summary>Gets or sets remaining quantity to claim in-game.</summary>
    public int Quantity { get; set; }

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

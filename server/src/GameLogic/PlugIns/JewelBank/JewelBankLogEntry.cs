// <copyright file="JewelBankLogEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.IO;
using System.Text.Json;

/// <summary>
/// Recent Jewel Bank activity record.
/// </summary>
public sealed class JewelBankLogEntry
{
    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets actor login.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets action (<c>Deposit</c>, <c>Withdraw</c>, <c>ClearMaster</c>).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets jewel key or detail.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets quantity.</summary>
    public int Amount { get; set; }

    /// <summary>Gets or sets origin (<c>game</c> or <c>website</c>).</summary>
    public string Source { get; set; } = "game";
}

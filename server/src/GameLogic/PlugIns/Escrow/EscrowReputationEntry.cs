// <copyright file="EscrowReputationEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Per-account escrow trade reputation metrics.
/// </summary>
public sealed class EscrowReputationEntry
{
    /// <summary>Gets or sets account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets completed contracts count.</summary>
    public int Completed { get; set; }

    /// <summary>Gets or sets cancelled contracts count.</summary>
    public int Cancelled { get; set; }

    /// <summary>Gets or sets disputes lost count.</summary>
    public int DisputesLost { get; set; }

    /// <summary>Gets or sets net reputation score.</summary>
    public int Score { get; set; }
}

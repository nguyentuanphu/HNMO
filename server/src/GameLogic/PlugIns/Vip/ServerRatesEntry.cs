// <copyright file="ServerRatesEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>
/// Persisted per-server drop and Zen rate multipliers (alongside <c>GameServerDefinition.ExperienceRate</c>).
/// </summary>
public sealed class ServerRatesEntry
{
    /// <summary>Gets or sets the zero-based game server identifier (<c>GameServerDefinition.ServerID</c>).</summary>
    public byte ServerId { get; set; }

    /// <summary>Gets or sets the item drop rate multiplier for this server (<c>1.0</c> = standard).</summary>
    public float DropRate { get; set; } = 1.0f;

    /// <summary>Gets or sets the Zen drop amount multiplier for this server (<c>1.0</c> = standard).</summary>
    public float ZenRate { get; set; } = 1.0f;
}

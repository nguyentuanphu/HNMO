// <copyright file="WorldBossAliveState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A world boss which is on a map now.
/// </summary>
public sealed class WorldBossAliveState
{
    /// <summary>Gets or sets the boss id.</summary>
    public string BossId { get; set; } = string.Empty;

    /// <summary>Gets or sets the game server which hosts it.</summary>
    public byte ServerId { get; set; }

    /// <summary>Gets or sets the identifier of this appearance (also of the kill).</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets the x coordinate where it appeared.</summary>
    public byte X { get; set; }

    /// <summary>Gets or sets the y coordinate where it appeared.</summary>
    public byte Y { get; set; }

    /// <summary>Gets or sets when it appeared.</summary>
    public DateTime SpawnedAtUtc { get; set; }

    /// <summary>Gets or sets when it leaves.</summary>
    public DateTime DespawnAtUtc { get; set; }

    /// <summary>Gets or sets its maximum health.</summary>
    public long MaximumHealth { get; set; }

    /// <summary>Gets or sets the damage by character id.</summary>
    public Dictionary<string, WorldBossDamageEntry> Damage { get; set; } = new(StringComparer.Ordinal);
}

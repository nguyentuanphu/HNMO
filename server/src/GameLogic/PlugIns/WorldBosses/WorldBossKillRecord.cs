// <copyright file="WorldBossKillRecord.cs" company="HNMO">
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
/// A kill (or an escape) of a world boss.
/// </summary>
public sealed class WorldBossKillRecord
{
    /// <summary>Gets or sets the identifier of the appearance.</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the boss id.</summary>
    public string BossId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets the game server.</summary>
    public byte ServerId { get; set; }

    /// <summary>Gets or sets when it appeared.</summary>
    public DateTime SpawnedAtUtc { get; set; }

    /// <summary>Gets or sets when it was killed or left.</summary>
    public DateTime EndedAtUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether it was killed (otherwise it left).</summary>
    public bool Killed { get; set; }

    /// <summary>Gets or sets the name of the character with the last hit.</summary>
    public string? LastHit { get; set; }

    /// <summary>Gets or sets all damage dealt.</summary>
    public long TotalDamage { get; set; }

    /// <summary>Gets or sets the damage table, best first.</summary>
    public List<WorldBossKillLine> Lines { get; set; } = new();
}

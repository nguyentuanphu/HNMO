// <copyright file="WorldBossKillLine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A line of the damage table of a kill.
/// </summary>
public sealed class WorldBossKillLine
{
    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the party key, if any.</summary>
    public string? Party { get; set; }

    /// <summary>Gets or sets the damage.</summary>
    public long Damage { get; set; }

    /// <summary>Gets or sets the share of all damage, in percent.</summary>
    public double Share { get; set; }

    /// <summary>Gets or sets the boss-hunt points.</summary>
    public int Points { get; set; }

    /// <summary>Gets or sets the reward tiers, e.g. "top1", "participation", "lasthit".</summary>
    public List<string> Tiers { get; set; } = new();
}

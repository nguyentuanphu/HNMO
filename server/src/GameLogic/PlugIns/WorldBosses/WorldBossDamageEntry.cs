// <copyright file="WorldBossDamageEntry.cs" company="HNMO">
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
/// The damage of one player (or of a party) to a world boss.
/// </summary>
public sealed class WorldBossDamageEntry
{
    /// <summary>Gets or sets the character id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the party (the party master's name), if the player was in one.</summary>
    public string? Party { get; set; }

    /// <summary>Gets or sets the damage.</summary>
    public long Damage { get; set; }
}

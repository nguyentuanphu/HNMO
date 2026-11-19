// <copyright file="GuildSiegeResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A castle siege won by a guild.
/// </summary>
public sealed class GuildSiegeResult
{
    /// <summary>Gets or sets when it ended.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the persistent guild id.</summary>
    public Guid GuildId { get; set; }

    /// <summary>Gets or sets the guild's name.</summary>
    public string Name { get; set; } = string.Empty;
}

// <copyright file="TopRaceEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A character's (or a guild's) value in a running race.
/// </summary>
public sealed class TopRaceEntry
{
    /// <summary>Gets or sets the key: the character id, or "guild:" and the persistent guild id.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's or guild's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name (empty for a guild).</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the class (<see cref="TopRaceClass"/>); -1 for a guild.</summary>
    public int Class { get; set; } = -1;

    /// <summary>Gets or sets the value.</summary>
    public long Value { get; set; }

    /// <summary>Gets or sets when the value was last raised (an earlier time wins a tie).</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

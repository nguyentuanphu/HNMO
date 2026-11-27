// <copyright file="PartyAdSnapshot.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.IO;
using System.Text.Json;

/// <summary>Saved party advertisement record for persistence across reconnects or website preview.</summary>
public sealed class PartyAdSnapshot
{
    /// <summary>Gets or sets leader character name.</summary>
    public string LeaderName { get; set; } = string.Empty;

    /// <summary>Gets or sets map name.</summary>
    public string MapName { get; set; } = string.Empty;

    /// <summary>Gets or sets hunting spot label.</summary>
    public string SpotName { get; set; } = string.Empty;

    /// <summary>Gets or sets min level.</summary>
    public int MinLevel { get; set; } = 1;

    /// <summary>Gets or sets max level (0 = no upper limit).</summary>
    public int MaxLevel { get; set; }

    /// <summary>Gets or sets a value indicating whether a password is required.</summary>
    public bool HasPassword { get; set; }

    /// <summary>Gets or sets current member count.</summary>
    public int MemberCount { get; set; } = 1;

    /// <summary>Gets or sets max party size.</summary>
    public int MaxSize { get; set; } = 5;

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

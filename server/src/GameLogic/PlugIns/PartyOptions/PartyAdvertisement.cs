// <copyright file="PartyAdvertisement.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.Collections.Concurrent;

/// <summary>In-memory live party advertisement entry held by <see cref="PartyOptionsEngine"/>.</summary>
public sealed class PartyAdvertisement
{
    /// <summary>Gets or sets leader character name.</summary>
    public string LeaderName { get; set; } = string.Empty;

    /// <summary>Gets or sets current map name.</summary>
    public string MapName { get; set; } = string.Empty;

    /// <summary>Gets or sets hunting spot label.</summary>
    public string SpotName { get; set; } = string.Empty;

    /// <summary>Gets or sets optional password (never sent on public API).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets minimum character level.</summary>
    public int MinLevel { get; set; } = 1;

    /// <summary>Gets or sets maximum character level (0 = unrestricted).</summary>
    public int MaxLevel { get; set; }

    /// <summary>Gets or sets current member count.</summary>
    public int MemberCount { get; set; } = 1;

    /// <summary>Gets or sets maximum party size.</summary>
    public int MaxSize { get; set; } = 5;

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

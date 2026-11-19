// <copyright file="WorldSeason.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

/// <summary>
/// The game season a world plays. It decides which game data and which season-only features the world's
/// game servers use, and which data files the web game loads for a player of that world.
/// </summary>
public enum WorldSeason
{
    /// <summary>Season 6 Episode 3: the default, what every world was before worlds had a season.</summary>
    Season6 = 0,

    /// <summary>Season 9.</summary>
    Season9 = 1,
}

/// <summary>Texts and keys of a <see cref="WorldSeason"/>.</summary>
public static class WorldSeasonExtensions
{
    /// <summary>Gets the short key used in addresses and the website's data ("s6", "s9").</summary>
    /// <param name="season">The season.</param>
    /// <returns>The key.</returns>
    public static string ToKey(this WorldSeason season) => season == WorldSeason.Season9 ? "s9" : "s6";

    /// <summary>Gets the badge players see next to a world ("S6.3", "S9").</summary>
    /// <param name="season">The season.</param>
    /// <returns>The badge text.</returns>
    public static string ToBadge(this WorldSeason season) => season == WorldSeason.Season9 ? "S9" : "S6.3";

    /// <summary>Reads a key written by <see cref="ToKey"/>; anything else is Season 6.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The season.</returns>
    public static WorldSeason ParseSeasonKey(string? key) =>
        string.Equals(key?.Trim(), "s9", StringComparison.OrdinalIgnoreCase) ? WorldSeason.Season9 : WorldSeason.Season6;
}

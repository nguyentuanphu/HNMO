// <copyright file="HnmoSeasonInstance.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.IO;

/// <summary>
/// Which season this server program plays. Each season runs as its own game server program with its own database,
/// ports and save files, started by the HNMO folder's launcher. Season 6.3 is the default and keeps today's
/// database, ports and files exactly; a Season 9 program is started with <c>HNMO_SEASON=s9</c>, and the HNMO Arena
/// (battles across servers and seasons) with <c>HNMO_SEASON=arena</c>.
/// </summary>
public static class HnmoSeasonInstance
{
    /// <summary>The environment variable which names the season (<c>s6</c> or <c>s9</c>).</summary>
    public const string EnvironmentVariableName = "HNMO_SEASON";

    /// <summary>The key of Season 6.3, the default.</summary>
    public const string SeasonSix = "s6";

    /// <summary>The key of Season 9.</summary>
    public const string SeasonNine = "s9";

    /// <summary>The key of the HNMO Arena program.</summary>
    public const string Arena = "arena";

    /// <summary>How far Season 9's ports are from Season 6.3's: connect server 44406 becomes 44506, and so on.</summary>
    public const int SeasonNinePortOffset = 100;

    /// <summary>How far the Arena's ports are from Season 6.3's: connect server 44406 becomes 44606, and so on.</summary>
    public const int ArenaPortOffset = 200;

    /// <summary>Gets the season of this program, read once from <see cref="EnvironmentVariableName"/>.</summary>
    public static string Current { get; } = Parse(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>Gets a value indicating whether this program plays Season 9.</summary>
    public static bool IsSeasonNine => Current == SeasonNine;

    /// <summary>Gets a value indicating whether this program is the HNMO Arena.</summary>
    public static bool IsArena => Current == Arena;

    /// <summary>
    /// Gets a value indicating whether this program runs next to Season 6.3's (Season 9 or the Arena): its own database,
    /// ports and save files, with the accounts and coins of Season 6.3's database.
    /// </summary>
    public static bool IsSecondary => Current != SeasonSix;

    /// <summary>Reads a season key; anything but <c>s9</c> or <c>arena</c> is Season 6.3.</summary>
    /// <param name="value">The value, for example of <see cref="EnvironmentVariableName"/>.</param>
    /// <returns><see cref="SeasonNine"/>, <see cref="Arena"/> or <see cref="SeasonSix"/>.</returns>
    public static string Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        SeasonNine => SeasonNine,
        Arena => Arena,
        _ => SeasonSix,
    };

    /// <summary>The database of a season: Season 6.3 keeps the configured name, Season 9 adds <c>_s9</c>, the Arena <c>_arena</c>.</summary>
    /// <param name="season">The season key.</param>
    /// <param name="databaseName">The configured database name (Season 6.3's).</param>
    /// <returns>The season's database name.</returns>
    public static string DatabaseNameFor(string season, string databaseName)
    {
        if (season is not (SeasonNine or Arena) || string.IsNullOrWhiteSpace(databaseName))
        {
            return databaseName;
        }

        return databaseName.EndsWith("_" + season, StringComparison.OrdinalIgnoreCase) ? databaseName : databaseName + "_" + season;
    }

    /// <summary>The shared (Season 6.3) database of a season's database: <c>hnmo_s9</c> and <c>hnmo_arena</c> give <c>hnmo</c>.</summary>
    /// <param name="databaseName">A season's database name.</param>
    /// <returns>The shared database name.</returns>
    public static string SharedDatabaseName(string databaseName)
    {
        foreach (var season in new[] { SeasonNine, Arena })
        {
            if (databaseName.EndsWith("_" + season, StringComparison.OrdinalIgnoreCase))
            {
                return databaseName[..^(season.Length + 1)];
            }
        }

        return databaseName;
    }

    /// <summary>How far a season's ports are from the configured (Season 6.3) ports.</summary>
    /// <param name="season">The season key.</param>
    /// <returns>0 for Season 6.3, <see cref="SeasonNinePortOffset"/> for Season 9, <see cref="ArenaPortOffset"/> for the Arena.</returns>
    public static int PortOffsetFor(string season) => season switch
    {
        SeasonNine => SeasonNinePortOffset,
        Arena => ArenaPortOffset,
        _ => 0,
    };

    /// <summary>
    /// Where a season keeps a save file: Season 6.3 at the given path, Season 9 in the <c>s9</c> folder next to it and the
    /// Arena in the <c>arena</c> folder, so two programs never write the same file. Paths already in that folder stay.
    /// </summary>
    /// <param name="season">The season key.</param>
    /// <param name="filePath">The file's path for Season 6.3.</param>
    /// <returns>The season's path.</returns>
    public static string SaveFileFor(string season, string filePath)
    {
        if (season is not (SeasonNine or Arena) || string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.Equals(Path.GetFileName(directory), season, StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }

        return Path.Combine(directory ?? string.Empty, season, Path.GetFileName(filePath));
    }
}

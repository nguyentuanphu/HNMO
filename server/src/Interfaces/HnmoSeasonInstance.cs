// <copyright file="HnmoSeasonInstance.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.IO;

/// <summary>
/// Which season this server program plays. Each season runs as its own game server program with its own database,
/// ports and save files, started by the HNMO folder's launcher. Season 6.3 is the default and keeps today's
/// database, ports and files exactly; a Season 9 program is started with <c>HNMO_SEASON=s9</c>.
/// </summary>
public static class HnmoSeasonInstance
{
    /// <summary>The environment variable which names the season (<c>s6</c> or <c>s9</c>).</summary>
    public const string EnvironmentVariableName = "HNMO_SEASON";

    /// <summary>The key of Season 6.3, the default.</summary>
    public const string SeasonSix = "s6";

    /// <summary>The key of Season 9.</summary>
    public const string SeasonNine = "s9";

    /// <summary>How far Season 9's ports are from Season 6.3's: connect server 44406 becomes 44506, and so on.</summary>
    public const int SeasonNinePortOffset = 100;

    /// <summary>Gets the season of this program, read once from <see cref="EnvironmentVariableName"/>.</summary>
    public static string Current { get; } = Parse(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>Gets a value indicating whether this program plays Season 9.</summary>
    public static bool IsSeasonNine => Current == SeasonNine;

    /// <summary>Reads a season key; anything but <c>s9</c> is Season 6.3.</summary>
    /// <param name="value">The value, for example of <see cref="EnvironmentVariableName"/>.</param>
    /// <returns><see cref="SeasonNine"/> or <see cref="SeasonSix"/>.</returns>
    public static string Parse(string? value) =>
        string.Equals(value?.Trim(), SeasonNine, StringComparison.OrdinalIgnoreCase) ? SeasonNine : SeasonSix;

    /// <summary>The database of a season: Season 6.3 keeps the configured name, Season 9 adds <c>_s9</c>.</summary>
    /// <param name="season">The season key.</param>
    /// <param name="databaseName">The configured database name (Season 6.3's).</param>
    /// <returns>The season's database name.</returns>
    public static string DatabaseNameFor(string season, string databaseName)
    {
        if (season != SeasonNine || string.IsNullOrWhiteSpace(databaseName))
        {
            return databaseName;
        }

        return databaseName.EndsWith("_" + SeasonNine, StringComparison.OrdinalIgnoreCase) ? databaseName : databaseName + "_" + SeasonNine;
    }

    /// <summary>How far a season's ports are from the configured (Season 6.3) ports.</summary>
    /// <param name="season">The season key.</param>
    /// <returns>0 for Season 6.3, <see cref="SeasonNinePortOffset"/> for Season 9.</returns>
    public static int PortOffsetFor(string season) => season == SeasonNine ? SeasonNinePortOffset : 0;

    /// <summary>
    /// Where a season keeps a save file: Season 6.3 at the given path, Season 9 in the <c>s9</c> folder next to it, so
    /// two programs never write the same file. Paths already in that folder stay as they are.
    /// </summary>
    /// <param name="season">The season key.</param>
    /// <param name="filePath">The file's path for Season 6.3.</param>
    /// <returns>The season's path.</returns>
    public static string SaveFileFor(string season, string filePath)
    {
        if (season != SeasonNine || string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.Equals(Path.GetFileName(directory), SeasonNine, StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }

        return Path.Combine(directory ?? string.Empty, SeasonNine, Path.GetFileName(filePath));
    }
}

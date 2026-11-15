// <copyright file="MissionConfigurationText.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Globalization;

/// <summary>
/// The short text forms of the lists of the missions configuration, as the admin panel's "Missions and rewards" page
/// edits them: reward items as "group number level count" separated by ";" (e.g. "14 13 0 1; 12 15 0 2"), and levels
/// or days with their coins as "number:coins" separated by "," (e.g. "100:20, 200:30").
/// </summary>
public static class MissionConfigurationText
{
    /// <summary>
    /// Formats reward items.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The text.</returns>
    public static string FormatItems(IEnumerable<MissionItemReward> items) =>
        string.Join("; ", items.Select(i => string.Create(CultureInfo.InvariantCulture, $"{i.Group} {i.Number} {i.Level} {i.Count}")));

    /// <summary>
    /// Parses reward items; the level and the count may be left out (0 and 1).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="items">The items.</param>
    /// <returns><c>true</c>, if the text is valid.</returns>
    public static bool TryParseItems(string? text, out List<MissionItemReward> items)
    {
        items = new List<MissionItemReward>();
        foreach (var part in Split(text, ';'))
        {
            var numbers = part.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
            if (numbers.Length is < 2 or > 4
                || !short.TryParse(numbers[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var group)
                || !short.TryParse(numbers[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            byte level = 0;
            var count = 1;
            if ((numbers.Length > 2 && !byte.TryParse(numbers[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
                || (numbers.Length > 3 && !int.TryParse(numbers[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                || group is < 0 or > 15 || number is < 0 or > 511 || level > 15 || count is < 1 or > 50)
            {
                return false;
            }

            items.Add(new MissionItemReward { Group = group, Number = number, Level = level, Count = count });
        }

        return true;
    }

    /// <summary>
    /// Formats numbers with their coins ("100:20, 200:30").
    /// </summary>
    /// <param name="pairs">The pairs.</param>
    /// <returns>The text.</returns>
    public static string FormatPairs(IEnumerable<(int Number, int Coins)> pairs) =>
        string.Join(", ", pairs.OrderBy(p => p.Number).Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Number}:{p.Coins}")));

    /// <summary>
    /// Parses numbers with their coins.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maximumNumber">The highest allowed number.</param>
    /// <param name="pairs">The pairs, ordered by number; a repeated number keeps the last coins.</param>
    /// <returns><c>true</c>, if the text is valid.</returns>
    public static bool TryParsePairs(string? text, int maximumNumber, out List<(int Number, int Coins)> pairs)
    {
        var result = new SortedDictionary<int, int>();
        pairs = new List<(int Number, int Coins)>();
        foreach (var part in Split(text, ','))
        {
            var halves = part.Split(':', StringSplitOptions.TrimEntries);
            if (halves.Length != 2
                || !int.TryParse(halves[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                || !int.TryParse(halves[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var coins)
                || number < 1 || number > maximumNumber || coins < 0 || coins > 1_000_000)
            {
                return false;
            }

            result[number] = coins;
        }

        pairs = result.Select(p => (p.Key, p.Value)).ToList();
        return true;
    }

    private static IEnumerable<string> Split(string? text, char separator) =>
        (text ?? string.Empty).Split([separator, '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

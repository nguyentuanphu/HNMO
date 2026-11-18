// <copyright file="PlayerRankingEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PlayerRanking;

using System.Text.Json;
using HNMO.GameLogic.Attributes;

/// <summary>
/// Pure ranking aggregation, sorting, and bulk stat-allocation planner for <see cref="PlayerRankingPlugIn"/>.
/// </summary>
public static class PlayerRankingEngine
{
    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Parses and validates a JSON bulk stat-allocation payload against available level-up points and class rules.
    /// </summary>
    /// <param name="jsonPayload">JSON object containing any combination of <c>str</c>, <c>agi</c>, <c>vit</c>, <c>ene</c>, <c>cmd</c>.</param>
    /// <param name="availablePoints">Current unspent character level-up points.</param>
    /// <param name="allowsCommand">Whether the character class supports the Command stat (Dark Lord).</param>
    /// <returns>The validated <see cref="StatAllocationPlan"/>.</returns>
    public static StatAllocationPlan PlanBulkAddStats(string? jsonPayload, int availablePoints, bool allowsCommand)
    {
        if (string.IsNullOrWhiteSpace(jsonPayload))
        {
            return Invalid("empty");
        }

        BulkStatInput? input;
        try
        {
            input = JsonSerializer.Deserialize<BulkStatInput>(jsonPayload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return Invalid("invalid_json");
        }

        if (input is null)
        {
            return Invalid("empty");
        }

        if (input.Str < 0 || input.Agi < 0 || input.Vit < 0 || input.Ene < 0 || input.Cmd < 0)
        {
            return Invalid("negative");
        }

        if (input.Cmd > 0 && !allowsCommand)
        {
            return Invalid("no_command");
        }

        long total = (long)input.Str + input.Agi + input.Vit + input.Ene + input.Cmd;
        if (total <= 0)
        {
            return Invalid("empty");
        }

        if (total > Math.Max(0, availablePoints))
        {
            return Invalid("insufficient_points");
        }

        return new StatAllocationPlan(true, null, input.Str, input.Agi, input.Vit, input.Ene, input.Cmd);
    }

    /// <summary>
    /// Builds the ranked online-players list excluding template players and game masters.
    /// </summary>
    /// <param name="players">All active players on the game context.</param>
    /// <param name="topCount">Maximum entries to return.</param>
    /// <returns>Sorted online character rows.</returns>
    public static IReadOnlyList<OnlineRankingRow> BuildOnlineRanking(IEnumerable<Player> players, int topCount)
    {
        ArgumentNullException.ThrowIfNull(players);
        var limit = Math.Clamp(topCount, 1, 100);
        return players
            .Where(p => !p.IsTemplatePlayer
                     && p.SelectedCharacter is { CharacterStatus: CharacterStatus.Normal } c
                     && !string.IsNullOrWhiteSpace(c.Name))
            .Select(p => new
            {
                Name = p.SelectedCharacter!.Name,
                ClassName = p.SelectedCharacter.CharacterClass?.Name.ToString() ?? string.Empty,
                Level = (int)(p.Attributes?[Stats.Level] ?? p.Level),
                MasterLevel = (int)(p.Attributes?[Stats.MasterLevel] ?? 0),
                Resets = (int)(p.Attributes?[Stats.Resets] ?? 0),
                MapName = p.CurrentMap?.Definition.Name.ToString() ?? string.Empty,
            })
            .OrderByDescending(x => x.Resets)
            .ThenByDescending(x => x.MasterLevel)
            .ThenByDescending(x => x.Level)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select((x, idx) => new OnlineRankingRow(idx + 1, x.Name, x.ClassName, x.Level, x.MasterLevel, x.Resets, x.MapName))
            .ToList();
    }

    /// <summary>
    /// Serializes a ranking payload into plain-ASCII JSON (non-ASCII characters automatically escaped).
    /// </summary>
    /// <param name="payload">The payload object.</param>
    /// <returns>Plain-ASCII JSON text.</returns>
    public static string SerializeAscii(object payload) => JsonSerializer.Serialize(payload, CamelCaseJson);

    private static StatAllocationPlan Invalid(string errorCode) => new(false, errorCode, 0, 0, 0, 0, 0);

    private sealed class BulkStatInput
    {
        public int Str { get; set; }

        public int Agi { get; set; }

        public int Vit { get; set; }

        public int Ene { get; set; }

        public int Cmd { get; set; }
    }

    /// <summary>
    /// Represents a live online player ranking entry.
    /// </summary>
    /// <param name="Rank">1-based rank index.</param>
    /// <param name="Name">Character name.</param>
    /// <param name="ClassName">Localized or English character class name.</param>
    /// <param name="Level">Character normal level.</param>
    /// <param name="MasterLevel">Character master level.</param>
    /// <param name="Resets">Character resets count.</param>
    /// <param name="MapName">Current map name.</param>
    public sealed record OnlineRankingRow(
        int Rank,
        string Name,
        string ClassName,
        int Level,
        int MasterLevel,
        int Resets,
        string MapName);
}

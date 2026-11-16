// <copyright file="BufferNpcEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BufferNpc;

using HNMO.DataModel.Configuration;

/// <summary>
/// Pure, unit-tested rule evaluator for <see cref="BufferNpcPlugIn"/>.
/// </summary>
public static class BufferNpcEngine
{
    /// <summary>
    /// Evaluates whether a character at a given NPC is eligible for the Buffer NPC buff set.
    /// </summary>
    /// <param name="config">Plug-in configuration.</param>
    /// <param name="isPlugInActive">Whether the plug-in is currently enabled.</param>
    /// <param name="npcNumber">Interacted NPC number.</param>
    /// <param name="playerLevel">Character level.</param>
    /// <param name="playerResets">Character resets.</param>
    /// <param name="playerZen">Character inventory zen.</param>
    /// <param name="availableEffects">Game configuration magic effect definitions keyed by number.</param>
    /// <returns>The evaluation decision.</returns>
    public static BufferNpcDecision Evaluate(
        BufferNpcConfiguration config,
        bool isPlugInActive,
        short npcNumber,
        int playerLevel,
        int playerResets,
        int playerZen,
        IReadOnlyDictionary<short, MagicEffectDefinition> availableEffects)
    {
        if (!isPlugInActive)
        {
            return new BufferNpcDecision(false, BufferNpcDenialReason.Disabled, Array.Empty<MagicEffectDefinition>(), 0, 0);
        }

        if (config.NpcNumbers is null || !config.NpcNumbers.Contains(npcNumber))
        {
            return new BufferNpcDecision(false, BufferNpcDenialReason.NpcNotConfigured, Array.Empty<MagicEffectDefinition>(), 0, 0);
        }

        var maxLevel = Math.Max(1, config.MaxLevel);
        if (playerLevel > maxLevel)
        {
            return new BufferNpcDecision(false, BufferNpcDenialReason.LevelTooHigh, Array.Empty<MagicEffectDefinition>(), 0, 0);
        }

        if (config.MaxResets >= 0 && playerResets > config.MaxResets)
        {
            return new BufferNpcDecision(false, BufferNpcDenialReason.ResetsTooHigh, Array.Empty<MagicEffectDefinition>(), 0, 0);
        }

        var zenCost = Math.Max(0, config.ZenCost);
        if (playerZen < zenCost)
        {
            return new BufferNpcDecision(false, BufferNpcDenialReason.NotEnoughZen, Array.Empty<MagicEffectDefinition>(), 0, zenCost);
        }

        var resolved = new List<MagicEffectDefinition>();
        var seen = new HashSet<short>();
        foreach (var number in config.BuffEffectNumbers ?? [])
        {
            if (!seen.Add(number))
            {
                continue;
            }

            if (availableEffects.TryGetValue(number, out var effectDef)
                && effectDef.PowerUpDefinitions.Any(d => d.Boost is not null && d.TargetAttribute is not null))
            {
                resolved.Add(effectDef);
            }
        }

        if (resolved.Count == 0)
        {
            return new BufferNpcDecision(false, BufferNpcDenialReason.NoValidEffects, Array.Empty<MagicEffectDefinition>(), 0, 0);
        }

        return new BufferNpcDecision(
            true,
            BufferNpcDenialReason.None,
            resolved,
            Math.Max(0, config.DurationSeconds),
            zenCost);
    }

    /// <summary>
    /// Parses a comma- or whitespace-separated list of integer IDs into distinct positive numbers.
    /// </summary>
    /// <param name="text">Raw input text.</param>
    /// <returns>Parsed short IDs in order of first appearance.</returns>
    public static List<short> ParseShortIdList(string? text)
    {
        var result = new List<short>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        var seen = new HashSet<short>();
        var tokens = text.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (short.TryParse(token, out var id) && id > 0 && seen.Add(id))
            {
                result.Add(id);
            }
        }

        return result;
    }
}

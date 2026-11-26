// <copyright file="PentagramStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

using System.Text;
using System.Text.Json;
using HNMO.DataModel.Entities;

/// <summary>Writes the pentagram state the web client shows (HnmoPentagramState, <c>F5 59</c>), as plain-ASCII JSON.</summary>
public static class PentagramStateWriter
{
    /// <summary>The state when the system is off or this isn't a Season 9 realm.</summary>
    public const string DisabledState = "{\"v\":1,\"enabled\":false,\"worn\":null,\"items\":[]}";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>Writes the state of a player.</summary>
    /// <param name="player">The player.</param>
    /// <param name="result">The result of the last action, or <c>null</c>.</param>
    /// <returns>The JSON.</returns>
    public static string Write(Player player, PentagramActionResult? result = null)
    {
        if (PentagramActions.Configuration(player) is not { } configuration)
        {
            return DisabledState;
        }

        object? Errtel(ErrtelInfo? info) => info is null
            ? null
            : new
            {
                number = info.Number,
                element = (byte)info.Element,
                options = info.Options.Select(o => new
                {
                    index = o.Index,
                    level = o.Level,
                    value = Pentagram.OptionValue(configuration, o.Index, o.Level),
                    name = PentagramData.Option(configuration, o.Index)?.Name ?? string.Empty,
                }).ToList(),
            };

        var worn = Pentagram.Worn(player);
        var items = new List<object>();
        foreach (var item in player.Inventory?.Items ?? [])
        {
            if (Pentagram.IsPentagram(item.Definition))
            {
                items.Add(new { slot = item.ItemSlot, number = item.Definition!.Number, element = item.PentagramElement, errtels = Pentagram.ReadSlots(item).Select(Errtel).ToList(), usable = Pentagram.UsableSlots(configuration, item.Definition) });
            }
            else if (Pentagram.IsErrtel(item.Definition))
            {
                items.Add(new { slot = item.ItemSlot, number = item.Definition!.Number, element = item.PentagramElement, errtel = Errtel(Pentagram.ReadErrtel(item)) });
            }
        }

        var payload = new
        {
            v = 1,
            enabled = true,
            worn = worn is null
                ? null
                : new
                {
                    number = worn.Definition!.Number,
                    element = worn.PentagramElement,
                    usable = Pentagram.UsableSlots(configuration, worn.Definition),
                    errtels = Pentagram.ReadSlots(worn).Select(Errtel).ToList(),
                },
            items,
            costs = new
            {
                levelUpZenPerLevel = configuration.ErrtelLevelUpZenPerLevel,
                rankUpZen = configuration.ErrtelRankUpZen,
                extractZen = configuration.ErrtelExtractZen,
                mixNpc = configuration.PentagramMixNpcNumber,
            },
            result = result?.ToString(),
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static string EscapeNonAscii(string json)
    {
        var sb = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c <= 0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u").Append(((int)c).ToString("x4"));
            }
        }

        return sb.ToString();
    }
}

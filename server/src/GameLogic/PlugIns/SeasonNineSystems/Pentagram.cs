// <copyright file="Pentagram.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

using System.Globalization;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;

/// <summary>
/// The Season 9 pentagram and errtel rules: which items they are, how the errtels sit in the pentagram's five slots
/// (<see cref="Item.PentagramErrtels"/>), and the elemental damage a hit adds.
/// </summary>
/// <remarks>
/// The option values are HNMO's own errtel data (<see cref="SeasonNineSystemsCatalog.ErrtelOptions"/>: the value at
/// levels 0, 5 and 10, linear in between). A hit's elemental damage is the attacker's elemental attack less the
/// defender's elemental defense, scaled by the element advantage
/// (<see cref="SeasonNineSystemsEngine.CalculateElementalMultiplier"/>) and the errtels' "against an element" and PvP
/// bonuses. Only a character wearing a pentagram of an element deals it. Monsters have no element in HNMO's data, so
/// against them the elemental attack counts without advantage.
/// </remarks>
public static class Pentagram
{
    /// <summary>The number of errtel slots of a pentagram.</summary>
    public const int SlotCount = 5;

    private const byte ItemGroup = 12;

    /// <summary>Gets a value indicating whether the item is a pentagram.</summary>
    /// <param name="definition">The item definition.</param>
    /// <returns><c>true</c> for a pentagram.</returns>
    public static bool IsPentagram(ItemDefinition? definition)
        => definition is { Group: ItemGroup, Number: >= 200 and <= 214 };

    /// <summary>Gets a value indicating whether the item is an errtel.</summary>
    /// <param name="definition">The item definition.</param>
    /// <returns><c>true</c> for an errtel.</returns>
    public static bool IsErrtel(ItemDefinition? definition)
        => definition is { Group: ItemGroup, Number: 221 or 231 or 241 or 251 or 261 };

    /// <summary>Gets the number of usable errtel slots of a pentagram: its grade (1..3) plus two, at most five.</summary>
    /// <param name="definition">The pentagram's definition.</param>
    /// <returns>The usable slots.</returns>
    public static int UsableSlots(ItemDefinition definition)
    {
        var grade = SeasonNineSystemsCatalog.Pentagrams.FirstOrDefault(p => p.ItemNumber == definition.Number).Grade;
        return Math.Clamp(grade + 2, 1, SlotCount);
    }

    /// <summary>Reads the element and options of an errtel item.</summary>
    /// <param name="errtel">The errtel.</param>
    /// <returns>Its description.</returns>
    public static ErrtelInfo ReadErrtel(Item errtel)
        => new(errtel.Definition?.Number ?? 0, (PentagramElement)errtel.PentagramElement, ParseOptions(errtel.PentagramErrtels));

    /// <summary>Writes the element and options of an errtel item.</summary>
    /// <param name="errtel">The errtel.</param>
    /// <param name="info">Its element and options.</param>
    public static void WriteErrtel(Item errtel, ErrtelInfo info)
    {
        errtel.PentagramElement = (byte)info.Element;
        errtel.PentagramErrtels = WriteOptions(info.Options);
    }

    /// <summary>Reads the five slots of a pentagram.</summary>
    /// <param name="pentagram">The pentagram.</param>
    /// <returns>The errtel of each slot, or <c>null</c> for an empty one.</returns>
    public static ErrtelInfo?[] ReadSlots(Item pentagram)
    {
        var slots = new ErrtelInfo?[SlotCount];
        var parts = (pentagram.PentagramErrtels ?? string.Empty).Split('|');
        for (var i = 0; i < SlotCount && i < parts.Length; i++)
        {
            var fields = parts[i].Split('=');
            if (fields.Length == 3
                && short.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                && byte.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var element))
            {
                slots[i] = new ErrtelInfo(number, (PentagramElement)element, ParseOptions(fields[2]));
            }
        }

        return slots;
    }

    /// <summary>Writes the five slots of a pentagram.</summary>
    /// <param name="pentagram">The pentagram.</param>
    /// <param name="slots">The slots.</param>
    public static void WriteSlots(Item pentagram, IReadOnlyList<ErrtelInfo?> slots)
    {
        var parts = new string[SlotCount];
        for (var i = 0; i < SlotCount; i++)
        {
            parts[i] = i < slots.Count && slots[i] is { } errtel
                ? string.Create(CultureInfo.InvariantCulture, $"{errtel.Number}={(byte)errtel.Element}={WriteOptions(errtel.Options)}")
                : string.Empty;
        }

        pentagram.PentagramErrtels = parts.All(string.IsNullOrEmpty) ? null : string.Join('|', parts);
    }

    /// <summary>The value of an errtel option at a level: the data's values at levels 0, 5 and 10, linear in between.</summary>
    /// <param name="optionIndex">The option index.</param>
    /// <param name="level">The level (0..10).</param>
    /// <returns>The value, or 0 for an unknown option.</returns>
    public static int OptionValue(int optionIndex, byte level)
    {
        foreach (var option in SeasonNineSystemsCatalog.ErrtelOptions)
        {
            if (option.Index != optionIndex)
            {
                continue;
            }

            var l = Math.Clamp((int)level, 0, 10);
            return l <= 5
                ? option.ValueLv0 + ((option.ValueLv5 - option.ValueLv0) * l / 5)
                : option.ValueLv5 + ((option.ValueLv10 - option.ValueLv5) * (l - 5) / 5);
        }

        return 0;
    }

    /// <summary>Gets the pentagram a player wears, if any.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The pentagram, or <c>null</c>.</returns>
    public static Item? Worn(Player player)
        => player.SelectedCharacter?.Inventory?.Items.FirstOrDefault(i => i.ItemSlot == InventoryConstants.PentagramSlot && IsPentagram(i.Definition));

    /// <summary>
    /// The elemental damage one hit adds: none unless the attacker wears a pentagram of an element and the Season 9
    /// pentagram system is on in this realm.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="defender">The defender.</param>
    /// <param name="isPvp">Whether a player hits a player.</param>
    /// <returns>The elemental damage.</returns>
    public static int ElementalDamage(IAttacker attacker, IAttackable defender, bool isPvp)
    {
        if (attacker is not Player attackerPlayer
            || SeasonNineSystemsPlugIn.Get(attackerPlayer.GameContext) is not { } plugIn
            || !plugIn.IsActive()
            || plugIn.Configuration is not { PentagramEnabled: true } configuration
            || Worn(attackerPlayer) is not { PentagramElement: > 0 } pentagram)
        {
            return 0;
        }

        var attackerElement = (PentagramElement)pentagram.PentagramElement;
        var defenderPentagram = defender is Player defenderPlayer ? Worn(defenderPlayer) : null;
        var defenderElement = (PentagramElement)(defenderPentagram?.PentagramElement ?? 0);
        var attack = Sum(pentagram, isPvp, 0, 20, 120);
        var defense = defenderPentagram is null ? 0 : Sum(defenderPentagram, isPvp, 50, 70, 170);
        var damage = Math.Max(0, attack - defense);
        if (damage == 0)
        {
            return 0;
        }

        var multiplier = SeasonNineSystemsEngine.CalculateElementalMultiplier(
            attackerElement,
            defenderElement,
            configuration.PentagramElementalAdvantageBonusPct,
            configuration.PentagramElementalDamageMultiplier);
        if (defenderElement != PentagramElement.Undefined && defenderPentagram is not null)
        {
            // "+x% attack against <element>" of the attacker, "+x% defense against <element>" of the defender.
            var against = (int)defenderElement - 1;
            multiplier *= 1 + ((SumPercent(pentagram, 10 + against) + SumPercent(pentagram, 110 + against)) / 100.0);
            var from = (int)attackerElement - 1;
            multiplier *= Math.Max(0, 1 - ((SumPercent(defenderPentagram, 60 + from) + SumPercent(defenderPentagram, 160 + from)) / 100.0));
        }

        if (isPvp)
        {
            multiplier *= 1 + (SumPercent(pentagram, 140) / 100.0);
            if (defenderPentagram is not null)
            {
                multiplier *= Math.Max(0, 1 - ((SumPercent(defenderPentagram, 90) + SumPercent(defenderPentagram, 190)) / 100.0));
            }
        }

        return Math.Max(0, (int)Math.Round(damage * multiplier));
    }

    private static int Sum(Item pentagram, bool isPvp, int always, int pvp1, int pvp2)
    {
        var total = 0;
        foreach (var errtel in ReadSlots(pentagram))
        {
            if (errtel is null)
            {
                continue;
            }

            foreach (var (index, level) in errtel.Options)
            {
                if (index == always || (isPvp && (index == pvp1 || index == pvp2)))
                {
                    total += OptionValue(index, level);
                }
            }
        }

        return total;
    }

    private static int SumPercent(Item pentagram, int optionIndex)
    {
        var total = 0;
        foreach (var errtel in ReadSlots(pentagram))
        {
            if (errtel is null)
            {
                continue;
            }

            foreach (var (index, level) in errtel.Options)
            {
                if (index == optionIndex)
                {
                    total += OptionValue(index, level);
                }
            }
        }

        return total;
    }

    private static List<(int Index, byte Level)> ParseOptions(string? text)
    {
        var options = new List<(int Index, byte Level)>();
        foreach (var part in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = part.Split(':');
            if (fields.Length == 2
                && int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && byte.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
            {
                options.Add((index, Math.Min(level, (byte)10)));
            }
        }

        return options;
    }

    private static string WriteOptions(IReadOnlyList<(int Index, byte Level)> options)
        => string.Join(',', options.Select(o => string.Create(CultureInfo.InvariantCulture, $"{o.Index}:{o.Level}")));
}

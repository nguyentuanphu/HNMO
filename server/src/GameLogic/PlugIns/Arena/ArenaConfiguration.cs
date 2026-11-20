// <copyright file="ArenaConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.ComponentModel.DataAnnotations;

/// <summary>The configuration of the <see cref="ArenaPlugIn"/>: the arena map and the arena character of every class.</summary>
public sealed class ArenaConfiguration
{
    /// <summary>Gets or sets the map arena characters play on (6: the Arena).</summary>
    [Range(0, 1000)]
    public short MapNumber { get; set; } = 6;

    /// <summary>Gets or sets the HNMO state file of the Arena's ranked records (in the Arena's own save folder).</summary>
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets how many large healing potions every arena character carries.</summary>
    [Range(0, 255)]
    public byte HealingPotions { get; set; } = 100;

    /// <summary>Gets or sets the arena character of every class.</summary>
    public List<ArenaClassTemplate> Templates { get; set; } = DefaultTemplates();

    /// <summary>The default arena characters: level 300, a +9 weapon and armour set of their class with luck, skill and +8.</summary>
    /// <returns>The templates.</returns>
    public static List<ArenaClassTemplate> DefaultTemplates()
    {
        static ArenaTemplateItem Item(byte slot, byte group, short number, bool skill = false) =>
            new() { Slot = slot, Group = group, Number = number, Level = 9, Luck = true, Skill = skill, OptionLevel = 2 };
        static List<ArenaTemplateItem> Set(short number, bool helm = true)
        {
            var items = new List<ArenaTemplateItem>();
            if (helm)
            {
                items.Add(Item(2, 7, number));
            }

            items.Add(Item(3, 8, number));
            items.Add(Item(4, 9, number));
            items.Add(Item(5, 10, number));
            items.Add(Item(6, 11, number));
            return items;
        }

        return
        [
            new() { ClassFamily = 0, Name = "Dark Wizard", Level = 300, Strength = 100, Agility = 300, Vitality = 300, Energy = 1300, Items = [Item(0, 5, 8, true), .. Set(18)] },
            new() { ClassFamily = 1, Name = "Dark Knight", Level = 300, Strength = 1100, Agility = 450, Vitality = 450, Energy = 100, Items = [Item(0, 0, 16, true), .. Set(16)] },
            new() { ClassFamily = 2, Name = "Fairy Elf", Level = 300, Strength = 200, Agility = 1100, Vitality = 400, Energy = 300, Items = [Item(1, 4, 17, true), new() { Slot = 0, Group = 4, Number = 15, Durability = 255 }, .. Set(19)] },
            new() { ClassFamily = 3, Name = "Magic Gladiator", Level = 300, Strength = 700, Agility = 400, Vitality = 350, Energy = 650, Items = [Item(0, 0, 31, true), .. Set(20, helm: false)] },
            new() { ClassFamily = 4, Name = "Dark Lord", Level = 300, Strength = 700, Agility = 350, Vitality = 300, Energy = 200, Leadership = 550, Items = [Item(0, 2, 10, true), .. Set(27)] },
            new() { ClassFamily = 5, Name = "Summoner", Level = 300, Strength = 100, Agility = 300, Vitality = 300, Energy = 1300, Items = [Item(0, 5, 18, true), .. Set(24)] },
            new() { ClassFamily = 6, Name = "Rage Fighter", Level = 300, Strength = 900, Agility = 300, Vitality = 700, Energy = 100, Items = [Item(0, 0, 33, true), .. Set(59)] },
        ];
    }
}

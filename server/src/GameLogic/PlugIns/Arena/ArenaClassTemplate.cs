// <copyright file="ArenaClassTemplate.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>
/// The arena character of a class: everyone of the class plays it the same (owner's choice, 2026-11-20: same level,
/// stats and gear per class).
/// </summary>
public sealed class ArenaClassTemplate
{
    /// <summary>Gets or sets the class family: 0 Dark Wizard, 1 Dark Knight, 2 Fairy Elf, 3 Magic Gladiator, 4 Dark Lord, 5 Summoner, 6 Rage Fighter (the class number divided by 4).</summary>
    public int ClassFamily { get; set; }

    /// <summary>Gets or sets the name shown in the admin panel.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; } = 300;

    /// <summary>Gets or sets the strength.</summary>
    public int Strength { get; set; }

    /// <summary>Gets or sets the agility.</summary>
    public int Agility { get; set; }

    /// <summary>Gets or sets the vitality.</summary>
    public int Vitality { get; set; }

    /// <summary>Gets or sets the energy.</summary>
    public int Energy { get; set; }

    /// <summary>Gets or sets the command (Dark Lord).</summary>
    public int Leadership { get; set; }

    /// <summary>Gets or sets the gear.</summary>
    public List<ArenaTemplateItem> Items { get; set; } = [];

    /// <summary>Gets or sets the class's damage in the Arena, in percent (class balance; 100 = unchanged).</summary>
    [System.ComponentModel.DataAnnotations.Range(10, 500)]
    public int DamagePercent { get; set; } = 100;

    /// <summary>Gets or sets the class's toughness in the Arena, in percent (class balance; 100 = unchanged, 125 = takes 1/1.25 of the damage).</summary>
    [System.ComponentModel.DataAnnotations.Range(10, 500)]
    public int DefensePercent { get; set; } = 100;
}

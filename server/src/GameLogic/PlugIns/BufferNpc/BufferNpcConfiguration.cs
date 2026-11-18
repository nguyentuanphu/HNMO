// <copyright file="BufferNpcConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BufferNpc;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="BufferNpcPlugIn"/> (OFF by default).
/// </summary>
public class BufferNpcConfiguration
{
    /// <summary>
    /// Built-in preset list of beneficial magic effects hosts can pick in the admin panel.
    /// </summary>
    public static readonly IReadOnlyList<BufferNpcBuffPreset> Presets =
    [
        new(3, "Elf Soldier Buff (Attack & Defense + Level scaling)", "Bùa Elf Soldier (Tăng Công & Thủ theo cấp)"),
        new(1, "Greater Damage (Attack boost)", "Tăng Sát Thương"),
        new(2, "Greater Defense (Defense boost)", "Tăng Phòng Thủ"),
        new(4, "Soul Barrier (Mana shield damage reduction)", "Khiên Mana (Giảm sát thương nhận)"),
        new(5, "Critical Damage Increase", "Tăng Sát Thương Chí Mạng"),
        new(7, "Ability Recover Speed Increase (AG regen)", "Tăng Tốc Độ Hồi AG"),
        new(8, "Greater Fortitude (Swell Life Max HP)", "Gồng Máu (Tăng Máu Tối Đa)"),
    ];

    /// <summary>
    /// Gets or sets the maximum character level eligible to receive Buffer NPC buffs (inclusive).
    /// </summary>
    [Display(Name = "Max level", Description = "Characters with level <= MaxLevel receive the configured buffs.")]
    [Range(1, 1000)]
    public int MaxLevel { get; set; } = 220;

    /// <summary>
    /// Gets or sets the maximum reset count eligible for Buffer NPC buffs (<c>-1</c> ignores resets).
    /// </summary>
    [Display(Name = "Max resets (-1 = ignore)", Description = "Characters with resets <= MaxResets qualify; set -1 to ignore resets.")]
    [Range(-1, 10000)]
    public int MaxResets { get; set; } = -1;

    /// <summary>
    /// Gets or sets the buff duration override in seconds (<c>0</c> uses each magic effect's default duration).
    /// </summary>
    [Display(Name = "Buff duration (seconds)", Description = "Duration of each applied buff in seconds (0 uses each effect's default).")]
    [Range(0, 86400)]
    public int DurationSeconds { get; set; } = 1800;

    /// <summary>
    /// Gets or sets the optional Zen fee charged per Buffer NPC blessing (<c>0</c> = free).
    /// </summary>
    [Display(Name = "Zen cost", Description = "Zen fee charged when receiving buffs (0 for free).")]
    [Range(0, 2000000000)]
    public int ZenCost { get; set; }

    /// <summary>
    /// Gets or sets the NPC monster numbers acting as Buffer NPCs (default <c>[257]</c>, Elf Soldier).
    /// </summary>
    [Display(Name = "NPC numbers", Description = "NPC definition numbers that act as Buffer NPCs (257 = Elf Soldier).")]
    public List<short> NpcNumbers { get; set; } = [257];

    /// <summary>
    /// Gets or sets the magic effect numbers applied by the Buffer NPC.
    /// </summary>
    [Display(Name = "Buff magic effects", Description = "Magic effect IDs granted together when talking to the Buffer NPC.")]
    public List<short> BuffEffectNumbers { get; set; } = [3, 1, 2];
}

// <copyright file="XMasSantaEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Generic;
using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;

/// <summary>Christmas Santa Village buff types (<c>IGC_XMasEvent.ini</c>).</summary>
public enum XMasBuffKind
{
    /// <summary>Buff 91: Blessing of Christmas (+Damage and +Defense).</summary>
    BlessingOfChristmas = 91,

    /// <summary>Buff 92: Santa Healing (+500 Maximum Life).</summary>
    SantaHealing = 92,

    /// <summary>Buff 93: Santa Protection (+500 Maximum Mana).</summary>
    SantaProtection = 93,
}

/// <summary>
/// Pure helper for Santa Village Little Santa NPCs (468..475) buff calculations
/// and Snowman (467) seasonal gift drops.
/// </summary>
public static class XMasSantaEngine
{
    /// <summary>Computes Blessing of Christmas bonus Attack Damage and Defense by player level.</summary>
    /// <param name="playerLevel">Character level.</param>
    /// <returns>Bonus damage and bonus defense matching <c>IGC_XMasEvent.ini</c>.</returns>
    public static (float DamageBonus, float DefenseBonus) ComputeBlessingBonuses(int playerLevel)
    {
        if (playerLevel > 180)
        {
            return (105f, 86f);
        }

        var clampedLevel = Math.Max(1, playerLevel);
        return ((clampedLevel / 3) + 45f, (clampedLevel / 5) + 50f);
    }

    /// <summary>Builds active power-ups for the requested Santa buff (<see cref="XMasBuffKind"/>).</summary>
    /// <param name="kind">Buff kind.</param>
    /// <param name="playerLevel">Character level.</param>
    /// <returns>Active <see cref="MagicEffect.ElementWithTarget"/> array representing applied bonuses.</returns>
    public static MagicEffect.ElementWithTarget[] BuildBuffPowerUps(XMasBuffKind kind, int playerLevel) => kind switch
    {
        XMasBuffKind.SantaHealing =>
        [
            new MagicEffect.ElementWithTarget(new SimpleElement(500f, AggregateType.AddRaw), Stats.MaximumHealth),
        ],
        XMasBuffKind.SantaProtection =>
        [
            new MagicEffect.ElementWithTarget(new SimpleElement(500f, AggregateType.AddRaw), Stats.MaximumMana),
        ],
        XMasBuffKind.BlessingOfChristmas => BuildBlessingElements(playerLevel),
        _ => [],
    };

    /// <summary>Picks a weighted gift from Snowman (467) matching <c>NPC_SantaClause(1..3)_Reward.xml</c>.</summary>
    /// <param name="randomFraction">Uniform random in [0, 1).</param>
    /// <returns>Reward entry.</returns>
    public static S6EventsOwedReward PickSnowmanGift(double randomFraction)
    {
        IReadOnlyList<S6ExchangeRewardEntry> gifts =
        [
            new() { Weight = 30, NameEn = "Christmas Blessings + 30,000 Zen", NameVi = "Quà Giáng Sinh + 30.000 Zen", Zen = 30_000, ItemGroup = 12, ItemNumber = 15, ItemCount = 1 },
            new() { Weight = 25, NameEn = "Snowman Transformation Ring + 60,000 Zen", NameVi = "Nhẫn Người Tuyết + 60.000 Zen", Zen = 60_000, ItemGroup = 13, ItemNumber = 68, ItemCount = 1 },
            new() { Weight = 25, NameEn = "Christmas Firecracker + 60,000 Zen", NameVi = "Pháo Hoa Giáng Sinh + 60.000 Zen", Zen = 60_000, ItemGroup = 14, ItemNumber = 99, ItemCount = 1 },
            new() { Weight = 20, NameEn = "Pet Rudolph + 90,000 Zen", NameVi = "Tuần Lộc Rudolph + 90.000 Zen", Zen = 90_000, ItemGroup = 13, ItemNumber = 67, ItemCount = 1 },
        ];
        var picked = S6EventsEngine.PickWeightedReward(gifts, randomFraction)!;
        return new S6EventsOwedReward
        {
            Zen = picked.Zen,
            ItemGroup = picked.ItemGroup,
            ItemNumber = picked.ItemNumber,
            ItemLevel = picked.ItemLevel,
            ItemCount = picked.ItemCount,
            ReasonEn = picked.NameEn,
            ReasonVi = picked.NameVi,
        };
    }

    private static MagicEffect.ElementWithTarget[] BuildBlessingElements(int playerLevel)
    {
        var (dmg, def) = ComputeBlessingBonuses(playerLevel);
        return
        [
            new MagicEffect.ElementWithTarget(new SimpleElement(dmg, AggregateType.AddRaw), Stats.MinimumPhysBaseDmg),
            new MagicEffect.ElementWithTarget(new SimpleElement(dmg, AggregateType.AddRaw), Stats.MaximumPhysBaseDmg),
            new MagicEffect.ElementWithTarget(new SimpleElement(def, AggregateType.AddRaw), Stats.DefenseBase),
        ];
    }
}

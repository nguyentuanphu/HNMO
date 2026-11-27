// <copyright file="SnowmanInvasionPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.InvasionEvents;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// Enables the Snowman Invasion feature (OFF by default).
/// </summary>
[PlugIn]
[Display(Name = "Snowman Invasion", Description = "Spawns Transformed Snowmen and Cursed Santa on a schedule (OFF by default).")]
[Guid("9E3B61C4-12D7-4F8A-8B53-41C7D0E29A15")]
public sealed class SnowmanInvasionPlugIn : BaseInvasionPlugIn<PeriodicInvasionConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc />
    protected override ushort? AnnouncedMonsterId => InvasionMonsters.CursedSanta;

    /// <inheritdoc />
    public object CreateDefaultConfig() => InvasionConfigurationDefaults.Snowmen;

    /// <inheritdoc />
    protected override MonsterDefinition? ResolveMonsterDefinition(IGameContext gameContext, ushort monsterId)
    {
        var existing = base.ResolveMonsterDefinition(gameContext, monsterId);
        if (existing is null)
        {
            return null;
        }

        if (existing.ObjectKind == NpcObjectKind.Monster && existing.Attributes.Count > 0)
        {
            return existing;
        }

        var isBoss = monsterId == InvasionMonsters.CursedSanta;
        var isGoblin = monsterId == InvasionMonsters.CursedGoblin;
        var clone = new MonsterDefinition
        {
            Number = existing.Number,
            Designation = existing.Designation,
            ObjectKind = NpcObjectKind.Monster,
            MoveRange = isGoblin ? (byte)3 : (byte)4,
            AttackRange = 2,
            ViewRange = isGoblin ? (byte)5 : (byte)6,
            MoveDelay = TimeSpan.FromMilliseconds(isGoblin ? 400 : 450),
            AttackDelay = TimeSpan.FromMilliseconds(isGoblin ? 1800 : 1500),
            RespawnDelay = TimeSpan.FromSeconds(10),
            Attribute = 2,
            NumberOfMaximumItemDrops = isBoss ? (byte)2 : (byte)1,
        };

        var pairs = new (AttributeDefinition Stat, float Value)[]
        {
            (Stats.Level, isBoss ? 55f : isGoblin ? 70f : 35f),
            (Stats.MaximumHealth, isBoss ? 12000f : isGoblin ? 12000f : 3500f),
            (Stats.MinimumPhysBaseDmg, isBoss ? 120f : isGoblin ? 220f : 55f),
            (Stats.MaximumPhysBaseDmg, isBoss ? 165f : isGoblin ? 260f : 80f),
            (Stats.DefenseBase, isBoss ? 80f : isGoblin ? 190f : 40f),
            (Stats.AttackRatePvm, isBoss ? 180f : isGoblin ? 440f : 90f),
            (Stats.DefenseRatePvm, isBoss ? 75f : isGoblin ? 130f : 35f),
        };

        foreach (var (stat, value) in pairs)
        {
            clone.Attributes.Add(new MonsterAttribute
            {
                AttributeDefinition = stat,
                Value = value,
            });
        }

        foreach (var group in existing.DropItemGroups)
        {
            clone.DropItemGroups.Add(group);
        }

        return clone;
    }
}

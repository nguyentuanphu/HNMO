// <copyright file="MonsterValueChange.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;

/// <summary>
/// A value of a monster definition, as the monster table of the Season 6/9 server suites has it.
/// </summary>
public enum MonsterValue
{
    /// <summary>The level.</summary>
    Level,

    /// <summary>The maximum health.</summary>
    MaximumHealth,

    /// <summary>The minimum damage.</summary>
    MinimumDamage,

    /// <summary>The maximum damage.</summary>
    MaximumDamage,

    /// <summary>The defense.</summary>
    Defense,

    /// <summary>The attack rate.</summary>
    AttackRate,

    /// <summary>The defense rate (block rate).</summary>
    DefenseRate,

    /// <summary>The ice resistance, 0 to 255.</summary>
    IceResistance,

    /// <summary>The poison resistance, 0 to 255.</summary>
    PoisonResistance,

    /// <summary>The lightning resistance, 0 to 255.</summary>
    LightningResistance,

    /// <summary>The fire resistance, 0 to 255.</summary>
    FireResistance,

    /// <summary>The water resistance, 0 to 255. The suites' monsters have none.</summary>
    WaterResistance,

    /// <summary>The element (<see cref="MonsterDefinition.Attribute"/>).</summary>
    Attribute,

    /// <summary>The move range in tiles.</summary>
    MoveRange,

    /// <summary>The attack range in tiles.</summary>
    AttackRange,

    /// <summary>The view range in tiles.</summary>
    ViewRange,

    /// <summary>The move delay (move speed) in milliseconds per step.</summary>
    MoveDelay,

    /// <summary>The attack delay (attack speed) in milliseconds.</summary>
    AttackDelay,

    /// <summary>The respawn delay in seconds.</summary>
    RespawnDelay,
}

/// <summary>
/// A change of a monster value from the value HNMO had to the value of the Season 6/9 server suites.
/// </summary>
/// <param name="Monster">The monster number.</param>
/// <param name="Value">The changed value.</param>
/// <param name="Old">The value HNMO had; <see cref="float.NaN"/> when the monster didn't have the attribute.</param>
/// <param name="New">The value of the suites; <see cref="float.NaN"/> when the attribute is removed.</param>
public readonly record struct MonsterValueChange(short Monster, MonsterValue Value, float Old, float New)
{
    private const float Tolerance = 0.01f;

    /// <summary>
    /// Gets the attribute definition of an attribute value, or <c>null</c> for a property of the definition.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The attribute definition.</returns>
    public static AttributeDefinition? AttributeOf(MonsterValue value) => value switch
    {
        MonsterValue.Level => Stats.Level,
        MonsterValue.MaximumHealth => Stats.MaximumHealth,
        MonsterValue.MinimumDamage => Stats.MinimumPhysBaseDmg,
        MonsterValue.MaximumDamage => Stats.MaximumPhysBaseDmg,
        MonsterValue.Defense => Stats.DefenseBase,
        MonsterValue.AttackRate => Stats.AttackRatePvm,
        MonsterValue.DefenseRate => Stats.DefenseRatePvm,
        MonsterValue.IceResistance => Stats.IceResistance,
        MonsterValue.PoisonResistance => Stats.PoisonResistance,
        MonsterValue.LightningResistance => Stats.LightningResistance,
        MonsterValue.FireResistance => Stats.FireResistance,
        MonsterValue.WaterResistance => Stats.WaterResistance,
        _ => null,
    };

    /// <summary>
    /// Gets the current value of a monster, in the units of the suites' table.
    /// </summary>
    /// <param name="monster">The monster definition.</param>
    /// <param name="value">The value.</param>
    /// <returns>The value; <see cref="float.NaN"/> when the monster doesn't have the attribute.</returns>
    public static float Get(MonsterDefinition monster, MonsterValue value)
    {
        if (AttributeOf(value) is { } attribute)
        {
            var monsterAttribute = monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == attribute.Id);
            if (monsterAttribute is null)
            {
                return float.NaN;
            }

            return IsResistance(value) ? MathF.Round(monsterAttribute.Value * 255) : monsterAttribute.Value;
        }

        return value switch
        {
            MonsterValue.Attribute => monster.Attribute,
            MonsterValue.MoveRange => monster.MoveRange,
            MonsterValue.AttackRange => monster.AttackRange,
            MonsterValue.ViewRange => monster.ViewRange,
            MonsterValue.MoveDelay => (float)monster.MoveDelay.TotalMilliseconds,
            MonsterValue.AttackDelay => (float)monster.AttackDelay.TotalMilliseconds,
            MonsterValue.RespawnDelay => (float)monster.RespawnDelay.TotalSeconds,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    /// <summary>
    /// Sets the value of a monster; adds or removes the attribute when needed.
    /// </summary>
    /// <param name="monster">The monster definition.</param>
    /// <param name="value">The value.</param>
    /// <param name="newValue">The new value, in the units of the suites' table; <see cref="float.NaN"/> removes the attribute.</param>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Set(MonsterDefinition monster, MonsterValue value, float newValue, IContext context, GameConfiguration gameConfiguration)
    {
        if (AttributeOf(value) is { } attribute)
        {
            var monsterAttribute = monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == attribute.Id);
            if (float.IsNaN(newValue))
            {
                if (monsterAttribute is not null)
                {
                    monster.Attributes.Remove(monsterAttribute);
                }

                return;
            }

            if (monsterAttribute is null)
            {
                monsterAttribute = context.CreateNew<MonsterAttribute>();
                monsterAttribute.AttributeDefinition = attribute.GetPersistent(gameConfiguration);
                monsterAttribute.SetGuid(monster.Number, monsterAttribute.AttributeDefinition.Id.ExtractFirstTwoBytes());
                monster.Attributes.Add(monsterAttribute);
            }

            monsterAttribute.Value = IsResistance(value) ? newValue / 255 : newValue;
            return;
        }

        switch (value)
        {
            case MonsterValue.Attribute:
                monster.Attribute = (byte)newValue;
                break;
            case MonsterValue.MoveRange:
                monster.MoveRange = (byte)newValue;
                break;
            case MonsterValue.AttackRange:
                monster.AttackRange = (byte)newValue;
                break;
            case MonsterValue.ViewRange:
                monster.ViewRange = (short)newValue;
                break;
            case MonsterValue.MoveDelay:
                monster.MoveDelay = TimeSpan.FromMilliseconds(newValue);
                break;
            case MonsterValue.AttackDelay:
                monster.AttackDelay = TimeSpan.FromMilliseconds(newValue);
                break;
            case MonsterValue.RespawnDelay:
                monster.RespawnDelay = TimeSpan.FromSeconds(newValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }
    }

    /// <summary>
    /// Determines whether the monster still has the old value (so the host didn't choose another one).
    /// </summary>
    /// <param name="monster">The monster definition.</param>
    /// <returns><c>true</c> when the monster has the old value.</returns>
    public bool HasOldValue(MonsterDefinition monster) => SameValue(Get(monster, this.Value), this.Old);

    /// <summary>
    /// Determines whether the monster has the new value.
    /// </summary>
    /// <param name="monster">The monster definition.</param>
    /// <returns><c>true</c> when the monster has the new value.</returns>
    public bool HasNewValue(MonsterDefinition monster) => SameValue(Get(monster, this.Value), this.New);

    /// <summary>
    /// Sets the new value when the monster of the configuration still has the old one.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns><c>true</c> when the value was changed.</returns>
    public bool ApplyTo(IContext context, GameConfiguration gameConfiguration)
    {
        var number = this.Monster;
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == number) is not { } monster
            || !this.HasOldValue(monster))
        {
            return false;
        }

        Set(monster, this.Value, this.New, context, gameConfiguration);
        return true;
    }

    private static bool IsResistance(MonsterValue value) => value is MonsterValue.IceResistance
        or MonsterValue.PoisonResistance or MonsterValue.LightningResistance or MonsterValue.FireResistance or MonsterValue.WaterResistance;

    private static bool SameValue(float a, float b) => float.IsNaN(a) ? float.IsNaN(b) : !float.IsNaN(b) && Math.Abs(a - b) < Tolerance;
}

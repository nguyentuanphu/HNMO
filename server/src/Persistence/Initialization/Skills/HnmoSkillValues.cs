// <copyright file="HnmoSkillValues.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Skills;

using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;

/// <summary>
/// The balance values of the HNMO skills (700 to 727) and their trial quests: the damage of the skills, the strength of
/// their buffs and debuffs and what the trial quests ask for.
/// </summary>
/// <remarks>
/// They're used by the <see cref="HnmoSkillsInitializer"/> and the <see cref="HnmoTrialQuestsInitializer"/> for new
/// databases, and by <see cref="Updates.StrongerHnmoSkillsUpdatePlugIn"/>, which sets them on existing databases.
/// The values are 20 % stronger than the first ones; costs and durations stayed as they were.
/// </remarks>
internal static class HnmoSkillValues
{
    /// <summary>
    /// The part of the damage taken by Iron Guard (702): x 0.76, 24 % less damage.
    /// </summary>
    internal const float IronGuardDamageReceived = 0.76f;

    /// <summary>
    /// The part by which Mana Shield (707) reduces the damage: 36 %.
    /// </summary>
    internal const float ManaShieldDamageDecrement = 0.36f;

    /// <summary>
    /// The health which Forest Blessing (710) regenerates per tick, before the energy bonus.
    /// </summary>
    internal const float ForestBlessingHealthPerTick = 36;

    /// <summary>
    /// The health which Forest Blessing (710) regenerates per tick for each point of energy: 1 / 20 x 1.2.
    /// </summary>
    internal const float ForestBlessingHealthPerEnergy = 0.06f;

    /// <summary>
    /// The damage factor of Twin Power (714): x 1.12, 12 % more damage.
    /// </summary>
    internal const float TwinPowerDamage = 1.12f;

    /// <summary>
    /// The attack and magic speed which War Command (717) adds.
    /// </summary>
    internal const float WarCommandSpeed = 24;

    /// <summary>
    /// The defense factor of the target of Weakening Curse (722): x 0.76.
    /// </summary>
    internal const float WeakeningCurseDefense = 0.76f;

    /// <summary>
    /// The defense factor of Iron Body (726): x 1.24.
    /// </summary>
    internal const float IronBodyDefense = 1.24f;

    /// <summary>
    /// The part of the damage which Iron Body (726) reflects: 12 %.
    /// </summary>
    internal const float IronBodyReflection = 0.12f;

    /// <summary>
    /// The number of monsters which have to be killed for a trial quest.
    /// </summary>
    internal const int TrialRequiredKills = 200;

    /// <summary>
    /// The number of Jewels of Chaos which a trial quest asks for.
    /// </summary>
    internal const int TrialRequiredJewels = 5;

    /// <summary>
    /// Gets the damage of the skills which deal damage.
    /// </summary>
    internal static IReadOnlyDictionary<SkillNumber, int> Damage { get; } = new Dictionary<SkillNumber, int>
    {
        [SkillNumber.EarthSplitter] = 108,
        [SkillNumber.BloodSlash] = 72,
        [SkillNumber.KnightCharge] = 144,
        [SkillNumber.IceDragon] = 90,
        [SkillNumber.MeteorRain] = 48,
        [SkillNumber.ThunderCage] = 72,
        [SkillNumber.PhoenixArrow] = 84,
        [SkillNumber.FrostArrowRain] = 66,
        [SkillNumber.ChainLightningBlade] = 96,
        [SkillNumber.FlameWhirl] = 120,
        [SkillNumber.HeavenSword] = 156,
        [SkillNumber.ShadowRavens] = 102,
        [SkillNumber.EarthStomp] = 132,
        [SkillNumber.RoyalBeam] = 192,
        [SkillNumber.PoisonSoul] = 60,
        [SkillNumber.SoulDrain] = 84,
        [SkillNumber.DragonFist] = 108,
        [SkillNumber.WhirlKicks] = 36,
        [SkillNumber.QuakeFist] = 180,
    };

    /// <summary>
    /// Gets the constant values of the power-ups of the buffs and debuffs, by effect and target attribute.
    /// </summary>
    internal static IReadOnlyList<(MagicEffectNumber Effect, AttributeDefinition Attribute, float Value)> EffectValues { get; } =
    [
        (MagicEffectNumber.IronGuard, Stats.DamageReceiveDecrement, IronGuardDamageReceived),
        (MagicEffectNumber.ManaShield, Stats.SoulBarrierReceiveDecrement, ManaShieldDamageDecrement),
        (MagicEffectNumber.ForestBlessing, Stats.HealthRecoveryAbsolute, ForestBlessingHealthPerTick),
        (MagicEffectNumber.TwinPower, Stats.AttackDamageIncrease, TwinPowerDamage),
        (MagicEffectNumber.WarCommand, Stats.AttackSpeed, WarCommandSpeed),
        (MagicEffectNumber.WarCommand, Stats.MagicSpeed, WarCommandSpeed),
        (MagicEffectNumber.WeakeningCurse, Stats.DefenseDecrement, WeakeningCurseDefense),
        (MagicEffectNumber.IronBody, Stats.DefenseFinal, IronBodyDefense),
        (MagicEffectNumber.IronBody, Stats.DamageReflection, IronBodyReflection),
    ];

    /// <summary>
    /// Sets the values on the HNMO skills, effects and trial quests which exist in the configuration.
    /// Setting them again changes nothing.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(GameConfiguration gameConfiguration)
    {
        foreach (var (number, damage) in Damage)
        {
            if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)number) is { } skill)
            {
                skill.AttackDamage = damage;
            }
        }

        foreach (var (effectNumber, attribute, value) in EffectValues)
        {
            foreach (var powerUp in FindPowerUps(gameConfiguration, effectNumber, attribute))
            {
                powerUp.Boost!.ConstantValue.Value = value;
            }
        }

        foreach (var powerUp in FindPowerUps(gameConfiguration, MagicEffectNumber.ForestBlessing, Stats.HealthRecoveryAbsolute))
        {
            foreach (var relationship in powerUp.Boost!.RelatedValues.Where(r => r.InputAttribute?.Id == Stats.TotalEnergy.Id))
            {
                relationship.InputOperand = ForestBlessingHealthPerEnergy;
            }
        }

        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == HnmoTrialQuestsInitializer.ElfSoldierNumber) is not { } elfSoldier)
        {
            return;
        }

        foreach (var quest in elfSoldier.Quests.Where(q => q.Group == HnmoTrialQuestsInitializer.TrialQuestGroup))
        {
            foreach (var kills in quest.RequiredMonsterKills)
            {
                kills.MinimumNumber = TrialRequiredKills;
            }

            foreach (var item in quest.RequiredItems.Where(HnmoTrialQuestsInitializer.IsJewelOfChaos))
            {
                item.MinimumNumber = TrialRequiredJewels;
            }
        }
    }

    private static IEnumerable<PowerUpDefinition> FindPowerUps(GameConfiguration gameConfiguration, MagicEffectNumber effectNumber, AttributeDefinition attribute)
    {
        return gameConfiguration.MagicEffects
            .Where(e => e.Number == (short)effectNumber)
            .SelectMany(e => e.PowerUpDefinitions)
            .Where(p => p.TargetAttribute?.Id == attribute.Id && p.Boost is not null);
    }
}

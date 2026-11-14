// <copyright file="HnmoSkillsInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Skills;

using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.CharacterClasses;

/// <summary>
/// Creates the 28 HNMO skills (700 to 727, four per class) and their magic effects.
/// It's used by the data initialization of new databases and by the <see cref="Updates.AddHnmoSkillsUpdatePlugIn"/>
/// for existing ones, and does nothing when the skills exist already.
/// </summary>
/// <remarks>
/// The skill number is 700 + 4 * class + letter (A 0, B 1, C 2, D 3), with the classes in the order
/// Dark Knight, Dark Wizard, Fairy Elf, Magic Gladiator, Dark Lord, Summoner and Rage Fighter.
/// A, B and C are learned by level (see the <c>LearnSkillsByLevelPlugIn</c> of the game logic), D by the class's
/// trial quest (see <see cref="HnmoTrialQuestsInitializer"/>). Every skill uses mechanics which already exist:
/// area skill settings for area skills, the ice element for slows, the poison element for damage over time,
/// the stun effect, buffs with power-ups on existing attributes and the existing summons.
/// The damage and the strength of the effects are the <see cref="HnmoSkillValues"/>.
/// Two skills have a small plug-in of their own in the game logic: Blood Slash (life steal) and Soul Drain (mana drain).
/// </remarks>
internal class HnmoSkillsInitializer : SkillsInitializerBase
{
    /// <summary>
    /// The number of the first HNMO skill.
    /// </summary>
    internal const short FirstSkillNumber = 700;

    /// <summary>
    /// The number of the last HNMO skill.
    /// </summary>
    internal const short LastSkillNumber = 727;

    /// <summary>
    /// The level at which the A skills are learned.
    /// </summary>
    internal const int LevelOfSkillA = 80;

    /// <summary>
    /// The level at which the B skills are learned.
    /// </summary>
    internal const int LevelOfSkillB = 150;

    /// <summary>
    /// The level at which the C skills are learned.
    /// </summary>
    internal const int LevelOfSkillC = 220;

    /// <summary>
    /// The level from which the trial quest for the D skills can be done.
    /// </summary>
    internal const int LevelOfSkillD = 250;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoSkillsInitializer"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public HnmoSkillsInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        if (this.GameConfiguration.Skills.Any(s => s.Number == FirstSkillNumber))
        {
            return;
        }

        this.CreateDarkKnightSkills();
        this.CreateDarkWizardSkills();
        this.CreateFairyElfSkills();
        this.CreateMagicGladiatorSkills();
        this.CreateDarkLordSkills();
        this.CreateSummonerSkills();
        this.CreateRageFighterSkills();
    }

    private static int Damage(SkillNumber number) => HnmoSkillValues.Damage[number];

    private void CreateDarkKnightSkills()
    {
        // A: area around the hero; slows with the ice element.
        this.CreateSkill(SkillNumber.EarthSplitter, LocalizedString.FromResource(() => SkillNames.EarthSplitter), CharacterClasses.AllKnights, DamageType.Physical, Damage(SkillNumber.EarthSplitter), 3, 10, 30, LevelOfSkillA, elementalModifier: ElementalType.Ice, skillType: SkillType.AreaSkillAutomaticHits);

        // B: three hits on one target; the BloodSlashSkillPlugIn heals the hero.
        this.CreateSkill(SkillNumber.BloodSlash, LocalizedString.FromResource(() => SkillNames.BloodSlash), CharacterClasses.AllKnights, DamageType.Physical, Damage(SkillNumber.BloodSlash), 2, 12, 25, LevelOfSkillB, skillType: SkillType.AreaSkillExplicitTarget, hitsPerAttack: 3);

        // C: self buff, 24 % less damage taken for 15 seconds.
        this.CreateSkill(SkillNumber.IronGuard, LocalizedString.FromResource(() => SkillNames.IronGuard), CharacterClasses.AllKnights, abilityConsumption: 20, manaConsumption: 60, levelRequirement: LevelOfSkillC, skillType: SkillType.Buff, skillTarget: SkillTarget.ImplicitPlayer, targetRestriction: SkillTargetRestriction.Self);
        this.SetEffect(SkillNumber.IronGuard, this.CreateSkillEffect(MagicEffectNumber.IronGuard, LocalizedString.FromResource(() => MagicEffectNames.IronGuardEffect), 15, (Stats.DamageReceiveDecrement, HnmoSkillValues.IronGuardDamageReceived, AggregateType.Multiplicate)));

        // D: moves the hero to the target and stuns it for one second.
        this.CreateSkill(SkillNumber.KnightCharge, LocalizedString.FromResource(() => SkillNames.KnightCharge), CharacterClasses.AllKnights, DamageType.Physical, Damage(SkillNumber.KnightCharge), 5, 15, 40, LevelOfSkillD, movesToTarget: true);
        this.SetEffect(SkillNumber.KnightCharge, this.CreateStunEffect(1));
    }

    private void CreateDarkWizardSkills()
    {
        // A: a line in front of the hero; slows with the ice element.
        this.CreateSkill(SkillNumber.IceDragon, LocalizedString.FromResource(() => SkillNames.IceDragon), CharacterClasses.AllWizards, DamageType.Wizardry, Damage(SkillNumber.IceDragon), 7, 8, 70, LevelOfSkillA, elementalModifier: ElementalType.Ice, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.IceDragon, true, 1.5f, 1.5f, 7f);

        // B: a 3 x 3 area takes up to five hits within three seconds.
        this.CreateSkill(SkillNumber.MeteorRain, LocalizedString.FromResource(() => SkillNames.MeteorRain), CharacterClasses.AllWizards, DamageType.Wizardry, Damage(SkillNumber.MeteorRain), 6, 12, 110, LevelOfSkillB, elementalModifier: ElementalType.Fire, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.MeteorRain, false, default, default, default, true, TimeSpan.Zero, TimeSpan.FromMilliseconds(600), 1, 5, hitChancePerDistanceMultiplier: 1.0f, useTargetAreaFilter: true, targetAreaDiameter: 3);

        // C: damage over time on one target, with the poison element.
        this.CreateSkill(SkillNumber.ThunderCage, LocalizedString.FromResource(() => SkillNames.ThunderCage), CharacterClasses.AllWizards, DamageType.Wizardry, Damage(SkillNumber.ThunderCage), 6, 10, 80, LevelOfSkillC, elementalModifier: ElementalType.Poison);

        // D: self buff, 36 % less damage taken, paid with mana like the soul barrier.
        this.CreateSkill(SkillNumber.ManaShield, LocalizedString.FromResource(() => SkillNames.ManaShield), CharacterClasses.AllWizards, abilityConsumption: 30, manaConsumption: 100, levelRequirement: LevelOfSkillD, skillType: SkillType.Buff, skillTarget: SkillTarget.ImplicitPlayer, targetRestriction: SkillTargetRestriction.Self);
        var manaShield = this.CreateSkillEffect(MagicEffectNumber.ManaShield, LocalizedString.FromResource(() => MagicEffectNames.ManaShieldEffect), 60, (Stats.SoulBarrierReceiveDecrement, HnmoSkillValues.ManaShieldDamageDecrement, AggregateType.AddRaw), (Stats.SoulBarrierManaTollPerHit, 0, AggregateType.AddRaw));
        this.AddRelatedValue(manaShield.PowerUpDefinitions.Last(), Stats.MaximumMana, 0.02f); // 2 % of the maximum mana per received hit
        this.SetEffect(SkillNumber.ManaShield, manaShield);
    }

    private void CreateFairyElfSkills()
    {
        // A: pierces every target in a line.
        this.CreateSkill(SkillNumber.PhoenixArrow, LocalizedString.FromResource(() => SkillNames.PhoenixArrow), CharacterClasses.AllElfs, DamageType.Physical, Damage(SkillNumber.PhoenixArrow), 7, 8, 30, LevelOfSkillA, elementalModifier: ElementalType.Fire, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.PhoenixArrow, true, 1f, 1f, 7f);

        // B: an area around the aimed point; slows with the ice element.
        this.CreateSkill(SkillNumber.FrostArrowRain, LocalizedString.FromResource(() => SkillNames.FrostArrowRain), CharacterClasses.AllElfs, DamageType.Physical, Damage(SkillNumber.FrostArrowRain), 6, 12, 60, LevelOfSkillB, elementalModifier: ElementalType.Ice, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.FrostArrowRain, false, default, default, default, useTargetAreaFilter: true, targetAreaDiameter: 4);

        // C: party buff, health regeneration for 10 seconds.
        this.CreateSkill(SkillNumber.ForestBlessing, LocalizedString.FromResource(() => SkillNames.ForestBlessing), CharacterClasses.AllElfs, abilityConsumption: 20, manaConsumption: 80, levelRequirement: LevelOfSkillC, skillType: SkillType.Buff, skillTarget: SkillTarget.ImplicitParty, targetRestriction: SkillTargetRestriction.Player);
        var forestBlessing = this.CreateSkillEffect(MagicEffectNumber.ForestBlessing, LocalizedString.FromResource(() => MagicEffectNames.ForestBlessingEffect), 10, (Stats.HealthRecoveryAbsolute, HnmoSkillValues.ForestBlessingHealthPerTick, AggregateType.AddRaw));
        this.AddRelatedValue(forestBlessing.PowerUpDefinitions.Last(), Stats.TotalEnergy, HnmoSkillValues.ForestBlessingHealthPerEnergy); // 0.06 more health per energy, per regeneration
        this.SetEffect(SkillNumber.ForestBlessing, forestBlessing);

        // D: summons a Splinter Wolf (351), see the summon mapping of the TargetedSkillDefaultPlugin.
        this.CreateSkill(SkillNumber.SpiritWolf, LocalizedString.FromResource(() => SkillNames.SpiritWolf), CharacterClasses.AllElfs, abilityConsumption: 30, manaConsumption: 150, levelRequirement: LevelOfSkillD, skillType: SkillType.SummonMonster);
    }

    private void CreateMagicGladiatorSkills()
    {
        // A: hits up to three targets around the aimed target, with the lightning element.
        this.CreateSkill(SkillNumber.ChainLightningBlade, LocalizedString.FromResource(() => SkillNames.ChainLightningBlade), CharacterClasses.AllMGs, DamageType.Physical, Damage(SkillNumber.ChainLightningBlade), 4, 10, 35, LevelOfSkillA, elementalModifier: ElementalType.Lightning, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.ChainLightningBlade, false, default, default, default, minimumHitsPerAttack: 3, maximumHitsPerAttack: 3, useTargetAreaFilter: true, targetAreaDiameter: 6);

        // B: area around the hero.
        this.CreateSkill(SkillNumber.FlameWhirl, LocalizedString.FromResource(() => SkillNames.FlameWhirl), CharacterClasses.AllMGs, DamageType.Physical, Damage(SkillNumber.FlameWhirl), 3, 15, 50, LevelOfSkillB, elementalModifier: ElementalType.Fire, skillType: SkillType.AreaSkillAutomaticHits);

        // C: self buff, 12 % more damage of any kind for 30 seconds.
        this.CreateSkill(SkillNumber.TwinPower, LocalizedString.FromResource(() => SkillNames.TwinPower), CharacterClasses.AllMGs, abilityConsumption: 20, manaConsumption: 60, levelRequirement: LevelOfSkillC, skillType: SkillType.Buff, skillTarget: SkillTarget.ImplicitPlayer, targetRestriction: SkillTargetRestriction.Self);
        this.SetEffect(SkillNumber.TwinPower, this.CreateSkillEffect(MagicEffectNumber.TwinPower, LocalizedString.FromResource(() => MagicEffectNames.TwinPowerEffect), 30, (Stats.AttackDamageIncrease, HnmoSkillValues.TwinPowerDamage, AggregateType.Multiplicate)));

        // D: an area around the aimed point.
        this.CreateSkill(SkillNumber.HeavenSword, LocalizedString.FromResource(() => SkillNames.HeavenSword), CharacterClasses.AllMGs, DamageType.Wizardry, Damage(SkillNumber.HeavenSword), 6, 20, 120, LevelOfSkillD, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.HeavenSword, false, default, default, default, useTargetAreaFilter: true, targetAreaDiameter: 4);
    }

    private void CreateDarkLordSkills()
    {
        // A: hits up to five targets around the aimed target.
        this.CreateSkill(SkillNumber.ShadowRavens, LocalizedString.FromResource(() => SkillNames.ShadowRavens), CharacterClasses.AllLords, DamageType.Physical, Damage(SkillNumber.ShadowRavens), 6, 10, 40, LevelOfSkillA, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.ShadowRavens, false, default, default, default, minimumHitsPerAttack: 5, maximumHitsPerAttack: 5, useTargetAreaFilter: true, targetAreaDiameter: 6);

        // B: party buff, faster attacks for 20 seconds.
        this.CreateSkill(SkillNumber.WarCommand, LocalizedString.FromResource(() => SkillNames.WarCommand), CharacterClasses.AllLords, abilityConsumption: 25, manaConsumption: 70, levelRequirement: LevelOfSkillB, skillType: SkillType.Buff, skillTarget: SkillTarget.ImplicitParty, targetRestriction: SkillTargetRestriction.Player);
        this.SetEffect(SkillNumber.WarCommand, this.CreateSkillEffect(MagicEffectNumber.WarCommand, LocalizedString.FromResource(() => MagicEffectNames.WarCommandEffect), 20, (Stats.AttackSpeed, HnmoSkillValues.WarCommandSpeed, AggregateType.AddRaw), (Stats.MagicSpeed, HnmoSkillValues.WarCommandSpeed, AggregateType.AddRaw)));

        // C: area around the hero, stuns for two seconds; needs the Dark Horse.
        this.CreateSkill(SkillNumber.EarthStomp, LocalizedString.FromResource(() => SkillNames.EarthStomp), CharacterClasses.AllLords, DamageType.Physical, Damage(SkillNumber.EarthStomp), 3, 20, 60, LevelOfSkillC, skillType: SkillType.AreaSkillAutomaticHits);
        var earthStomp = this.GetSkill(SkillNumber.EarthStomp);
        earthStomp.Requirements.Add(this.CreateRequirement(Stats.IsHorseEquipped, 1));
        earthStomp.MagicEffectDef = this.CreateStunEffect(2);

        // D: a line in front of the hero.
        this.CreateSkill(SkillNumber.RoyalBeam, LocalizedString.FromResource(() => SkillNames.RoyalBeam), CharacterClasses.AllLords, DamageType.Physical, Damage(SkillNumber.RoyalBeam), 7, 20, 90, LevelOfSkillD, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.RoyalBeam, true, 1f, 1.5f, 7f);
    }

    private void CreateSummonerSkills()
    {
        // A: an area around the aimed point, damage over time with the poison element.
        this.CreateSkill(SkillNumber.PoisonSoul, LocalizedString.FromResource(() => SkillNames.PoisonSoul), CharacterClasses.AllSummoners, DamageType.Wizardry, Damage(SkillNumber.PoisonSoul), 6, 10, 70, LevelOfSkillA, elementalModifier: ElementalType.Poison, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.PoisonSoul, false, default, default, default, useTargetAreaFilter: true, targetAreaDiameter: 4);

        // B: summons a Devil (37), see the summon mapping of the TargetedSkillDefaultPlugin.
        this.CreateSkill(SkillNumber.LittleDemon, LocalizedString.FromResource(() => SkillNames.LittleDemon), CharacterClasses.AllSummoners, abilityConsumption: 25, manaConsumption: 120, levelRequirement: LevelOfSkillB, skillType: SkillType.SummonMonster);

        // C: the defense of the target drops by 24 % for 10 seconds.
        this.CreateSkill(SkillNumber.WeakeningCurse, LocalizedString.FromResource(() => SkillNames.WeakeningCurse), CharacterClasses.AllSummoners, distance: 6, abilityConsumption: 15, manaConsumption: 50, levelRequirement: LevelOfSkillC, skillType: SkillType.Buff);
        this.SetEffect(SkillNumber.WeakeningCurse, this.CreateSkillEffect(MagicEffectNumber.WeakeningCurse, LocalizedString.FromResource(() => MagicEffectNames.WeakeningCurseEffect), 10, (Stats.DefenseDecrement, HnmoSkillValues.WeakeningCurseDefense, AggregateType.Multiplicate)));

        // D: one target; the SoulDrainSkillPlugIn gives the hero mana.
        this.CreateSkill(SkillNumber.SoulDrain, LocalizedString.FromResource(() => SkillNames.SoulDrain), CharacterClasses.AllSummoners, DamageType.Wizardry, Damage(SkillNumber.SoulDrain), 6, 10, 20, LevelOfSkillD, skillType: SkillType.AreaSkillExplicitTarget);
    }

    private void CreateRageFighterSkills()
    {
        // A: a line in front of the hero.
        this.CreateSkill(SkillNumber.DragonFist, LocalizedString.FromResource(() => SkillNames.DragonFist), CharacterClasses.AllFighters, DamageType.Physical, Damage(SkillNumber.DragonFist), 5, 10, 30, LevelOfSkillA, skillType: SkillType.AreaSkillAutomaticHits);
        this.AddAreaSkillSettings(SkillNumber.DragonFist, true, 1f, 1.5f, 5f);

        // B: four hits on every target around the hero.
        this.CreateSkill(SkillNumber.WhirlKicks, LocalizedString.FromResource(() => SkillNames.WhirlKicks), CharacterClasses.AllFighters, DamageType.Physical, Damage(SkillNumber.WhirlKicks), 2, 15, 40, LevelOfSkillB, skillType: SkillType.AreaSkillAutomaticHits, hitsPerAttack: 4);

        // C: self buff, 24 % more defense and 12 % damage reflection for 30 seconds.
        this.CreateSkill(SkillNumber.IronBody, LocalizedString.FromResource(() => SkillNames.IronBody), CharacterClasses.AllFighters, abilityConsumption: 25, manaConsumption: 70, levelRequirement: LevelOfSkillC, skillType: SkillType.Buff, skillTarget: SkillTarget.ImplicitPlayer, targetRestriction: SkillTargetRestriction.Self);
        this.SetEffect(SkillNumber.IronBody, this.CreateSkillEffect(MagicEffectNumber.IronBody, LocalizedString.FromResource(() => MagicEffectNames.IronBodyEffect), 30, (Stats.DefenseFinal, HnmoSkillValues.IronBodyDefense, AggregateType.Multiplicate), (Stats.DamageReflection, HnmoSkillValues.IronBodyReflection, AggregateType.AddRaw)));

        // D: area around the hero; the lightning element moves the targets.
        this.CreateSkill(SkillNumber.QuakeFist, LocalizedString.FromResource(() => SkillNames.QuakeFist), CharacterClasses.AllFighters, DamageType.Physical, Damage(SkillNumber.QuakeFist), 3, 25, 80, LevelOfSkillD, elementalModifier: ElementalType.Lightning, skillType: SkillType.AreaSkillAutomaticHits);
    }

    private Skill GetSkill(SkillNumber number) => this.GameConfiguration.Skills.First(s => s.Number == (short)number);

    private void SetEffect(SkillNumber number, MagicEffectDefinition effect) => this.GetSkill(number).MagicEffectDef = effect;

    /// <summary>
    /// Creates a stun effect which always stuns.
    /// </summary>
    /// <remarks>
    /// It's like the stun effect of the master skills, which stuns only with the chance of its
    /// <see cref="Stats.MasteryStunChance"/> power-up; here it's 100 %.
    /// It has the number of the stun effect, so that the clients show it as such.
    /// </remarks>
    private MagicEffectDefinition CreateStunEffect(float durationInSeconds)
    {
        return this.CreateSkillEffect(
            MagicEffectNumber.Stunned,
            LocalizedString.FromResource(() => MagicEffectNames.StunEffect),
            durationInSeconds,
            (Stats.IsStunned, 1, AggregateType.AddRaw),
            (Stats.MasteryStunChance, 1, AggregateType.AddRaw));
    }

    private MagicEffectDefinition CreateSkillEffect(MagicEffectNumber number, LocalizedString name, float durationInSeconds, params (AttributeDefinition Attribute, float Value, AggregateType AggregateType)[] powerUps)
    {
        var effect = this.Context.CreateNew<MagicEffectDefinition>();
        this.GameConfiguration.MagicEffects.Add(effect);
        effect.Number = (short)number;
        effect.Name = name;
        effect.InformObservers = true;
        effect.SendDuration = false;
        effect.StopByDeath = true;
        effect.Duration = this.Context.CreateNew<PowerUpDefinitionValue>();
        effect.Duration.ConstantValue.Value = durationInSeconds;
        foreach (var (attribute, value, aggregateType) in powerUps)
        {
            var powerUp = this.Context.CreateNew<PowerUpDefinition>();
            powerUp.TargetAttribute = attribute.GetPersistent(this.GameConfiguration);
            powerUp.Boost = this.Context.CreateNew<PowerUpDefinitionValue>();
            powerUp.Boost.ConstantValue.Value = value;
            powerUp.Boost.ConstantValue.AggregateType = aggregateType;
            effect.PowerUpDefinitions.Add(powerUp);
        }

        return effect;
    }

    private void AddRelatedValue(PowerUpDefinition powerUp, AttributeDefinition inputAttribute, float multiplier)
    {
        var relationship = this.Context.CreateNew<AttributeRelationship>();
        relationship.InputAttribute = inputAttribute.GetPersistent(this.GameConfiguration);
        relationship.InputOperator = InputOperator.Multiply;
        relationship.InputOperand = multiplier;
        powerUp.Boost!.RelatedValues.Add(relationship);
    }
}

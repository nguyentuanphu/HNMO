// <copyright file="SeasonNineOnlyItemsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Craftings;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Items;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.VersionSeasonSix.Items;
using HNMO.PlugIns;

/// <summary>
/// Adds the 279 unique Season 9-only item definitions (281 rows in the Season 9 server suite item table,
/// resolving the duplicate 13-229 / 13-230 entries to Wooparoo / Wooparoo (Evolved)) when running in Season 9.
/// Existing items are never overwritten.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineOnlyItemsUpdatePlugIn_Name), Description = nameof(PlugInResources.SeasonNineOnlyItemsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B92C5D31-69F3-4A7C-82E4-8D9B3F5E2002")]
public class SeasonNineOnlyItemsUpdatePlugIn : SeasonNineUpdatePlugInBase
{
    private const byte EquipmentMaximumLevel = 15;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineOnlyItemsUpdatePlugIn"/> class.
    /// </summary>
    public SeasonNineOnlyItemsUpdatePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineOnlyItemsUpdatePlugIn"/> class with a test season selector.
    /// </summary>
    /// <param name="isSeasonNine">Returns <c>true</c> when running in Season 9 mode.</param>
    internal SeasonNineOnlyItemsUpdatePlugIn(Func<bool> isSeasonNine)
        : base(isSeasonNine)
    {
    }

    /// <inheritdoc />
    public override string Name => PlugInResources.SeasonNineOnlyItemsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SeasonNineOnlyItemsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 23, 20, 0, DateTimeKind.Utc);

    /// <summary>
    /// Ensures the 13 Combination Notes (14-191..203) and their matching level-400 5-socket weapons
    /// exist in <paramref name="config"/> without duplicating items already present.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="config">The game configuration.</param>
    /// <returns>The item definitions created during this call.</returns>
    internal static IReadOnlyList<ItemDefinition> EnsureSocketWeaponMixItems(IContext context, GameConfiguration config)
    {
        var addedItems = new List<ItemDefinition>();
        var addedSocketWeapons = new List<ItemDefinition>();
        var weaponsByKey = SeasonNineOnlyItemsCatalog.Weapons.ToDictionary(w => (w.Group, w.Number));
        var notesByNumber = SeasonNineOnlyItemsCatalog.MiscItems
            .Where(m => m.Group == 14 && m.Number is >= 191 and <= 203)
            .ToDictionary(m => m.Number);

        foreach (var (noteNumber, weaponGroup, weaponNumber) in SeasonNineOnlyItemsCatalog.SocketWeaponRecipes)
        {
            if (notesByNumber.TryGetValue(noteNumber, out var noteSpec)
                && CreateMiscItem(context, config, noteSpec) is { } note)
            {
                addedItems.Add(note);
            }

            if (weaponsByKey.TryGetValue((weaponGroup, weaponNumber), out var weaponSpec)
                && CreateWeapon(context, config, weaponSpec) is { } weapon)
            {
                addedItems.Add(weapon);
                if (weapon.MaximumSockets > 0)
                {
                    addedSocketWeapons.Add(weapon);
                }
            }
        }

        LinkSocketCraftings(config, addedSocketWeapons);
        return addedItems;
    }

    /// <inheritdoc />
    protected override ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var createdSocketItems = new List<ItemDefinition>();

        foreach (var spec in SeasonNineOnlyItemsCatalog.Weapons)
        {
            if (CreateWeapon(context, gameConfiguration, spec) is { } weapon && weapon.MaximumSockets > 0)
            {
                createdSocketItems.Add(weapon);
            }
        }

        foreach (var spec in SeasonNineOnlyItemsCatalog.Shields)
        {
            if (CreateShield(context, gameConfiguration, spec) is { } shield && shield.MaximumSockets > 0)
            {
                createdSocketItems.Add(shield);
            }
        }

        foreach (var spec in SeasonNineOnlyItemsCatalog.Armors)
        {
            if (CreateArmor(context, gameConfiguration, spec) is { } armor && armor.MaximumSockets > 0)
            {
                createdSocketItems.Add(armor);
            }
        }

        foreach (var spec in SeasonNineOnlyItemsCatalog.Wings)
        {
            CreateWing(context, gameConfiguration, spec);
        }

        foreach (var spec in SeasonNineOnlyItemsCatalog.MiscItems)
        {
            CreateMiscItem(context, gameConfiguration, spec);
        }

        LinkSocketCraftings(gameConfiguration, createdSocketItems);
        ItemStatProfiles.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static ItemDefinition? CreateWeapon(
        IContext context,
        GameConfiguration config,
        SeasonNineOnlyItemsCatalog.WeaponSpec spec)
    {
        if (ItemExists(config, spec.Group, spec.Number))
        {
            return null;
        }

        var item = CreateBaseItem(
            context,
            config,
            spec.Group,
            spec.Number,
            spec.Width,
            spec.Height,
            spec.DropLevel,
            spec.Durability,
            EquipmentMaximumLevel,
            spec.DropsFromMonsters,
            SeasonNineItemNames.ResourceManager.GetLocalizedString(spec.NameKey));

        if (spec.Slot == 0 && (spec.DarkKnightClass > 0 || spec.MagicGladiatorClass > 0 || spec.RageFighterClass > 0) && spec.Width == 1)
        {
            item.ItemSlot = config.ItemSlotTypes.First(t => t.ItemSlots.Contains(0) && t.ItemSlots.Contains(1));
        }
        else
        {
            item.ItemSlot = config.ItemSlotTypes.First(t => t.ItemSlots.Contains(spec.Slot));
        }

        if (spec.SkillNumber > 0)
        {
            item.Skill = config.Skills.FirstOrDefault(s => s.Number == spec.SkillNumber);
        }

        AddQualifiedClasses(config, item, spec.DarkWizardClass, spec.DarkKnightClass, spec.ElfClass, spec.MagicGladiatorClass, spec.DarkLordClass, spec.SummonerClass, spec.RageFighterClass);

        if (spec.MinimumDamage <= 0 && spec.MagicPower <= 0)
        {
            return item;
        }

        var weaponDmgTable = FindBonusTable(config, nameof(LevelBonusNames.DamageIncreaseWeapons));
        if (spec.MinimumDamage > 0)
        {
            AddPowerUp(context, config, item, Stats.MinimumPhysBaseDmgByWeapon, spec.MinimumDamage, AggregateType.AddRaw, weaponDmgTable);
            AddPowerUp(context, config, item, Stats.MaximumPhysBaseDmgByWeapon, spec.MaximumDamage, AggregateType.AddRaw, weaponDmgTable);
        }

        AddPowerUp(context, config, item, Stats.AttackSpeedByWeapon, spec.AttackSpeed, AggregateType.AddRaw);
        AddRequirementIfNeeded(context, config, item, Stats.Level, spec.LevelRequirement);
        AddRequirementIfNeeded(context, config, item, Stats.TotalStrengthRequirementValue, spec.StrengthRequirement);
        AddRequirementIfNeeded(context, config, item, Stats.TotalAgilityRequirementValue, spec.AgilityRequirement);
        AddRequirementIfNeeded(context, config, item, Stats.TotalEnergyRequirementValue, spec.EnergyRequirement);
        AddRequirementIfNeeded(context, config, item, Stats.TotalVitalityRequirementValue, spec.VitalityRequirement);

        AddOptionIfFound(config, item, FindLuck(config));
        AddWeaponOptionsAndRise(context, config, item, spec);
        AddWeaponKindAttributes(context, config, item, spec);

        if (spec.SocketCount > 0)
        {
            AttachWeaponSockets(config, item, spec.SocketCount);
        }

        return item;
    }

    private static void AddWeaponOptionsAndRise(
        IContext context,
        GameConfiguration config,
        ItemDefinition item,
        SeasonNineOnlyItemsCatalog.WeaponSpec spec)
    {
        if (spec.MagicPower == 0 || spec.DarkLordClass > 0 || spec.Group == (byte)ItemGroups.Swords)
        {
            AddOptionIfFound(config, item, FindOptionByNeutralName(config, ExcellentOptions.PhysicalAttackOptionsName));
            AddOptionIfFound(config, item, FindOptionByNeutralName(config, HarmonyOptions.PhysicalAttackOptionsName));

            if (spec.SkillNumber == (int)SkillNumber.PowerSlash && spec.MagicPower > 0)
            {
                AddOptionIfFound(config, item, FindOptionByTarget(config, Stats.BaseDamageBonus));
                AddRisePowerUp(context, config, item, Stats.StaffRise, spec.MagicPower, nameof(LevelBonusNames.StaffRiseEven), nameof(LevelBonusNames.StaffRiseOdd));
            }
            else
            {
                AddOptionIfFound(config, item, FindOptionByTarget(config, Stats.PhysicalBaseDmg));
                if (spec.MagicPower > 0)
                {
                    AddRisePowerUp(context, config, item, Stats.ScepterRise, spec.MagicPower, nameof(LevelBonusNames.ScepterRiseEven), nameof(LevelBonusNames.ScepterRiseOdd));
                }
            }

            return;
        }

        AddOptionIfFound(config, item, FindOptionByNeutralName(config, ExcellentOptions.WizardryAttackOptionsName));
        AddOptionIfFound(config, item, FindOptionByNeutralName(config, HarmonyOptions.WizardryAttackOptionsName));
        AddOptionIfFound(config, item, FindOptionByTarget(config, Stats.WizardryBaseDmg));
        AddRisePowerUp(context, config, item, Stats.StaffRise, spec.MagicPower, nameof(LevelBonusNames.StaffRiseEven), nameof(LevelBonusNames.StaffRiseOdd));
    }

    private static void AddRisePowerUp(
        IContext context,
        GameConfiguration config,
        ItemDefinition item,
        AttributeDefinition attribute,
        int magicPower,
        string evenKey,
        string oddKey)
    {
        var table = FindBonusTable(config, magicPower % 2 == 0 ? evenKey : oddKey);
        AddPowerUp(context, config, item, attribute, magicPower / 2.0f, AggregateType.AddRaw, table);
    }

    private static void AddWeaponKindAttributes(
        IContext context,
        GameConfiguration config,
        ItemDefinition item,
        SeasonNineOnlyItemsCatalog.WeaponSpec spec)
    {
        AddPowerUp(context, config, item, Stats.EquippedWeaponCount, 1, AggregateType.AddRaw);
        if (spec.Group < (byte)ItemGroups.Spears && spec.Width == 1)
        {
            AddPowerUp(context, config, item, Stats.DoubleWieldWeaponCount, 1, AggregateType.AddRaw);
        }

        if (spec.Group == (byte)ItemGroups.Bows)
        {
            AddPowerUp(context, config, item, Stats.AmmunitionConsumptionRate, 1, AggregateType.AddRaw);
            AddPowerUp(context, config, item, spec.Slot == 0 ? Stats.IsCrossBowEquipped : Stats.IsBowEquipped, 1, AggregateType.AddRaw);
            AddPowerUp(context, config, item, Stats.ExtraProjectiles, 1, AggregateType.AddRaw);
        }

        if (spec.Group < (byte)ItemGroups.Bows && spec.Width == 2)
        {
            AddPowerUp(context, config, item, Stats.IsTwoHandedWeaponEquipped, 1, AggregateType.AddRaw);
        }

        if (spec.Group == (byte)ItemGroups.Swords)
        {
            if (spec.RageFighterClass > 0)
            {
                AddPowerUp(context, config, item, Stats.IsGloveWeaponEquipped, 1, AggregateType.AddRaw);
            }
            else
            {
                AddPowerUp(context, config, item, spec.Width == 1 ? Stats.IsOneHandedSwordEquipped : Stats.IsTwoHandedSwordEquipped, 1, AggregateType.AddRaw);
            }
        }
        else if (spec.Group == (byte)ItemGroups.Spears)
        {
            AddPowerUp(context, config, item, Stats.IsSpearEquipped, 1, AggregateType.AddRaw);
        }
        else if (spec.Group == (byte)ItemGroups.Scepters)
        {
            var attr = spec.SkillNumber == (int)SkillNumber.ForceWave ? Stats.IsScepterEquipped : Stats.IsMaceEquipped;
            AddPowerUp(context, config, item, attr, 1, AggregateType.AddRaw);
        }
        else if (spec.Group == (byte)ItemGroups.Staff)
        {
            AddPowerUp(context, config, item, spec.Width == 1 ? Stats.IsOneHandedStaffEquipped : Stats.IsTwoHandedStaffEquipped, 1, AggregateType.AddRaw);
        }
    }

    private static ItemDefinition? CreateShield(
        IContext context,
        GameConfiguration config,
        SeasonNineOnlyItemsCatalog.ShieldSpec spec)
    {
        if (ItemExists(config, (byte)ItemGroups.Shields, spec.Number))
        {
            return null;
        }

        var shield = CreateArmorBase(
            context,
            config,
            (byte)ItemGroups.Shields,
            spec.Number,
            spec.Slot,
            spec.Width,
            spec.Height,
            SeasonNineItemNames.ResourceManager.GetLocalizedString(spec.NameKey),
            spec.DropLevel,
            0,
            spec.Durability,
            spec.LevelRequirement,
            spec.StrengthRequirement,
            spec.AgilityRequirement,
            spec.EnergyRequirement,
            spec.VitalityRequirement,
            spec.LeadershipRequirement,
            spec.DarkWizardClass,
            spec.DarkKnightClass,
            spec.ElfClass,
            spec.MagicGladiatorClass,
            spec.DarkLordClass,
            spec.SummonerClass,
            spec.RageFighterClass,
            isShield: true);

        if (spec.SkillNumber != 0)
        {
            shield.Skill = config.Skills.FirstOrDefault(s => s.Number == spec.SkillNumber);
        }

        if (spec.Defense > 0)
        {
            AddPowerUp(context, config, shield, Stats.DefenseShield, spec.Defense, AggregateType.AddRaw, FindBonusTable(config, nameof(LevelBonusNames.DefenseIncreaseShields)));
        }

        if (spec.DefenseRate > 0)
        {
            AddPowerUp(context, config, shield, Stats.DefenseRatePvm, spec.DefenseRate, AggregateType.AddRaw, FindBonusTable(config, nameof(LevelBonusNames.DefenseRateIncreaseShields)));
        }

        AddPowerUp(context, config, shield, Stats.IsShieldEquipped, 1, AggregateType.AddRaw);
        AddOptionIfFound(config, shield, FindDefenseRateOption(config));

        if (spec.SocketCount > 0)
        {
            AttachArmorSockets(config, shield, spec.SocketCount);
        }

        return shield;
    }

    private static ItemDefinition? CreateArmor(
        IContext context,
        GameConfiguration config,
        SeasonNineOnlyItemsCatalog.ArmorSpec spec)
    {
        if (ItemExists(config, spec.Group, spec.Number))
        {
            return null;
        }

        var armor = CreateArmorBase(
            context,
            config,
            spec.Group,
            spec.Number,
            spec.Slot,
            spec.Width,
            spec.Height,
            SeasonNineItemNames.ResourceManager.GetLocalizedString(spec.NameKey),
            spec.DropLevel,
            spec.Defense,
            spec.Durability,
            spec.LevelRequirement,
            spec.StrengthRequirement,
            spec.AgilityRequirement,
            spec.EnergyRequirement,
            spec.VitalityRequirement,
            spec.LeadershipRequirement,
            spec.DarkWizardClass,
            spec.DarkKnightClass,
            spec.ElfClass,
            spec.MagicGladiatorClass,
            spec.DarkLordClass,
            spec.SummonerClass,
            spec.RageFighterClass,
            isShield: false);

        if (spec.Group == (byte)ItemGroups.Gloves)
        {
            if (spec.SpeedBonus > 0)
            {
                AddPowerUp(context, config, armor, Stats.AttackSpeedAny, spec.SpeedBonus, AggregateType.AddRaw);
            }

            AddPowerUp(context, config, armor, Stats.MovementSpeedUnderwater, 0, AggregateType.Maximum, FindBonusTable(config, nameof(LevelBonusNames.RunningMovementSpeed)));
        }
        else if (spec.Group == (byte)ItemGroups.Boots)
        {
            if (spec.SpeedBonus > 0)
            {
                AddPowerUp(context, config, armor, Stats.WalkSpeed, spec.SpeedBonus, AggregateType.AddRaw);
            }

            AddPowerUp(context, config, armor, Stats.MovementSpeed, 0, AggregateType.Maximum, FindBonusTable(config, nameof(LevelBonusNames.RunningMovementSpeed)));
        }

        if (spec.SocketCount > 0)
        {
            AttachArmorSockets(config, armor, spec.SocketCount);
        }

        return armor;
    }

    private static ItemDefinition CreateArmorBase(
        IContext context,
        GameConfiguration config,
        byte group,
        short number,
        byte slot,
        byte width,
        byte height,
        LocalizedString name,
        byte dropLevel,
        int defense,
        byte durability,
        int levelRequirement,
        int strengthRequirement,
        int agilityRequirement,
        int energyRequirement,
        int vitalityRequirement,
        int leadershipRequirement,
        int darkWizardClass,
        int darkKnightClass,
        int elfClass,
        int magicGladiatorClass,
        int darkLordClass,
        int summonerClass,
        int rageFighterClass,
        bool isShield)
    {
        var armor = CreateBaseItem(context, config, group, number, width, height, dropLevel, durability, EquipmentMaximumLevel, dropsFromMonsters: true, name);
        armor.ItemSlot = config.ItemSlotTypes.First(st => st.ItemSlots.Contains(slot));
        AddRequirementIfNeeded(context, config, armor, Stats.Level, levelRequirement);
        AddRequirementIfNeeded(context, config, armor, Stats.TotalStrengthRequirementValue, strengthRequirement);
        AddRequirementIfNeeded(context, config, armor, Stats.TotalAgilityRequirementValue, agilityRequirement);
        AddRequirementIfNeeded(context, config, armor, Stats.TotalEnergyRequirementValue, energyRequirement);
        AddRequirementIfNeeded(context, config, armor, Stats.TotalVitalityRequirementValue, vitalityRequirement);
        AddRequirementIfNeeded(context, config, armor, Stats.TotalLeadershipRequirementValue, leadershipRequirement);

        if (defense > 0)
        {
            AddPowerUp(context, config, armor, Stats.DefenseBase, defense, AggregateType.AddRaw, FindBonusTable(config, nameof(LevelBonusNames.DefenseIncreaseArmors)));
        }

        AddQualifiedClasses(config, armor, darkWizardClass, darkKnightClass, elfClass, magicGladiatorClass, darkLordClass, summonerClass, rageFighterClass);
        AddOptionIfFound(config, armor, FindLuck(config));
        if (!isShield)
        {
            AddOptionIfFound(config, armor, FindDefenseOption(config));
        }

        AddOptionIfFound(config, armor, FindOptionByNeutralName(config, ExcellentOptions.DefenseOptionsName));
        AddOptionIfFound(config, armor, FindOptionByNeutralName(config, HarmonyOptions.DefenseOptionsName));
        return armor;
    }

    private static void CreateWing(
        IContext context,
        GameConfiguration config,
        SeasonNineOnlyItemsCatalog.WingSpec spec)
    {
        if (ItemExists(config, (byte)ItemGroups.Orbs, spec.Number))
        {
            return;
        }

        var wing = CreateBaseItem(
            context,
            config,
            (byte)ItemGroups.Orbs,
            spec.Number,
            spec.Width,
            spec.Height,
            spec.DropLevel,
            spec.Durability,
            EquipmentMaximumLevel,
            dropsFromMonsters: false,
            SeasonNineItemNames.ResourceManager.GetLocalizedString(spec.NameKey));

        wing.ItemSlot = config.ItemSlotTypes.First(st => st.ItemSlots.Contains(InventoryConstants.WingsSlot));
        AddRequirementIfNeeded(context, config, wing, Stats.Level, spec.LevelRequirement);

        var isThirdTier = spec.LevelRequirement >= 350;
        if (spec.Defense > 0)
        {
            var defenseTable = FindBonusTable(config, isThirdTier ? nameof(LevelBonusNames.DefenseBonus3rdWings) : nameof(LevelBonusNames.DefenseBonus1stAnd2ndWings));
            AddPowerUp(context, config, wing, Stats.DefenseBase, spec.Defense, AggregateType.AddRaw, defenseTable);
        }

        var initialDmg = isThirdTier ? 39 : 32;
        var initialAbsorb = isThirdTier ? 39 : 25;
        var damageTable = FindBonusTable(config, isThirdTier ? nameof(LevelBonusNames.DamageIncrease1stAnd3rdWings) : nameof(LevelBonusNames.DamageIncrease2ndWings));
        var absorbTable = FindBonusTable(config, nameof(LevelBonusNames.WingAbsorb));
        AddPowerUp(context, config, wing, Stats.DamageReceiveDecrement, 1f - (initialAbsorb / 100f), AggregateType.Multiplicate, absorbTable);
        AddPowerUp(context, config, wing, Stats.AttackDamageIncrease, 1f + (initialDmg / 100f), AggregateType.Multiplicate, damageTable);

        AddPowerUp(context, config, wing, Stats.CanFly, 1, AggregateType.AddRaw);
        AddPowerUp(context, config, wing, Stats.MovementSpeed, MovementSpeedConstants.DefaultWingMovementSpeed, AggregateType.Maximum);
        AddPowerUp(context, config, wing, Stats.MovementSpeedUnderwater, MovementSpeedConstants.DefaultWingMovementSpeed, AggregateType.Maximum);
        AddQualifiedClasses(config, wing, spec.DarkWizardClass, spec.DarkKnightClass, spec.ElfClass, spec.MagicGladiatorClass, spec.DarkLordClass, spec.SummonerClass, spec.RageFighterClass);
        AddOptionIfFound(config, wing, FindLuck(config));
    }

    private static ItemDefinition? CreateMiscItem(
        IContext context,
        GameConfiguration config,
        SeasonNineOnlyItemsCatalog.MiscSpec spec)
    {
        if (ItemExists(config, spec.Group, spec.Number))
        {
            return null;
        }

        var item = CreateBaseItem(
            context,
            config,
            spec.Group,
            spec.Number,
            spec.Width,
            spec.Height,
            spec.DropLevel,
            spec.Durability,
            maximumItemLevel: 0,
            spec.DropsFromMonsters,
            SeasonNineItemNames.ResourceManager.GetLocalizedString(spec.NameKey));

        item.Value = spec.Value;
        item.IsTradable = spec.IsTradable;
        item.IsDroppable = spec.IsDroppable;
        item.IsStorable = spec.IsStorable;
        item.IsSellableToNpc = spec.IsSellableToNpc;
        item.IsPersonalStoreSellable = spec.IsPersonalStoreSellable;
        item.IsRepairable = spec.IsRepairable;

        if (spec.Slot >= 0)
        {
            item.ItemSlot = config.ItemSlotTypes.FirstOrDefault(st => st.ItemSlots.Contains(spec.Slot));
            if (item.ItemSlot is not null)
            {
                item.MaximumItemLevel = EquipmentMaximumLevel;
            }
        }

        AddRequirementIfNeeded(context, config, item, Stats.Level, spec.LevelRequirement);

        if (spec.Group == (byte)ItemGroups.Scrolls && spec.Number == 25)
        {
            AddRequirementIfNeeded(
                context,
                config,
                item,
                Stats.TotalEnergyRequirementValue,
                OrbRequirements.CalculateScrollEnergy(spec.EnergyRequirement, spec.DropLevel, spec.LevelRequirement));
        }
        else if (spec.EnergyRequirement > 0)
        {
            AddRequirementIfNeeded(context, config, item, Stats.TotalEnergyRequirementValue, spec.EnergyRequirement);
        }

        AddQualifiedClasses(config, item, spec.DarkWizardClass, spec.DarkKnightClass, spec.ElfClass, spec.MagicGladiatorClass, spec.DarkLordClass, spec.SummonerClass, spec.RageFighterClass);
        return item;
    }

    private static ItemDefinition CreateBaseItem(
        IContext context,
        GameConfiguration config,
        byte group,
        short number,
        byte width,
        byte height,
        byte dropLevel,
        byte durability,
        byte maximumItemLevel,
        bool dropsFromMonsters,
        LocalizedString name)
    {
        var item = context.CreateNew<ItemDefinition>();
        config.Items.Add(item);
        item.Group = group;
        item.Number = number;
        item.Width = width;
        item.Height = height;
        item.DropLevel = dropLevel;
        item.Durability = durability;
        item.MaximumItemLevel = maximumItemLevel;
        item.DropsFromMonsters = dropsFromMonsters;
        item.Name = name;
        item.SetGuid(group, number);
        return item;
    }

    private static void AttachWeaponSockets(GameConfiguration config, ItemDefinition item, int socketCount)
    {
        item.MaximumSockets = (byte)socketCount;
        AddOptionIfFound(config, item, FindOptionBySourceKey(config, nameof(ItemOptionNames.SocketOptionsFire)));
        AddOptionIfFound(config, item, FindOptionBySourceKey(config, nameof(ItemOptionNames.SocketOptionsLightning)));
        AddOptionIfFound(config, item, FindOptionBySourceKey(config, nameof(ItemOptionNames.SocketOptionsIce)));
        var isStaffOrStick = item.Name.ValueInNeutralLanguageAsSpan.Contains("Staff", StringComparison.OrdinalIgnoreCase)
            || item.Name.ValueInNeutralLanguageAsSpan.Contains("Stick", StringComparison.OrdinalIgnoreCase);
        var bonusKey = isStaffOrStick ? nameof(ItemOptionNames.SocketBonusOptionsWizardry) : nameof(ItemOptionNames.SocketBonusOptionsPhysical);
        AddOptionIfFound(config, item, FindOptionBySourceKey(config, bonusKey));
    }

    private static void AttachArmorSockets(GameConfiguration config, ItemDefinition item, int socketCount)
    {
        item.MaximumSockets = (byte)socketCount;
        AddOptionIfFound(config, item, FindOptionBySourceKey(config, nameof(ItemOptionNames.SocketOptionsWater)));
        AddOptionIfFound(config, item, FindOptionBySourceKey(config, nameof(ItemOptionNames.SocketOptionsEarth)));
        AddOptionIfFound(config, item, FindOptionBySourceKey(config, nameof(ItemOptionNames.SocketOptionsWind)));
        AddOptionIfFound(config, item, FindOptionBySourceKey(config, nameof(ItemOptionNames.SocketBonusOptionsArmors)));
    }

    private static void LinkSocketCraftings(GameConfiguration config, List<ItemDefinition> socketItems)
    {
        if (socketItems.Count == 0)
        {
            return;
        }

        foreach (var monster in config.Monsters)
        {
            foreach (var crafting in monster.ItemCraftings.Where(c => c.Number is 44 or 45))
            {
                if (crafting.SimpleCraftingSettings is not { } settings)
                {
                    continue;
                }

                var socketReq = settings.RequiredItems.FirstOrDefault(r =>
                    r.Reference == MountSeedSphereCrafting.SocketItemReference
                    || r.Reference == RemoveSeedSphereCrafting.SocketItemReference);
                if (socketReq is null)
                {
                    continue;
                }

                foreach (var item in socketItems)
                {
                    if (!socketReq.PossibleItems.Contains(item))
                    {
                        socketReq.PossibleItems.Add(item);
                    }
                }
            }
        }
    }

    private static bool ItemExists(GameConfiguration config, byte group, short number) =>
        config.Items.Any(item => item.Group == group && item.Number == number);

    private static void AddRequirementIfNeeded(IContext context, GameConfiguration config, ItemDefinition item, AttributeDefinition attribute, int minimumValue)
    {
        if (minimumValue <= 0)
        {
            return;
        }

        var req = context.CreateNew<AttributeRequirement>();
        req.Attribute = attribute.GetPersistent(config);
        req.MinimumValue = minimumValue;
        item.Requirements.Add(req);
    }

    private static void AddPowerUp(IContext context, GameConfiguration config, ItemDefinition item, AttributeDefinition attribute, float baseValue, AggregateType aggregateType, ItemLevelBonusTable? bonusTable = null)
    {
        var powerUp = context.CreateNew<ItemBasePowerUpDefinition>();
        powerUp.TargetAttribute = attribute.GetPersistent(config);
        powerUp.BaseValue = baseValue;
        powerUp.AggregateType = aggregateType;
        powerUp.BonusPerLevelTable = bonusTable;
        item.BasePowerUpAttributes.Add(powerUp);
    }

    private static void AddQualifiedClasses(
        GameConfiguration config,
        ItemDefinition item,
        int wizardRank,
        int knightRank,
        int elfRank,
        int mgRank,
        int dlRank,
        int summonerRank,
        int rfRank)
    {
        var flags = CharacterClasses.None;
        flags |= MapRank(wizardRank, CharacterClasses.DarkWizard, CharacterClasses.SoulMaster, CharacterClasses.GrandMaster);
        flags |= MapRank(knightRank, CharacterClasses.DarkKnight, CharacterClasses.BladeKnight, CharacterClasses.BladeMaster);
        flags |= MapRank(elfRank, CharacterClasses.FairyElf, CharacterClasses.MuseElf, CharacterClasses.HighElf);
        flags |= MapRank(mgRank, CharacterClasses.MagicGladiator, CharacterClasses.MagicGladiator, CharacterClasses.DuelMaster);
        flags |= MapRank(dlRank, CharacterClasses.DarkLord, CharacterClasses.DarkLord, CharacterClasses.LordEmperor);
        flags |= MapRank(summonerRank, CharacterClasses.Summoner, CharacterClasses.BloodySummoner, CharacterClasses.DimensionMaster);
        flags |= MapRank(rfRank, CharacterClasses.RageFighter, CharacterClasses.RageFighter, CharacterClasses.FistMaster);

        if (flags == CharacterClasses.None)
        {
            return;
        }

        foreach (var cc in config.DetermineCharacterClasses(flags, ignoreMissing: true))
        {
            item.QualifiedCharacters.Add(cc);
        }
    }

    private static CharacterClasses MapRank(int rank, CharacterClasses first, CharacterClasses second, CharacterClasses master) => rank switch
    {
        1 => first | second | master,
        2 => second | master,
        3 => master,
        _ => CharacterClasses.None,
    };

    private static void AddOptionIfFound(GameConfiguration config, ItemDefinition item, ItemOptionDefinition? option)
    {
        if (option is not null && !item.PossibleItemOptions.Contains(option))
        {
            item.PossibleItemOptions.Add(option);
        }
    }

    private static ItemLevelBonusTable? FindBonusTable(GameConfiguration config, string levelBonusKey) =>
        config.ItemLevelBonusTables.FirstOrDefault(t => t.Name.SourceKey == $"LevelBonusNames/{levelBonusKey}");

    private static ItemOptionDefinition? FindOptionBySourceKey(GameConfiguration config, string key) =>
        config.ItemOptions.FirstOrDefault(o => o.Name.SourceKey == $"ItemOptionNames/{key}");

    private static ItemOptionDefinition? FindOptionByNeutralName(GameConfiguration config, string neutralName) =>
        config.ItemOptions.FirstOrDefault(o => o.Name.ValueInNeutralLanguage == neutralName);

    private static ItemOptionDefinition? FindLuck(GameConfiguration config) =>
        config.ItemOptions.FirstOrDefault(o => o.PossibleOptions.Any(po => po.OptionType == ItemOptionTypes.Luck));

    private static ItemOptionDefinition? FindOptionByTarget(GameConfiguration config, AttributeDefinition target) =>
        config.ItemOptions.FirstOrDefault(o => o.PossibleOptions.Any(po => po.OptionType == ItemOptionTypes.Option && po.PowerUpDefinition?.TargetAttribute == target));

    private static ItemOptionDefinition? FindDefenseOption(GameConfiguration config) =>
        config.ItemOptions.FirstOrDefault(o => o.PossibleOptions.Any(po => po.OptionType == ItemOptionTypes.Option && po.PowerUpDefinition?.TargetAttribute == Stats.DefenseBase));

    private static ItemOptionDefinition? FindDefenseRateOption(GameConfiguration config) =>
        config.ItemOptions.FirstOrDefault(o => o.PossibleOptions.Any(po => po.OptionType == ItemOptionTypes.Option && po.PowerUpDefinition?.TargetAttribute == Stats.DefenseRatePvm));
}

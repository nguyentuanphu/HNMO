// <copyright file="BuildCalculatorService.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BuildCalculator;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Server-authoritative build calculator (ROADMAP 14.21): runs HNMO's real <see cref="ItemAwareAttributeSystem"/>
/// and <see cref="ItemPowerUpFactory"/> over an ephemeral in-memory character so website calculations match
/// live in-game stats 1-to-1 with zero formula duplication.
/// </summary>
public static class BuildCalculatorService
{
    /// <summary>Projects available classes and equippable item definitions for the website picker.</summary>
    /// <param name="config">Loaded <see cref="GameConfiguration"/>.</param>
    /// <returns>Serializable metadata payload.</returns>
    public static object GetMetadata(GameConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var classes = config.CharacterClasses
            .OrderBy(c => c.Number)
            .Select(c => new
            {
                number = c.Number,
                name = c.Name,
                canCreate = c.CanGetCreated,
                isMaster = c.IsMasterClass,
                defaults = c.StatAttributes.ToDictionary(
                    sa => sa.Attribute?.Designation ?? string.Empty,
                    sa => (int)Math.Round(sa.BaseValue),
                    StringComparer.OrdinalIgnoreCase),
            })
            .ToList();

        var items = config.Items
            .Where(d => d.ItemSlot is not null)
            .OrderBy(d => d.Group)
            .ThenBy(d => d.Number)
            .Select(d => new
            {
                group = d.Group,
                number = d.Number,
                nameEn = d.Name.GetTranslation(CultureInfo.GetCultureInfo("en")) ?? d.Name.ValueInNeutralLanguage,
                nameVi = d.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? d.Name.ValueInNeutralLanguage,
                maxLevel = d.MaximumItemLevel,
                hasSkill = d.Skill is not null,
                slots = d.ItemSlot!.ItemSlots.ToList(),
                slotGroupName = d.ItemSlot.Description.ValueInNeutralLanguage,
                classes = d.QualifiedCharacters.Select(qc => qc.Number).ToList(),
                isAncient = d.PossibleItemSetGroups.Count > 0,
                hasExcellent = d.PossibleItemOptions.SelectMany(o => o.PossibleOptions).Any(p => p.OptionType == HNMO.DataModel.Configuration.Items.ItemOptionTypes.Excellent),
            })
            .ToList();

        return new { v = 1, classes, items };
    }

    /// <summary>Computes exact character stats using the live server attribute graph.</summary>
    /// <param name="config">Active <see cref="GameConfiguration"/>.</param>
    /// <param name="input">Build configuration.</param>
    /// <returns>Computed stats or <c>null</c> if <see cref="BuildCalculatorInput.ClassNumber"/> does not exist.</returns>
    public static BuildCalculatorResult? Compute(GameConfiguration config, BuildCalculatorInput input)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(input);

        var charClass = config.CharacterClasses.FirstOrDefault(c => c.Number == input.ClassNumber);
        if (charClass is null)
        {
            return null;
        }

        var account = new CalculatorAccount();
        var character = new CalculatorCharacter
        {
            Name = "CalculatorPreview",
            CharacterClass = charClass,
        };

        foreach (var statAttrDef in charClass.StatAttributes)
        {
            if (statAttrDef.Attribute is null)
            {
                continue;
            }

            character.Attributes.Add(new StatAttribute(statAttrDef.Attribute, statAttrDef.BaseValue));
        }

        SetStatIfDefined(character, charClass, Stats.Level, Math.Clamp(input.Level, 1, 1000));
        SetStatIfDefined(character, charClass, Stats.MasterLevel, Math.Clamp(input.MasterLevel, 0, 1000));
        SetStatIfDefined(character, charClass, Stats.BaseStrength, Math.Clamp(input.Strength, 0, 100_000));
        SetStatIfDefined(character, charClass, Stats.BaseAgility, Math.Clamp(input.Agility, 0, 100_000));
        SetStatIfDefined(character, charClass, Stats.BaseVitality, Math.Clamp(input.Vitality, 0, 100_000));
        SetStatIfDefined(character, charClass, Stats.BaseEnergy, Math.Clamp(input.Energy, 0, 100_000));
        SetStatIfDefined(character, charClass, Stats.BaseLeadership, Math.Clamp(input.Leadership, 0, 100_000));

        using var attributes = new ItemAwareAttributeSystem(account, character, config);
        var powerUpFactory = new ItemPowerUpFactory(NullLogger<ItemPowerUpFactory>.Instance, config);

        var equippedItems = new List<Item>();
        foreach (var entry in input.Items ?? [])
        {
            if (entry.Slot >= InventoryConstants.EquippableSlotsCount)
            {
                continue;
            }

            var def = config.Items.FirstOrDefault(d => d.Group == entry.Group && d.Number == entry.Number);
            if (def is null)
            {
                continue;
            }

            var level = Math.Min(entry.Level, def.MaximumItemLevel);
            var spec = new ItemSpecification(entry.Group, entry.Number, level, entry.Skill, entry.Luck, entry.OptionLevel, entry.ExcellentOptions);
            if (!GameMasterItemFactory.IsValid(def, spec))
            {
                spec = new ItemSpecification(entry.Group, entry.Number, level, entry.Skill && def.Skill is not null);
            }

            var item = GameMasterItemFactory.CreateItem(def, spec);
            item.ItemSlot = entry.Slot;
            item.Durability = Math.Max((byte)1, item.GetMaximumDurabilityOfOnePiece());
            equippedItems.Add(item);
            attributes.ItemPowerUps[item] = powerUpFactory.GetPowerUps(item, attributes).ToList();
        }

        if (equippedItems.Count > 0)
        {
            attributes.ItemSetPowerUps = powerUpFactory.GetSetPowerUps(equippedItems, attributes, config).ToList();
        }

        return new BuildCalculatorResult
        {
            ClassName = charClass.Name,
            TotalStrength = (int)Math.Round(attributes[Stats.TotalStrength]),
            TotalAgility = (int)Math.Round(attributes[Stats.TotalAgility]),
            TotalVitality = (int)Math.Round(attributes[Stats.TotalVitality]),
            TotalEnergy = (int)Math.Round(attributes[Stats.TotalEnergy]),
            TotalLeadership = (int)Math.Round(attributes[Stats.TotalLeadership]),
            MinPhysDmg = (int)Math.Round(attributes[Stats.MinimumPhysBaseDmg]),
            MaxPhysDmg = (int)Math.Round(attributes[Stats.MaximumPhysBaseDmg]),
            MinWizDmg = (int)Math.Round(attributes[Stats.MinimumWizBaseDmg]),
            MaxWizDmg = (int)Math.Round(attributes[Stats.MaximumWizBaseDmg]),
            MinCurseDmg = (int)Math.Round(attributes[Stats.MinimumCurseBaseDmg]),
            MaxCurseDmg = (int)Math.Round(attributes[Stats.MaximumCurseBaseDmg]),
            DefenseBase = (int)Math.Round(attributes[Stats.DefenseBase]),
            DefensePvm = (int)Math.Round(attributes[Stats.DefensePvm]),
            DefensePvp = (int)Math.Round(attributes[Stats.DefensePvp]),
            DefenseRatePvm = (int)Math.Round(attributes[Stats.DefenseRatePvm]),
            DefenseRatePvp = (int)Math.Round(attributes[Stats.DefenseRatePvp]),
            AttackRatePvm = (int)Math.Round(attributes[Stats.AttackRatePvm]),
            AttackRatePvp = (int)Math.Round(attributes[Stats.AttackRatePvp]),
            AttackSpeed = (int)Math.Round(attributes[Stats.AttackSpeed]),
            MagicSpeed = (int)Math.Round(attributes[Stats.MagicSpeed]),
            MaximumHealth = (int)Math.Round(attributes[Stats.MaximumHealth]),
            MaximumMana = (int)Math.Round(attributes[Stats.MaximumMana]),
            MaximumShield = (int)Math.Round(attributes[Stats.MaximumShield]),
            MaximumAbility = (int)Math.Round(attributes[Stats.MaximumAbility]),
            CriticalDamageChancePct = Math.Round(attributes[Stats.CriticalDamageChance] * 100.0, 2),
            ExcellentDamageChancePct = Math.Round(attributes[Stats.ExcellentDamageChance] * 100.0, 2),
        };
    }

    private static void SetStatIfDefined(Character character, CharacterClass charClass, AttributeDefinition def, float value)
    {
        var stat = character.Attributes.FirstOrDefault(a => a.Definition?.Id == def.Id);
        if (stat is not null)
        {
            stat.Value = value;
        }
        else if (charClass.StatAttributes.Any(sa => sa.Attribute?.Id == def.Id))
        {
            character.Attributes.Add(new StatAttribute(def, value));
        }
    }

    private sealed class CalculatorAccount : Account
    {
        public CalculatorAccount()
        {
            this.Attributes = new List<StatAttribute>();
        }
    }

    private sealed class CalculatorCharacter : Character
    {
        public CalculatorCharacter()
        {
            this.Attributes = new List<StatAttribute>();
        }
    }
}

// <copyright file="WebsiteClassGuideProjection.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;

/// <summary>Projects bounded class and skill facts from the saved game definitions.</summary>
public static class WebsiteClassGuideProjection
{
    private const int MaximumClasses = 32;
    private const int MaximumAttributes = 32;
    private const int MaximumFormulas = 80;
    private const int MaximumSkills = 160;
    private const int MaximumTextLength = 120;
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");
    private static readonly HashSet<AttributeDefinition> FormulaTargets =
    [
        Stats.MaximumHealth, Stats.MaximumMana, Stats.MaximumShield, Stats.MaximumShieldTemp, Stats.MaximumAbility,
        Stats.MinimumPhysBaseDmg, Stats.MaximumPhysBaseDmg, Stats.MinimumWizBaseDmg, Stats.MaximumWizBaseDmg,
        Stats.MinimumCurseBaseDmg, Stats.MaximumCurseBaseDmg, Stats.DefenseBase, Stats.DefenseFinal,
        Stats.AttackSpeed, Stats.AttackRatePvm, Stats.DefenseRatePvm,
    ];

    /// <summary>Returns the active classes, including promotions which cannot be directly created.</summary>
    /// <param name="configuration">The running world's configuration.</param>
    /// <returns>The class selector choices.</returns>
    public static CharacterClass[] AvailableClasses(GameConfiguration configuration)
        => configuration.CharacterClasses.Where(item => item.IsActive).OrderBy(item => item.Number).Take(MaximumClasses).ToArray();

    /// <summary>Returns the public details for one active class.</summary>
    /// <param name="configuration">The running world's configuration.</param>
    /// <param name="characterClass">The selected active class.</param>
    /// <returns>The class attributes, configured contributions and qualified skills.</returns>
    public static object Build(GameConfiguration configuration, CharacterClass characterClass)
    {
        var formulas = characterClass.AttributeCombinations.Where(relationship => relationship.TargetAttribute is { } target
                && FormulaTargets.Contains(target) && Label(relationship.InputAttribute) is not null
                && (relationship.OperandAttribute is { } operand ? Label(operand) is not null : float.IsFinite(relationship.InputOperand))
                && Enum.IsDefined(relationship.InputOperator) && Enum.IsDefined(relationship.AggregateType))
            .Take(MaximumFormulas + 1).ToArray();
        var skills = configuration.Skills.Where(skill => skill.QualifiedCharacters.Contains(characterClass))
            .OrderBy(skill => skill.Number).Take(MaximumSkills + 1).ToArray();
        return new
        {
            id = characterClass.Number,
            nextClass = characterClass.NextGenerationClass is { IsActive: true } next && AvailableClasses(configuration).Contains(next) ? Class(next) : null,
            home = characterClass.HomeMap is { IsActive: true } map ? Name(map.Name) : null,
            stats = characterClass.StatAttributes.Where(stat => Label(stat.Attribute) is not null && float.IsFinite(stat.BaseValue)).Take(MaximumAttributes)
                .Select(stat => new { attribute = Label(stat.Attribute), value = stat.BaseValue, stat.IncreasableByPlayer }).ToArray(),
            constants = characterClass.BaseAttributeValues.Where(value => value.Definition is { } target && FormulaTargets.Contains(target)
                    && float.IsFinite(value.Value) && Enum.IsDefined(value.AggregateType))
                .Take(MaximumAttributes).Select(value => new { attribute = Label(value.Definition), value.Value, aggregate = value.AggregateType.ToString() }).ToArray(),
            formulas = formulas.Take(MaximumFormulas).Select(relationship => new
            {
                target = Label(relationship.TargetAttribute),
                input = Label(relationship.InputAttribute),
                operand = Label(relationship.OperandAttribute),
                coefficient = relationship.OperandAttribute is null ? (float?)relationship.InputOperand : null,
                operation = relationship.InputOperator.ToString(),
                aggregate = relationship.AggregateType.ToString(),
            }).ToArray(),
            skills = skills.Take(MaximumSkills).Select(Skill).ToArray(),
            truncated = formulas.Length > MaximumFormulas || skills.Length > MaximumSkills,
        };
    }

    internal static object Class(CharacterClass value) => new
    {
        id = value.Number,
        name = Name(value.Name),
        value.CanGetCreated,
        creationLevel = value.CanGetCreated ? (short?)value.LevelRequirementByCreation : null,
        value.IsMasterClass,
    };

    internal static string? LimitText(string? value)
    {
        var text = value?.Trim();
        return text is { Length: > 0 } ? text[..Math.Min(text.Length, MaximumTextLength)] : null;
    }

    private static object Skill(Skill skill) => new
    {
        id = skill.Number,
        name = Name(skill.Name),
        skill.Range,
        skill.AttackDamage,
        masterRank = skill.MasterDefinition?.Rank,
        requirements = AttrValues(skill.Requirements),
        costs = AttrValues(skill.ConsumeRequirements),
    };

    private static object[] AttrValues(IEnumerable<AttributeRequirement> values)
        => values.Where(requirement => Label(requirement.Attribute) is not null).Take(MaximumAttributes)
            .Select(requirement => (object)new { attribute = Label(requirement.Attribute), value = requirement.MinimumValue }).ToArray();

    private static object Name(LocalizedString value) => new
    {
        en = LimitText(value.GetTranslation(English)),
        vi = LimitText(value.GetTranslation(Vietnamese)),
    };

    private static string? Label(AttributeDefinition? attribute) => LimitText(attribute?.Designation);
}

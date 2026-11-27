// <copyright file="SeasonNineSystemsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Applies Season 9 progression systems when running as Season 9: raises <see cref="GameConfiguration.MaximumMasterLevel"/>
/// from 200 to 330 (preserving customized caps) and registers the four Season 9-only skills (618-621).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineSystemsUpdatePlugIn_Name), Description = nameof(PlugInResources.SeasonNineSystemsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C4E12A80-9F3B-4D72-8E11-5B09A2C6F301")]
public class SeasonNineSystemsUpdatePlugIn : SeasonNineUpdatePlugInBase
{
    private const short SeasonSixMasterLevelCap = 200;
    private const short SeasonNineMasterLevelCap = 330;

    private static readonly (SkillNumber Number, string LocalizedName, short Distance, int Mana, SkillType Type, SkillTarget Target)[] SeasonNineOnlySkills =
    [
        (SkillNumber.SpellOfProtection, "Spell of Protection||vi=Phép Bảo Hộ||de=Schutzzauber||zh-CN=守护咒语", 0, 30, SkillType.Buff, SkillTarget.Explicit),
        (SkillNumber.SpellOfRestriction, "Spell of Restriction||vi=Phép Trói Buộc||de=Bannzauber||zh-CN=束缚咒语", 3, 30, SkillType.Other, SkillTarget.Explicit),
        (SkillNumber.SpellOfPursuit, "Spell of Pursuit||vi=Phép Truy Đuổi||de=Verfolgungszauber||zh-CN=追击咒语", 0, 30, SkillType.Buff, SkillTarget.Explicit),
        (SkillNumber.ShieldBurnS9, "Shield-Burn||vi=Thiêu Đốt Khiên SD||de=Schildbrand||zh-CN=护盾焚烧", 3, 30, SkillType.Other, SkillTarget.Explicit),
    ];

    /// <summary>Initializes a new instance of the <see cref="SeasonNineSystemsUpdatePlugIn"/> class.</summary>
    public SeasonNineSystemsUpdatePlugIn()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SeasonNineSystemsUpdatePlugIn"/> class with a test season selector.</summary>
    /// <param name="isSeasonNine">Season selector predicate.</param>
    internal SeasonNineSystemsUpdatePlugIn(Func<bool> isSeasonNine)
        : base(isSeasonNine)
    {
    }

    /// <inheritdoc />
    public override string Name => PlugInResources.SeasonNineSystemsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SeasonNineSystemsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 26, 9, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    /// <remarks>Version 3: Skills 618-620 cost 30 mana matching the Season 9 client skill table and have active plug-ins.</remarks>
    public override int Version => 3;

    /// <inheritdoc />
    public override DateTime UpdatedAt => new(2026, 11, 27, 15, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.MaximumMasterLevel == SeasonSixMasterLevelCap)
        {
            gameConfiguration.MaximumMasterLevel = SeasonNineMasterLevelCap;
        }

        // Every class but the Rage Fighter line (Rage Fighter 24, Fist Master 25) learns them.
        var qualified = gameConfiguration.CharacterClasses
            .Where(c => c.Number is not (24 or 25))
            .ToList();

        foreach (var spec in SeasonNineOnlySkills)
        {
            if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)spec.Number) is not { } skill)
            {
                skill = context.CreateNew<Skill>();
                skill.Number = (short)spec.Number;
                skill.Name = new LocalizedString(spec.LocalizedName);
                skill.Range = spec.Distance;
                skill.SkillType = spec.Type;
                skill.Target = spec.Target;
                skill.DamageType = DamageType.Wizardry;
                skill.NumberOfHitsPerAttack = 1;
                skill.SetGuid(skill.Number);
                gameConfiguration.Skills.Add(skill);
            }

            // Cumulative (version 1 added them with a mana cost for 618-620 and for every class).
            var manaAttribute = Stats.CurrentMana.GetPersistent(gameConfiguration);
            var manaCost = skill.ConsumeRequirements.FirstOrDefault(r => Equals(r.Attribute, manaAttribute));
            if (spec.Mana <= 0 && manaCost is not null)
            {
                skill.ConsumeRequirements.Remove(manaCost);
            }
            else if (spec.Mana > 0 && manaCost is null)
            {
                manaCost = context.CreateNew<AttributeRequirement>();
                manaCost.Attribute = manaAttribute;
                manaCost.MinimumValue = spec.Mana;
                skill.ConsumeRequirements.Add(manaCost);
            }
            else if (spec.Mana > 0 && manaCost is not null)
            {
                manaCost.MinimumValue = spec.Mana;
            }

            foreach (var notQualified in skill.QualifiedCharacters.Where(c => !qualified.Contains(c)).ToList())
            {
                skill.QualifiedCharacters.Remove(notQualified);
            }

            foreach (var cls in qualified.Where(c => !skill.QualifiedCharacters.Contains(c)))
            {
                skill.QualifiedCharacters.Add(cls);
            }
        }

        return ValueTask.CompletedTask;
    }
}

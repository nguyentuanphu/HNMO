// <copyright file="WebsiteClassGuideTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.API;

/// <summary>Validates class guide projections against the engine's configured attributes and skills.</summary>
[TestFixture]
public sealed class WebsiteClassGuideTests
{
    /// <summary>Uses the active class, localized names, real formula operators, and the configured skill requirements and costs.</summary>
    [Test]
    public void ClassFactsUseConfiguredValuesAndExactClassEligibility()
    {
        using var persistence = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = persistence.CreateNew<GameConfiguration>();
        var starter = persistence.CreateNew<CharacterClass>();
        starter.Number = 4;
        starter.Name = new LocalizedString("Test fighter").WithTranslation(CultureInfo.GetCultureInfo("vi"), "Đấu sĩ thử nghiệm");
        starter.CanGetCreated = true;
        starter.LevelRequirementByCreation = 82;
        starter.StatAttributes.Add(persistence.CreateNew<StatAttributeDefinition>(Stats.BaseStrength, 47f, true));
        starter.BaseAttributeValues.Add(persistence.CreateNew<ConstValueAttribute>(150f, Stats.MaximumHealth, AggregateType.AddRaw));
        starter.AttributeCombinations.Add(persistence.CreateNew<AttributeRelationship>(Stats.MaximumHealth, 2.75f, Stats.TotalVitality, AggregateType.AddFinal));
        starter.AttributeCombinations.Add(persistence.CreateNew<AttributeRelationship>(Stats.MaximumShield, Stats.TotalVitality, Stats.TotalEnergy, AggregateType.Multiplicate));
        starter.AttributeCombinations.Add(persistence.CreateNew<AttributeRelationship>(Stats.MaximumMana, float.NaN, Stats.TotalEnergy, AggregateType.AddRaw));
        var next = persistence.CreateNew<CharacterClass>();
        next.Number = 5;
        next.Name = new LocalizedString("Test master").WithTranslation(CultureInfo.GetCultureInfo("vi"), "Bậc thầy thử nghiệm");
        starter.NextGenerationClass = next;
        var inactive = persistence.CreateNew<CharacterClass>();
        inactive.IsActive = false;
        inactive.Number = 6;
        configuration.CharacterClasses.Add(starter);
        configuration.CharacterClasses.Add(next);
        configuration.CharacterClasses.Add(inactive);
        var skill = persistence.CreateNew<Skill>();
        skill.Name = new("Test skill");
        skill.Number = 18;
        skill.Range = 7;
        skill.AttackDamage = 96;
        skill.QualifiedCharacters.Add(starter);
        var requirement = persistence.CreateNew<AttributeRequirement>();
        requirement.Attribute = Stats.TotalStrength;
        requirement.MinimumValue = 179;
        skill.Requirements.Add(requirement);
        var cost = persistence.CreateNew<AttributeRequirement>();
        cost.Attribute = Stats.CurrentMana;
        cost.MinimumValue = 30;
        skill.ConsumeRequirements.Add(cost);
        var unrelated = persistence.CreateNew<Skill>();
        unrelated.QualifiedCharacters.Add(next);
        configuration.Skills.Add(skill);
        configuration.Skills.Add(unrelated);

        Assert.That(WebsiteClassGuideProjection.AvailableClasses(configuration).Select(item => item.Number), Is.EqualTo(new byte[] { 4, 5 }));
        var data = Project(configuration, starter);
        Assert.That(data["stats"]![0]!["value"]!.GetValue<float>(), Is.EqualTo(47));
        Assert.That(data["constants"]![0]!["value"]!.GetValue<float>(), Is.EqualTo(150));
        Assert.That(data["nextClass"]!["id"]!.GetValue<byte>(), Is.EqualTo(5));
        Assert.That(data["nextClass"]!["name"]!["vi"]!.GetValue<string>(), Is.EqualTo("Bậc thầy thử nghiệm"));
        var formulas = data["formulas"]!.AsArray();
        Assert.That(formulas, Has.Count.EqualTo(2));
        Assert.That(formulas[0]!["coefficient"]!.GetValue<float>(), Is.EqualTo(2.75f));
        Assert.That(formulas[0]!["aggregate"]!.GetValue<string>(), Is.EqualTo("AddFinal"));
        Assert.That(formulas[1]!["coefficient"], Is.Null);
        Assert.That(formulas[1]!["operand"]!.GetValue<string>(), Is.EqualTo("Total Vitality"));
        var skillRows = data["skills"]!.AsArray();
        Assert.That(skillRows, Has.Count.EqualTo(1));
        Assert.That(skillRows[0]!["attackDamage"]!.GetValue<int>(), Is.EqualTo(96));
        Assert.That(skillRows[0]!["requirements"]![0]!["value"]!.GetValue<int>(), Is.EqualTo(179));
        Assert.That(skillRows[0]!["costs"]![0]!["value"]!.GetValue<int>(), Is.EqualTo(30));
    }

    /// <summary>Limits the published rows while preserving stable configured skill order.</summary>
    [Test]
    public void ClassSkillsAreBounded()
    {
        using var persistence = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = persistence.CreateNew<GameConfiguration>();
        var characterClass = persistence.CreateNew<CharacterClass>();
        for (short number = 200; number > 0; number--)
        {
            var skill = persistence.CreateNew<Skill>();
            skill.Number = number;
            skill.QualifiedCharacters.Add(characterClass);
            configuration.Skills.Add(skill);
        }

        var result = Project(configuration, characterClass);
        Assert.That(result["skills"]!.AsArray(), Has.Count.EqualTo(160));
        Assert.That(result["skills"]![159]!["id"]!.GetValue<short>(), Is.EqualTo(160));
        Assert.That(result["truncated"]!.GetValue<bool>(), Is.True);
    }

    /// <summary>The host's switch has native captions in each administrative locale.</summary>
    /// <param name="locale">The administrative locale.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void HostSwitchHasLocalizedMetadata(string locale)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
            Assert.That(WebsiteServerPageLabels.ClassGuide, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.ClassGuide)));
            Assert.That(WebsiteServerPageLabels.ClassGuideDescription, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.ClassGuideDescription)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    private static JsonObject Project(GameConfiguration configuration, CharacterClass value)
        => JsonSerializer.SerializeToNode(WebsiteClassGuideProjection.Build(configuration, value), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
}

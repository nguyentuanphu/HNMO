// <copyright file="HnmoSkillsDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Quests;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests the data of the HNMO skills (700 to 727) and their trial quests.
/// </summary>
[TestFixture]
internal class HnmoSkillsDataTest
{
    private static readonly (string Name, CharacterClassNumber[] Classes)[] ClassFamilies =
    [
        ("DarkKnight", [CharacterClassNumber.DarkKnight, CharacterClassNumber.BladeKnight, CharacterClassNumber.BladeMaster]),
        ("DarkWizard", [CharacterClassNumber.DarkWizard, CharacterClassNumber.SoulMaster, CharacterClassNumber.GrandMaster]),
        ("FairyElf", [CharacterClassNumber.FairyElf, CharacterClassNumber.MuseElf, CharacterClassNumber.HighElf]),
        ("MagicGladiator", [CharacterClassNumber.MagicGladiator, CharacterClassNumber.DuelMaster]),
        ("DarkLord", [CharacterClassNumber.DarkLord, CharacterClassNumber.LordEmperor]),
        ("Summoner", [CharacterClassNumber.Summoner, CharacterClassNumber.BloodySummoner, CharacterClassNumber.DimensionMaster]),
        ("RageFighter", [CharacterClassNumber.RageFighter, CharacterClassNumber.FistMaster]),
    ];

    private static readonly string[] Names =
    [
        "Earth Splitter", "Blood Slash", "Iron Guard", "Charge",
        "Ice Dragon", "Meteor Rain", "Thunder Cage", "Mana Shield",
        "Phoenix Arrow", "Frost Arrow Rain", "Forest Blessing", "Spirit Wolf",
        "Chain Lightning Blade", "Flame Whirl", "Twin Power", "Heaven Sword",
        "Shadow Ravens", "War Command", "Earth Stomp", "Royal Beam",
        "Poison Soul", "Little Demon", "Weakening Curse", "Soul Drain",
        "Dragon Fist", "Whirl Kicks", "Iron Body", "Quake Fist",
    ];

    private static readonly int[] Levels = [80, 150, 220, 250];

    /// <summary>
    /// A new database has the 28 skills with their numbers, names, classes and levels, and their effects.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSkillsAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        AssertSkills(gameConfiguration);
    }

    /// <summary>
    /// A new database has the trial quests of the elf soldier: one per class evolution, from level 250, 100 kills and
    /// a jewel of chaos, rewarded with the D skill of the class.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheTrialQuestsAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        AssertTrialQuests(gameConfiguration);
    }

    /// <summary>
    /// The update adds the skills and quests to a database which doesn't have them, and applying it twice
    /// doesn't add anything twice.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateAddsTheSkillsAndQuestsToAnExistingDatabaseAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        foreach (var skill in gameConfiguration.Skills.Where(IsHnmoSkill).ToList())
        {
            gameConfiguration.Skills.Remove(skill);
        }

        var elfSoldier = gameConfiguration.Monsters.Single(m => m.Number == HnmoTrialQuestsInitializer.ElfSoldierNumber);
        foreach (var quest in elfSoldier.Quests.Where(q => q.Group == HnmoTrialQuestsInitializer.TrialQuestGroup).ToList())
        {
            elfSoldier.Quests.Remove(quest);
        }

        Assert.That(gameConfiguration.Skills.Any(IsHnmoSkill), Is.False);

        var update = new AddHnmoSkillsUpdatePlugIn();
        for (var i = 0; i < 2; i++)
        {
            await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        }

        AssertSkills(gameConfiguration);
        AssertTrialQuests(gameConfiguration);
    }

    /// <summary>
    /// The skills which the level plug-in teaches by default are the A, B and C skills, at the levels of their requirements.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LevelPlugInTeachesTheSkillsAtTheirRequiredLevelAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        foreach (var entry in LearnSkillsByLevelPlugInConfiguration.CreateDefault().Skills)
        {
            var skill = gameConfiguration.Skills.Single(s => s.Number == entry.SkillNumber);
            Assert.That(GetLevelRequirement(skill), Is.EqualTo(entry.MinimumLevel), skill.Name.ToString());
        }
    }

    /// <summary>
    /// The list of the web client (<c>client/web/public/data/new-skills.json</c>) has the numbers of the definitions.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WebClientListMatchesTheDefinitionsAsync()
    {
        var path = FindWebClientList();
        if (path is null)
        {
            Assert.Ignore("The web client isn't next to the server.");
            return;
        }

        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);
        var actual = JsonNode.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false))!.AsArray();
        var expected = new JsonArray(gameConfiguration.Skills.Where(IsHnmoSkill).OrderBy(s => s.Number).Select(s => (JsonNode)CreateWebClientEntry(s)).ToArray());
        Assert.That(actual, Has.Count.EqualTo(28));
        for (var i = 0; i < expected.Count; i++)
        {
            var entry = actual[i]!.AsObject().DeepClone().AsObject();
            Assert.That(entry.Remove("cooldownMs"), Is.True, "cooldownMs");
            Assert.That(JsonNode.DeepEquals(entry, expected[i]), Is.True, $"expected:{Environment.NewLine}{expected.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
        }
    }

    private static bool IsHnmoSkill(Skill skill) => skill.Number is >= HnmoSkillsInitializer.FirstSkillNumber and <= HnmoSkillsInitializer.LastSkillNumber;

    private static int? GetLevelRequirement(Skill skill) =>
        (int?)skill.Requirements.FirstOrDefault(r => r.Attribute == Stats.Level)?.MinimumValue;

    private static int GetConsumption(Skill skill, AttributeDefinition attribute) =>
        skill.ConsumeRequirements.FirstOrDefault(r => r.Attribute == attribute)?.MinimumValue ?? 0;

    private static void AssertSkills(GameConfiguration gameConfiguration)
    {
        var skills = gameConfiguration.Skills.Where(IsHnmoSkill).OrderBy(s => s.Number).ToList();
        Assert.That(skills.Select(s => (int)s.Number), Is.EqualTo(Enumerable.Range(700, 28)));
        for (var i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            var (_, classes) = ClassFamilies[i / 4];
            Assert.That(skill.Name.ToString(), Is.EqualTo(Names[i]));
            Assert.That(skill.QualifiedCharacters.Select(c => (CharacterClassNumber)c.Number), Is.EquivalentTo(classes), skill.Name.ToString());
            Assert.That(GetLevelRequirement(skill), Is.EqualTo(Levels[i % 4]), skill.Name.ToString());
            Assert.That(GetConsumption(skill, Stats.CurrentMana), Is.GreaterThan(0), skill.Name.ToString());
            if (skill.SkillType is SkillType.Buff)
            {
                Assert.That(skill.MagicEffectDef, Is.Not.Null, skill.Name.ToString());
                Assert.That(skill.MagicEffectDef!.PowerUpDefinitions.All(p => p.TargetAttribute is not null && p.Boost is not null), Is.True, skill.Name.ToString());
            }
            else if (skill.SkillType is not SkillType.SummonMonster)
            {
                Assert.That(skill.AttackDamage, Is.GreaterThan(0), skill.Name.ToString());
                Assert.That(skill.DamageType, Is.Not.EqualTo(DamageType.None), skill.Name.ToString());
            }
        }

        Assert.That(gameConfiguration.Skills.Count(IsHnmoSkill), Is.EqualTo(28), "no duplicates");
        Assert.That(skills.Single(s => s.Number == 718).Requirements.Any(r => r.Attribute == Stats.IsHorseEquipped), Is.True, "Earth Stomp needs the dark horse");
        Assert.That(skills.Single(s => s.Number == 703).MagicEffectDef?.PowerUpDefinitions.Any(p => p.TargetAttribute == Stats.IsStunned), Is.True, "Charge stuns");
        Assert.That(gameConfiguration.Monsters.Any(m => m.Number == 351), Is.True, "the monster of Spirit Wolf");
        Assert.That(gameConfiguration.Monsters.Any(m => m.Number == 37), Is.True, "the monster of Little Demon");
    }

    private static void AssertTrialQuests(GameConfiguration gameConfiguration)
    {
        var elfSoldier = gameConfiguration.Monsters.Single(m => m.Number == HnmoTrialQuestsInitializer.ElfSoldierNumber);
        var quests = elfSoldier.Quests.Where(q => q.Group == HnmoTrialQuestsInitializer.TrialQuestGroup).ToList();
        Assert.That(quests, Has.Count.EqualTo(ClassFamilies.Sum(f => f.Classes.Length)));
        for (var i = 0; i < ClassFamilies.Length; i++)
        {
            foreach (var classNumber in ClassFamilies[i].Classes)
            {
                var quest = quests.Single(q => q.QualifiedCharacter?.Number == (byte)classNumber);
                Assert.That(quest.MinimumCharacterLevel, Is.EqualTo(250));
                Assert.That(quest.Repeatable, Is.False);
                Assert.That(quest.RequiredMonsterKills.Single().MinimumNumber, Is.EqualTo(100));
                Assert.That(quest.RequiredMonsterKills.Single().Monster, Is.Not.Null);
                Assert.That(quest.RequiredItems.Single().Item, Is.SameAs(gameConfiguration.Items.Single(item => item.Group == 12 && item.Number == 15)));
                var reward = quest.Rewards.Single();
                Assert.That(reward.RewardType, Is.EqualTo(QuestRewardType.Skill));
                Assert.That(reward.SkillReward?.Number, Is.EqualTo(700 + (4 * i) + 3), classNumber.ToString());
            }
        }
    }

    private static JsonObject CreateWebClientEntry(Skill skill)
    {
        var index = skill.Number - HnmoSkillsInitializer.FirstSkillNumber;
        var letter = index % 4;
        return new JsonObject
        {
            ["number"] = (int)skill.Number,
            ["class"] = ClassFamilies[index / 4].Name,
            ["letter"] = ((char)('A' + letter)).ToString(),
            ["name"] = skill.Name.ToString(),
            ["level"] = letter == 3 ? null : GetLevelRequirement(skill),
            ["skillType"] = skill.SkillType.ToString(),
            ["target"] = skill.Target.ToString(),
            ["damageType"] = skill.DamageType.ToString(),
            ["damage"] = skill.AttackDamage,
            ["manaCost"] = GetConsumption(skill, Stats.CurrentMana),
            ["agCost"] = GetConsumption(skill, Stats.CurrentAbility),
            ["range"] = (int)skill.Range,
            ["element"] = skill.ElementalModifierTarget?.Designation?.Replace(" resistance", string.Empty, StringComparison.OrdinalIgnoreCase),
            ["effect"] = skill.MagicEffectDef?.Number,
            ["areaRange"] = GetAreaRange(skill),
        };
    }

    private static float? GetAreaRange(Skill skill)
    {
        if (skill.AreaSkillSettings is { UseFrustumFilter: true } frustum)
        {
            return frustum.FrustumDistance;
        }

        if (skill.AreaSkillSettings is { UseTargetAreaFilter: true } targetArea)
        {
            return targetArea.TargetAreaDiameter / 2;
        }

        if (skill.SkillType is SkillType.AreaSkillAutomaticHits)
        {
            return skill.Range;
        }

        return skill.ImplicitTargetRange > 0 ? skill.ImplicitTargetRange : null;
    }

    private static string? FindWebClientList()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "client", "web", "public", "data", "new-skills.json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}

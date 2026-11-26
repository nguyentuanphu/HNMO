// <copyright file="EpisodeQuestsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Quests;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Adds missing Episode 1 and Episode 2 steps for Shadow Phantom Soldier (257)
/// and synchronizes all 120 Episode 19 random quest Experience and Zen rewards.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.EpisodeQuestsUpdatePlugIn_Name), Description = nameof(PlugInResources.EpisodeQuestsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D1E9B4C7-6E2A-48F5-9B3C-7A1E5F204D88")]
public class EpisodeQuestsUpdatePlugIn : UpdatePlugInBase
{
    private const short ShadowPhantomSoldierNumber = 257;
    private const short Episode1Group = 1;
    private const short Episode2Group = 2;
    private const short Episode19Group = 19;

    private static readonly CharacterClassNumber[] StarterClasses =
    [
        CharacterClassNumber.DarkKnight,
        CharacterClassNumber.DarkWizard,
        CharacterClassNumber.FairyElf,
        CharacterClassNumber.Summoner,
    ];

    private static readonly IReadOnlyDictionary<short, (int Experience, int Money)> Ep19Rewards = new Dictionary<short, (int Experience, int Money)>
    {
        [5] = (70000, 0),
        [6] = (80000, 0),
        [7] = (90000, 200000),
        [8] = (100000, 0),
        [9] = (110000, 0),
        [10] = (120000, 0),
        [11] = (60000, 0),
        [12] = (50000, 0),
        [13] = (50000, 0),
        [14] = (60000, 0),
        [15] = (70000, 0),
        [16] = (80000, 0),
        [17] = (90000, 0),
        [18] = (100000, 0),
        [19] = (110000, 0),
        [20] = (120000, 0),
        [21] = (90000, 0),
        [22] = (100000, 0),
        [23] = (110000, 0),
        [24] = (120000, 0),
        [25] = (50000, 0),
        [26] = (60000, 0),
        [27] = (70000, 0),
        [28] = (80000, 0),
        [29] = (120000, 100000),
        [30] = (110000, 200000),
        [31] = (100000, 200000),
        [32] = (90000, 0),
        [33] = (80000, 0),
        [34] = (70000, 0),
        [35] = (60000, 0),
        [36] = (50000, 0),
        [37] = (110000, 100000),
        [38] = (120000, 200000),
        [39] = (50000, 200000),
        [40] = (60000, 0),
        [41] = (70000, 0),
        [42] = (80000, 0),
        [43] = (90000, 0),
        [44] = (100000, 0),
        [50] = (70000, 200000),
        [51] = (80000, 100000),
        [52] = (90000, 0),
        [53] = (100000, 0),
        [54] = (110000, 0),
        [55] = (120000, 0),
        [56] = (130000, 0),
        [57] = (140000, 0),
        [58] = (110000, 200000),
        [59] = (120000, 0),
        [60] = (130000, 0),
        [61] = (140000, 0),
        [62] = (70000, 0),
        [63] = (80000, 0),
        [64] = (90000, 0),
        [65] = (100000, 0),
        [66] = (140000, 0),
        [67] = (130000, 0),
        [68] = (120000, 0),
        [69] = (110000, 0),
        [70] = (100000, 0),
        [71] = (90000, 0),
        [72] = (80000, 0),
        [73] = (70000, 0),
        [74] = (90000, 200000),
        [75] = (100000, 0),
        [76] = (110000, 0),
        [77] = (120000, 0),
        [78] = (130000, 0),
        [79] = (140000, 0),
        [80] = (70000, 0),
        [81] = (80000, 0),
        [82] = (130000, 100000),
        [83] = (140000, 0),
        [84] = (70000, 0),
        [85] = (80000, 0),
        [86] = (90000, 0),
        [87] = (100000, 0),
        [88] = (110000, 0),
        [89] = (120000, 0),
        [95] = (90000, 0),
        [96] = (100000, 0),
        [97] = (110000, 0),
        [98] = (120000, 0),
        [99] = (130000, 0),
        [100] = (140000, 0),
        [101] = (150000, 200000),
        [102] = (160000, 0),
        [103] = (110000, 0),
        [104] = (120000, 0),
        [105] = (130000, 0),
        [106] = (140000, 0),
        [107] = (150000, 0),
        [108] = (160000, 0),
        [109] = (90000, 0),
        [110] = (100000, 0),
        [111] = (160000, 0),
        [112] = (150000, 0),
        [113] = (140000, 0),
        [114] = (130000, 0),
        [115] = (120000, 0),
        [116] = (110000, 0),
        [117] = (100000, 0),
        [118] = (90000, 0),
        [119] = (100000, 0),
        [120] = (110000, 0),
        [121] = (120000, 0),
        [122] = (130000, 0),
        [123] = (140000, 0),
        [124] = (150000, 0),
        [125] = (160000, 0),
        [126] = (90000, 0),
        [127] = (140000, 200000),
        [128] = (150000, 0),
        [129] = (160000, 0),
        [130] = (90000, 0),
        [131] = (100000, 0),
        [132] = (110000, 0),
        [133] = (120000, 0),
        [134] = (130000, 0),
    };

    /// <inheritdoc />
    public override string Name => PlugInResources.EpisodeQuestsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.EpisodeQuestsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 26, 11, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Applies Episode 1/2 quest definitions and Episode 19 reward synchronization to the configuration.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var questGiver = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == ShadowPhantomSoldierNumber);
        if (questGiver is null)
        {
            return;
        }

        AddEpisode1Quests(context, gameConfiguration, questGiver);
        AddEpisode2Quests(context, gameConfiguration, questGiver);
        SynchronizeEpisode19Rewards(context, questGiver);
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static void AddEpisode1Quests(IContext context, GameConfiguration gameConfiguration, MonsterDefinition questGiver)
    {
        foreach (var starterClass in StarterClasses)
        {
            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.FirstStepsGift), Episode1Group, 3, 4, 2, 1, 80, starterClass, repeatable: false) is { } firstSteps)
            {
                firstSteps
                    .WithItemReward(13, 20, context, gameConfiguration, itemLevel: 1)
                    .WithItemReward(13, 20, context, gameConfiguration, itemLevel: 2);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.CharacterWindowTutorial), Episode1Group, 5, 9, 2, 1, 80, starterClass, repeatable: false, requiresClientAction: true) is { } charTutorial)
            {
                charTutorial.WithExperienceReward(50, context);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.InventoryTutorial), Episode1Group, 10, 15, 2, 1, 80, starterClass, repeatable: false, requiresClientAction: true) is { } invTutorial)
            {
                invTutorial
                    .WithExperienceReward(50, context)
                    .WithMoneyReward(500, context);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.PreparePotions), Episode1Group, 17, 20, 18, 1, 80, starterClass) is { } preparePotions)
            {
                preparePotions
                    .WithItemRequirement(14, 0, context, gameConfiguration, itemCount: 5)
                    .WithExperienceReward(100, context)
                    .WithMoneyReward(1000, context);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.PrepareTownPortal), Episode1Group, 28, 29, 32, 1, 80, starterClass) is { } preparePortal)
            {
                preparePortal
                    .WithItemRequirement(14, 10, context, gameConfiguration, itemCount: 1)
                    .WithExperienceReward(200, context)
                    .WithMoneyReward(3000, context);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ReceiveElfBlessing), Episode1Group, 30, 31, 32, 1, 80, starterClass, repeatable: false) is { } elfBlessing)
            {
                elfBlessing.WithExperienceReward(100, context);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.MountUniriaTrial), Episode1Group, 60, 62, 61, 25, 80, starterClass, repeatable: false) is { } mountUniria)
            {
                mountUniria.WithItemReward(13, 2, context, gameConfiguration);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ManaPotionMilestone), Episode1Group, 66, 67, 68, 1, 80, starterClass, repeatable: false) is { } manaSupply)
            {
                manaSupply.WithItemReward(14, 3, context, gameConfiguration, durability: 5);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.LargeManaPotionMilestone), Episode1Group, 70, 71, 72, 1, 80, starterClass, repeatable: false) is { } largeManaSupply)
            {
                largeManaSupply.WithItemReward(14, 6, context, gameConfiguration, durability: 5);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.GuardianAngelMilestone), Episode1Group, 73, 74, 75, 1, 80, starterClass, repeatable: false) is { } guardianAngel)
            {
                guardianAngel.WithItemReward(13, 0, context, gameConfiguration);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.BlessingZenMilestone), Episode1Group, 76, 77, 78, 1, 80, starterClass, repeatable: false) is { } adventureFunding)
            {
                adventureFunding.WithMoneyReward(30000, context);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.AdventureExperienceMilestone), Episode1Group, 79, 80, 81, 1, 80, starterClass, repeatable: false) is { } adventureExp)
            {
                adventureExp.WithExperienceReward(3000, context);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.VeteranZenMilestone), Episode1Group, 82, 83, 84, 1, 80, starterClass, repeatable: false) is { } veteranZen)
            {
                veteranZen.WithMoneyReward(10000, context);
            }

            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.JourneyRingsGift), Episode1Group, 85, 90, 89, 1, 80, starterClass, repeatable: false) is { } journeyRings)
            {
                journeyRings
                    .WithItemReward(13, 20, context, gameConfiguration, itemLevel: 1)
                    .WithItemReward(13, 20, context, gameConfiguration, itemLevel: 2);
            }
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.FirstHunt), Episode1Group, 22, 23, 21, 1, 80, CharacterClassNumber.FairyElf) is { } huntElf)
        {
            huntElf
                .WithMonsterKillRequirement(3, 27, context, gameConfiguration)
                .WithExperienceReward(250, context)
                .WithMoneyReward(1000, context);
        }

        foreach (var lorenciaClass in new[] { CharacterClassNumber.DarkKnight, CharacterClassNumber.DarkWizard })
        {
            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.FirstHunt), Episode1Group, 22, 24, 21, 1, 80, lorenciaClass) is { } huntLorencia)
            {
                huntLorencia
                    .WithMonsterKillRequirement(3, 2, context, gameConfiguration)
                    .WithExperienceReward(250, context)
                    .WithMoneyReward(1000, context);
            }
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.FirstHunt), Episode1Group, 22, 25, 21, 1, 80, CharacterClassNumber.Summoner) is { } huntSummoner)
        {
            huntSummoner
                .WithMonsterKillRequirement(3, 418, context, gameConfiguration)
                .WithExperienceReward(250, context)
                .WithMoneyReward(1000, context);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ThirtyMonsterHunt), Episode1Group, 32, 33, 36, 1, 80, CharacterClassNumber.FairyElf) is { } hunt30Elf)
        {
            hunt30Elf
                .WithMonsterKillRequirement(30, 28, context, gameConfiguration)
                .WithExperienceReward(5000, context)
                .WithMoneyReward(3000, context);
        }

        foreach (var lorenciaClass in new[] { CharacterClassNumber.DarkKnight, CharacterClassNumber.DarkWizard })
        {
            if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ThirtyMonsterHunt), Episode1Group, 32, 34, 36, 1, 80, lorenciaClass) is { } hunt30Lorencia)
            {
                hunt30Lorencia
                    .WithMonsterKillRequirement(30, 1, context, gameConfiguration)
                    .WithExperienceReward(5000, context)
                    .WithMoneyReward(3000, context);
            }
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ThirtyMonsterHunt), Episode1Group, 32, 35, 36, 1, 80, CharacterClassNumber.Summoner) is { } hunt30Summoner)
        {
            hunt30Summoner
                .WithMonsterKillRequirement(30, 421, context, gameConfiguration)
                .WithExperienceReward(5000, context)
                .WithMoneyReward(3000, context);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.StarterEquipmentGift), Episode1Group, 39, 40, 48, 1, 80, CharacterClassNumber.FairyElf, repeatable: false) is { } elfWeaponGift)
        {
            elfWeaponGift
                .WithItemReward(4, 8, context, gameConfiguration, itemLevel: 3, hasSkill: true)
                .WithMoneyReward(5000, context);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.StarterEquipmentGift), Episode1Group, 39, 41, 48, 1, 80, CharacterClassNumber.FairyElf, repeatable: false) is { } elfArrowGift)
        {
            elfArrowGift.WithItemReward(12, 11, context, gameConfiguration);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.StarterEquipmentGift), Episode1Group, 36, 49, 50, 1, 80, CharacterClassNumber.DarkKnight, repeatable: false) is { } knightWeaponGift)
        {
            knightWeaponGift.WithItemReward(1, 2, context, gameConfiguration, itemLevel: 3, hasSkill: true);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.StarterEquipmentGift), Episode1Group, 36, 54, 55, 1, 80, CharacterClassNumber.DarkWizard, repeatable: false) is { } wizardScrollGift)
        {
            wizardScrollGift
                .WithItemReward(15, 3, context, gameConfiguration)
                .WithMoneyReward(10000, context);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.StarterEquipmentGift), Episode1Group, 36, 69, 55, 1, 80, CharacterClassNumber.Summoner, repeatable: false) is { } summonerScrollGift)
        {
            summonerScrollGift
                .WithItemReward(15, 3, context, gameConfiguration)
                .WithMoneyReward(10000, context);
        }
    }

    private static void AddEpisode2Quests(IContext context, GameConfiguration gameConfiguration, MonsterDefinition questGiver)
    {
        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastleIntroduction), Episode2Group, 1, 8, 2, 15, 49, repeatable: false) is { } ccIntro1)
        {
            ccIntro1.WithItemReward(13, 29, context, gameConfiguration);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastleIntroduction), Episode2Group, 19, 26, 20, 50, 119, repeatable: false) is { } ccIntro2)
        {
            ccIntro2.WithItemReward(13, 29, context, gameConfiguration);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastle1Preparation), Episode2Group, 9, 11, 10, 15, 49) is { } cc1Prep)
        {
            cc1Prep
                .WithMonsterKillRequirement(40, 12, context, gameConfiguration)
                .WithItemReward(14, 2, context, gameConfiguration, durability: 10)
                .WithItemReward(14, 5, context, gameConfiguration, durability: 10)
                .WithItemReward(14, 35, context, gameConfiguration, durability: 4);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastle2Preparation), Episode2Group, 27, 29, 28, 50, 119) is { } cc2Prep)
        {
            cc2Prep
                .WithMonsterKillRequirement(60, 13, context, gameConfiguration)
                .WithItemReward(14, 3, context, gameConfiguration, durability: 5)
                .WithItemReward(14, 6, context, gameConfiguration, durability: 5)
                .WithItemReward(14, 35, context, gameConfiguration, durability: 8);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastle3Preparation), Episode2Group, 37, 39, 38, 120, 179) is { } cc3Prep)
        {
            cc3Prep
                .WithMonsterKillRequirement(80, 10, context, gameConfiguration)
                .WithItemReward(14, 3, context, gameConfiguration, durability: 10)
                .WithItemReward(14, 6, context, gameConfiguration, durability: 10)
                .WithItemReward(14, 38, context, gameConfiguration, durability: 5);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastle4Preparation), Episode2Group, 46, 48, 47, 180, 239) is { } cc4Prep)
        {
            cc4Prep
                .WithMonsterKillRequirement(100, 37, context, gameConfiguration)
                .WithItemReward(14, 36, context, gameConfiguration, durability: 15)
                .WithItemReward(14, 39, context, gameConfiguration, durability: 5);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastle5Preparation), Episode2Group, 55, 57, 56, 240, 299) is { } cc5Prep)
        {
            cc5Prep
                .WithMonsterKillRequirement(100, 70, context, gameConfiguration)
                .WithItemReward(14, 37, context, gameConfiguration, durability: 10)
                .WithItemReward(14, 39, context, gameConfiguration, durability: 10);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastle6Preparation), Episode2Group, 64, 66, 65, 300, 400) is { } cc6Prep)
        {
            cc6Prep
                .WithMonsterKillRequirement(30, 59, context, gameConfiguration)
                .WithItemReward(14, 37, context, gameConfiguration, durability: 20)
                .WithItemReward(14, 40, context, gameConfiguration, durability: 10);
        }

        if (EnsureQuest(context, gameConfiguration, questGiver, LocalizedString.FromResource(() => QuestNames.ChaosCastle7Preparation), Episode2Group, 73, 75, 74, 400, 0) is { } cc7Prep)
        {
            cc7Prep
                .WithMonsterKillRequirement(100, 444, context, gameConfiguration)
                .WithItemReward(14, 37, context, gameConfiguration, durability: 30)
                .WithItemReward(14, 40, context, gameConfiguration, durability: 15);
        }
    }

    private static void SynchronizeEpisode19Rewards(IContext context, MonsterDefinition questGiver)
    {
        foreach (var quest in questGiver.Quests.Where(q => q.Group == Episode19Group))
        {
            if (!Ep19Rewards.TryGetValue(quest.Number, out var expected))
            {
                continue;
            }

            SynchronizeRewardType(context, quest, QuestRewardType.Experience, expected.Experience);
            SynchronizeRewardType(context, quest, QuestRewardType.Money, expected.Money);
        }
    }

    private static void SynchronizeRewardType(IContext context, QuestDefinition quest, QuestRewardType rewardType, int expectedValue)
    {
        var existing = quest.Rewards.FirstOrDefault(r => r.RewardType == rewardType);
        if (expectedValue > 0)
        {
            if (existing is not null)
            {
                existing.Value = expectedValue;
            }
            else if (rewardType == QuestRewardType.Experience)
            {
                quest.WithExperienceReward(expectedValue, context);
            }
            else
            {
                quest.WithMoneyReward(expectedValue, context);
            }
        }
        else if (existing is not null)
        {
            quest.Rewards.Remove(existing);
        }
    }

    private static QuestDefinition? EnsureQuest(
        IContext context,
        GameConfiguration gameConfiguration,
        MonsterDefinition questGiver,
        LocalizedString name,
        short group,
        short startingNumber,
        short number,
        short refuseNumber,
        int minimumLevel,
        int maximumLevel,
        CharacterClassNumber? qualifiedCharacter = null,
        bool repeatable = true,
        bool requiresClientAction = false)
    {
        var exists = questGiver.Quests.Any(q =>
            q.Group == group
            && q.Number == number
            && (qualifiedCharacter is null
                ? q.QualifiedCharacter is null
                : q.QualifiedCharacter?.Number == (byte)qualifiedCharacter.Value));
        if (exists)
        {
            return null;
        }

        var questDefinition = context.CreateNew<QuestDefinition>();
        questDefinition.SetGuid(group, number, (byte?)qualifiedCharacter ?? 0);
        questDefinition.QuestGiver = questGiver;
        questGiver.Quests.Add(questDefinition);
        questDefinition.Name = name;
        questDefinition.Group = group;
        questDefinition.Number = number;
        questDefinition.StartingNumber = startingNumber;
        questDefinition.RefuseNumber = refuseNumber;
        questDefinition.MinimumCharacterLevel = minimumLevel;
        questDefinition.MaximumCharacterLevel = maximumLevel;
        questDefinition.Repeatable = repeatable;
        questDefinition.RequiresClientAction = requiresClientAction;
        if (qualifiedCharacter.HasValue)
        {
            questDefinition.QualifiedCharacter = gameConfiguration.CharacterClasses.First(c => c.Number == (byte)qualifiedCharacter.Value);
        }

        return questDefinition;
    }
}

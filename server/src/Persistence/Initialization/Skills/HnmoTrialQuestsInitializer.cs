// <copyright file="HnmoTrialQuestsInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Skills;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Configuration.Quests;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.CharacterClasses;

/// <summary>
/// Creates the trial quests which teach the D skills of the HNMO skills (see <see cref="HnmoSkillsInitializer"/>).
/// It's used by the data initialization of new databases and by the <see cref="Updates.AddHnmoSkillsUpdatePlugIn"/>
/// for existing ones, and does nothing when the quests exist already.
/// </summary>
/// <remarks>
/// The Elf Soldier, who stands in Lorencia and gives the other quests of the quest dialog already, gives one trial
/// quest per class, from level 250: kill 200 monsters of a set kind and bring five Jewels of Chaos. The reward is the
/// D skill of the class. As quests are bound to one character class, every evolution of a class gets its own copy.
/// </remarks>
internal class HnmoTrialQuestsInitializer : InitializerBase
{
    /// <summary>
    /// The quest group of the trial quests.
    /// </summary>
    internal const short TrialQuestGroup = 40;

    /// <summary>
    /// The number of the elf soldier, who gives the trial quests.
    /// </summary>
    internal const short ElfSoldierNumber = 257;

    /// <summary>
    /// The number of monsters which have to be killed.
    /// </summary>
    internal const int RequiredKills = HnmoSkillValues.TrialRequiredKills;

    /// <summary>
    /// The number of Jewels of Chaos which have to be brought.
    /// </summary>
    internal const int RequiredJewels = HnmoSkillValues.TrialRequiredJewels;

    private const short StartingNumber = 0;

    private const short QuestNumber = 1;

    private const short RefuseNumber = 2;

    private const byte JewelOfChaosGroup = 12;

    private const short JewelOfChaosNumber = 15;

    /// <summary>
    /// Initializes a new instance of the <see cref="HnmoTrialQuestsInitializer"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public HnmoTrialQuestsInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <summary>
    /// Gets the trial of every class, in the order of the skill numbers: its classes, the D skill and the monster to kill.
    /// </summary>
    internal static IReadOnlyList<(CharacterClasses Classes, SkillNumber Skill, short MonsterNumber)> Trials { get; } =
    [
        (CharacterClasses.AllKnights, SkillNumber.KnightCharge, 352), // Iron Rider, Kanturu
        (CharacterClasses.AllWizards, SkillNumber.ManaShield, 353), // Satyros, Kanturu
        (CharacterClasses.AllElfs, SkillNumber.SpiritWolf, 354), // Blade Hunter, Kanturu
        (CharacterClasses.AllMGs, SkillNumber.HeavenSword, 355), // Kentauros, Kanturu
        (CharacterClasses.AllLords, SkillNumber.RoyalBeam, 356), // Gigantis, Kanturu
        (CharacterClasses.AllSummoners, SkillNumber.SoulDrain, 306), // Death Rider, Aida
        (CharacterClasses.AllFighters, SkillNumber.QuakeFist, 454), // Ice Walker, Raklion
    ];

    /// <inheritdoc />
    public override void Initialize()
    {
        var elfSoldier = this.GameConfiguration.Monsters.First(m => m.Number == ElfSoldierNumber);
        if (elfSoldier.Quests.Any(q => q.Group == TrialQuestGroup))
        {
            return;
        }

        var jewelOfChaos = this.GameConfiguration.Items.First(i => i.Group == JewelOfChaosGroup && i.Number == JewelOfChaosNumber);
        LocalizedString[] names =
        [
            LocalizedString.FromResource(() => QuestNames.TrialOfTheDarkKnight),
            LocalizedString.FromResource(() => QuestNames.TrialOfTheDarkWizard),
            LocalizedString.FromResource(() => QuestNames.TrialOfTheFairyElf),
            LocalizedString.FromResource(() => QuestNames.TrialOfTheMagicGladiator),
            LocalizedString.FromResource(() => QuestNames.TrialOfTheDarkLord),
            LocalizedString.FromResource(() => QuestNames.TrialOfTheSummoner),
            LocalizedString.FromResource(() => QuestNames.TrialOfTheRageFighter),
        ];

        for (var i = 0; i < Trials.Count; i++)
        {
            var (classes, skillNumber, monsterNumber) = Trials[i];
            var skill = this.GameConfiguration.Skills.First(s => s.Number == (short)skillNumber);
            var monster = this.GameConfiguration.Monsters.First(m => m.Number == monsterNumber);
            foreach (var characterClass in this.GameConfiguration.DetermineCharacterClasses(classes))
            {
                this.CreateQuest(elfSoldier, characterClass, names[i], monster, jewelOfChaos, skill);
            }
        }
    }

    /// <summary>
    /// Determines whether the item requirement asks for Jewels of Chaos.
    /// </summary>
    /// <param name="requirement">The item requirement.</param>
    /// <returns><c>true</c>, if the requirement asks for Jewels of Chaos.</returns>
    internal static bool IsJewelOfChaos(QuestItemRequirement requirement)
        => requirement.Item is { Group: JewelOfChaosGroup, Number: JewelOfChaosNumber };

    private void CreateQuest(MonsterDefinition questGiver, CharacterClass characterClass, LocalizedString name, MonsterDefinition monster, ItemDefinition jewel, Skill skill)
    {
        var quest = this.Context.CreateNew<QuestDefinition>();
        quest.SetGuid(TrialQuestGroup, QuestNumber, characterClass.Number);
        questGiver.Quests.Add(quest);
        quest.QuestGiver = questGiver;
        quest.Name = name;
        quest.Group = TrialQuestGroup;
        quest.Number = QuestNumber;
        quest.StartingNumber = StartingNumber;
        quest.RefuseNumber = RefuseNumber;
        quest.MinimumCharacterLevel = HnmoSkillsInitializer.LevelOfSkillD;
        quest.Repeatable = false;
        quest.QualifiedCharacter = characterClass;

        var kills = this.Context.CreateNew<QuestMonsterKillRequirement>();
        kills.MinimumNumber = RequiredKills;
        kills.Monster = monster;
        quest.RequiredMonsterKills.Add(kills);

        var jewelRequirement = this.Context.CreateNew<QuestItemRequirement>();
        jewelRequirement.MinimumNumber = RequiredJewels;
        jewelRequirement.Item = jewel;
        quest.RequiredItems.Add(jewelRequirement);

        var reward = this.Context.CreateNew<QuestReward>();
        reward.RewardType = QuestRewardType.Skill;
        reward.Value = 1;
        reward.SkillReward = skill;
        quest.Rewards.Add(reward);
    }
}

// <copyright file="HnmoSkillsLearningTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Quests;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlayerActions.Quests;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using CharacterQuestState = HNMO.Persistence.BasicModel.CharacterQuestState;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;
using QuestDefinition = HNMO.Persistence.BasicModel.QuestDefinition;
using QuestMonsterKillRequirement = HNMO.Persistence.BasicModel.QuestMonsterKillRequirement;
using QuestMonsterKillRequirementState = HNMO.Persistence.BasicModel.QuestMonsterKillRequirementState;
using QuestReward = HNMO.Persistence.BasicModel.QuestReward;

/// <summary>
/// Tests how the HNMO skills are learned: the A, B and C skills by level, the D skills by the reward of the trial quest.
/// </summary>
[TestFixture]
public class HnmoSkillsLearningTest
{
    private const short TrialQuestGroup = 40;

    /// <summary>
    /// The skills of a level are learned when the character reaches it, with a message; the skills of higher levels
    /// and the skills of other classes are not.
    /// </summary>
    /// <returns>The task.</returns>
    /// <summary>
    /// Bots don't get the trial quest skills (each class's fourth skill) by level.
    /// </summary>
    [Test]
    public void TrialQuestSkillsAreNotForBotsByLevel()
    {
        Assert.That(new[] { 703, 707, 711, 715, 719, 723, 727 }.All(HNMO.GameLogic.Bots.BotProgression.IsTrialQuestSkill), Is.True);
        Assert.That(new[] { 700, 701, 702, 704, 726, 699, 728 }.Any(HNMO.GameLogic.Bots.BotProgression.IsTrialQuestSkill), Is.False);
    }

    [Test]
    public async Task SkillsAreLearnedAtTheirLevelAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var ownClass = player.SelectedCharacter!.CharacterClass!;
        AddSkill(player, 700, "Earth Splitter", ownClass);
        AddSkill(player, 701, "Blood Slash", ownClass);
        AddSkill(player, 702, "Iron Guard", ownClass);
        AddSkill(player, 703, "Charge", ownClass);
        AddSkill(player, 704, "Ice Dragon", new Mock<CharacterClass>().Object);
        var plugIn = new LearnSkillsByLevelPlugIn();

        player.Attributes![Stats.Level] = 79;
        await plugIn.LearnSkillsAsync(player).ConfigureAwait(false);
        Assert.That(player.SkillList!.ContainsSkill(700), Is.False);

        player.Attributes[Stats.Level] = 150;
        await plugIn.LearnSkillsAsync(player).ConfigureAwait(false);
        Assert.That(player.SkillList.ContainsSkill(700), Is.True);
        Assert.That(player.SkillList.ContainsSkill(701), Is.True);
        Assert.That(player.SkillList.ContainsSkill(702), Is.False);
        Assert.That(player.SkillList.ContainsSkill(704), Is.False, "the skill of another class");
        var messages = Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!);
        messages.Verify(v => v!.ShowMessageAsync("You have learned Earth Splitter.", MessageType.BlueNormal), Times.Once);
        messages.Verify(v => v!.ShowMessageAsync("You have learned Blood Slash.", MessageType.BlueNormal), Times.Once);

        player.Attributes[Stats.Level] = 400;
        await plugIn.LearnSkillsAsync(player).ConfigureAwait(false);
        Assert.That(player.SkillList.ContainsSkill(702), Is.True);
        Assert.That(player.SkillList.ContainsSkill(703), Is.False, "the D skill is learned by the trial quest");
        messages.Verify(v => v!.ShowMessageAsync("You have learned Earth Splitter.", MessageType.BlueNormal), Times.Once);
    }

    /// <summary>
    /// A character which is already past the levels learns the skills when it enters the game.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SkillsAreLearnedWhenEnteringTheGameAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var ownClass = player.SelectedCharacter!.CharacterClass!;
        AddSkill(player, 700, "Earth Splitter", ownClass);
        AddSkill(player, 701, "Blood Slash", ownClass);
        AddSkill(player, 702, "Iron Guard", ownClass);
        player.Attributes![Stats.Level] = 230;

        await new LearnSkillsByLevelPlugIn().PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);

        Assert.That(player.SkillList!.ContainsSkill(700), Is.True);
        Assert.That(player.SkillList.ContainsSkill(701), Is.True);
        Assert.That(player.SkillList.ContainsSkill(702), Is.True);
    }

    /// <summary>
    /// The default configuration teaches the A, B and C skills of every class at the levels 80, 150 and 220.
    /// </summary>
    [Test]
    public void DefaultConfigurationHasTheSkillsOfTheLevels()
    {
        var skills = LearnSkillsByLevelPlugInConfiguration.CreateDefault().Skills;

        Assert.That(skills, Has.Count.EqualTo(21));
        Assert.That(skills.Select(s => s.SkillNumber), Is.EquivalentTo(Enumerable.Range(700, 28).Where(n => n % 4 != 3).Select(n => (short)n)));
        Assert.That(skills.Where(s => s.SkillNumber % 4 == 0).Select(s => s.MinimumLevel), Is.All.EqualTo(80));
        Assert.That(skills.Where(s => s.SkillNumber % 4 == 1).Select(s => s.MinimumLevel), Is.All.EqualTo(150));
        Assert.That(skills.Where(s => s.SkillNumber % 4 == 2).Select(s => s.MinimumLevel), Is.All.EqualTo(220));
    }

    /// <summary>
    /// Completing a trial quest teaches the skill of its reward.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task QuestRewardTeachesTheSkillAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var skill = AddSkill(player, 703, "Charge", player.SelectedCharacter!.CharacterClass!);
        var (quest, state) = SetUpTrialQuest(player, skill, killCount: 100);

        await new QuestCompletionAction().CompleteQuestAsync(player, TrialQuestGroup, quest.Number).ConfigureAwait(false);

        Assert.That(player.SkillList!.ContainsSkill(703), Is.True);
        Assert.That(state.LastFinishedQuest, Is.SameAs(quest));
        Assert.That(state.ActiveQuest, Is.Null);
    }

    /// <summary>
    /// A trial quest whose monsters aren't killed yet isn't completed and doesn't teach the skill.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task QuestRewardNeedsTheKillsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var skill = AddSkill(player, 703, "Charge", player.SelectedCharacter!.CharacterClass!);
        var (quest, state) = SetUpTrialQuest(player, skill, killCount: 99);

        await new QuestCompletionAction().CompleteQuestAsync(player, TrialQuestGroup, quest.Number).ConfigureAwait(false);

        Assert.That(player.SkillList!.ContainsSkill(703), Is.False);
        Assert.That(state.ActiveQuest, Is.SameAs(quest));
    }

    private static Skill AddSkill(Player player, short number, string name, CharacterClass characterClass)
    {
        var skillMock = new Mock<Skill>();
        skillMock.SetupAllProperties();
        skillMock.Setup(s => s.QualifiedCharacters).Returns(new List<CharacterClass> { characterClass });
        var skill = skillMock.Object;
        skill.Number = number;
        skill.Name = name;
        skill.SkillType = SkillType.DirectHit;
        player.GameContext.Configuration.Skills.Add(skill);
        return skill;
    }

    private static (QuestDefinition Quest, CharacterQuestState State) SetUpTrialQuest(Player player, Skill skill, int killCount)
    {
        var kills = new QuestMonsterKillRequirement { MinimumNumber = 100, Monster = new MonsterDefinition { Number = 352 } };
        var quest = new QuestDefinition { Group = TrialQuestGroup, Number = 1, MinimumCharacterLevel = 250 };
        quest.RequiredMonsterKills.Add(kills);
        quest.Rewards.Add(new QuestReward { RewardType = QuestRewardType.Skill, Value = 1, SkillReward = skill });

        var state = new CharacterQuestState { Group = TrialQuestGroup, ActiveQuest = quest };
        state.RequirementStates.Add(new QuestMonsterKillRequirementState { Requirement = kills, KillCount = killCount });
        Mock.Get(player.SelectedCharacter!).Setup(c => c.QuestStates).Returns(new List<DataModel.Entities.CharacterQuestState> { state });
        return (quest, state);
    }
}

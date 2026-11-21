// <copyright file="QuestCompletionAtNpcTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration.Quests;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Quests;
using Moq;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests that quests can only be completed while talking to their quest giver NPC (owner, 21 Nov 2026).
/// </summary>
[TestFixture]
public class QuestCompletionAtNpcTest
{
    private const short Group = 18;
    private const short Number = 1;

    /// <summary>A quest cannot be completed when no NPC dialog is open.</summary>
    [Test]
    public async Task CompletingQuestWithoutOpenedNpcIsRefusedAsync()
    {
        var (player, quest, state, _) = await CreatePlayerWithActiveQuestAsync().ConfigureAwait(false);
        player.OpenedNpc = null;

        await new QuestCompletionAction().CompleteQuestAsync(player, Group, Number).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(state.ActiveQuest, Is.SameAs(quest), "quest remains active when no NPC is open");
            Assert.That(state.LastFinishedQuest, Is.Null);
        });
    }

    /// <summary>A quest cannot be completed when a different NPC's dialog is open.</summary>
    [Test]
    public async Task CompletingQuestAtWrongNpcIsRefusedAsync()
    {
        var (player, quest, state, _) = await CreatePlayerWithActiveQuestAsync().ConfigureAwait(false);
        var otherNpcDef = new BasicModel.MonsterDefinition { Number = 250, Designation = "Wandering Merchant" };
        player.OpenedNpc = new NonPlayerCharacter(new BasicModel.MonsterSpawnArea(), otherNpcDef, player.CurrentMap!);

        await new QuestCompletionAction().CompleteQuestAsync(player, Group, Number).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(state.ActiveQuest, Is.SameAs(quest), "quest remains active at wrong NPC");
            Assert.That(state.LastFinishedQuest, Is.Null);
        });
    }

    /// <summary>A quest completes when the player is talking to its quest giver NPC.</summary>
    [Test]
    public async Task CompletingQuestAtQuestGiverNpcSucceedsAsync()
    {
        var (player, quest, state, questNpc) = await CreatePlayerWithActiveQuestAsync().ConfigureAwait(false);
        player.OpenedNpc = questNpc;

        await new QuestCompletionAction().CompleteQuestAsync(player, Group, Number).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(state.ActiveQuest, Is.Null, "quest cleared on completion at quest giver");
            Assert.That(state.LastFinishedQuest, Is.SameAs(quest));
            Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(10));
        });
    }

    private static async Task<(Player Player, BasicModel.QuestDefinition Quest, BasicModel.CharacterQuestState State, NonPlayerCharacter QuestNpc)> CreatePlayerWithActiveQuestAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var questNpcDef = new BasicModel.MonsterDefinition { Number = 257, Designation = "Shadow Phantom Soldier" };
        var quest = new BasicModel.QuestDefinition
        {
            Group = Group,
            Number = Number,
            QuestGiver = questNpcDef,
        };
        quest.Rewards.Add(new BasicModel.QuestReward { RewardType = QuestRewardType.LevelUpPoints, Value = 10 });
        questNpcDef.Quests.Add(quest);

        var state = new BasicModel.CharacterQuestState { Group = Group, ActiveQuest = quest };
        Mock.Get(player.SelectedCharacter!).Setup(c => c.QuestStates).Returns(new List<DataModel.Entities.CharacterQuestState> { state });
        var questNpc = new NonPlayerCharacter(new BasicModel.MonsterSpawnArea(), questNpcDef, player.CurrentMap!);
        return (player, quest, state, questNpc);
    }
}

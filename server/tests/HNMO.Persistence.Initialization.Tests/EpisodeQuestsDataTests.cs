// <copyright file="EpisodeQuestsDataTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Quests;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests Episode 1, Episode 2, and Episode 19 quest data initialization and update plugin parity.
/// </summary>
[TestFixture]
internal class EpisodeQuestsDataTests
{
    /// <summary>
    /// Verifies that a freshly initialized Season 6 configuration includes the Episode 1 and Episode 2 steps and synced Episode 19 rewards.
    /// </summary>
    [Test]
    public async Task NewSeason6DatabaseHasEpisode1And2StepsAndSyncedEp19RewardsAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        AssertEpisodeParity(gameConfiguration);
    }

    /// <summary>
    /// Verifies that <see cref="EpisodeQuestsUpdatePlugIn"/> restores missing Episode 1/2 steps and resyncs drifted Episode 19 rewards idempotently.
    /// </summary>
    [Test]
    public async Task UpdatePlugInRestoresEpisodeQuestsAndSyncsEp19IdempotentlyAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;

        var questGiver = gameConfiguration.Monsters.First(m => m.Number == 257);
        foreach (var legacy in questGiver.Quests.Where(q => q.Group is 1 or 2).ToList())
        {
            questGiver.Quests.Remove(legacy);
        }

        var ep19Step7 = questGiver.Quests.Single(q => q.Group == 19 && q.Number == 7);
        ep19Step7.Rewards.Clear();
        ep19Step7.WithExperienceReward(1234, context);

        var ep19Step35 = questGiver.Quests.Single(q => q.Group == 19 && q.Number == 35);
        ep19Step35.Rewards.Clear();

        var update = new EpisodeQuestsUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        var countAfterFirstApply = questGiver.Quests.Count;
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(questGiver.Quests.Count, Is.EqualTo(countAfterFirstApply));
        AssertEpisodeParity(gameConfiguration);
    }

    private static void AssertEpisodeParity(GameConfiguration gameConfiguration)
    {
        var questGiver = gameConfiguration.Monsters.First(m => m.Number == 257);
        var ep1 = questGiver.Quests.Where(q => q.Group == 1).ToList();
        var ep2 = questGiver.Quests.Where(q => q.Group == 2).ToList();
        var ep19 = questGiver.Quests.Where(q => q.Group == 19).ToDictionary(q => q.Number);

        Assert.Multiple(() =>
        {
            // 14 steps x 4 starter classes + 2 steps x 4 (23..25, 33..35 across DW/DK/FE/Sum) + 5 class gift steps = 69 definitions.
            Assert.That(ep1, Has.Count.EqualTo(69));
            Assert.That(ep1.Where(q => q.Number == 9).All(q => q.RequiresClientAction && !q.Repeatable), Is.True);
            Assert.That(ep1.Where(q => q.Number == 15).All(q => q.RequiresClientAction && !q.Repeatable), Is.True);
            Assert.That(ep1.First(q => q.Number == 67).Rewards.Single().ItemReward?.Durability, Is.EqualTo(5));
            Assert.That(ep1.First(q => q.Number == 71).Rewards.Single().ItemReward?.Durability, Is.EqualTo(5));
            Assert.That(ep1.Single(q => q.Number == 49 && q.QualifiedCharacter?.Number == (byte)CharacterClassNumber.DarkKnight).Rewards.Single().ItemReward?.Definition?.Number, Is.EqualTo(2));

            Assert.That(ep2, Has.Count.EqualTo(9));
            Assert.That(ep2.Where(q => q.Number is 8 or 26).All(q => !q.Repeatable), Is.True);
            var cc1 = ep2.Single(q => q.Number == 11);
            Assert.That(cc1.RequiredMonsterKills.Single().MinimumNumber, Is.EqualTo(40));
            Assert.That(cc1.RequiredMonsterKills.Single().Monster?.Number, Is.EqualTo(12));
            Assert.That(cc1.Rewards.Select(r => r.ItemReward?.Durability), Is.EquivalentTo(new double[] { 10, 10, 4 }));

            var cc7 = ep2.Single(q => q.Number == 75);
            Assert.That(cc7.MinimumCharacterLevel, Is.EqualTo(400));
            Assert.That(cc7.MaximumCharacterLevel, Is.Zero);
            Assert.That(cc7.RequiredMonsterKills.Single().Monster?.Number, Is.EqualTo(444));
            Assert.That(cc7.Rewards.Select(r => r.ItemReward?.Durability), Is.EquivalentTo(new double[] { 30, 15 }));

            Assert.That(ep19, Has.Count.EqualTo(120));
            var step7 = ep19[7];
            Assert.That(step7.Rewards.Single(r => r.RewardType == QuestRewardType.Experience).Value, Is.EqualTo(90000));
            Assert.That(step7.Rewards.Single(r => r.RewardType == QuestRewardType.Money).Value, Is.EqualTo(200000));
            var step35 = ep19[35];
            Assert.That(step35.Rewards.Single(r => r.RewardType == QuestRewardType.Experience).Value, Is.EqualTo(60000));
            Assert.That(step35.Rewards.Any(r => r.RewardType == QuestRewardType.Money), Is.False);
            var step101 = ep19[101];
            Assert.That(step101.Rewards.Single(r => r.RewardType == QuestRewardType.Experience).Value, Is.EqualTo(150000));
            Assert.That(step101.Rewards.Single(r => r.RewardType == QuestRewardType.Money).Value, Is.EqualTo(200000));
            var step127 = ep19[127];
            Assert.That(step127.Rewards.Single(r => r.RewardType == QuestRewardType.Experience).Value, Is.EqualTo(140000));
            Assert.That(step127.Rewards.Single(r => r.RewardType == QuestRewardType.Money).Value, Is.EqualTo(200000));
        });
    }

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return (context, gameConfiguration);
    }
}

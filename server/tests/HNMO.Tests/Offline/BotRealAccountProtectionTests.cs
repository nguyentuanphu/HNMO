// <copyright file="BotRealAccountProtectionTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Offline;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Bots;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies that real player accounts whose login name matches a generated bot login name
/// (e.g. <c>bot0001</c>) are never taken over or modified by the bot system.
/// </summary>
[TestFixture]
public class BotRealAccountProtectionTests
{
    /// <summary>
    /// Spawning a generated bot skips a real account (<see cref="Account.IsBot"/> is false)
    /// even when its login name matches <see cref="BotGenerator.GetLoginName"/>.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpawnBotAsync_NeverTakesOverRealAccountWithBotLoginNameAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        await CreateAccountAsync(gameContext, BotGenerator.GetLoginName(1), "RealHuman", isBot: false).ConfigureAwait(false);
        await CreateAccountAsync(gameContext, BotGenerator.GetLoginName(2), "GeneratedBot", isBot: true).ConfigureAwait(false);

        var manager = new BotManager();
        try
        {
            var spawnedReal = await manager.SpawnBotAsync(gameContext, BotGenerator.GetLoginName(1), 0).ConfigureAwait(false);
            var spawnedBot = await manager.SpawnBotAsync(gameContext, BotGenerator.GetLoginName(2), 0).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(spawnedReal, Is.False, "A real account must never be spawned as a bot.");
                Assert.That(manager.IsActive(BotGenerator.GetLoginName(1), 0), Is.False);
                Assert.That(spawnedBot, Is.True, "A flagged bot account should still spawn normally.");
                Assert.That(manager.IsActive(BotGenerator.GetLoginName(2), 0), Is.True);
                Assert.That(manager.BotCount, Is.EqualTo(1));
            });
        }
        finally
        {
            await manager.StopAllAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Generating missing bot accounts leaves an existing real account named <c>bot0001</c>
    /// untouched (preserving <see cref="Account.IsBot"/> = false and its original characters).
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EnsureBotsAsync_DoesNotOverwriteOrFlagRealAccountNamedLikeBotAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var map = gameContext.Configuration.Maps.First();
        var stats = new List<StatAttributeDefinition>
        {
            new(Stats.Level, 1, false),
            new(Stats.MasterLevel, 0, false),
            new(Stats.PointsPerLevelUp, 5, false),
            new(Stats.BaseStrength, 28, true),
            new(Stats.BaseAgility, 20, true),
            new(Stats.BaseVitality, 25, true),
            new(Stats.BaseEnergy, 10, true),
            new(Stats.CurrentHealth, 100, false),
            new(Stats.CurrentMana, 50, false),
        };
        using var configCtx = gameContext.PersistenceContextProvider.CreateNewContext();
        var knightClass = CreateClass(configCtx, 4, "Dark Knight", map, stats);
        var elfClass = CreateClass(configCtx, BotClassNumbers.FairyElfNumber, "Fairy Elf", map, stats);
        gameContext.Configuration.CharacterClasses.Add(knightClass);
        gameContext.Configuration.CharacterClasses.Add(elfClass);
        gameContext.Configuration.MaximumCharactersPerAccount = 5;

        await CreateAccountAsync(gameContext, BotGenerator.GetLoginName(1), "RealHuman", isBot: false, knightClass).ConfigureAwait(false);

        var generator = new BotGenerator(gameContext, NullLogger.Instance);
        var created = await generator.EnsureBotsAsync(2, 1, BotStartupProfile.For(true)).ConfigureAwait(false);

        using var verifyContext = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration);
        var realAccount = await verifyContext.GetAccountByLoginNameAsync(BotGenerator.GetLoginName(1)).ConfigureAwait(false);
        var botAccount = await verifyContext.GetAccountByLoginNameAsync(BotGenerator.GetLoginName(2)).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.EqualTo(1));
            Assert.That(realAccount, Is.Not.Null);
            Assert.That(realAccount!.IsBot, Is.False);
            Assert.That(realAccount.Characters.Select(c => c.Name), Is.EqualTo(new[] { "RealHuman" }));
            Assert.That(botAccount, Is.Not.Null);
            Assert.That(botAccount!.IsBot, Is.True);
        });
    }

    private static async ValueTask CreateAccountAsync(
        IGameContext gameContext,
        string loginName,
        string characterName,
        bool isBot,
        CharacterClass? characterClass = null)
    {
        var config = gameContext.Configuration;
        using var ctx = gameContext.PersistenceContextProvider.CreateNewPlayerContext(config);
        var account = ctx.CreateNew<Account>();
        account.LoginName = loginName;
        account.IsBot = isBot;

        var character = ctx.CreateNew<Character>();
        character.Name = characterName;
        character.CharacterSlot = 0;
        if (characterClass is null && config.CharacterClasses.FirstOrDefault() is null)
        {
            var fallbackClass = ctx.CreateNew<CharacterClass>();
            fallbackClass.HomeMap = config.Maps.FirstOrDefault();
            character.CharacterClass = fallbackClass;
        }
        else
        {
            character.CharacterClass = characterClass ?? config.CharacterClasses.First();
        }

        account.Characters.Add(character);
        await ctx.SaveChangesAsync().ConfigureAwait(false);
    }

    private static CharacterClass CreateClass(
        HNMO.Persistence.IContext configCtx,
        byte number,
        string name,
        GameMapDefinition map,
        IEnumerable<StatAttributeDefinition> stats)
    {
        var characterClass = configCtx.CreateNew<CharacterClass>();
        characterClass.Number = number;
        characterClass.Name = name;
        characterClass.CanGetCreated = true;
        characterClass.IsActive = true;
        characterClass.HomeMap = map;
        foreach (var stat in stats)
        {
            var definition = configCtx.CreateNew<StatAttributeDefinition>();
            definition.Attribute = stat.Attribute;
            definition.BaseValue = stat.BaseValue;
            definition.IncreasableByPlayer = stat.IncreasableByPlayer;
            characterClass.StatAttributes.Add(definition);
        }

        return characterClass;
    }
}

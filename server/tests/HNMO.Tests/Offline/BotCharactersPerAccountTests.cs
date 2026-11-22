// <copyright file="BotCharactersPerAccountTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Offline;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Bots;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Verifies that the effective characters-per-account limit is computed consistently in one place,
/// respecting both <see cref="BotConfiguration.MaxCharactersPerAccount"/> and
/// <see cref="GameConfiguration.MaximumCharactersPerAccount"/> across partitioning, generation,
/// spawning, and presence rotation.
/// </summary>
[TestFixture]
public class BotCharactersPerAccountTests
{
    /// <summary>
    /// Partition sizing must divide server bot capacity by the same clamped characters-per-account
    /// limit used when generating and spawning characters, instead of ignoring
    /// <see cref="GameConfiguration.MaximumCharactersPerAccount"/>.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CreatePartition_RespectsGameConfigurationMaximumCharactersPerAccountAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var loginServer = new LoginServer.LoginServer();
        using var gameContext = LoginTestSetup.CreateGameServerContext(persistence, loginServer, maximumPlayers: 100);
        gameContext.Configuration.MaximumCharactersPerAccount = 2;

        // Persist the game server definition so GetAccountCapacitiesAsync reads MaximumPlayers = 100.
        using (var cfgCtx = persistence.CreateNewContext())
        {
            var serverDef = cfgCtx.CreateNew<GameServerDefinition>();
            serverDef.ServerID = 0;
            var serverConfig = cfgCtx.CreateNew<GameServerConfiguration>();
            serverConfig.MaximumPlayers = 100;
            serverDef.ServerConfiguration = serverConfig;
            await cfgCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        var botConfig = new BotConfiguration
        {
            NumberOfAccounts = 30,
            MaxCharactersPerAccount = 5,
            BotCapacityPercent = 60, // 60 slots available -> 30 accounts at 2 chars/account (not 12 at 5 chars/account).
        };

        var partition = await BotServerPartition.CreateAsync(gameContext, botConfig, NullLogger.Instance).ConfigureAwait(false);

        Assert.That(partition.AccountCount, Is.EqualTo(30));
    }

    /// <summary>
    /// Verifies <see cref="BotConfiguration.GetEffectiveCharactersPerAccount"/> clamps cleanly against
    /// both the hard limit (5) and any positive <see cref="GameConfiguration.MaximumCharactersPerAccount"/>.
    /// </summary>
    [TestCase(5, null, 5)]
    [TestCase(10, null, 5)]
    [TestCase(0, null, 1)]
    [TestCase(5, 2, 2)]
    [TestCase(1, 3, 1)]
    [TestCase(5, 0, 5)]
    public void GetEffectiveCharactersPerAccount_ClampsToLowestLimit(int configured, int? gameLimit, int expected)
    {
        var botConfig = new BotConfiguration { MaxCharactersPerAccount = configured };
        Assert.That(botConfig.GetEffectiveCharactersPerAccount(gameLimit), Is.EqualTo(expected));
    }
}

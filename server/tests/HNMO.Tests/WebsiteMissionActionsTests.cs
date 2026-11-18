// <copyright file="WebsiteMissionActionsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Checks the second ownership boundary and that offline reads never become reward claims.</summary>
[TestFixture]
[NonParallelizable]
public class WebsiteMissionActionsTests
{
    private WebsiteMissionActions _actions = null!;
    private WebsiteMissionSubject _subject = null!;
    private MissionsPlugIn _missions = null!;
    private Account _account = null!;

    /// <summary>Creates a persisted owner and an enabled mission engine without any live players.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        using var setup = persistence.CreateNewContext();
        var configuration = setup.CreateNew<GameConfiguration>();
        await setup.SaveChangesAsync().ConfigureAwait(false);
        using var players = persistence.CreateNewPlayerContext(configuration);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        var character = players.CreateNew<Character>();
        character.Name = "AnnKnight";
        this._account.Characters.Add(character);
        await players.SaveChangesAsync().ConfigureAwait(false);
        this._subject = new(configuration.GetId(), this._account.GetId(), character.GetId(), "ann");
        this._missions = new MissionsPlugIn { Configuration = new MissionsConfiguration { FileName = string.Empty } };
        var manager = new PlugInManager(null, NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugInAtPlugInPoint<IPlayerStateChangedPlugIn>(this._missions);
        var context = new Mock<IGameContext>();
        context.SetupGet(c => c.Configuration).Returns(configuration);
        context.SetupGet(c => c.PersistenceContextProvider).Returns(persistence);
        context.SetupGet(c => c.PlugInManager).Returns(manager);
        context.Setup(c => c.GetPlayersAsync()).ReturnsAsync(new List<Player>());
        this._actions = new(context.Object);
    }

    /// <summary>The internal service independently rejects a different world, account or character.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RejectsUnownedSubjectsAsync()
    {
        Assert.That((await this._actions.ReadAsync(this._subject with { ConfigurationId = Guid.NewGuid() }, CancellationToken.None).ConfigureAwait(false)).Status, Is.EqualTo("wrong-world"));
        Assert.That((await this._actions.ReadAsync(this._subject with { AccountId = Guid.NewGuid() }, CancellationToken.None).ConfigureAwait(false)).Status, Is.EqualTo("missing"));
        Assert.That((await this._actions.ReadAsync(this._subject with { CharacterId = Guid.NewGuid() }, CancellationToken.None).ConfigureAwait(false)).Status, Is.EqualTo("missing"));
        this._account.State = AccountState.Banned;
        Assert.That((await this._actions.ReadAsync(this._subject, CancellationToken.None).ConfigureAwait(false)).Status, Is.EqualTo("missing"));
    }

    /// <summary>Offline viewing and refused claims leave actual progress and calendar state unchanged.</summary>
    /// <param name="action">The existing mission action.</param>
    /// <param name="argument">The selected mission or invitation code.</param>
    /// <returns>The test task.</returns>
    [TestCase("mission", "hunt")]
    [TestCase("streak", "")]
    [TestCase("calendar", "")]
    [TestCase("invite", "FRIEND")]
    public async Task ReadsOfflineButNeverClaimsOfflineAsync(string action, string argument)
    {
        var engine = this._missions.GetEngine();
        var before = System.Text.Json.JsonSerializer.Serialize(engine.Store.Data);
        var read = await this._actions.ReadAsync(this._subject, CancellationToken.None).ConfigureAwait(false);
        Assert.That(read.Status, Is.EqualTo("ok"));
        Assert.That(read.CharacterOnline, Is.False);
        Assert.That(read.BoardJson, Does.Contain("\"calendar\""));
        var claim = await this._actions.ClaimAsync(this._subject, action, argument, CancellationToken.None).ConfigureAwait(false);
        Assert.That(claim.Status, Is.EqualTo("offline"));
        Assert.That(System.Text.Json.JsonSerializer.Serialize(engine.Store.Data), Is.EqualTo(before));
    }

    /// <summary>Unknown administrative actions cannot be routed through this player channel.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RejectsUnknownActionsAsync()
    {
        var reply = await this._actions.ClaimAsync(this._subject, "grant", "1000", CancellationToken.None).ConfigureAwait(false);
        Assert.That(reply.Status, Is.EqualTo("invalid"));
    }
}

// <copyright file="WebsiteAccountMissionsControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.API;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Mission routing must stamp ownership from authenticated persistence, never from browser identifiers.</summary>
[TestFixture]
public class WebsiteAccountMissionsControllerTests
{
    private WebsiteAccountMissionsController _controller = null!;
    private Account _account = null!;
    private Character _character = null!;
    private GameConfiguration _configuration = null!;
    private Mock<IManageableServer> _server = null!;
    private IList<IManageableServer> _servers = null!;

    /// <summary>Creates an owned character and an authenticated internal server channel.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        this._configuration = setup.CreateNew<GameConfiguration>();
        await setup.SaveChangesAsync().ConfigureAwait(false);
        using var players = provider.CreateNewPlayerContext(this._configuration);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
        this._character = players.CreateNew<Character>();
        this._character.Name = "AnnKnight";
        this._account.Characters.Add(this._character);
        await players.SaveChangesAsync().ConfigureAwait(false);
        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);
        this._server = new Mock<IManageableServer>();
        this._server.SetupGet(s => s.Type).Returns(ServerType.GameServer);
        this._server.As<IWebsiteAccountGameServer>().Setup(s => s.ReadWebsiteMissionsAsync(It.IsAny<WebsiteMissionSubject>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WebsiteMissionsReply("ok", "{}", true));
        this._server.As<IWebsiteAccountGameServer>().Setup(s => s.ClaimWebsiteMissionsAsync(It.IsAny<WebsiteMissionSubject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WebsiteMissionsReply("ok", "{}", true));
        this._servers = new List<IManageableServer> { this._server.Object };
        var servers = new Mock<IServerProvider>();
        servers.SetupGet(s => s.Servers).Returns(this._servers);
        var login = new Mock<ILoginServer>();
        login.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte> { ["ann"] = 0 });
        this._controller = new WebsiteAccountMissionsController(provider, source.Object, servers.Object, login.Object, NullLogger<WebsiteAccountMissionsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    /// <summary>State requests ignore action fields and forward only server-stamped identifiers.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReadsOnlyOwnedStateAndNeverTurnsAReadIntoAClaimAsync()
    {
        Assert.That(await this._controller.StateAsync(new("ann", "secret", "AnnKnight", "calendar")).ConfigureAwait(false), Is.InstanceOf<OkObjectResult>());
        this._server.As<IWebsiteAccountGameServer>().Verify(s => s.ReadWebsiteMissionsAsync(
            It.Is<WebsiteMissionSubject>(v => v.AccountId == this._account.GetId() && v.CharacterId == this._character.GetId() && v.ConfigurationId == this._configuration.GetId() && v.LoginName == "ann"), It.IsAny<CancellationToken>()), Times.Once);
        this._server.As<IWebsiteAccountGameServer>().Verify(s => s.ClaimWebsiteMissionsAsync(It.IsAny<WebsiteMissionSubject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(this._controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
    }

    /// <summary>Wrong credentials and banned accounts never reach the game server.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RejectsUnauthorizedReadersAsync()
    {
        Assert.That(await this._controller.StateAsync(new("ann", "wrong", "AnnKnight")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        this._account.State = AccountState.Banned;
        Assert.That(await this._controller.StateAsync(new("ann", "secret", "AnnKnight")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        this._server.As<IWebsiteAccountGameServer>().Verify(s => s.ReadWebsiteMissionsAsync(It.IsAny<WebsiteMissionSubject>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A character name outside this account cannot be read or claimed.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RejectsOtherCharactersAsync()
    {
        Assert.That(await this._controller.ClaimAsync(new("ann", "secret", "BobKnight", "calendar")).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
        this._server.As<IWebsiteAccountGameServer>().Verify(s => s.ClaimWebsiteMissionsAsync(It.IsAny<WebsiteMissionSubject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Only explicit recognized actions are forwarded with the owned subject.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RoutesAnOwnedClaimAsync()
    {
        Assert.That(await this._controller.ClaimAsync(new("ann", "secret", "AnnKnight", "mission", "hunt")).ConfigureAwait(false), Is.InstanceOf<OkObjectResult>());
        this._server.As<IWebsiteAccountGameServer>().Verify(s => s.ClaimWebsiteMissionsAsync(It.Is<WebsiteMissionSubject>(v => v.AccountId == this._account.GetId() && v.CharacterId == this._character.GetId()), "mission", "hunt", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(await this._controller.ClaimAsync(new("ann", "secret", "AnnKnight", "admin")).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
    }

    /// <summary>Missing channels produce an availability result without losing authentication or leaking other worlds.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReportsAnUnavailableServerAsync()
    {
        this._servers.Clear();
        var result = (OkObjectResult)await this._controller.StateAsync(new("ann", "secret", "AnnKnight")).ConfigureAwait(false);
        Assert.That(((WebsiteMissionsReply)result.Value!).Status, Is.EqualTo("unavailable"));
    }
}

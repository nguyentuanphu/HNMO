// <copyright file="WebsiteGiftBoxControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.IO;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Checks own-account mail authentication, lease ownership, actual delivery and replay safety.</summary>
[TestFixture]
public class WebsiteGiftBoxControllerTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);
    private InMemoryPersistenceContextProvider _provider = null!;
    private Account _account = null!;
    private Character _character = null!;
    private Mock<ILoginServer> _login = null!;
    private WebsiteGiftBoxController _controller = null!;
    private GiftBoxDispatch _box = null!;
    private Guid _letter;

    /// <summary>Creates a real account and owed mail with isolated row persistence.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._provider = new();
        using var setup = this._provider.CreateNewContext();
        var game = setup.CreateNew<GameConfiguration>();
        game.MaximumInventoryMoney = 2000000000;
        await setup.SaveChangesAsync().ConfigureAwait(false);
        using var players = this._provider.CreateNewPlayerContext(game);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
        this._account.EMail = "private@example.test";
        this._character = players.CreateNew<Character>();
        this._character.Name = "Hero";
        this._character.Inventory = players.CreateNew<ItemStorage>();
        this._account.Characters.Add(this._character);
        await players.SaveChangesAsync().ConfigureAwait(false);
        var rows = new InMemoryHnmoPlayerStateRowBackend();
        this._box = new(rows);
        this._letter = this.Send(this._account.GetId());
        var configurations = new Mock<IDataSource<GameConfiguration>>();
        configurations.Setup(source => source.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        this._login = new();
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(true);
        this._login.Setup(login => login.ReleaseAccountReservationAsync("ann", It.IsAny<int>())).Returns(ValueTask.CompletedTask);
        this._controller = new(this._provider, configurations.Object, this._login.Object, NullLogger<WebsiteGiftBoxController>.Instance, rows, new FixedClock())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    /// <summary>The trusted operator bridge policy applies, and display fields cannot reveal account credentials.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task StateIsAuthorizedOwnedAndPrivateAsync()
    {
        var policy = typeof(WebsiteGiftBoxController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single();
        Assert.That(policy.Policy, Is.EqualTo(AdminPolicies.AuthenticatedOperator));
        Assert.That(policy.AuthenticationSchemes, Is.EqualTo(ApiKeyAuthenticationDefaults.ApiSchemes));
        var json = Content(await this._controller.StateAsync(this.Request()).ConfigureAwait(false));
        Assert.That(json.GetProperty("detail").GetProperty("title").GetString(), Is.EqualTo("Quà"));
        Assert.That(json.GetRawText(), Does.Not.Contain("private@example.test").And.Not.Contain(this._account.PasswordHash).And.Not.Contain("private-host").And.Not.Contain(this._account.GetId().ToString()));
        Assert.That(this._controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        this._login.Verify(login => login.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>The same letter grants its actual coins and Zen exactly once, with a private unique lease.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task CollectionAndReplayUseOneDeliveryAsync()
    {
        var first = Content(await this._controller.CollectAsync(this.Request()).ConfigureAwait(false));
        var again = Content(await this._controller.CollectAsync(this.Request()).ConfigureAwait(false));
        Assert.That(first.GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(again.GetProperty("status").GetString(), Is.EqualTo("AlreadyCollected"));
        Assert.That(this._account.WCoinC, Is.EqualTo(10));
        Assert.That(this._character.Inventory!.Money, Is.EqualTo(100));
        this._login.Verify(login => login.TryReserveAccountAsync("ann", It.Is<int>(id => id > byte.MaxValue)), Times.Exactly(2));
        this._login.Verify(login => login.ReleaseAccountReservationAsync("ann", It.Is<int>(id => id > byte.MaxValue)), Times.Exactly(2));
    }

    /// <summary>Wrong credentials, bans and bots never acquire a collection lease.</summary>
    /// <param name="mode">The rejected authentication case.</param>
    /// <returns>The test task.</returns>
    [TestCase("password")]
    [TestCase("banned")]
    [TestCase("bot")]
    public async Task AuthenticationPrecedesLeaseAsync(string mode)
    {
        this._account.State = mode == "banned" ? AccountState.Banned : AccountState.Normal;
        this._account.IsBot = mode == "bot";
        var request = mode == "password" ? this.Request() with { Password = "wrong" } : this.Request();
        Assert.That(await this._controller.CollectAsync(request).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        this._login.Verify(login => login.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        Assert.That(this._account.WCoinC, Is.Zero);
    }

    /// <summary>A password change during lease acquisition invalidates initial authorization.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task FreshAuthenticationUnderLeaseIsRequiredAsync()
    {
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>())).Callback(() => this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("changed", 4)).ReturnsAsync(true);
        Assert.That(await this._controller.CollectAsync(this.Request()).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That(this._account.WCoinC, Is.Zero);
        this._login.Verify(login => login.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);
    }

    /// <summary>An online account can read its mail but must collect using the game.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task OnlineAccountIsNotMutatedAsync()
    {
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(false);
        var json = Content(await this._controller.CollectAsync(this.Request()).ConfigureAwait(false));
        Assert.That(json.GetProperty("status").GetString(), Is.EqualTo("Online"));
        Assert.That(this._account.WCoinC, Is.Zero);
        Assert.That(this._character.Inventory!.Money, Is.Zero);
    }

    /// <summary>Foreign letters and character identifiers cannot redirect delivery.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task AccountAndCharacterOwnershipAreEnforcedAsync()
    {
        var foreign = this.Send(Guid.NewGuid());
        var otherMail = Content(await this._controller.CollectAsync(this.Request() with { LetterId = foreign }).ConfigureAwait(false));
        Assert.That(otherMail.GetProperty("status").GetString(), Is.EqualTo("NotFound"));
        var otherCharacter = Content(await this._controller.CollectAsync(this.Request() with { CharacterId = Guid.NewGuid() }).ConfigureAwait(false));
        Assert.That(otherCharacter.GetProperty("status").GetString(), Is.EqualTo("InvalidCharacter"));
        Assert.That(this._account.WCoinC, Is.Zero);
    }

    /// <summary>A lost lease response still releases only this request's unique reservation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LostLeaseResponseHasScopedCleanupAsync()
    {
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>())).ThrowsAsync(new IOException("injected lost response"));
        Assert.That(await this._controller.CollectAsync(this.Request()).ConfigureAwait(false), Is.InstanceOf<ObjectResult>().With.Property("StatusCode").EqualTo(503));
        this._login.Verify(login => login.ReleaseAccountReservationAsync("ann", It.Is<int>(id => id > byte.MaxValue)), Times.Once);
        Assert.That(this._account.WCoinC, Is.Zero);
    }

    private static JsonElement Content(IActionResult result) => JsonDocument.Parse(((ContentResult)result).Content!).RootElement.Clone();

    private WebsiteGiftBoxRequest Request() => new("ann", "secret", LetterId: this._letter, CharacterId: this._character.GetId(), Language: "vi");

    private Guid Send(Guid account)
    {
        var id = Guid.NewGuid();
        this._box.Send(id, "private-host", "private-reason", [account], "Gift", "Quà", "Thank you.", "Cảm ơn bạn.", new() { Coins = 10, Zen = 100, CoinType = CashShopCoinType.WCoinC }, Now);
        return id;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}

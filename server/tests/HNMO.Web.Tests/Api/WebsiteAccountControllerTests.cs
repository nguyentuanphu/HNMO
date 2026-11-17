// <copyright file="WebsiteAccountControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.CashShop;
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

/// <summary>Account isolation, bounded history and safe snapshots for the player dashboard.</summary>
[TestFixture]
public class WebsiteAccountControllerTests
{
    private WebsiteAccountController _controller = null!;
    private Account _account = null!;
    private IPersistenceContextProvider _persistence = null!;
    private GameConfiguration _configuration = null!;

    private Mock<ILoginServer> _login = null!;
    private Mock<IManageableServer> _server = null!;
    private IList<IManageableServer> _servers = null!;

    /// <summary>Creates two accounts whose grants must never be mixed.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        this._persistence = provider;
        using var setup = provider.CreateNewContext();
        var configuration = setup.CreateNew<GameConfiguration>();
        this._configuration = configuration;
        await setup.SaveChangesAsync().ConfigureAwait(false);
        using var players = provider.CreateNewPlayerContext(configuration);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
        this._account.EMail = "private@example.test";
        this._account.VaultPassword = "private-pin";
        this._account.WCoinC = 150;
        var character = players.CreateNew<Character>();
        character.Name = "AnnKnight";
        this._account.Characters.Add(character);
        for (var index = 0; index < 21; index++)
        {
            var grant = players.CreateNew<CashShopCoinGrant>();
            grant.AccountId = this._account.GetId();
            grant.Amount = index;
            grant.Reason = $"Own reward {index}";
            grant.CreatedAt = new DateTime(2026, 11, 16, 0, index, 0, DateTimeKind.Utc);
        }

        var other = players.CreateNew<Account>();
        other.LoginName = "bob";
        var otherGrant = players.CreateNew<CashShopCoinGrant>();
        otherGrant.AccountId = other.GetId();
        otherGrant.Reason = "Other account private reward";
        otherGrant.CreatedAt = DateTime.UtcNow;
        await players.SaveChangesAsync().ConfigureAwait(false);
        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(configuration);
        this._login = new Mock<ILoginServer>();
        this._login.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte> { ["ann"] = 0 });
        this._login.Setup(s => s.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);
        this._server = new Mock<IManageableServer>();
        this._server.SetupGet(s => s.Id).Returns(0);
        this._server.SetupGet(s => s.Type).Returns(ServerType.GameServer);
        this._server.As<IGameServerAdministration>();
        this._servers = new List<IManageableServer> { this._server.Object };
        var servers = new Mock<IServerProvider>();
        servers.SetupGet(s => s.Servers).Returns(this._servers);
        this._controller = new WebsiteAccountController(provider, source.Object, this._login.Object, servers.Object, NullLogger<WebsiteAccountController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    /// <summary>Wrong, missing and banned credentials all reveal no account data.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RejectsInvalidCredentialsAsync()
    {
        foreach (var request in new[] { new WebsiteAccountRequest("ann", "wrong"), new WebsiteAccountRequest("missing", "secret"), new WebsiteAccountRequest(null, null) })
        {
            Assert.That(await this._controller.OverviewAsync(request).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        }

        this._account.State = AccountState.Banned;
        Assert.That(await this._controller.OverviewAsync(new("ann", "secret")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
    }

    /// <summary>Pages contain only this account's grants, with no password, PIN or e-mail disclosure.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PagesHistoryWithoutLeakingOtherAccountsOrSecretsAsync()
    {
        var first = await this.ReadAsync(0).ConfigureAwait(false);
        Assert.That(first.GetProperty("history").GetArrayLength(), Is.EqualTo(20));
        Assert.That(first.GetProperty("hasMore").GetBoolean(), Is.True);
        Assert.That(first.GetProperty("accountOnline").GetBoolean(), Is.True);
        Assert.That(first.GetProperty("balances").GetProperty("wCoinC").GetInt32(), Is.EqualTo(150));
        Assert.That(first.GetRawText(), Does.Not.Contain("private").And.Not.Contain("secret").And.Not.Contain("Password").And.Not.Contain("Other account"));
        var second = await this.ReadAsync(1).ConfigureAwait(false);
        Assert.That(second.GetProperty("history").GetArrayLength(), Is.EqualTo(1));
        Assert.That(second.GetProperty("hasMore").GetBoolean(), Is.False);
        Assert.That(first.GetProperty("history")[0].GetProperty("createdAt").GetString(), Does.EndWith("Z"));
        Assert.That(this._controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
    }

    /// <summary>Unbounded reads are rejected before querying persistence.</summary>
    /// <param name="page">An invalid page.</param>
    /// <returns>The test task.</returns>
    [TestCase(-1)]
    [TestCase(107374182)]
    [TestCase(int.MaxValue)]
    public async Task RejectsUnboundedPagesAsync(int page)
        => Assert.That(await this._controller.OverviewAsync(new("ann", "secret", page)).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());

    /// <summary>Retained entries beyond the old thousand-entry ceiling remain accessible in bounded pages.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PagesBeyondOneThousandEntriesAsync()
    {
        using var context = this._persistence.CreateNewPlayerContext(this._configuration);
        for (var index = 0; index < 1000; index++)
        {
            var grant = context.CreateNew<CashShopCoinGrant>();
            grant.AccountId = this._account.GetId();
            grant.CreatedAt = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        var page = await this.ReadAsync(50).ConfigureAwait(false);
        Assert.That(page.GetProperty("history").GetArrayLength(), Is.EqualTo(20));
        Assert.That(page.GetProperty("hasMore").GetBoolean(), Is.True);
        var last = await this.ReadAsync(51).ConfigureAwait(false);
        Assert.That(last.GetProperty("history").GetArrayLength(), Is.EqualTo(1));
        Assert.That(last.GetProperty("history")[0].GetProperty("reason").GetString(), Is.EqualTo("Own reward 0"));
        Assert.That(last.GetProperty("hasMore").GetBoolean(), Is.False);
    }

    /// <summary>Purchase metadata is returned as data for bilingual rendering, without changing legacy reasons.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReturnsBilingualPurchaseMetadataAsync()
    {
        using var context = this._persistence.CreateNewPlayerContext(this._configuration);
        var package = context.CreateNew<CashShopPackage>();
        package.Name = new LocalizedString("en=Test package||vi=Gói thử nghiệm");
        CashShopPurchaseReceipt.Create(context, this._account, package, 10, "AnnMage");
        await context.SaveChangesAsync().ConfigureAwait(false);
        var json = await this.ReadAsync(0).ConfigureAwait(false);
        var row = json.GetProperty("history")[0];
        Assert.That(row.GetProperty("amount").GetInt32(), Is.EqualTo(-10));
        Assert.That(row.GetProperty("purchase").GetProperty("nameVi").GetString(), Is.EqualTo("Gói thử nghiệm"));
        Assert.That(row.GetProperty("purchase").GetProperty("giftRecipient").GetString(), Is.EqualTo("AnnMage"));
        Assert.That(row.GetProperty("pending").GetBoolean(), Is.False);
    }

    /// <summary>A valid player password alone cannot bypass the trusted bridge's API-key policy.</summary>
    [Test]
    public void RequiresTrustedBridgeAuthorization()
    {
        var policy = typeof(WebsiteAccountController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single();
        Assert.That(policy.Policy, Is.EqualTo(AdminPolicies.AuthenticatedOperator));
        Assert.That(policy.AuthenticationSchemes, Is.EqualTo(ApiKeyAuthenticationDefaults.ApiSchemes));
    }

    /// <summary>Live presence requires both the authenticated account and its exact persistent character identity.</summary>
    /// <param name="sameAccount">Whether the live result belongs to the account.</param>
    /// <param name="sameCharacter">Whether the live result has the character's identifier.</param>
    /// <returns>The test task.</returns>
    [TestCase(true, true)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task ScopesLivePresenceToOwnCharacterAsync(bool sameAccount, bool sameCharacter)
    {
        var character = this._account.Characters.Single();
        var live = new LivePlayerInfo(sameCharacter ? character.GetId() : Guid.NewGuid(), sameAccount ? "ann" : "bob", character.Name, 0, 1, 2, 300, 0, null, false);
        this._server.As<IGameServerAdministration>().Setup(s => s.GetPlayerAsync(character.Name, It.IsAny<CancellationToken>())).ReturnsAsync(live);
        var json = await this.ReadAsync(0).ConfigureAwait(false);
        Assert.That(json.GetProperty("characters")[0].GetProperty("online").ValueKind, Is.EqualTo(sameAccount && sameCharacter ? JsonValueKind.True : JsonValueKind.Null));
        Assert.That(json.ToString(), Does.Not.Contain("bob"));
    }

    /// <summary>An account outside the game confirms its characters are offline without live-server queries.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReportsOfflineCharactersWithoutQueryingAServerAsync()
    {
        this._login.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte>());
        var json = await this.ReadAsync(0).ConfigureAwait(false);
        Assert.That(json.GetProperty("characters")[0].GetProperty("online").GetBoolean(), Is.False);
        this._server.As<IGameServerAdministration>().Verify(s => s.GetPlayerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A missing server channel is shown as unknown instead of inventing offline status.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReportsUnknownPresenceWhenTheServerChannelIsMissingAsync()
    {
        this._servers.Clear();
        var json = await this.ReadAsync(0).ConfigureAwait(false);
        Assert.That(json.GetProperty("characters")[0].GetProperty("online").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    /// <summary>A failed live query leaves saved account data available and marks presence unknown.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task KeepsSavedDataWhenTheLiveServerFailsAsync()
    {
        this._server.As<IGameServerAdministration>().Setup(s => s.GetPlayerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("unavailable"));
        var json = await this.ReadAsync(0).ConfigureAwait(false);
        Assert.That(json.GetProperty("characters")[0].GetProperty("online").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(json.GetProperty("balances").GetProperty("wCoinC").GetInt32(), Is.EqualTo(150));
    }

    /// <summary>A valid password change hashes the replacement, exposes no secrets and releases its reservation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ChangesPasswordAndReleasesReservationAsync()
    {
        var result = await this._controller.ChangePasswordAsync(new("ann", "secret", "replacement")).ConfigureAwait(false);
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(BCrypt.Net.BCrypt.Verify("replacement", this._account.PasswordHash), Is.True);
        Assert.That(BCrypt.Net.BCrypt.Verify("secret", this._account.PasswordHash), Is.False);
        var response = JsonSerializer.Serialize(((OkObjectResult)result).Value);
        Assert.That(response, Does.Not.Contain("secret").And.Not.Contain("replacement").And.Not.Contain(this._account.PasswordHash));
        Assert.That(this._controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        this._login.Verify(s => s.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);
    }

    /// <summary>A live or already reserved account cannot have its saved password overwritten.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RefusesPasswordChangeWhenAccountIsOnlineAsync()
    {
        this._login.Setup(s => s.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(false);
        Assert.That(await this._controller.ChangePasswordAsync(new("ann", "secret", "replacement")).ConfigureAwait(false), Is.InstanceOf<ConflictObjectResult>());
        Assert.That(BCrypt.Net.BCrypt.Verify("secret", this._account.PasswordHash), Is.True);
        this._login.Verify(s => s.ReleaseAccountReservationAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>Wrong, missing, banned and automated accounts cannot enter the password-change reservation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RejectsUnauthorizedPasswordChangesAsync()
    {
        Assert.That(await this._controller.ChangePasswordAsync(new("ann", "wrong", "replacement")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That(await this._controller.ChangePasswordAsync(new("missing", "secret", "replacement")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That(await this._controller.ChangePasswordAsync(new(null, null, "replacement")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        this._account.State = AccountState.Banned;
        Assert.That(await this._controller.ChangePasswordAsync(new("ann", "secret", "replacement")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        this._account.State = AccountState.Normal;
        this._account.IsBot = true;
        Assert.That(await this._controller.ChangePasswordAsync(new("ann", "secret", "replacement")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That(BCrypt.Net.BCrypt.Verify("secret", this._account.PasswordHash), Is.True);
        this._login.Verify(s => s.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>Authentication is checked again after reservation, rejecting credentials invalidated by a competing change.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RejectsCredentialsChangedBeforeReservationAsync()
    {
        this._login.Setup(s => s.TryReserveAccountAsync("ann", It.IsAny<int>()))
            .Callback(() => this._account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("other-change", 4))
            .ReturnsAsync(true);
        Assert.That(await this._controller.ChangePasswordAsync(new("ann", "secret", "replacement")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That(BCrypt.Net.BCrypt.Verify("other-change", this._account.PasswordHash), Is.True);
        this._login.Verify(s => s.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);
    }

    /// <summary>Invalid replacement values cannot mutate or reserve an account.</summary>
    /// <param name="password">An invalid new password.</param>
    /// <returns>The test task.</returns>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("ab")]
    [TestCase("abcdefghijklmnopqrstu")]
    [TestCase("mật-khẩu")]
    [TestCase("line\nbreak")]
    public async Task RejectsInvalidNewPasswordsAsync(string? password)
    {
        Assert.That(await this._controller.ChangePasswordAsync(new("ann", "secret", password)).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(BCrypt.Net.BCrypt.Verify("secret", this._account.PasswordHash), Is.True);
        this._login.Verify(s => s.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    private async ValueTask<JsonElement> ReadAsync(int page)
    {
        var result = (OkObjectResult)await this._controller.OverviewAsync(new("ann", "secret", page)).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}

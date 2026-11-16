// <copyright file="WebsiteAccountControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.Persistence.InMemory;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

/// <summary>Account isolation, bounded history and safe snapshots for the player dashboard.</summary>
[TestFixture]
public class WebsiteAccountControllerTests
{
    private WebsiteAccountController _controller = null!;
    private Account _account = null!;

    /// <summary>Creates two accounts whose grants must never be mixed.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var configuration = setup.CreateNew<GameConfiguration>();
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
        var login = new Mock<ILoginServer>();
        login.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte> { ["ann"] = 0 });
        this._controller = new WebsiteAccountController(provider, source.Object, login.Object)
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
    [TestCase(50)]
    [TestCase(int.MaxValue)]
    public async Task RejectsUnboundedPagesAsync(int page)
        => Assert.That(await this._controller.OverviewAsync(new("ann", "secret", page)).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());

    /// <summary>A valid player password alone cannot bypass the trusted bridge's API-key policy.</summary>
    [Test]
    public void RequiresTrustedBridgeAuthorization()
    {
        var policy = typeof(WebsiteAccountController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single();
        Assert.That(policy.Policy, Is.EqualTo(AdminPolicies.AuthenticatedOperator));
        Assert.That(policy.AuthenticationSchemes, Is.EqualTo(ApiKeyAuthenticationDefaults.ApiSchemes));
    }

    private async ValueTask<JsonElement> ReadAsync(int page)
    {
        var result = (OkObjectResult)await this._controller.OverviewAsync(new("ann", "secret", page)).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}

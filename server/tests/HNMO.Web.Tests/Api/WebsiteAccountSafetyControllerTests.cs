// <copyright file="WebsiteAccountSafetyControllerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Api;

using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Tests for the <see cref="WebsiteAccountSafetyController"/>: the account page's safety panel checks the password
/// (and records the website login with the address), sets, changes and clears the second password with the old one
/// and its wrong-try limit, and locks and unlocks the account's items with the second password and the wait.
/// </summary>
[TestFixture]
[NonParallelizable]
public class WebsiteAccountSafetyControllerTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _configuration = null!;
    private WebsiteAccountSafetyController _controller = null!;
    private Mock<ILoginServer> _login = null!;
    private string _file = null!;
    private Guid _itemId;

    /// <summary>
    /// Sets up an account with a character holding a sword, the plug-in's settings and an offline login server.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        AccountSafetyEngine.WorkFactor = 4;
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-safety-api-{Guid.NewGuid():N}.json");
        this._provider = new InMemoryPersistenceContextProvider();
        ItemDefinition sword;
        using (var setup = this._provider.CreateNewContext())
        {
            this._configuration = setup.CreateNew<GameConfiguration>();
            var plugin = setup.CreateNew<PlugInConfiguration>();
            plugin.TypeId = typeof(AccountSafetyPlugIn).GUID;
            plugin.IsActive = true;
            plugin.CustomConfiguration = JsonSerializer.Serialize(new AccountSafetyConfiguration { FileName = this._file, MaxWrongTries = 2, UnlockWaitHours = 24 });
            this._configuration.PlugInConfigurations.Add(plugin);
            sword = setup.CreateNew<ItemDefinition>();
            sword.Name = "Kris";
            sword.Width = 1;
            sword.Height = 2;
            this._configuration.Items.Add(sword);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

        using (var players = this._provider.CreateNewPlayerContext(this._configuration))
        {
            var account = players.CreateNew<Account>();
            account.LoginName = "ann";
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
            var character = players.CreateNew<Character>();
            character.Name = "AnnKnight";
            character.Inventory = players.CreateNew<ItemStorage>();
            var item = players.CreateNew<Item>();
            item.Definition = sword;
            item.Level = 7;
            item.ItemSlot = 12;
            character.Inventory.Items.Add(item);
            account.Characters.Add(character);
            this._itemId = item.GetId();
            await players.SaveChangesAsync().ConfigureAwait(false);
        }

        this._login = new Mock<ILoginServer>();
        this._login.Setup(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);
        var servers = new Mock<IServerProvider>();
        servers.Setup(s => s.Servers).Returns(new List<IManageableServer>());
        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);
        this._controller = new WebsiteAccountSafetyController(this._provider, source.Object, this._login.Object, servers.Object, Mock.Of<ILogger<WebsiteAccountSafetyController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    /// <summary>
    /// Deletes the file.
    /// </summary>
    [TearDown]
    public void TearDown() => File.Delete(this._file);

    /// <summary>
    /// A wrong password gets nothing; the right one shows the items, the options and the login history with the
    /// address masked, a wrong one after it is in the history too.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheAccountShowsItsLoginsAndItemsAsync()
    {
        Assert.That(await this._controller.AccountAsync(new SafetyLoginRequest("ann", "wrong", "203.0.113.7")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        await this.AccountAsync().ConfigureAwait(false);
        Assert.That(await this._controller.AccountAsync(new SafetyLoginRequest("ann", "wrong", "198.51.100.1")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        var json = await this.AccountAsync().ConfigureAwait(false);

        Assert.That(json.GetProperty("hasPin").GetBoolean(), Is.False);
        var item = json.GetProperty("items")[0];
        Assert.That((item.GetProperty("name").GetString(), item.GetProperty("character").GetString(), item.GetProperty("place").GetString(), item.GetProperty("locked").GetBoolean()), Is.EqualTo(("Kris +7", "AnnKnight", "bag", false)));
        var logins = json.GetProperty("logins");
        Assert.That(logins.GetArrayLength(), Is.EqualTo(3), "the first wrong one came before the store knew the account");
        Assert.That((logins[0].GetProperty("address").GetString(), logins[0].GetProperty("source").GetString(), logins[0].GetProperty("success").GetBoolean()), Is.EqualTo(("203.0.x.x", "website", true)));
        Assert.That((logins[1].GetProperty("address").GetString(), logins[1].GetProperty("success").GetBoolean()), Is.EqualTo(("198.51.x.x", false)));
        Assert.That(json.GetProperty("options").GetProperty("waitHours").GetInt32(), Is.EqualTo(24));
    }

    /// <summary>
    /// The second password is set (4 digits), changed only with the old one, wrong old ones lock out, and it's cleared.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheSecondPasswordIsSetChangedAndClearedAsync()
    {
        Assert.That((await this.PinAsync(null, "12").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("invalid"));
        var set = await this.PinAsync(null, "1234").ConfigureAwait(false);
        Assert.That((set.GetProperty("result").GetString(), set.GetProperty("hasPin").GetBoolean()), Is.EqualTo(("ok", true)));
        var stored = (await this.AccountEntityAsync().ConfigureAwait(false)).VaultPassword;
        Assert.That(AccountSafetyEngine.PinMatches(stored, "1234"), Is.True, "kept hashed in the vault password");
        Assert.That(stored, Is.Not.EqualTo("1234"));
        this._login.Verify(l => l.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);

        Assert.That((await this.PinAsync("1111", "5678").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("wrong"));
        Assert.That((await this.PinAsync("1111", "5678").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("lockedOut"));
        Assert.That((await this.PinAsync("1234", "5678").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("lockedOut"), "even the right one during the lockout");
        AccountSafetyStore.Open(this._file).Data.Accounts["ann"].LockedOutUntilUtc = null;

        Assert.That((await this.PinAsync("1234", "5678").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("ok"));
        Assert.That(AccountSafetyEngine.PinMatches((await this.AccountEntityAsync().ConfigureAwait(false)).VaultPassword, "5678"), Is.True);
        var cleared = await this.PinAsync("5678", string.Empty).ConfigureAwait(false);
        Assert.That((cleared.GetProperty("result").GetString(), cleared.GetProperty("hasPin").GetBoolean()), Is.EqualTo(("ok", false)));
        Assert.That((await this.AccountEntityAsync().ConfigureAwait(false)).VaultPassword, Is.Empty);
    }

    /// <summary>
    /// While the login server holds the account elsewhere, the second password isn't changed.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AnAccountOnAnotherServerKeepsItsPinAsync()
    {
        this._login.Setup(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(false);
        Assert.That((await this.PinAsync(null, "1234").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("online"));
        Assert.That((await this.AccountEntityAsync().ConfigureAwait(false)).VaultPassword, Is.Empty);
    }

    /// <summary>
    /// An item is locked; its unlock needs the second password and waits; another account's item id isn't found.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemsAreLockedAndUnlockedAsync()
    {
        await this.PinAsync(null, "4321").ConfigureAwait(false);
        var locked = await this.ItemAsync(this._itemId, false, null).ConfigureAwait(false);
        Assert.That(locked.GetProperty("result").GetString(), Is.EqualTo(nameof(ItemLockResult.Locked)));
        Assert.That(locked.GetProperty("items")[0].GetProperty("locked").GetBoolean(), Is.True);
        Assert.That((await this.ItemAsync(Guid.NewGuid(), false, null).ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("noItem"));

        Assert.That((await this.ItemAsync(this._itemId, true, "0000").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("wrong"));
        var unlock = await this.ItemAsync(this._itemId, true, "4321").ConfigureAwait(false);
        Assert.That(unlock.GetProperty("result").GetString(), Is.EqualTo(nameof(ItemLockResult.UnlockStarted)));
        Assert.That(unlock.GetProperty("items")[0].GetProperty("unlockAt").GetString(), Is.Not.Null);
        Assert.That(unlock.GetProperty("items")[0].GetProperty("locked").GetBoolean(), Is.True, "it waits");
    }

    private async Task<JsonElement> AccountAsync() =>
        Parse(await this._controller.AccountAsync(new SafetyLoginRequest("ann", "secret", "203.0.113.7")).ConfigureAwait(false));

    private async Task<JsonElement> PinAsync(string? oldPin, string newPin) =>
        Parse(await this._controller.PinAsync(new SafetyPinRequest("ann", "secret", oldPin, newPin, "203.0.113.7")).ConfigureAwait(false));

    private async Task<JsonElement> ItemAsync(Guid id, bool unlock, string? pin) =>
        Parse(await this._controller.ItemAsync(new SafetyItemRequest("ann", "secret", id, unlock, pin, "203.0.113.7")).ConfigureAwait(false));

    private static JsonElement Parse(IActionResult result) => JsonDocument.Parse(((ContentResult)result).Content!).RootElement;

    private async Task<Account> AccountEntityAsync()
    {
        using var context = this._provider.CreateNewPlayerContext(this._configuration);
        return (await context.GetAccountByLoginNameAsync("ann", default).ConfigureAwait(false))!;
    }
}

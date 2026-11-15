// <copyright file="WebsiteGiftCodesControllerTests.cs" company="HNMO">
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
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Tests for the <see cref="WebsiteGiftCodesController"/>: the public codes, and the account page's characters and
/// redemptions, which check the account's password.
/// </summary>
[TestFixture]
[NonParallelizable]
public class WebsiteGiftCodesControllerTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private PlugInConfiguration _plugin = null!;
    private GameConfiguration _configuration = null!;
    private WebsiteGiftCodesController _controller = null!;
    private string _file = null!;
    private GiftCodeStore _store = null!;

    /// <summary>
    /// Sets up an account with a character, the plug-in's settings and two codes.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-gift-codes-api-{Guid.NewGuid():N}.json");
        this._store = GiftCodeStore.Open(this._file);
        lock (this._store.Lock)
        {
            this._store.Data.Codes.Add(new GiftCodeDefinition { Id = "w", Code = "WELCOME", Public = true, Note = "Welcome", Reward = new GiftCodeReward { Coins = 50, Zen = 1000 } });
            this._store.Data.Codes.Add(new GiftCodeDefinition { Id = "s", Code = "SECRET", Reward = new GiftCodeReward { Coins = 5 } });
            this._store.CodesChanged();
        }

        this._provider = new InMemoryPersistenceContextProvider();
        using (var setup = this._provider.CreateNewContext())
        {
            this._configuration = setup.CreateNew<GameConfiguration>();
            this._plugin = setup.CreateNew<PlugInConfiguration>();
            this._plugin.TypeId = typeof(GiftCodesPlugIn).GUID;
            this._plugin.IsActive = true;
            this._plugin.CustomConfiguration = JsonSerializer.Serialize(new GiftCodesConfiguration { FileName = this._file });
            this._configuration.PlugInConfigurations.Add(this._plugin);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

        using (var players = this._provider.CreateNewPlayerContext(this._configuration))
        {
            var account = players.CreateNew<Account>();
            account.LoginName = "ann";
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
            var character = players.CreateNew<Character>();
            character.Name = "AnnKnight";
            account.Characters.Add(character);
            await players.SaveChangesAsync().ConfigureAwait(false);
        }

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);
        this._controller = new WebsiteGiftCodesController(this._provider, source.Object, Mock.Of<ILogger<WebsiteGiftCodesController>>())
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
    /// The public codes are listed; the others aren't.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PublicCodesAreListedAsync()
    {
        var json = ((ContentResult)await this._controller.GetAsync().ConfigureAwait(false)).Content!;
        Assert.That(json, Does.Contain("\"code\":\"WELCOME\"").And.Not.Contain("SECRET"));

        this._plugin.IsActive = false;
        json = ((ContentResult)await this._controller.GetAsync().ConfigureAwait(false)).Content!;
        Assert.That(json, Is.EqualTo("{\"v\":1,\"enabled\":false}"));
    }

    /// <summary>
    /// A wrong password gets nothing; the right one lists the characters and enters a code once: the coins are granted
    /// with the redemption's reference and the zen waits for the character.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheAccountEntersACodeOnceAsync()
    {
        Assert.That(await this._controller.CharactersAsync(new GiftCodeAccountRequest("ann", "wrong")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That(await this._controller.RedeemAsync(new GiftCodeRedeemRequest("ann", "wrong", "AnnKnight", "welcome")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());

        var characters = (OkObjectResult)await this._controller.CharactersAsync(new GiftCodeAccountRequest("ann", "secret")).ConfigureAwait(false);
        Assert.That(JsonSerializer.Serialize(characters.Value), Does.Contain("AnnKnight"));
        Assert.That(await this._controller.RedeemAsync(new GiftCodeRedeemRequest("ann", "secret", "Nobody", "welcome")).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());

        var answer = JsonDocument.Parse(((ContentResult)await this._controller.RedeemAsync(new GiftCodeRedeemRequest("ann", "secret", "AnnKnight", " welcome ")).ConfigureAwait(false)).Content!).RootElement;
        Assert.That(answer.GetProperty("result").GetString(), Is.EqualTo("Ok"));
        Assert.That(answer.GetProperty("reward").GetProperty("coins").GetInt32(), Is.EqualTo(50));

        using (var players = this._provider.CreateNewPlayerContext(this._configuration))
        {
            var grant = await players.GetCashShopCoinGrantByReferenceAsync("giftcode:w:a:ann").ConfigureAwait(false);
            Assert.That(grant?.Amount, Is.EqualTo(50));
            Assert.That(grant?.GrantedBy, Is.EqualTo(GiftCodeEngine.GrantedBy));
        }

        var engine = new GiftCodeEngine(new GiftCodesConfiguration(), this._store);
        Assert.That(engine.PendingCoins(), Is.Empty, "The coins were given.");
        Assert.That(engine.LogOf("ann", 5).Single().Source, Is.EqualTo("website"));
        lock (this._store.Lock)
        {
            Assert.That(this._store.Data.Owed.Values.Single().Single().Zen, Is.EqualTo(1000), "The zen waits for the character in the game.");
        }

        var again = JsonDocument.Parse(((ContentResult)await this._controller.RedeemAsync(new GiftCodeRedeemRequest("ann", "secret", "AnnKnight", "WELCOME")).ConfigureAwait(false)).Content!).RootElement;
        Assert.That(again.GetProperty("result").GetString(), Is.EqualTo("AlreadyUsedAccount"));
        var wrong = JsonDocument.Parse(((ContentResult)await this._controller.RedeemAsync(new GiftCodeRedeemRequest("ann", "secret", "AnnKnight", "NOPE")).ConfigureAwait(false)).Content!).RootElement;
        Assert.That((wrong.GetProperty("result").GetString(), wrong.GetProperty("triesLeft").GetInt32()), Is.EqualTo(("Invalid", 4)));
    }

    /// <summary>
    /// When the website may not enter codes, the account page's calls are refused.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheWebsiteCanBeSwitchedOffAsync()
    {
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new GiftCodesConfiguration { FileName = this._file, AllowWebsite = false });
        var result = await this._controller.RedeemAsync(new GiftCodeRedeemRequest("ann", "secret", "AnnKnight", "welcome")).ConfigureAwait(false);
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }
}

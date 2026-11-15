// <copyright file="WebsiteResetsControllerTests.cs" company="HNMO">
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
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Tests for the <see cref="WebsiteResetsController"/>: the public rankings, an account's characters, and the reset,
/// relife, stat points and delegated reset switch of the account page, which check the password and change a
/// character only while the account is offline.
/// </summary>
[TestFixture]
[NonParallelizable]
public class WebsiteResetsControllerTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private PlugInConfiguration _plugin = null!;
    private GameConfiguration _configuration = null!;
    private WebsiteResetsController _controller = null!;
    private Mock<ILoginServer> _login = null!;
    private Dictionary<string, byte> _online = null!;
    private string _file = null!;

    /// <summary>
    /// Sets up an account with a level 400 character, the plug-in's settings and an offline login server.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-resets-api-{Guid.NewGuid():N}.json");
        this._provider = new InMemoryPersistenceContextProvider();
        using (var setup = this._provider.CreateNewContext())
        {
            this._configuration = setup.CreateNew<GameConfiguration>();
            this._plugin = setup.CreateNew<PlugInConfiguration>();
            this._plugin.TypeId = typeof(ResetOptionsPlugIn).GUID;
            this._plugin.IsActive = true;
            this._plugin.CustomConfiguration = JsonSerializer.Serialize(new ResetOptionsConfiguration
            {
                FileName = this._file,
                Tiers = [new ResetTier { FromResets = 0, ToResets = -1, RequiredLevel = 400, LevelAfterReset = 1, Zen = 1_000, Points = 500, ClearStats = true, DailyLimit = 1 }],
                Website = new WebsiteResetOptions { ClearStatsZen = 100 },
            });
            this._configuration.PlugInConfigurations.Add(this._plugin);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

        using (var players = this._provider.CreateNewPlayerContext(this._configuration))
        {
            var characterClass = players.CreateNew<CharacterClass>();
            characterClass.Number = 0;
            characterClass.StatAttributes.Add(players.CreateNew<StatAttributeDefinition>(Stats.BaseStrength, 18f, true));
            characterClass.StatAttributes.Add(players.CreateNew<StatAttributeDefinition>(Stats.BaseEnergy, 30f, true));
            var account = players.CreateNew<Account>();
            account.LoginName = "ann";
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret", 4);
            var character = players.CreateNew<Character>();
            character.Name = "AnnWizard";
            character.CharacterClass = characterClass;
            character.LevelUpPoints = 20;
            character.Inventory = players.CreateNew<ItemStorage>();
            character.Inventory.Money = 5_000;
            character.Attributes.Add(players.CreateNew<StatAttribute>(Stats.Level, 400f));
            character.Attributes.Add(players.CreateNew<StatAttribute>(Stats.Resets, 0f));
            character.Attributes.Add(players.CreateNew<StatAttribute>(Stats.BaseStrength, 118f));
            character.Attributes.Add(players.CreateNew<StatAttribute>(Stats.BaseEnergy, 30f));
            account.Characters.Add(character);
            await players.SaveChangesAsync().ConfigureAwait(false);
        }

        this._online = new Dictionary<string, byte>();
        this._login = new Mock<ILoginServer>();
        this._login.Setup(l => l.GetSnapshotAsync()).ReturnsAsync(() => new Dictionary<string, byte>(this._online));
        this._login.Setup(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(() => this._online.Count == 0);
        var servers = new Mock<IServerProvider>();
        servers.Setup(s => s.Servers).Returns(new List<IManageableServer>());
        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);
        this._controller = new WebsiteResetsController(this._provider, source.Object, this._login.Object, servers.Object, Mock.Of<ILogger<WebsiteResetsController>>())
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
    /// A wrong password gets nothing; the right one lists the characters with their resets, today's limit and stats.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheAccountListsItsCharactersAsync()
    {
        Assert.That(await this._controller.AccountAsync(new ResetAccountRequest("ann", "wrong")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        Assert.That(await this._controller.ActionAsync(new ResetActionRequest("ann", "wrong", "AnnWizard", "reset")).ConfigureAwait(false), Is.InstanceOf<UnauthorizedObjectResult>());
        var json = JsonDocument.Parse(((ContentResult)await this._controller.AccountAsync(new ResetAccountRequest("ann", "secret")).ConfigureAwait(false)).Content!).RootElement;
        var character = json.GetProperty("characters")[0];
        Assert.That(character.GetProperty("state").GetProperty("name").GetString(), Is.EqualTo("AnnWizard"));
        Assert.That(character.GetProperty("state").GetProperty("dailyLimit").GetInt32(), Is.EqualTo(1));
        Assert.That(character.GetProperty("state").GetProperty("next").GetProperty("result").GetString(), Is.EqualTo("Ok"));
        Assert.That(character.GetProperty("stats").GetProperty("str").GetInt32(), Is.EqualTo(118));
        Assert.That(json.TryGetProperty("busy", out _), Is.False, "Offline: no busy state.");
    }

    /// <summary>
    /// An offline character is reset on the website: it pays, its resets, level, stats and points change, the store
    /// remembers it; the daily limit stops the next one; the public ranking has it.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AnOfflineCharacterIsResetAsync()
    {
        var answer = await this.ActAsync("reset").ConfigureAwait(false);
        Assert.That(answer.GetProperty("result").GetString(), Is.EqualTo("Ok"));
        Assert.That(answer.GetProperty("character").GetProperty("state").GetProperty("resets").GetInt32(), Is.EqualTo(1));
        var character = await this.CharacterAsync().ConfigureAwait(false);
        Assert.That((Value(character, Stats.Resets), Value(character, Stats.Level), Value(character, Stats.BaseStrength)), Is.EqualTo((1, 1, 18)));
        Assert.That((character.LevelUpPoints, character.Inventory!.Money), Is.EqualTo((500, 4_000)));
        this._login.Verify(l => l.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);

        var store = ResetStore.Open(this._file);
        lock (store.Lock)
        {
            Assert.That(store.Data.Log.Single().Source, Is.EqualTo(ResetSource.Website));
        }

        await this.SetLevelAsync(400).ConfigureAwait(false);
        Assert.That((await this.ActAsync("reset").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("DailyLimit"));
        var ranking = JsonDocument.Parse(((ContentResult)await this._controller.GetAsync().ConfigureAwait(false)).Content!).RootElement;
        Assert.That(ranking.GetProperty("monthly")[0].GetProperty("name").GetString(), Is.EqualTo("AnnWizard"));
    }

    /// <summary>
    /// While the account is in the game nothing changes (the reset and the stats); the delegated reset switch works.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AnOnlineAccountChangesNothingAsync()
    {
        this._online["ann"] = 0;
        Assert.That((await this.ActAsync("reset").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("Online"));
        Assert.That((await this.ActAsync("add-stats", str: 5).ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("Online"));
        Assert.That((await this.ActAsync("clear-stats").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("Online"));
        var character = await this.CharacterAsync().ConfigureAwait(false);
        Assert.That((Value(character, Stats.Resets), Value(character, Stats.BaseStrength), character.LevelUpPoints), Is.EqualTo((0, 118, 20)));
        this._login.Verify(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);

        var on = await this.ActAsync("delegated-on").ConfigureAwait(false);
        Assert.That(on.GetProperty("result").GetString(), Is.EqualTo("Ok"));
        Assert.That(on.GetProperty("character").GetProperty("state").GetProperty("delegated").GetProperty("on").GetBoolean(), Is.True);
        Assert.That(on.GetProperty("character").GetProperty("busy").GetString(), Is.EqualTo("online"));
    }

    /// <summary>
    /// Free points are added to the class's stats while offline, not more than the free points; clearing gives them back
    /// for its zen.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StatPointsAreAddedAndClearedOfflineAsync()
    {
        Assert.That((await this.ActAsync("add-stats", str: 21).ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("NotEnoughPoints"));
        Assert.That((await this.ActAsync("add-stats", agi: 1).ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("NothingToAdd"), "Agility isn't a stat of this test class.");
        Assert.That((await this.ActAsync("add-stats", str: 5, ene: 15).ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("Ok"));
        var character = await this.CharacterAsync().ConfigureAwait(false);
        Assert.That((Value(character, Stats.BaseStrength), Value(character, Stats.BaseEnergy), character.LevelUpPoints), Is.EqualTo((123, 45, 0)));

        Assert.That((await this.ActAsync("clear-stats").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("Ok"));
        character = await this.CharacterAsync().ConfigureAwait(false);
        Assert.That((Value(character, Stats.BaseStrength), Value(character, Stats.BaseEnergy), character.LevelUpPoints, character.Inventory!.Money), Is.EqualTo((18, 30, 105 + 15, 4_900)));
    }

    /// <summary>
    /// The website's switches are honoured.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheWebsiteSwitchesCountAsync()
    {
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new ResetOptionsConfiguration { FileName = this._file, Website = new WebsiteResetOptions { AllowReset = false } });
        Assert.That((await this.ActAsync("reset").ConfigureAwait(false)).GetProperty("result").GetString(), Is.EqualTo("NotAllowed"));
        Assert.That(await this._controller.ActionAsync(new ResetActionRequest("ann", "secret", "AnnWizard", "explode")).ConfigureAwait(false), Is.InstanceOf<BadRequestObjectResult>());
        this._plugin.IsActive = false;
        Assert.That(((ObjectResult)await this._controller.ActionAsync(new ResetActionRequest("ann", "secret", "AnnWizard", "reset")).ConfigureAwait(false)).StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    private static int Value(Character character, AttributeDefinition attribute) =>
        (int)(character.Attributes.FirstOrDefault(a => a.Definition?.Id == attribute.Id)?.Value ?? -1);

    private async Task<JsonElement> ActAsync(string action, int? str = null, int? agi = null, int? ene = null)
    {
        var result = await this._controller.ActionAsync(new ResetActionRequest("ann", "secret", "AnnWizard", action, str, agi, null, ene)).ConfigureAwait(false);
        return JsonDocument.Parse(((ContentResult)result).Content!).RootElement;
    }

    private async Task<Character> CharacterAsync()
    {
        using var players = this._provider.CreateNewPlayerContext(this._configuration);
        var account = await players.GetAccountByLoginNameAsync("ann").ConfigureAwait(false);
        return account!.Characters.Single();
    }

    private async Task SetLevelAsync(int level)
    {
        using var players = this._provider.CreateNewPlayerContext(this._configuration);
        var account = await players.GetAccountByLoginNameAsync("ann").ConfigureAwait(false);
        account!.Characters.Single().Attributes.First(a => a.Definition?.Id == Stats.Level.Id).Value = level;
        await players.SaveChangesAsync().ConfigureAwait(false);
    }
}

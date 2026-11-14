// <copyright file="HnmoSkillServiceTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Text.Json.Nodes;
using System.Globalization;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Administration;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests the logic of the admin panel's "HNMO skills" page (<see cref="HnmoSkillService"/>) and of setting and
/// delivering cash shop coins (<see cref="CashShopCoinService.SetBalanceAsync"/>, <see cref="CashShopCoinDelivery"/>).
/// </summary>
[TestFixture]
public class HnmoSkillServiceTests
{
    private InMemoryPersistenceContextProvider _persistence = null!;
    private GameConfiguration _configuration = null!;
    private Mock<IDataSource<GameConfiguration>> _configurationSource = null!;
    private Mock<ILoginServer> _loginServer = null!;
    private Mock<IServerProvider> _serverProvider = null!;
    private Mock<IGameServerAdministration> _live = null!;
    private Dictionary<string, byte> _online = null!;
    private HnmoSkillService _service = null!;

    /// <summary>
    /// Sets up a configuration with the four skills of the dark knight and one of the dark wizard, and an account
    /// "knight" with the character "Hero".
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._persistence = new InMemoryPersistenceContextProvider();
        this._configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var knight = new HNMO.Persistence.BasicModel.CharacterClass { Id = Guid.NewGuid(), Number = 0, Name = "Dark Knight" };
        var wizard = new HNMO.Persistence.BasicModel.CharacterClass { Id = Guid.NewGuid(), Number = 8, Name = "Dark Wizard" };
        this._configuration.CharacterClasses.Add(knight);
        this._configuration.CharacterClasses.Add(wizard);
        AddSkill(700, "Earth Splitter", knight);
        AddSkill(701, "Blood Slash", knight);
        AddSkill(702, "Iron Guard", knight);
        AddSkill(703, "Charge", knight);
        AddSkill(704, "Ice Dragon", wizard);
        AddSkill(41, "Twisting Slash", knight);

        this._configurationSource = new Mock<IDataSource<GameConfiguration>>();
        this._configurationSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);

        this._online = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        this._loginServer = new Mock<ILoginServer>();
        this._loginServer.Setup(l => l.GetSnapshotAsync()).ReturnsAsync(() => new Dictionary<string, byte>(this._online));
        this._loginServer.Setup(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);

        var server = new Mock<IGameServer>();
        server.SetupGet(s => s.Id).Returns(3);
        server.SetupGet(s => s.ServerState).Returns(ServerState.Started);
        this._live = server.As<IGameServerAdministration>();
        this._serverProvider = new Mock<IServerProvider>();
        this._serverProvider.SetupGet(p => p.Servers).Returns([server.Object]);

        this._service = new HnmoSkillService(this._persistence, this._configurationSource.Object, this._loginServer.Object, this._serverProvider.Object);

        using var context = this._persistence.CreateNewPlayerContext(this._configuration);
        var account = context.CreateNew<Account>();
        account.LoginName = "knight";
        var character = context.CreateNew<Character>();
        character.Name = "Hero";
        character.CharacterClass = knight;
        account.Characters.Add(character);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The page lists the characters of the account with the HNMO skills of their class only.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ListsTheSkillsOfTheClassAsync()
    {
        var characters = await this._service.GetCharactersAsync("knight").ConfigureAwait(false);

        Assert.That(characters, Has.Count.EqualTo(1));
        var hero = characters![0];
        Assert.That(hero.CharacterName, Is.EqualTo("Hero"));
        Assert.That(hero.Online, Is.False);
        Assert.That(hero.Skills.Select(s => s.Number), Is.EqualTo(new[] { 700, 701, 702, 703 }));
        Assert.That(hero.Skills.Select(s => s.Letter), Is.EqualTo(new[] { 'A', 'B', 'C', 'D' }));
        Assert.That(hero.Skills.Select(s => s.Learned), Is.All.False);
        Assert.That(await this._service.GetCharactersAsync("nobody").ConfigureAwait(false), Is.Null);
    }

    /// <summary>
    /// An offline character gets the D skill in the database, while its account is reserved at the login server;
    /// granting it again changes nothing, and removing takes it away.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OfflineCharacterIsChangedInTheDatabaseAsync()
    {
        var granted = await this._service.SetSkillAsync("Hero", 703, true).ConfigureAwait(false);

        Assert.That(granted, Is.EqualTo(new HnmoSkillChangeResult(HnmoSkillChangeStatus.Learned, false)));
        Assert.That(await this.GetStoredSkillsAsync().ConfigureAwait(false), Is.EqualTo(new[] { 703 }));
        this._loginServer.Verify(l => l.TryReserveAccountAsync("knight", It.IsAny<int>()), Times.Once);
        this._loginServer.Verify(l => l.ReleaseAccountReservationAsync("knight", It.IsAny<int>()), Times.Once);
        var listed = await this._service.GetCharactersAsync("knight").ConfigureAwait(false);
        Assert.That(listed![0].Skills.Single(s => s.Number == 703).Learned, Is.True);

        var again = await this._service.SetSkillAsync("Hero", 703, true).ConfigureAwait(false);
        Assert.That(again.Status, Is.EqualTo(HnmoSkillChangeStatus.Unchanged));
        Assert.That(await this.GetStoredSkillsAsync().ConfigureAwait(false), Is.EqualTo(new[] { 703 }));

        var removed = await this._service.SetSkillAsync("Hero", 703, false).ConfigureAwait(false);
        Assert.That(removed.Status, Is.EqualTo(HnmoSkillChangeStatus.Removed));
        Assert.That(await this.GetStoredSkillsAsync().ConfigureAwait(false), Is.Empty);
        this._live.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Skills of another class, skills which aren't HNMO skills and unknown characters are refused without a change.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task InvalidRequestsAreRefusedAsync()
    {
        Assert.That((await this._service.SetSkillAsync("Hero", 704, true).ConfigureAwait(false)).Status, Is.EqualTo(HnmoSkillChangeStatus.SkillNotOfClass));
        Assert.That((await this._service.SetSkillAsync("Hero", 41, true).ConfigureAwait(false)).Status, Is.EqualTo(HnmoSkillChangeStatus.SkillNotOfClass));
        Assert.That((await this._service.SetSkillAsync("Nobody", 703, true).ConfigureAwait(false)).Status, Is.EqualTo(HnmoSkillChangeStatus.CharacterNotFound));
        Assert.That(await this.GetStoredSkillsAsync().ConfigureAwait(false), Is.Empty);
        this._loginServer.Verify(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// When the login server refuses the reservation (e.g. the account is logging in), nothing changes.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task BusyAccountIsNotChangedAsync()
    {
        this._loginServer.Setup(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(false);

        var result = await this._service.SetSkillAsync("Hero", 703, true).ConfigureAwait(false);

        Assert.That(result.Status, Is.EqualTo(HnmoSkillChangeStatus.AccountBusy));
        Assert.That(await this.GetStoredSkillsAsync().ConfigureAwait(false), Is.Empty);
    }

    /// <summary>
    /// An online account is changed by its game server, through the live operation, and not in the database.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OnlineCharacterIsChangedByItsGameServerAsync()
    {
        this._online["knight"] = 3;
        var plugInId = typeof(HnmoSkillsAdminOperationPlugIn).GUID;
        var preview = new JsonObject { ["learnedBefore"] = false };
        this._live.Setup(l => l.PreviewAdminOperationAsync(plugInId, HnmoSkillsAdminOperationPlugIn.OperationName, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        this._live.Setup(l => l.ExecuteAdminOperationAsync(plugInId, HnmoSkillsAdminOperationPlugIn.OperationName, It.IsAny<string>(), preview, It.IsAny<CancellationToken>())).ReturnsAsync(new JsonObject { ["changed"] = true });

        var result = await this._service.SetSkillAsync("Hero", 703, true).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(new HnmoSkillChangeResult(HnmoSkillChangeStatus.Learned, true)));
        this._live.Verify(
            l => l.ExecuteAdminOperationAsync(plugInId, HnmoSkillsAdminOperationPlugIn.OperationName, It.Is<string>(a => IsRequest(a, "Hero", 703, true)), preview, It.IsAny<CancellationToken>()),
            Times.Once);
        this._loginServer.Verify(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        Assert.That(await this.GetStoredSkillsAsync().ConfigureAwait(false), Is.Empty, "the game server saves it");
        Assert.That((await this._service.GetCharactersAsync("knight").ConfigureAwait(false))![0].Online, Is.True);
    }

    /// <summary>
    /// When the account is online but the game server refuses (e.g. in the character selection), nothing changes.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OnlineCharacterWhichIsNotInTheGameIsNotChangedAsync()
    {
        this._online["knight"] = 3;
        this._live.Setup(l => l.PreviewAdminOperationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("The character is not in the game on this server."));

        var result = await this._service.SetSkillAsync("Hero", 703, true).ConfigureAwait(false);

        Assert.That(result.Status, Is.EqualTo(HnmoSkillChangeStatus.NotInGame));
        Assert.That(result.Online, Is.True);
        Assert.That(await this.GetStoredSkillsAsync().ConfigureAwait(false), Is.Empty);
        this._loginServer.Verify(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// The page says what changed and whether the player sees it now or at the next login.
    /// </summary>
    [Test]
    public void ResultTextSaysWhatChanged()
    {
        var online = HnmoSkills.Describe(new HnmoSkillChangeResult(HnmoSkillChangeStatus.Learned, true), "Hero", "Charge (703)");
        Assert.That(online, Is.EqualTo((true, "Hero now has the skill Charge (703). The player sees it in the game now.")));
        var offline = HnmoSkills.Describe(new HnmoSkillChangeResult(HnmoSkillChangeStatus.Removed, false), "Hero", "Charge (703)");
        Assert.That(offline.Success, Is.True);
        Assert.That(offline.Text, Does.Contain("removed").And.Contain("next login"));
        Assert.That(HnmoSkills.Describe(new HnmoSkillChangeResult(HnmoSkillChangeStatus.AccountBusy, false), "Hero", "Charge").Success, Is.False);
    }

    /// <summary>
    /// The page shows the four skills of the character; "Grant" on the D skill teaches it and the page says so.
    /// </summary>
    [Test]
    public void PageGrantsTheSkill()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            using var context = new BunitContext();
            context.Services.AddSingleton(this._service);
            context.Services.AddSingleton(AdminOperationTestServices.CreateExecutor());
            context.Services.AddSingleton(Mock.Of<IToastService>());
            context.Services.AddSingleton(Mock.Of<ILogger<HnmoSkills>>());
            context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();

            var cut = context.Render<HnmoSkills>(parameters => parameters.Add(p => p.LoginName, "knight"));
            cut.WaitForAssertion(() => Assert.That(cut.FindAll("tr[data-skill]"), Has.Count.EqualTo(4)));
            Assert.That(cut.Markup, Does.Contain(Resources.HnmoSkillsOffline));

            cut.Find("tr[data-skill='703'] button").Click();

            cut.WaitForAssertion(() => Assert.That(cut.Find("#hnmo-skills-result").TextContent, Does.Contain("Hero now has the skill Charge (703).")));
            Assert.That(cut.Find("tr[data-skill='703'] button").TextContent, Is.EqualTo(Resources.HnmoSkillsRemove));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    /// <summary>
    /// Setting the balance grants the difference to the saved balance and the pending grants, and nothing when it's equal.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SetBalanceGrantsTheDifferenceAsync()
    {
        using (var context = this._persistence.CreateNewPlayerContext(this._configuration))
        {
            var account = (await context.GetAccountByLoginNameAsync("knight").ConfigureAwait(false))!;
            account.WCoinC = 300;
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var coins = new CashShopCoinService(this._persistence, this._configurationSource.Object);
        await coins.GrantAsync("knight", CashShopCoinType.WCoinC, 200, "pending", null, "test").ConfigureAwait(false);

        var set = await coins.SetBalanceAsync("knight", CashShopCoinType.WCoinC, 1000, "set", "test").ConfigureAwait(false);
        Assert.That(set, Is.EqualTo(((CashShopCoinGrantStatus?)CashShopCoinGrantStatus.Created, 500)));
        var lower = await coins.SetBalanceAsync("knight", CashShopCoinType.WCoinC, 100, "set", "test").ConfigureAwait(false);
        Assert.That(lower, Is.EqualTo(((CashShopCoinGrantStatus?)CashShopCoinGrantStatus.Created, -900)));
        var same = await coins.SetBalanceAsync("knight", CashShopCoinType.WCoinC, 100, "set", "test").ConfigureAwait(false);
        Assert.That(same, Is.EqualTo(((CashShopCoinGrantStatus?)null, 0)));
        var unknown = await coins.SetBalanceAsync("nobody", CashShopCoinType.WCoinC, 100, "set", "test").ConfigureAwait(false);
        Assert.That(unknown.Status, Is.EqualTo(CashShopCoinGrantStatus.AccountNotFound));

        var summary = await coins.GetSummaryAsync("knight").ConfigureAwait(false);
        Assert.That(summary!.Grants.Select(g => g.Amount), Is.EquivalentTo(new[] { 200, 500, -900 }));
    }

    /// <summary>
    /// The coins reach an account in the game at once, through its game server; an offline account gets them later.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CoinsReachAnAccountInTheGameAtOnceAsync()
    {
        var delivery = new CashShopCoinDelivery(this._loginServer.Object, this._serverProvider.Object);
        Assert.That(await delivery.ApplyNowAsync("knight").ConfigureAwait(false), Is.EqualTo(CashShopCoinDeliveryStatus.Offline));

        this._online["knight"] = 3;
        var plugInId = typeof(CashShopCoinGrantsAdminOperationPlugIn).GUID;
        var preview = new JsonObject { ["loginName"] = "knight" };
        this._live.Setup(l => l.PreviewAdminOperationAsync(plugInId, CashShopCoinGrantsAdminOperationPlugIn.OperationName, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        this._live.Setup(l => l.ExecuteAdminOperationAsync(plugInId, CashShopCoinGrantsAdminOperationPlugIn.OperationName, It.IsAny<string>(), preview, It.IsAny<CancellationToken>())).ReturnsAsync(new JsonObject());
        Assert.That(await delivery.ApplyNowAsync("KNIGHT").ConfigureAwait(false), Is.EqualTo(CashShopCoinDeliveryStatus.Applied));
        this._live.Verify(l => l.ExecuteAdminOperationAsync(plugInId, CashShopCoinGrantsAdminOperationPlugIn.OperationName, "{\"loginName\":\"knight\"}", preview, It.IsAny<CancellationToken>()), Times.Once);

        this._live.Setup(l => l.PreviewAdminOperationAsync(plugInId, CashShopCoinGrantsAdminOperationPlugIn.OperationName, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("The account is not in the game on this server."));
        Assert.That(await delivery.ApplyNowAsync("knight").ConfigureAwait(false), Is.EqualTo(CashShopCoinDeliveryStatus.Pending));
    }

    private static bool IsRequest(string arguments, string characterName, int skillNumber, bool learn)
    {
        var json = JsonNode.Parse(arguments)!.AsObject();
        return json["characterName"]!.GetValue<string>() == characterName
               && json["skillNumber"]!.GetValue<int>() == skillNumber
               && json["learn"]!.GetValue<bool>() == learn
               && Guid.TryParse(json["characterId"]!.GetValue<string>(), out _);
    }

    private void AddSkill(short number, string name, CharacterClass characterClass)
    {
        var skill = new HNMO.Persistence.BasicModel.Skill { Id = Guid.NewGuid(), Number = number, Name = name };
        skill.QualifiedCharacters.Add(characterClass);
        this._configuration.Skills.Add(skill);
    }

    private async Task<int[]> GetStoredSkillsAsync()
    {
        using var context = this._persistence.CreateNewPlayerContext(this._configuration);
        var account = (await context.GetAccountByLoginNameAsync("knight").ConfigureAwait(false))!;
        return account.Characters.Single().LearnedSkills.Select(s => (int)s.Skill!.Number).OrderBy(n => n).ToArray();
    }
}

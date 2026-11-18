// <copyright file="EveryAdminPageHelpTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Bunit;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.Interfaces;
using HNMO.Network.Analyzer;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Initialization.Captions;
using HNMO.Persistence.Initialization.Updates;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Components;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Map.Map;
using HNMO.Web.Shared.Components.Modal;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Shared.Models;
using HNMO.Web.Shared.Services;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Tests.Mcp;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AdminResources = HNMO.Web.AdminPanel.Properties.Resources;
using Guild = HNMO.DataModel.Entities.Guild;
using SharedResources = HNMO.Web.Shared.Properties.Resources;

/// <summary>Renders every real AdminPanel @page route in both EN and VI and verifies PageHelp disclosure and button hints.</summary>
[TestFixture]
[NonParallelizable]
public class EveryAdminPageHelpTests
{
    /// <summary>Every routable page component in HNMO.Web.AdminPanel across EN and VI cultures.</summary>
    public static IEnumerable<TestCaseData> EveryPageCases
        => DiscoverPageTypes()
            .SelectMany(type => new[] { "en", "vi" }
                .Select(lang => new TestCaseData(type, lang).SetName($"PageHelp_Render_{type.Name}_{lang}")));

    private static IEnumerable<Type> DiscoverPageTypes()
        => typeof(PageHelp).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract
                && typeof(IComponent).IsAssignableFrom(t)
                && t.GetCustomAttributes<RouteAttribute>(inherit: false).Any())
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    /// <summary>Renders the real page component, clicks PageHelp to reveal its alert-info box, and verifies button hover hints.</summary>
    /// <param name="pageType">The routable page component type.</param>
    /// <param name="language">The culture ("en" or "vi").</param>
    /// <returns>The async test task.</returns>
    [TestCaseSource(nameof(EveryPageCases))]
    public async Task EveryAdminPageRendersPageHelpInBothLanguagesAsync(Type pageType, string language)
    {
        var prevUi = CultureInfo.CurrentUICulture;
        var prevAdmin = AdminResources.Culture;
        var prevShared = SharedResources.Culture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.CurrentUICulture = culture;
            AdminResources.Culture = culture;
            SharedResources.Culture = culture;

            await using var ui = new BunitContext();
            ui.JSInterop.Mode = JSRuntimeMode.Loose;

            var (gameConfig, server, connectServer, account, guild, map, provider) = await CreateSeededGraphAsync().ConfigureAwait(false);
            ConfigureServices(ui, gameConfig, server, account, guild, map, provider);

            var parameters = BuildRouteParameters(pageType, account, guild, map, connectServer);
            var cut = ui.Render(builder =>
            {
                builder.OpenComponent(0, pageType);
                builder.AddMultipleAttributes(1, parameters);
                builder.CloseComponent();
            });

            cut.WaitForState(() => cut.FindAll(".page-help button").Count > 0, TimeSpan.FromSeconds(5));
            var helpButtons = cut.FindAll(".page-help button");
            Assert.That(helpButtons, Is.Not.Empty, $"{pageType.Name} did not render .page-help button");
            foreach (var helpButton in helpButtons)
            {
                Assert.That(helpButton.GetAttribute("title"), Is.Not.Null.And.Not.Empty, $"{pageType.Name} help button lacks title hint");
            }

            helpButtons[0].Click();
            cut.WaitForState(() => cut.FindAll(".page-help .alert-info").Count > 0, TimeSpan.FromSeconds(3));
            var helpText = cut.Find(".page-help .alert-info").TextContent.Trim();
            Assert.That(helpText, Is.Not.Empty, $"{pageType.Name} rendered empty PageHelp alert in {language}");

            // Third-party QuickGrid column-sort buttons carry class "col-title" and expose no Title parameter.
            var untitledButtons = cut.FindAll("button")
                .Where(b => !b.ClassList.Contains("col-title") && string.IsNullOrWhiteSpace(b.GetAttribute("title")))
                .Select(b => b.OuterHtml)
                .ToList();
            Assert.That(untitledButtons, Is.Empty, $"{pageType.Name} ({language}) has button(s) missing title hint: {string.Join(" | ", untitledButtons.Take(3))}");
        }
        finally
        {
            AdminResources.Culture = prevAdmin;
            SharedResources.Culture = prevShared;
            CultureInfo.CurrentUICulture = prevUi;
        }
    }

    private static async ValueTask<(GameConfiguration Game, GameServerDefinition Server, ConnectServerDefinition ConnectServer, Account Account, Guild Guild, GameMapDefinition Map, InMemoryPersistenceContextProvider Provider)> CreateSeededGraphAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var game = setup.CreateNew<GameConfiguration>();
        var map = setup.CreateNew<GameMapDefinition>();
        map.Number = 0;
        map.Name = "Lorencia";
        map.TerrainData = new byte[ushort.MaxValue + 3];
        game.Maps.Add(map);

        var server = setup.CreateNew<GameServerDefinition>();
        server.ServerID = 0;
        server.Description = "Main";
        server.GameConfiguration = game;
        var configDef = setup.CreateNew<GameServerConfiguration>();
        server.ServerConfiguration = configDef;

        var connectServer = setup.CreateNew<ConnectServerDefinition>();
        connectServer.ServerId = 0;
        connectServer.Description = "CS";
        await setup.SaveChangesAsync().ConfigureAwait(false);

        using var players = provider.CreateNewPlayerContext(game);
        var account = players.CreateNew<Account>();
        account.LoginName = "ann";
        account.EMail = "ann@example.test";
        account.State = AccountState.Normal;
        var ch = players.CreateNew<Character>();
        ch.Name = "AnnHero";
        ch.CurrentMap = map;
        ch.Inventory = players.CreateNew<ItemStorage>();
        account.Characters.Add(ch);
        await players.SaveChangesAsync().ConfigureAwait(false);

        using var guildCtx = provider.CreateNewGuildContext();
        var guild = guildCtx.CreateNew<Guild>();
        guild.Name = "Knights";
        await guildCtx.SaveChangesAsync().ConfigureAwait(false);

        return (game, server, connectServer, account, guild, map, provider);
    }

    private static void ConfigureServices(
        BunitContext ui,
        GameConfiguration gameConfig,
        GameServerDefinition gameServerDef,
        Account account,
        Guild guild,
        GameMapDefinition map,
        InMemoryPersistenceContextProvider provider)
    {
        var services = ui.Services;
        services.AddLogging();
        services.AddLocalization();
        services.AddMemoryCache();
        services.AddToasts();

        var configRoot = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        services.AddSingleton<IConfiguration>(configRoot);
        services.AddAdminPanelAuth(configRoot);
        ui.AddAdministratorAuthorization();

        services.AddSingleton<IPersistenceContextProvider>(provider);
        var migratable = new Mock<IMigratableDatabaseContextProvider>();
        migratable.Setup(m => m.RepositoryProvider).Returns(provider.RepositoryProvider);
        services.AddSingleton(migratable.Object);

        var gameSource = new Mock<IDataSource<GameConfiguration>>();
        gameSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(gameConfig);
        services.AddSingleton(gameSource.Object);

        var accountSource = new Mock<IDataSource<Account>>();
        accountSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        services.AddSingleton(accountSource.Object);

        var loginMock = new Mock<ILoginServer>();
        loginMock.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte>());
        services.AddSingleton(loginMock.Object);

        var plugInManager = new PlugInManager(Array.Empty<PlugInConfiguration>(), NullLoggerFactory.Instance, null, null);
        var mapInit = new MapInitializer(gameConfig, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var mediator = new ConfigurationChangeMediator();
        var gameContext = new GameContext(gameConfig, provider, mapInit, NullLoggerFactory.Instance, plugInManager, NullDropGenerator.Instance, mediator);
        mapInit.PlugInManager = plugInManager;
        mapInit.PathFinderPool = gameContext.PathFinderPool;

        var gameServerMock = new Mock<IGameServer>();
        gameServerMock.Setup(s => s.Id).Returns(0);
        gameServerMock.Setup(s => s.Description).Returns("Main");
        var providerServerMock = gameServerMock.As<IGameServerContextProvider>();
        providerServerMock.Setup(s => s.Context).Returns(new HNMO.GameServer.GameServerContext(
            gameServerDef,
            new Mock<IGuildServer>().Object,
            new Mock<IEventPublisher>().Object,
            loginMock.Object,
            new Mock<IFriendServer>().Object,
            provider,
            mapInit,
            NullLoggerFactory.Instance,
            plugInManager,
            NullDropGenerator.Instance,
            mediator));

        var manageableList = new List<IManageableServer> { gameServerMock.Object };
        services.AddSingleton<IList<IManageableServer>>(manageableList);
        var serverProviderMock = new Mock<IServerProvider>();
        serverProviderMock.Setup(p => p.Servers).Returns(manageableList);
        services.AddSingleton(serverProviderMock.Object);

        services.AddSingleton<IConfigurationChangePublisher>(IConfigurationChangePublisher.None);
        services.AddSingleton(plugInManager);
        services.AddSingleton<SetupService>();
        services.AddSingleton<ConfigurationSearchIndexCache>();
        services.AddSingleton<OnlinePlayerHistory>();
        services.AddSingleton<PacketAnalyzerProvider>();
        services.AddSingleton<WorldEditorDataService>();
        services.AddSingleton<IServerRestartService, NoOpServerRestartService>();
        services.AddSingleton<IHostSettingsService, HostSettingsService>();

        var adminUsers = new Mock<IAdminUserRepository>();
        adminUsers.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<AdminUser>());
        services.AddSingleton(adminUsers.Object);
        var apiKeys = new Mock<IApiKeyRepository>();
        apiKeys.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ApiKey>());
        services.AddSingleton(apiKeys.Object);
        services.AddSingleton<IAdminAuditRepository>(new InMemoryAdminAuditRepository());

        services.AddSingleton(new Mock<IConnectServerInstanceManager>().Object);
        services.AddSingleton(new Mock<IGameServerInstanceManager>().Object);
        services.AddSingleton(new Mock<IBackupService>().Object);
        services.AddSingleton(new Mock<IDatabaseSnapshotService>().Object);
        var mapController = new Mock<IMapController>();
        mapController.Setup(c => c.Players).Returns(new ConcurrentDictionary<int, Player>());
        var mapFactory = new Mock<IMapFactory>();
        mapFactory.Setup(m => m.GetMapContainerIdentifier(It.IsAny<int>(), It.IsAny<Guid>())).Returns("map-0");
        mapFactory.Setup(m => m.CreateMapAsync(It.IsAny<HNMO.Web.Map.IObservableGameServer>(), It.IsAny<Guid>())).ReturnsAsync(mapController.Object);
        services.AddSingleton(mapFactory.Object);

        services.AddSingleton(new AdminWriteSettings { ReadOnly = false });
        services.AddSingleton(new AdminAuditSettings());
        services.AddSingleton(new AdminLogAccess { LocalFilesAvailable = false });
        services.AddSingleton(new AdminChangeConfirmation(TimeProvider.System));
        services.AddScoped<AdminOperationExecutor>(_ => AdminOperationTestServices.CreateExecutor());
        services.AddScoped<IAdminOperationContext, UnconfiguredAdminOperationContext>();

        services.AddScoped<ModalService>();
        services.AddScoped<IModalService>(sp => sp.GetRequiredService<ModalService>());
        services.AddScoped<ILookupController, PersistentObjectsLookupController>();
        services.AddScoped<CreationPanelService>();
        services.AddScoped<NavigationHistory>();
        services.AddScoped<LoadingOverlayService>();
        services.AddScoped<IChangeNotificationService, ChangeNotificationService>();
        services.AddScoped<IAdminReadModel, AdminReadModel>();
        services.AddScoped<DataUpdateService>();
        services.AddScoped<ConfigurationCaptionService>();
        services.AddScoped<AccountService>();
        services.AddScoped<IDataService<Account>>(sp => sp.GetRequiredService<AccountService>());
        services.AddScoped<IGuildMemberEnricher, CharacterGuildMemberEnricher>();
        services.AddScoped<GuildService>();
        services.AddScoped<IGuildService>(sp => sp.GetRequiredService<GuildService>());
        services.AddScoped<IDataService<GuildListItem>>(sp => sp.GetRequiredService<GuildService>());
        services.AddScoped<PlugInController>();
        services.AddScoped<IDataService<PlugInConfigurationViewItem>>(sp => sp.GetRequiredService<PlugInController>());
        services.AddScoped<ChatCommandController>();
        services.AddScoped<IDataService<ChatCommandViewItem>>(sp => sp.GetRequiredService<ChatCommandController>());
        services.AddScoped<AdminUserManagementService>();
        services.AddScoped<CastleSiegeManagementService>();
        services.AddScoped<LoggedInAccountService>();
        services.AddScoped<IDataService<LoggedInAccount>>(sp => sp.GetRequiredService<LoggedInAccountService>());
        services.AddScoped<OfflineAccountService>();
        services.AddScoped<IDataService<OfflineAccount>>(sp => sp.GetRequiredService<OfflineAccountService>());
        services.AddScoped<BotAccountService>();
        services.AddScoped<IDataService<BotAccount>>(sp => sp.GetRequiredService<BotAccountService>());
        services.AddSingleton(new LoggingConfigurationService(Path.Combine(Path.GetTempPath(), "hnmo-pagehelp-logging.json")));
        services.AddSingleton(new DiscordConfigurationService(Path.Combine(Path.GetTempPath(), "hnmo-pagehelp-discord.json"), configRoot));
        services.AddDataProtection();
        services.AddSingleton(sp => new WebsiteMailSettingsStore(
            new HNMO.GameLogic.PlugIns.HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-pagehelp-mail-{Guid.NewGuid():N}.json")),
            sp.GetRequiredService<IDataProtectionProvider>()));
        services.AddAdminReadOperations();

        ui.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    private static Dictionary<string, object> BuildRouteParameters(Type pageType, Account account, Guild guild, GameMapDefinition map, ConnectServerDefinition connectServer)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var prop in pageType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<ParameterAttribute>() is null)
            {
                continue;
            }

            object? value = prop.Name switch
            {
                "LoginName" => account.LoginName,
                "GuildId" => guild.GetId(),
                "GameServerId" or "gameServerId" => 0,
                "MapId" or "mapId" => map.GetId(),
                "TypeString" or "typeString" => typeof(GameMapDefinition).FullName,
                "Id" when pageType == typeof(EditConnectionServer) => connectServer.GetId(),
                "Id" => map.GetId(),
                "AccountId" => account.GetId(),
                _ => null,
            };
            if (value is not null && prop.PropertyType.IsInstanceOfType(value))
            {
                result[prop.Name] = value;
            }
        }

        return result;
    }
}

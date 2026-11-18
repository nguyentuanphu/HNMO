// <copyright file="CharacterToolsOperationsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Tests.Mcp;
using Moq;

/// <summary>Unit tests for <see cref="CharacterToolsOperations"/> (ROADMAP 14.15).</summary>
[TestFixture]
public sealed class CharacterToolsOperationsTests
{
    private string _tempStateDir = null!;
    private string _activityFile = null!;

    /// <summary>Creates an isolated temporary activity log file per test.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempStateDir = Path.Combine(Path.GetTempPath(), $"hnmo-chartools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempStateDir);
        this._activityFile = Path.Combine(this._tempStateDir, "activity.json");
    }

    /// <summary>Removes temporary activity log files.</summary>
    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(this._tempStateDir))
            {
                Directory.Delete(this._tempStateDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup lock in temp folder.
        }
    }

    /// <summary>Verifies online unsafe edits (level/stats/Zen/class) are refused while live unstuck and buffs succeed.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task OnlineCharacter_RefusesOfflineEdits_AndAllowsUnstuckAndBuffsAsync()
    {
        var (provider, config, _, lorencia, dkClass, buffDef) = await SeedWorldAsync().ConfigureAwait(false);
        using var players = provider.CreateNewPlayerContext(config);
        var account = players.CreateNew<Account>();
        account.LoginName = "heroacc";
        account.State = AccountState.Normal;
        var ch = CreateCharacter(players, account, "HeroOnline", lorencia, dkClass);
        await players.SaveChangesAsync().ConfigureAwait(false);

        var configSource = new Mock<IDataSource<GameConfiguration>>();
        configSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var login = new Mock<ILoginServer>();
        login.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte> { ["heroacc"] = 0 });

        var server = new Mock<IGameServer>();
        server.SetupGet(s => s.Id).Returns(0);
        server.SetupGet(s => s.ServerState).Returns(ServerState.Started);
        var live = server.As<IGameServerAdministration>();
        var liveInfo = new LivePlayerInfo(ch.GetId(), "heroacc", "HeroOnline", 0, 10, 10, 50, 0, null, false);
        live.Setup(s => s.GetPlayerAsync("HeroOnline", It.IsAny<CancellationToken>())).ReturnsAsync(liveInfo);
        live.Setup(s => s.TeleportAsync("HeroOnline", liveInfo.CharacterId, 0, It.IsAny<byte>(), It.IsAny<byte>(), It.IsAny<CancellationToken>())).ReturnsAsync(GameServerAdminResult.Success);
        live.Setup(s => s.ApplyBuffAsync("HeroOnline", liveInfo.CharacterId, buffDef.Number, 600, It.IsAny<CancellationToken>())).ReturnsAsync(GameServerAdminResult.Success);
        live.Setup(s => s.ClearBuffsAsync("HeroOnline", liveInfo.CharacterId, It.IsAny<CancellationToken>())).ReturnsAsync(GameServerAdminResult.Success);

        var serverProvider = new Mock<IServerProvider>();
        serverProvider.SetupGet(s => s.Servers).Returns(new List<IManageableServer> { server.Object });

        var audit = new InMemoryAdminAuditRepository();
        var ops = new CharacterToolsOperations(provider, configSource.Object, login.Object, serverProvider.Object, AdminOperationTestServices.CreateExecutor(audit: audit), new AdminWriteSettings { ReadOnly = false })
        {
            ActivityLogFileName = this._activityFile,
        };

        // 1. Unsafe edit while online MUST be refused.
        Assert.ThrowsAsync<AdminOperationException>(async () =>
            await ops.EditOfflineCharacterAsync("HeroOnline", new CharacterEditRequest { Level = 200, Reason = "Boost player" }, CancellationToken.None).ConfigureAwait(false));

        // 2. Missing reason MUST be refused.
        Assert.ThrowsAsync<AdminOperationException>(async () =>
            await ops.MoveToTownAsync("HeroOnline", null, " ", CancellationToken.None).ConfigureAwait(false));

        // 3. Online move to town + live buff + clear buffs succeed and audit.
        var moved = await ops.MoveToTownAsync("HeroOnline", null, "Stuck on terrain", CancellationToken.None).ConfigureAwait(false);
        Assert.That(moved["online"]?.GetValue<bool>(), Is.True);

        var buffed = await ops.ApplyBuffAsync("HeroOnline", buffDef.Number, 600, "Event support", CancellationToken.None).ConfigureAwait(false);
        Assert.That(buffed["effectNumber"]?.GetValue<short>(), Is.EqualTo(buffDef.Number));

        var cleared = await ops.ClearBuffsAsync("HeroOnline", "Cleanup after test", CancellationToken.None).ConfigureAwait(false);
        Assert.That(cleared["cleared"]?.GetValue<bool>(), Is.True);

        live.Verify(s => s.TeleportAsync("HeroOnline", liveInfo.CharacterId, 0, It.IsAny<byte>(), It.IsAny<byte>(), It.IsAny<CancellationToken>()), Times.Once);
        live.Verify(s => s.ApplyBuffAsync("HeroOnline", liveInfo.CharacterId, buffDef.Number, 600, It.IsAny<CancellationToken>()), Times.Once);
        live.Verify(s => s.ClearBuffsAsync("HeroOnline", liveInfo.CharacterId, It.IsAny<CancellationToken>()), Times.Once);

        var (auditItems, _) = await audit.ReadAsync(0, 20, CancellationToken.None).ConfigureAwait(false);
        Assert.That(auditItems, Has.Count.EqualTo(3));

        var activityEntries = ActivityLogStore.Open(this._activityFile).Query(new ActivityLogQuery { Kind = ActivityKind.Administration, MaxResults = 20 });
        Assert.That(activityEntries, Has.Count.EqualTo(3));
    }

    /// <summary>Verifies offline edits update level, stats, Zen, class, position, and record audit and activity entries.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task OfflineCharacter_UpdatesStatsZenClassAndTownMove_WithAuditAsync()
    {
        var (provider, config, smClass, lorencia, dkClass, _) = await SeedWorldAsync().ConfigureAwait(false);
        using var players = provider.CreateNewPlayerContext(config);
        var account = players.CreateNew<Account>();
        account.LoginName = "offacc";
        account.State = AccountState.Normal;
        var ch = CreateCharacter(players, account, "OffHero", lorencia, dkClass);
        ch.PositionX = 12;
        ch.PositionY = 12;
        await players.SaveChangesAsync().ConfigureAwait(false);

        var configSource = new Mock<IDataSource<GameConfiguration>>();
        configSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var login = new Mock<ILoginServer>();
        login.Setup(s => s.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte>());
        login.Setup(s => s.TryReserveAccountAsync("offacc", It.IsAny<int>())).ReturnsAsync(true);
        login.Setup(s => s.ReleaseAccountReservationAsync("offacc", It.IsAny<int>())).Returns(ValueTask.CompletedTask);

        var serverProvider = new Mock<IServerProvider>();
        serverProvider.SetupGet(s => s.Servers).Returns(new List<IManageableServer>());

        var audit = new InMemoryAdminAuditRepository();
        var ops = new CharacterToolsOperations(provider, configSource.Object, login.Object, serverProvider.Object, AdminOperationTestServices.CreateExecutor(audit: audit), new AdminWriteSettings { ReadOnly = false })
        {
            ActivityLogFileName = this._activityFile,
        };

        await ops.MoveToTownAsync("OffHero", 0, "Relocate to spawn", CancellationToken.None).ConfigureAwait(false);
        await ops.EditOfflineCharacterAsync(
            "OffHero",
            new CharacterEditRequest
            {
                Level = 320,
                Strength = 500,
                Agility = 450,
                Zen = 9_000_000,
                CharacterClassId = smClass.GetId(),
                Reason = "Manual recovery compensation",
            },
            CancellationToken.None).ConfigureAwait(false);

        var snapshot = await ops.LoadCharacterAsync("OffHero", CancellationToken.None).ConfigureAwait(false);
        Assert.That(snapshot.IsOnline, Is.False);
        Assert.That(snapshot.Level, Is.EqualTo(320));
        Assert.That(snapshot.Strength, Is.EqualTo(500));
        Assert.That(snapshot.Agility, Is.EqualTo(450));
        Assert.That(snapshot.Zen, Is.EqualTo(9_000_000));
        Assert.That(snapshot.CharacterClassId, Is.EqualTo(smClass.GetId()));
        Assert.That(snapshot.X, Is.EqualTo(125));
        Assert.That(snapshot.Y, Is.EqualTo(125));

        var activityEntries = ActivityLogStore.Open(this._activityFile).Query(new ActivityLogQuery { Kind = ActivityKind.Administration, MaxResults = 20 });
        Assert.That(activityEntries, Has.Count.EqualTo(2));
        Assert.That(activityEntries[0].Details, Does.Contain("Manual recovery compensation"));
    }

    private static async ValueTask<(InMemoryPersistenceContextProvider Provider, GameConfiguration Config, CharacterClass SmClass, GameMapDefinition Lorencia, CharacterClass DkClass, MagicEffectDefinition BuffDef)> SeedWorldAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var ctx = provider.CreateNewContext();
        var config = ctx.CreateNew<GameConfiguration>();
        config.MaximumLevel = 400;
        config.MaximumMasterLevel = 200;
        config.MaximumInventoryMoney = 2_000_000_000;

        var lorencia = ctx.CreateNew<GameMapDefinition>();
        lorencia.Number = 0;
        lorencia.Name = "Lorencia";
        lorencia.IsActive = true;
        lorencia.TerrainData = new byte[ushort.MaxValue + 3];
        var gate = ctx.CreateNew<ExitGate>();
        gate.IsSpawnGate = true;
        gate.X1 = 125;
        gate.X2 = 125;
        gate.Y1 = 125;
        gate.Y2 = 125;
        gate.Map = lorencia;
        lorencia.ExitGates.Add(gate);
        config.Maps.Add(lorencia);

        var dk = ctx.CreateNew<CharacterClass>();
        dk.Number = 16;
        dk.Name = "Dark Knight";
        dk.HomeMap = lorencia;
        config.CharacterClasses.Add(dk);

        var sm = ctx.CreateNew<CharacterClass>();
        sm.Number = 0;
        sm.Name = "Dark Wizard";
        sm.HomeMap = lorencia;
        config.CharacterClasses.Add(sm);

        var buff = ctx.CreateNew<MagicEffectDefinition>();
        buff.Number = 3;
        buff.Name = "Greater Damage";
        var dur = ctx.CreateNew<PowerUpDefinitionValue>();
        dur.ConstantValue!.Value = 60f;
        buff.Duration = dur;
        var pd = ctx.CreateNew<PowerUpDefinition>();
        pd.TargetAttribute = Stats.MinimumPhysBaseDmg;
        var boost = ctx.CreateNew<PowerUpDefinitionValue>();
        boost.ConstantValue!.Value = 50f;
        pd.Boost = boost;
        buff.PowerUpDefinitions.Add(pd);
        config.MagicEffects.Add(buff);

        await ctx.SaveChangesAsync().ConfigureAwait(false);
        return (provider, config, sm, lorencia, dk, buff);
    }

    private static Character CreateCharacter(IPlayerContext ctx, Account account, string name, GameMapDefinition map, CharacterClass cc)
    {
        var ch = ctx.CreateNew<Character>();
        ch.Name = name;
        ch.CurrentMap = map;
        ch.CharacterClass = cc;
        ch.Inventory = ctx.CreateNew<ItemStorage>();
        ch.Inventory.Money = 1000;
        AddStat(ctx, ch, Stats.Level, 10);
        AddStat(ctx, ch, Stats.MasterLevel, 0);
        AddStat(ctx, ch, Stats.Resets, 0);
        AddStat(ctx, ch, Stats.BaseStrength, 25);
        AddStat(ctx, ch, Stats.BaseAgility, 20);
        AddStat(ctx, ch, Stats.BaseVitality, 20);
        AddStat(ctx, ch, Stats.BaseEnergy, 15);
        AddStat(ctx, ch, Stats.BaseLeadership, 0);
        account.Characters.Add(ch);
        return ch;
    }

    private static void AddStat(IPlayerContext ctx, Character ch, AttributeDefinition def, float value)
    {
        var sa = ctx.CreateNew<StatAttribute>(def, value);
        ch.Attributes.Add(sa);
    }
}

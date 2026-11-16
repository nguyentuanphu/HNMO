// <copyright file="WorldsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Worlds;

/// <summary>
/// Unit and integration tests for multi-world isolated databases, templates, Sydney opening locks,
/// grouped snapshots, and one-off character transfers (ROADMAP 15.7).
/// </summary>
[TestFixture]
public sealed class WorldsTests
{
    /// <summary>
    /// Verifies creating a Fresh and CopiedCharacters world from a template world with Sydney opening dates
    /// across separate persistence providers.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask CreatesWorldsFromTemplateWithFreshAndCopiedCharactersAsync()
    {
        var registry = new WorldRegistryService();
        registry.EnsureDefaultWorld([0, 1]);

        var srcDb = CreateSeededProvider(expRate: 10.0f, accountLogin: "heroacc", charName: "BladeOne", level: 120);
        var freshDb = CreateEmptyConfigProvider();
        var copiedDb = CreateEmptyConfigProvider();

        registry.RegisterWorldContextProvider("main", srcDb);
        registry.RegisterWorldContextProvider("s2-fresh", freshDb);
        registry.RegisterWorldContextProvider("s3-copy", copiedDb);

        // 2026-12-01 19:00 Sydney (AEDT UTC+11) == 2026-12-01 08:00 UTC
        var freshWorld = await registry.CreateWorldFromTemplateAsync(new WorldTemplateCreateRequest
        {
            SourceWorldId = "main",
            NewWorldId = "s2-fresh",
            NameEn = "World 2 — Fresh",
            NameVi = "Thế giới 2 — Mới",
            DatabaseName = "hnmo_world_2",
            ServerIds = [2],
            Mode = WorldTemplateMode.Fresh,
            OpeningSydneyText = "2026-12-01 19:00",
            ExperienceMultiplier = 2.5,
            TransferEnabled = true,
        }).ConfigureAwait(false);

        Assert.That(freshWorld.OpeningDateUtc, Is.EqualTo(new DateTimeOffset(2026, 12, 1, 8, 0, 0, TimeSpan.Zero)));
        Assert.That(freshWorld.IsOpenAt(new DateTimeOffset(2026, 12, 1, 7, 59, 0, TimeSpan.Zero)), Is.False);
        Assert.That(freshWorld.IsOpenAt(new DateTimeOffset(2026, 12, 1, 8, 0, 0, TimeSpan.Zero)), Is.True);
        Assert.That(freshWorld.IsRunning, Is.False);

        using (var freshCtx = freshDb.CreateNewContext())
        {
            var cfg = (await freshCtx.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var accounts = (await freshCtx.GetAsync<Account>().ConfigureAwait(false)).ToList();
            Assert.That(cfg.ExperienceRate, Is.EqualTo(25.0f).Within(0.01f));
            Assert.That(accounts, Is.Empty);
        }

        await registry.CreateWorldFromTemplateAsync(new WorldTemplateCreateRequest
        {
            SourceWorldId = "main",
            NewWorldId = "s3-copy",
            NameEn = "World 3 — Inherited",
            NameVi = "Thế giới 3 — Kế thừa",
            DatabaseName = "hnmo_world_3",
            ServerIds = [3],
            Mode = WorldTemplateMode.CopiedCharacters,
            ExperienceMultiplier = 1.5,
            TransferEnabled = false,
        }).ConfigureAwait(false);

        using (var copiedCtx = copiedDb.CreateNewContext())
        {
            var accounts = (await copiedCtx.GetAsync<Account>().ConfigureAwait(false)).ToList();
            Assert.That(accounts.SelectMany(a => a.Characters).Select(c => c.Name), Contains.Item("BladeOne"));
        }

        // Start/stop & grouped snapshot
        var serverMock0 = CreateServerMock(0, 14, 100, ServerState.Started);
        var serverMock2 = CreateServerMock(2, 0, 100, ServerState.Stopped);
        var grouped = registry.BuildGroupedSnapshot([serverMock0.Object, serverMock2.Object], new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.Zero));
        Assert.That(grouped.Count, Is.EqualTo(3));
        Assert.That(grouped[0].TotalPlayers, Is.EqualTo(14));
        Assert.That(grouped[1].IsOpen, Is.False);
        Assert.That(grouped[1].OpeningDateSydney, Does.Contain("2026-12-01 19:00"));
    }

    /// <summary>
    /// Verifies atomic one-off character transfer across two separate world databases
    /// and that repeating the transfer fails cleanly.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask OneOffCharacterTransferMovesCharacterAndBlocksRepeatAsync()
    {
        var registry = new WorldRegistryService();
        registry.EnsureDefaultWorld([0]);
        registry.UpsertWorld(new WorldDefinition
        {
            WorldId = "s2",
            NameEn = "World 2",
            NameVi = "Thế giới 2",
            DatabaseName = "hnmo_world_2",
            ServerIds = [1],
            TransferEnabled = true,
            IsRunning = true,
        });

        var db1 = CreateSeededProvider(expRate: 1.0f, accountLogin: "alice", charName: "MuseElf", level: 250);
        var db2 = CreateEmptyConfigProvider(maxSlots: 5);
        registry.RegisterWorldContextProvider("main", db1);
        registry.RegisterWorldContextProvider("s2", db2);

        var record = await registry.TransferCharacterOnceAsync("main", "s2", "alice", "MuseElf", "gm-one").ConfigureAwait(false);
        Assert.That(record.SourceWorldId, Is.EqualTo("main"));
        Assert.That(record.TargetWorldId, Is.EqualTo("s2"));
        Assert.That(record.CharacterName, Is.EqualTo("MuseElf"));

        using (var srcCheck = db1.CreateNewContext())
        {
            var srcChars = (await srcCheck.GetAsync<Account>().ConfigureAwait(false)).SelectMany(a => a.Characters).ToList();
            Assert.That(srcChars, Is.Empty);
        }

        using (var dstCheck = db2.CreateNewContext())
        {
            var dstChars = (await dstCheck.GetAsync<Account>().ConfigureAwait(false)).SelectMany(a => a.Characters).ToList();
            Assert.That(dstChars.Single().Name, Is.EqualTo("MuseElf"));
            Assert.That(dstChars.Single().LevelUpPoints, Is.EqualTo(250));
        }

        // Switching direction back to main (assuming MuseElf recreated or moved back) must be rejected by one-off transfer log
        registry.UpsertWorld(new WorldDefinition { WorldId = "main", TransferEnabled = true, ServerIds = [0] });
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await registry.TransferCharacterOnceAsync("s2", "main", "alice", "MuseElf", "gm-one").ConfigureAwait(false));
    }

    private static InMemoryPersistenceContextProvider CreateSeededProvider(float expRate, string accountLogin, string charName, int level)
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var ctx = provider.CreateNewContext();
        var cfg = ctx.CreateNew<GameConfiguration>();
        cfg.ExperienceRate = expRate;
        cfg.MasterExperienceRate = expRate;
        cfg.MaximumCharactersPerAccount = 5;
        var cls = ctx.CreateNew<CharacterClass>();
        cls.Number = 0;
        cls.Name = "Dark Knight";
        cfg.CharacterClasses.Add(cls);
        var map = ctx.CreateNew<GameMapDefinition>();
        map.Number = 0;
        map.Name = "Lorencia";
        cfg.Maps.Add(map);

        var acc = ctx.CreateNew<Account>();
        acc.LoginName = accountLogin;
        acc.LanguageIsoCode = "en";
        var ch = ctx.CreateNew<Character>();
        ch.Id = Guid.NewGuid();
        ch.Name = charName;
        ch.CharacterClass = cls;
        ch.CurrentMap = map;
        ch.Experience = 10000;
        ch.LevelUpPoints = level;
        acc.Characters.Add(ch);
        ctx.SaveChangesAsync().AsTask().GetAwaiter().GetResult();
        return provider;
    }

    private static InMemoryPersistenceContextProvider CreateEmptyConfigProvider(byte maxSlots = 5)
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var ctx = provider.CreateNewContext();
        var cfg = ctx.CreateNew<GameConfiguration>();
        cfg.ExperienceRate = 1.0f;
        cfg.MasterExperienceRate = 1.0f;
        cfg.MaximumCharactersPerAccount = maxSlots;
        var cls = ctx.CreateNew<CharacterClass>();
        cls.Number = 0;
        cls.Name = "Dark Knight";
        cfg.CharacterClasses.Add(cls);
        var map = ctx.CreateNew<GameMapDefinition>();
        map.Number = 0;
        map.Name = "Lorencia";
        cfg.Maps.Add(map);
        ctx.SaveChangesAsync().AsTask().GetAwaiter().GetResult();
        return provider;
    }

    private static Mock<IGameServer> CreateServerMock(int id, int online, int capacity, ServerState state)
    {
        var mock = new Mock<IGameServer>();
        mock.SetupGet(s => s.Id).Returns(id);
        mock.SetupGet(s => s.CurrentConnections).Returns(online);
        mock.SetupGet(s => s.MaximumConnections).Returns(capacity);
        mock.SetupGet(s => s.ServerState).Returns(state);
        return mock;
    }
}

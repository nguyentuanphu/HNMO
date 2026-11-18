// <copyright file="MaintenanceToolsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Operations;

using System.Threading;
using Moq;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Tests.Mcp;
using Account = HNMO.DataModel.Entities.Account;
using Character = HNMO.DataModel.Entities.Character;
using ItemStorage = HNMO.DataModel.Entities.ItemStorage;
using StatAttribute = HNMO.AttributeSystem.StatAttribute;

/// <summary>
/// Unit tests for <see cref="MaintenanceToolsOperations"/> (ROADMAP 14.8).
/// </summary>
[TestFixture]
public sealed class MaintenanceToolsTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _config = null!;
    private InMemoryAdminAuditRepository _audit = null!;
    private MaintenanceToolsOperations _ops = null!;
    private DateTimeOffset _now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Sets up seeded accounts, duplicate items, experience anomalies, and audited operations.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._provider = new InMemoryPersistenceContextProvider();
        using var setup = this._provider.CreateNewContext();
        this._config = setup.CreateNew<GameConfiguration>();
        this._config.MaximumLevel = 400;
        var levelAttr = setup.CreateNew<AttributeDefinition>();
        levelAttr.Id = Stats.Level.Id;
        levelAttr.Designation = "Level";
        this._config.Attributes.Add(levelAttr);

        var swordDef = setup.CreateNew<ItemDefinition>();
        swordDef.Name = "Blade";
        swordDef.MaximumItemLevel = 15;
        this._config.Items.Add(swordDef);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        using var playerCtx = this._provider.CreateNewPlayerContext(this._config);

        // 1. Old empty account -> candidate for purge
        var emptyOld = playerCtx.CreateNew<Account>();
        emptyOld.LoginName = "emptyold";
        emptyOld.RegistrationDate = this._now.UtcDateTime.AddDays(-120);
        emptyOld.State = AccountState.Normal;

        // 2. Old account with a character & items -> skipped by emptyOnly, checked for dupes & XP
        var activeOld = playerCtx.CreateNew<Account>();
        activeOld.LoginName = "alice";
        activeOld.RegistrationDate = this._now.UtcDateTime.AddDays(-150);
        activeOld.State = AccountState.Normal;
        activeOld.Vault = playerCtx.CreateNew<ItemStorage>();

        var dupGuid = Guid.NewGuid();
        var item1 = new HNMO.Persistence.BasicModel.Item { Id = dupGuid, Definition = swordDef, ItemSlot = 1, Level = 9, Durability = 1 };
        activeOld.Vault.Items.Add(item1);

        var aliceChar = playerCtx.CreateNew<Character>();
        aliceChar.Name = "AliceBM";
        aliceChar.Attributes.Add(playerCtx.CreateNew<StatAttribute>(levelAttr, 10f));
        aliceChar.Experience = 100; // below level 10 floor (10 * 18 * 9 * 9 = 14,580)
        aliceChar.Inventory = playerCtx.CreateNew<ItemStorage>();
        var item2 = new HNMO.Persistence.BasicModel.Item { Id = dupGuid, Definition = swordDef, ItemSlot = 12, Level = 9, Durability = 1 };
        aliceChar.Inventory.Items.Add(item2);
        activeOld.Characters.Add(aliceChar);

        // 3. Recent empty account -> not inactive yet
        var recent = playerCtx.CreateNew<Account>();
        recent.LoginName = "recentacc";
        recent.RegistrationDate = this._now.UtcDateTime.AddDays(-5);
        recent.State = AccountState.Normal;

        await playerCtx.SaveChangesAsync().ConfigureAwait(false);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._config);
        this._audit = new InMemoryAdminAuditRepository();
        var executor = AdminOperationTestServices.CreateExecutor(audit: this._audit);
        this._ops = new MaintenanceToolsOperations(this._provider, source.Object, executor, new FixedTimeProvider(this._now));
    }

    /// <summary>Verifies inactive empty account preview, audited purge, and retention of populated/recent accounts.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task InactiveAccountsPreviewAndPurgeWritesAuditAsync()
    {
        var preview = await this._ops.PreviewInactiveAccountsAsync(90, emptyOnly: true, default).ConfigureAwait(false);
        Assert.That(preview.Select(p => p.LoginName), Is.EqualTo(new[] { "emptyold" }));

        var deleted = await this._ops.PurgeInactiveAccountsAsync(90, emptyOnly: true, default).ConfigureAwait(false);
        Assert.That(deleted, Is.EqualTo(1));
        var (purgeAudit, _) = await this._audit.ReadAsync(0, 50).ConfigureAwait(false);
        Assert.That(purgeAudit.Any(e => e.Operation == "maintenance_purge_inactive_accounts"), Is.True);

        var remaining = await this._ops.PreviewInactiveAccountsAsync(90, emptyOnly: false, default).ConfigureAwait(false);
        Assert.That(remaining.Select(p => p.LoginName), Is.EqualTo(new[] { "alice" }));
    }

    /// <summary>Verifies duplicate GUID scan detects cross-storage copies and audited cleanup removes extras.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task DuplicateItemScanAndCleanupWritesAuditAsync()
    {
        var preview = await this._ops.PreviewDuplicateItemIdsAsync(default).ConfigureAwait(false);
        Assert.That(preview, Has.Count.EqualTo(2));

        var removed = await this._ops.RemoveDuplicateItemsAsync(default).ConfigureAwait(false);
        Assert.That(removed, Is.EqualTo(1));
        var (dupeAudit, _) = await this._audit.ReadAsync(0, 50).ConfigureAwait(false);
        Assert.That(dupeAudit.Any(e => e.Operation == "maintenance_remove_duplicate_items"), Is.True);

        var after = await this._ops.PreviewDuplicateItemIdsAsync(default).ConfigureAwait(false);
        Assert.That(after, Is.Empty);
    }

    /// <summary>Verifies character experience dry-run preview and audited clamp fix.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ExperienceFixerPreviewAndApplyWritesAuditAsync()
    {
        var preview = await this._ops.PreviewExperienceIssuesAsync(default).ConfigureAwait(false);
        Assert.That(preview, Has.Count.EqualTo(1));
        Assert.That(preview[0].CharacterName, Is.EqualTo("AliceBM"));
        Assert.That(preview[0].FixedExperience, Is.EqualTo(14580));

        var fixedCount = await this._ops.FixExperienceIssuesAsync(default).ConfigureAwait(false);
        Assert.That(fixedCount, Is.EqualTo(1));
        var (expAudit, _) = await this._audit.ReadAsync(0, 50).ConfigureAwait(false);
        Assert.That(expAudit.Any(e => e.Operation == "maintenance_fix_experience"), Is.True);

        var after = await this._ops.PreviewExperienceIssuesAsync(default).ConfigureAwait(false);
        Assert.That(after, Is.Empty);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

// <copyright file="JewelBankEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.Persistence.InMemory;

/// <summary>
/// Unit tests for <see cref="JewelBankEngine"/> and <see cref="ResetApplier.ClearMasterSkillsOfflineAsync"/>.
/// </summary>
[TestFixture]
public class JewelBankEngineTest
{
    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-jb-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes temp directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>Deposits, capacity enforcement, withdrawals, pending delivery consume, and audit logging work cleanly.</summary>
    [Test]
    public void DepositWithdrawAndPendingDeliveryFlow()
    {
        var cfg = new JewelBankConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            Jewels =
            [
                new() { Key = "bless", NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện", Group = 14, Number = 13, Enabled = true, MaxCapacity = 5 },
                new() { Key = "chaos", NameEn = "Jewel of Chaos", NameVi = "Ngọc Hỗn Nguyên", Group = 12, Number = 15, Enabled = false, MaxCapacity = 100 },
            ],
        };
        var engine = new JewelBankEngine(cfg, JewelBankStore.Open(cfg.FileName), enabled: true);

        Assert.That(engine.Deposit("alice", "chaos", 1, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.JewelNotAllowed));
        Assert.That(engine.Deposit("alice", "bless", 3, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.Deposit("alice", "bless", 3, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.CapacityExceeded));
        Assert.That(engine.GetBalances("ALICE")["bless"], Is.EqualTo(3));

        Assert.That(engine.TryWithdraw("alice", "bless", 5, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.InsufficientBalance));
        Assert.That(engine.TryWithdraw("alice", "bless", 2, "website", DateTime.UtcNow, enqueuePending: true), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.GetBalances("alice")["bless"], Is.EqualTo(1));

        var pending = engine.PeekPending("alice");
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].Quantity, Is.EqualTo(2));
        engine.ConsumePending(pending[0].Id, 2);
        Assert.That(engine.PeekPending("alice"), Is.Empty);
    }

    /// <summary>Clearing master skills offline deletes master entries, refunds points, and keeps normal skills intact.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ClearMasterSkillsOfflineRefundsPointsAndPreservesNormalSkills()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var gameConfig = new GameConfiguration();
        using var context = persistence.CreateNewPlayerContext(gameConfig);

        var character = context.CreateNew<Character>();
        character.Name = "MasterHero";
        character.MasterLevelUpPoints = 2;

        var normalSkill = context.CreateNew<Skill>();
        normalSkill.Number = 41;
        normalSkill.Name = "Twisting Slash";

        var masterSkill = context.CreateNew<Skill>();
        masterSkill.Number = 330;
        masterSkill.Name = "Twisting Slash Strengthener";
        masterSkill.MasterDefinition = context.CreateNew<MasterSkillDefinition>();

        var normalEntry = context.CreateNew<SkillEntry>();
        normalEntry.Skill = normalSkill;
        normalEntry.Level = 0;

        var masterEntry = context.CreateNew<SkillEntry>();
        masterEntry.Skill = masterSkill;
        masterEntry.Level = 15;

        character.LearnedSkills.Add(normalEntry);
        character.LearnedSkills.Add(masterEntry);

        Assert.That(ResetApplier.CountSpentMasterPoints(character), Is.EqualTo(15));
        var refunded = await ResetApplier.ClearMasterSkillsOfflineAsync(character, context).ConfigureAwait(false);
        Assert.That(refunded, Is.EqualTo(15));
        Assert.That(character.MasterLevelUpPoints, Is.EqualTo(17));
        Assert.That(character.LearnedSkills, Has.Count.EqualTo(1));
        Assert.That(character.LearnedSkills.First().Skill, Is.SameAs(normalSkill));
    }
}

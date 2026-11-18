// <copyright file="ServerImporterTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.ServerImporter;
using HNMO.Interfaces;
using HNMO.Persistence.InMemory;

/// <summary>
/// Unit tests for <see cref="LegacyFileParsers"/> and <see cref="LegacyServerImportEngine"/> using synthetic sample fixtures.
/// </summary>
[TestFixture]
public class ServerImporterTests
{
    private string _tempDir = string.Empty;
    private string _backupDir = string.Empty;

    /// <summary>Creates isolated temp folders for synthetic fixtures.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-imp-src-{Guid.NewGuid():N}");
        this._backupDir = Path.Combine(Path.GetTempPath(), $"hnmo-imp-bak-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
        Directory.CreateDirectory(this._backupDir);
    }

    /// <summary>Deletes temporary test folders.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }

        if (Directory.Exists(this._backupDir))
        {
            Directory.Delete(this._backupDir, recursive: true);
        }
    }

    /// <summary>Verifies version detection distinguishes Season 6 vs Season 9 structural cues cleanly.</summary>
    [Test]
    public void DetectsSeason6AndSeason9Folders()
    {
        var s6 = Path.Combine(this._tempDir, "s6");
        Directory.CreateDirectory(Path.Combine(s6, "Data"));
        File.WriteAllText(Path.Combine(s6, "Data", "CommonServer.cfg"), "ItemDropPer = 45\n");
        var (v6, _) = LegacyFileParsers.DetectVersion(s6);
        Assert.That(v6, Is.EqualTo(LegacyServerVersion.Season6));

        var s9 = Path.Combine(this._tempDir, "s9");
        Directory.CreateDirectory(Path.Combine(s9, "BattleCore"));
        File.WriteAllText(Path.Combine(s9, "PentagramOption.xml"), "<Pentagrams />");
        File.WriteAllText(Path.Combine(s9, "Common.ini"), "MaxMasterLevel = 330\n");
        var (v9, cues) = LegacyFileParsers.DetectVersion(s9);
        Assert.That(v9, Is.EqualTo(LegacyServerVersion.Season9));
        Assert.That(cues.Count, Is.GreaterThanOrEqualTo(3));
    }

    /// <summary>Verifies synthetic file parsing, 16-byte item decoding, dry-run preview, atomic backup, apply, and undo rollback.</summary>
    [Test]
    public async Task PreviewApplyAndUndoCompleteImportLifecycle()
    {
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Data", "Monster", "Spawns"));
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Config", "Shops"));
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Config", "Events"));

        File.WriteAllText(Path.Combine(this._tempDir, "Data", "CommonServer.cfg"), "ItemDropPer = 60\nZenDropMultipler = 2.5\n");
        File.WriteAllText(Path.Combine(this._tempDir, "Config", "ExpSystem.xml"), "<ExpSystem><StaticExp Normal=\"500\" Master=\"250\" /></ExpSystem>");
        File.WriteAllText(Path.Combine(this._tempDir, "Config", "ChaosBox.ini"), "Mix10Rate_Normal = 75\nMix15Rate_Normal = 45\nMixAddLuckRate_Normal = 20\n");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Config", "DropManager.xml"),
            "<DropManager><Monster Index=\"2\" ItemRate=\"5000\" MonsterMinLevel=\"1\" MonsterMaxLevel=\"20\"><Item Cat=\"0\" Index=\"1\" DropRate=\"10\" Exc=\"1\" /><Item Cat=\"12\" Index=\"200\" ElementalItem=\"1\" DropRate=\"10\" /></Monster></DropManager>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Config", "ShopList.xml"),
            "<ShopList><Shop NPCIndex=\"251\" MapNumber=\"0\" FileName=\"NPC251.xml\" /></ShopList>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Config", "Shops", "NPC251.xml"),
            "<Shop><Tab><Item Cat=\"0\" Index=\"1\" Level=\"3\" Durability=\"22\" Skill=\"1\" /></Tab></Shop>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Config", "Events", "BonusEvent.xml"),
            "<BonusEvent><Event Enable=\"1\" Name=\"Golden Hour\" StartHour=\"20\" StartMinute=\"0\" MapNumber=\"0\" ExpBonus=\"50\" DropBonus=\"25\" /></BonusEvent>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Config", "Events", "ArcaBattle.xml"),
            "<ArcaBattle />");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Data", "Monster", "Spawns", "MonsterSetBase.txt"),
            "1\n2 0 10 120 120 140 140 -1 5\nend\n5\n2 0 10 50 50 1 1 1\nend\n");

        var persistence = new InMemoryPersistenceContextProvider();
        var context = persistence.CreateNewContext();
        var config = CreateSyntheticConfig(context);
        config.ExperienceRate = 1.0f;

        var itemSlot = new byte[16];
        itemSlot[0] = 1; // Short Sword index 1
        itemSlot[1] = (5 << 3) | 0x80 | 0x04; // +5 skill luck
        itemSlot[2] = 22;
        itemSlot[9] = 0 << 4; // group 0

        var fakeDb = new StubDatabaseReader(new LegacyDatabaseSnapshot
        {
            Accounts = [new LegacyAccountRow { LoginName = "legacyuser1", Email = "u1@example.com" }],
            Characters =
            [
                new LegacyCharacterRow
                {
                    AccountId = "legacyuser1",
                    Name = "LegacyHero",
                    ClassNumber = 16,
                    Level = 150,
                    LevelUpPoints = 200,
                    Strength = 350,
                    Agility = 200,
                    Vitality = 100,
                    Energy = 50,
                    Money = 123456,
                    MapNumber = 0,
                    MapPosX = 130,
                    MapPosY = 125,
                    InventoryBlob = itemSlot,
                },
            ],
            Vaults = [new LegacyVaultRow { AccountId = "legacyuser1", Money = 99999, ItemsBlob = itemSlot }],
            Guilds = [new LegacyGuildRow { Name = "OldGuild", MasterName = "LegacyHero", Score = 55, Logo = new byte[32] }],
            GuildMembers = [new LegacyGuildMemberRow { GuildName = "OldGuild", CharacterName = "LegacyHero", Status = 128 }],
        });

        var options = new LegacyImportOptions
        {
            FolderPath = this._tempDir,
            MssqlConnectionString = "Server=localhost;Database=LegacySample;Trusted_Connection=True;",
            ImportDatabaseEntities = true,
            PasswordMode = LegacyPasswordMigrationMode.UseTemporaryPassphrase,
            TemporaryPassphrase = "TempMigration#2026",
        };

        var preview = await LegacyServerImportEngine.BuildDryRunPreviewAsync(options, config, fakeDb).ConfigureAwait(false);
        Assert.That(preview.TotalMapped, Is.GreaterThanOrEqualTo(6));
        Assert.That(preview.UnmappedWarnings.Any(w => w.Contains("Arca Battle", StringComparison.Ordinal)), Is.True);
        Assert.That(preview.UnmappedWarnings.Any(w => w.Contains("elemental", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(config.ExperienceRate, Is.EqualTo(1.0f)); // Dry-run is non-destructive

        var applyResult = await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, fakeDb).ConfigureAwait(false);
        Assert.That(File.Exists(applyResult.BackupFilePath), Is.True);
        Assert.That(config.ExperienceRate, Is.EqualTo(500.0f));
        Assert.That(config.DropItemGroups.Count, Is.EqualTo(1));

        var hanzo = config.Monsters.First(m => m.Number == 251);
        Assert.That(hanzo.MerchantStore?.Items.Count, Is.EqualTo(1));
        var lorencia = config.Maps.First(m => m.Number == 0);
        Assert.That(lorencia.MonsterSpawns.Count, Is.EqualTo(1));

        var importedAccount = (await context.GetAsync<Account>().ConfigureAwait(false)).First(a => a.LoginName == "legacyuser1");
        Assert.That(importedAccount.PasswordHash, Does.StartWith("$2"));
        Assert.That(BCrypt.Net.BCrypt.Verify("TempMigration#2026", importedAccount.PasswordHash), Is.True);
        Assert.That(importedAccount.Vault?.Money, Is.EqualTo(99999));
        Assert.That(importedAccount.Vault?.Items.Count, Is.EqualTo(1));
        var importedChar = importedAccount.Characters.First();
        Assert.That(importedChar.Inventory?.Items.Count, Is.EqualTo(1));

        var importedGuild = (await context.GetAsync<HNMO.DataModel.Entities.Guild>().ConfigureAwait(false)).First(g => g.Name == "OldGuild");
        Assert.That(importedGuild.Members.Count, Is.EqualTo(1));

        var rolledBack = await LegacyServerImportEngine.UndoBackupAsync(applyResult.BackupId, this._backupDir, context, config).ConfigureAwait(false);
        Assert.That(rolledBack, Is.True);
        Assert.That(config.ExperienceRate, Is.EqualTo(1.0f));
        Assert.That(config.DropItemGroups.Count, Is.Zero);
        Assert.That(lorencia.MonsterSpawns.Count, Is.Zero);
        Assert.That(hanzo.MerchantStore, Is.Null);
        Assert.That((await context.GetAsync<Account>().ConfigureAwait(false)).Any(a => a.LoginName == "legacyuser1"), Is.False);
        Assert.That((await context.GetAsync<HNMO.DataModel.Entities.Guild>().ConfigureAwait(false)).Any(g => g.Name == "OldGuild"), Is.False);
    }

    private static GameConfiguration CreateSyntheticConfig(HNMO.Persistence.IContext context)
    {
        var config = context.CreateNew<GameConfiguration>();
        foreach (var stat in new[] { Stats.Level, Stats.BaseStrength, Stats.BaseAgility, Stats.BaseVitality, Stats.BaseEnergy, Stats.BaseLeadership })
        {
            var def = context.CreateNew<AttributeDefinition>();
            def.Id = stat.Id;
            def.Designation = stat.Designation;
            config.Attributes.Add(def);
        }

        var map0 = context.CreateNew<GameMapDefinition>();
        map0.Number = 0;
        map0.Name = new LocalizedString("Lorencia");
        map0.IsActive = true;
        config.Maps.Add(map0);

        var dk = context.CreateNew<CharacterClass>();
        dk.Number = 16;
        dk.Name = new LocalizedString("Dark Knight");
        dk.HomeMap = map0;
        dk.IsActive = true;
        dk.CanGetCreated = true;
        config.CharacterClasses.Add(dk);

        var budge = context.CreateNew<MonsterDefinition>();
        budge.Number = 2;
        budge.Designation = new LocalizedString("Budge Dragon");
        config.Monsters.Add(budge);

        var hanzo = context.CreateNew<MonsterDefinition>();
        hanzo.Number = 251;
        hanzo.Designation = new LocalizedString("Hanzo the Blacksmith");
        hanzo.NpcWindow = NpcWindow.Merchant;
        config.Monsters.Add(hanzo);

        var sword = context.CreateNew<ItemDefinition>();
        sword.Group = 0;
        sword.Number = 1;
        sword.Name = new LocalizedString("Short Sword");
        sword.Durability = 22;
        sword.Width = 1;
        sword.Height = 3;
        config.Items.Add(sword);

        return config;
    }

    private sealed class StubDatabaseReader(LegacyDatabaseSnapshot snapshot) : ILegacyDatabaseReader
    {
        public Task<LegacyDatabaseSnapshot> ReadAsync(string connectionString, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}

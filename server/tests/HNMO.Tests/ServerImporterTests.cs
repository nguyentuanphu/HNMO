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
using HNMO.PlugIns;

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
        var goblin = context.CreateNew<MonsterDefinition>();
        goblin.Number = 238;
        goblin.Designation = new LocalizedString("Chaos Goblin");
        config.Monsters.Add(goblin);
        var plus10 = AddUpgradeCrafting(context, goblin, 3, 60);
        var plus15 = AddUpgradeCrafting(context, goblin, 50, 50);

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

        // The +10 and +15 rates are the base rates of the +10 and +15 upgrade craftings (3 and 50), not of recipes 10-15
        // (wings, pets, potions), and MixAddLuckRate is what luck adds to them. ZenDropMultipler is the master-level zen
        // bonus, not a zen multiplier, so it changes nothing.
        Assert.That((plus10.SuccessPercent, plus10.SuccessPercentageAdditionForLuck), Is.EqualTo(((byte)75, 20)));
        Assert.That((plus15.SuccessPercent, plus15.SuccessPercentageAdditionForLuck), Is.EqualTo(((byte)45, 20)));
        Assert.That(config.PlugInConfigurations.Any(p => p.TypeId == typeof(RateTuningPlugIn).GUID), Is.False);
        Assert.That(config.Monsters.First(m => m.Number == 2).DropItemGroups, Has.Count.EqualTo(1), "the monster's drops are the monster's");

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
        Assert.That(config.Monsters.First(m => m.Number == 2).DropItemGroups, Is.Empty);
        Assert.That((plus10.SuccessPercent, plus10.SuccessPercentageAdditionForLuck), Is.EqualTo(((byte)60, 25)));
        Assert.That(lorencia.MonsterSpawns.Count, Is.Zero);
        Assert.That(hanzo.MerchantStore, Is.Null);
        Assert.That((await context.GetAsync<Account>().ConfigureAwait(false)).Any(a => a.LoginName == "legacyuser1"), Is.False);
        Assert.That((await context.GetAsync<HNMO.DataModel.Entities.Guild>().ConfigureAwait(false)).Any(g => g.Name == "OldGuild"), Is.False);
    }

    /// <summary>
    /// The servers' ItemDropPer (the chance a killed monster drops an item) and ExcItemDropPer (excellent items, n/10000)
    /// set the chances of HNMO's common random item group and common excellent group; the undo puts them back. They were
    /// read for the preview but never applied.
    /// </summary>
    [Test]
    public async Task ItemAndExcellentDropRatesSetTheCommonDropGroupsAsync()
    {
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Data"));
        File.WriteAllText(Path.Combine(this._tempDir, "Data", "CommonServer.cfg"), "ItemDropPer = 60 ; 1[%]: MIN, 100[%]: MAX\nExcItemDropPer = 20 ; n/10000\n");
        var persistence = new InMemoryPersistenceContextProvider();
        var context = persistence.CreateNewContext();
        var config = CreateSyntheticConfig(context);
        var random = context.CreateNew<DropItemGroup>();
        random.ItemType = SpecialItemType.RandomItem;
        random.Chance = 0.3;
        var excellent = context.CreateNew<DropItemGroup>();
        excellent.ItemType = SpecialItemType.Excellent;
        excellent.Chance = 0.0001;
        config.DropItemGroups.Add(random);
        config.DropItemGroups.Add(excellent);

        var options = new LegacyImportOptions { FolderPath = this._tempDir };
        var preview = await LegacyServerImportEngine.BuildDryRunPreviewAsync(options, config, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);
        var result = await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(preview.Rates?.ExcellentItemDropPerTenThousand, Is.EqualTo(20));
            Assert.That(random.Chance, Is.EqualTo(0.6).Within(1e-9));
            Assert.That(excellent.Chance, Is.EqualTo(0.002).Within(1e-9));
        });

        Assert.That(await LegacyServerImportEngine.UndoBackupAsync(result.BackupId, this._backupDir, context, config).ConfigureAwait(false), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(random.Chance, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(excellent.Chance, Is.EqualTo(0.0001).Within(1e-9));
        });
    }

    /// <summary>
    /// HNMO's configuration has several maps with the same number (Devil Square 1-4 are all map 9, 5-7 all map 32): the
    /// preview and the import work with it, and a spawn of such a number goes to the first map with it. The preview
    /// threw "An item with the same key has already been added. Key: 9" on every HNMO configuration.
    /// </summary>
    [Test]
    public async Task PreviewAndImportWorkWithMapsSharingANumberAsync()
    {
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Data", "Monster", "Spawns"));
        File.WriteAllText(Path.Combine(this._tempDir, "Data", "CommonServer.cfg"), "ItemDropPer = 60\n");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Data", "Monster", "Spawns", "MonsterSetBase.txt"),
            "1\n2 9 10 120 120 140 140 -1 5\nend\n");

        var persistence = new InMemoryPersistenceContextProvider();
        var context = persistence.CreateNewContext();
        var config = CreateSyntheticConfig(context);
        var devilSquares = new System.Collections.Generic.List<GameMapDefinition>();
        for (var i = 1; i <= 3; i++)
        {
            var square = context.CreateNew<GameMapDefinition>();
            square.Number = 9;
            square.Name = new LocalizedString($"Devil Square {i}");
            square.IsActive = true;
            config.Maps.Add(square);
            devilSquares.Add(square);
        }

        var options = new LegacyImportOptions { FolderPath = this._tempDir };
        var preview = await LegacyServerImportEngine.BuildDryRunPreviewAsync(options, config, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);
        Assert.That(preview.TotalMapped, Is.GreaterThanOrEqualTo(1));

        await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(devilSquares[0].MonsterSpawns.Count, Is.EqualTo(1), "the spawn of map 9 goes to the first map numbered 9");
            Assert.That(devilSquares[1].MonsterSpawns, Is.Empty);
            Assert.That(devilSquares[2].MonsterSpawns, Is.Empty);
        });
    }

    /// <summary>
    /// A "Monster_(N)" item bag is what monster N drops, with the bag's ItemRate as the chance, and the group is the
    /// monster's own (a group only in the configuration's list is never rolled). The other bags (boxes, event and mix
    /// rewards) are not monster drops: they made every kill drop an item.
    /// </summary>
    [Test]
    public async Task ItemBagsBecomeTheMonstersDropsAndRewardBagsAreLeftOutAsync()
    {
        var bags = Path.Combine(this._tempDir, "IGCData", "ItemBags");
        Directory.CreateDirectory(bags);
        const string Pool = "<DropAllow DW=\"1\"><Drop Rate=\"10000\" Count=\"1\"><Item Cat=\"0\" Index=\"1\" ItemMinLevel=\"0\" Exc=\"0\" /></Drop></DropAllow>";
        File.WriteAllText(Path.Combine(bags, "Monster_(2)_Budge_Dragon.xml"), $"<ItemBag><BagConfig ItemRate=\"1000\" />{Pool}</ItemBag>");
        File.WriteAllText(Path.Combine(bags, "Mix_LuckyCoin(30)_Reward.xml"), $"<ItemBag><BagConfig ItemRate=\"10000\" />{Pool}</ItemBag>");

        var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = CreateSyntheticConfig(context);
        var options = new LegacyImportOptions { FolderPath = this._tempDir };
        var preview = await LegacyServerImportEngine.BuildDryRunPreviewAsync(options, config, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);
        await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);

        var budge = config.Monsters.First(m => m.Number == 2);
        Assert.Multiple(() =>
        {
            Assert.That(preview.UnmappedWarnings.Any(w => w.Contains("Mix_LuckyCoin(30)_Reward.xml", StringComparison.Ordinal)), Is.True);
            Assert.That(config.DropItemGroups, Has.Count.EqualTo(1));
            Assert.That(budge.DropItemGroups, Has.Count.EqualTo(1));
            Assert.That(budge.DropItemGroups.Single().Chance, Is.EqualTo(0.1).Within(1e-9));
            Assert.That(config.Maps.Single().DropItemGroups, Is.Empty, "no drop of every monster on the map");
        });
    }

    /// <summary>
    /// The spawns a map already has are not added again; the event and invasion spawns (groups 3 and 4), the castle
    /// siege copy of the list and the copies in the suite's Tools folder are not imported; the facing direction is kept.
    /// </summary>
    [Test]
    public async Task SpawnsAreNotDoubledAsync()
    {
        var spawns = Path.Combine(this._tempDir, "Data", "Monster", "Spawns");
        Directory.CreateDirectory(spawns);
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Tools", "MSB"));
        const string List = "0\n251 0 0 116 141 3\nend\n1\n2 0 30 120 120 140 140 -1 5\n2 9 30 119 80 150 115 -1 35\nend\n2\n2 0 30 60 60 -1\nend\n4\n2 0 30 13 45 0\nend\n3\n2 0 30 10 10 240 240 -1 2\nend\n";
        File.WriteAllText(Path.Combine(spawns, "MonsterSetBase.txt"), List);
        File.WriteAllText(Path.Combine(spawns, "MonsterSetBase_CS.txt"), List);
        File.WriteAllText(Path.Combine(this._tempDir, "Tools", "MSB", "MonsterSetBase.txt"), List);

        var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = CreateSyntheticConfig(context);
        var lorencia = config.Maps.Single();
        var existing = context.CreateNew<MonsterSpawnArea>();
        existing.GameMap = lorencia;
        existing.MonsterDefinition = config.Monsters.First(m => m.Number == 2);
        existing.X1 = 121;
        existing.Y1 = 119;
        existing.X2 = 141;
        existing.Y2 = 140;
        existing.Quantity = 5;
        lorencia.MonsterSpawns.Add(existing);

        // Devil Square: its monsters come in waves with the event.
        var devilSquare = context.CreateNew<GameMapDefinition>();
        devilSquare.Number = 9;
        devilSquare.Name = new LocalizedString("Devil Square");
        config.Maps.Add(devilSquare);
        var wave = context.CreateNew<MonsterSpawnArea>();
        wave.GameMap = devilSquare;
        wave.MonsterDefinition = existing.MonsterDefinition;
        wave.SpawnTrigger = SpawnTrigger.ManuallyForEvent;
        devilSquare.MonsterSpawns.Add(wave);

        var options = new LegacyImportOptions { FolderPath = this._tempDir };
        await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);

        // The map had the field spawn; Hanzo and the single Budge Dragon are new. Devil Square keeps its waves only.
        Assert.That(lorencia.MonsterSpawns, Has.Count.EqualTo(3));
        Assert.That(devilSquare.MonsterSpawns, Has.Count.EqualTo(1));
        var hanzo = lorencia.MonsterSpawns.Single(s => s.MonsterDefinition?.Number == 251);
        Assert.That((hanzo.X1, hanzo.Y1, hanzo.Direction), Is.EqualTo(((byte)116, (byte)141, Direction.SouthEast)));
    }

    /// <summary>
    /// The servers' bonus windows without a map are server-wide happy hours: they go to the bonus events (they went to
    /// the timetable, which only shows them), last from StartHour to EndHour, and their ItemDropBonus, percentage points
    /// added to ItemDropPer, becomes the matching share of the drops. Importing again does not add them twice.
    /// </summary>
    [Test]
    public async Task BonusWindowsBecomeServerWideBonusEventsAsync()
    {
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Data"));
        File.WriteAllText(Path.Combine(this._tempDir, "Data", "CommonServer.cfg"), "ItemDropPer = 50\n");
        File.WriteAllText(
            Path.Combine(this._tempDir, "IGC_BonusEvent.xml"),
            "<BonusEvent Enable=\"1\"><Event DayOfWeek=\"-1\" StartHour=\"20\" EndHour=\"22\" ExpMultiplier=\"1.5\" ItemDropBonus=\"15\" /><Event DayOfWeek=\"-1\" StartHour=\"23\" EndHour=\"1\" ExpMultiplier=\"2\" ItemDropBonus=\"0\" /></BonusEvent>");

        var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = CreateSyntheticConfig(context);
        var options = new LegacyImportOptions { FolderPath = this._tempDir };
        await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);
        await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);

        var bonus = config.PlugInConfigurations.Single(p => p.TypeId == typeof(BonusEventsPlugIn).GUID).GetConfiguration<BonusEventsConfiguration>(null)!;
        Assert.That(bonus.Events, Has.Count.EqualTo(2));
        var evening = bonus.Events.Single(e => e.StartTimeSydney == "20:00");
        Assert.Multiple(() =>
        {
            Assert.That(evening.MapNumber, Is.Null);
            Assert.That(evening.DurationMinutes, Is.EqualTo(120));
            Assert.That(evening.ExpBonusPercent, Is.EqualTo(50));
            Assert.That(evening.ItemDropBonusPercent, Is.EqualTo(30));
            Assert.That(bonus.Events.Single(e => e.StartTimeSydney == "23:00").DurationMinutes, Is.EqualTo(120));
        });
    }

    /// <summary>
    /// A shop's items take the first free place of the 8-column grid they fit in (they got slots 0, 1, 2 ... and
    /// overlapped), keep their luck and option, and the game masters' test shop is not imported.
    /// </summary>
    [Test]
    public async Task ShopItemsArePlacedOnTheGridWithTheirOptionsAsync()
    {
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Shops"));
        File.WriteAllText(
            Path.Combine(this._tempDir, "IGC_ShopList.xml"),
            "<ShopList><Shop NPCIndex=\"251\" MapNumber=\"0\" GMShop=\"1\" FileName=\"Test.xml\" /><Shop NPCIndex=\"251\" MapNumber=\"0\" GMShop=\"0\" FileName=\"Hanzo.xml\" /></ShopList>");
        var nineSwords = string.Concat(Enumerable.Repeat("<Item Cat=\"0\" Index=\"1\" Level=\"0\" Durability=\"0\" Skill=\"0\" Luck=\"1\" Option=\"1\" Exc=\"0\" />", 9));
        File.WriteAllText(Path.Combine(this._tempDir, "Shops", "Hanzo.xml"), $"<Shop>{nineSwords}</Shop>");
        File.WriteAllText(Path.Combine(this._tempDir, "Shops", "Test.xml"), "<Shop><Item Cat=\"0\" Index=\"1\" /></Shop>");

        var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = CreateSyntheticConfig(context);
        var sword = config.Items.Single();
        var optionDefinition = context.CreateNew<ItemOptionDefinition>();
        foreach (var type in new[] { ItemOptionTypes.Luck, ItemOptionTypes.Option })
        {
            var option = context.CreateNew<IncreasableItemOption>();
            option.OptionType = type;
            optionDefinition.PossibleOptions.Add(option);
        }

        sword.PossibleItemOptions.Add(optionDefinition);

        var options = new LegacyImportOptions { FolderPath = this._tempDir };
        await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);

        var store = config.Monsters.First(m => m.Number == 251).MerchantStore!;
        Assert.Multiple(() =>
        {
            Assert.That(store.Items.Select(i => (int)i.ItemSlot).Order(), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 24 }));
            Assert.That(store.Items.All(i => i.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck)), Is.True);
            Assert.That(store.Items.All(i => i.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Option && o.Level == 1)), Is.True);
        });
    }

    /// <summary>The servers' Jewel of Soul and Jewel of Life rates go to the rate tuning.</summary>
    [Test]
    public async Task JewelRatesGoToTheRateTuningAsync()
    {
        File.WriteAllText(Path.Combine(this._tempDir, "IGC_Common.ini"), "UseSoulRate = 55\nUseSoulWithLuckRate = 75\nUseLifeRate = 45\nZenDropMultipler = 1.0\n");
        var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = CreateSyntheticConfig(context);
        var options = new LegacyImportOptions { FolderPath = this._tempDir };
        await LegacyServerImportEngine.ApplyImportAsync(options, context, config, this._backupDir, new StubDatabaseReader(new LegacyDatabaseSnapshot())).ConfigureAwait(false);

        var tuning = config.PlugInConfigurations.Single(p => p.TypeId == typeof(RateTuningPlugIn).GUID).GetConfiguration<RateTuningConfiguration>(null)!;
        Assert.Multiple(() =>
        {
            Assert.That((tuning.OverrideSoulRates, tuning.SoulSuccessRatePercent, tuning.SoulLuckBonusPercent), Is.EqualTo((true, 55, 20)));
            Assert.That((tuning.OverrideLifeRate, tuning.LifeSuccessRatePercent), Is.EqualTo((true, 45)));
            Assert.That(tuning.ZenDropMultiplierPercent, Is.EqualTo(100));
        });
    }

    private static HNMO.DataModel.Configuration.ItemCrafting.SimpleCraftingSettings AddUpgradeCrafting(HNMO.Persistence.IContext context, MonsterDefinition goblin, byte number, byte successPercent)
    {
        var crafting = context.CreateNew<HNMO.DataModel.Configuration.ItemCrafting.ItemCrafting>();
        crafting.Number = number;
        crafting.SimpleCraftingSettings = context.CreateNew<HNMO.DataModel.Configuration.ItemCrafting.SimpleCraftingSettings>();
        crafting.SimpleCraftingSettings.SuccessPercent = successPercent;
        crafting.SimpleCraftingSettings.SuccessPercentageAdditionForLuck = 25;
        goblin.ItemCraftings.Add(crafting);
        return crafting.SimpleCraftingSettings;
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

    /// <summary>Verifies the Season 6 and Season 9 XML/DAT/INI configuration schemas (BonusEvent multipliers, SetItemDropManager, ItemBags, InvasionManager, IllusionTemple Monute typo, MonsterSpawn XML).</summary>
    /// <summary>
    /// The servers' databases keep 32 bytes per item slot (inventories of 7,584 bytes, vaults of 7,680); read as 16-byte
    /// slots, every item after the first came out at the wrong slot and with another item's bytes. Older 16-byte blobs
    /// stay 16.
    /// </summary>
    [Test]
    public void ReadsThirtyTwoByteItemSlots()
    {
        var blob = Enumerable.Repeat((byte)0xFF, 7584).ToArray();
        void Put(int slot, byte number, byte group, byte durability)
        {
            var at = slot * 32;
            Array.Clear(blob, at, 32);
            blob[at] = number;
            blob[at + 1] = (3 << 3) | 0x04; // +3, luck
            blob[at + 2] = durability;
            blob[at + 9] = (byte)(group << 4);
        }

        Put(0, 1, 0, 22);   // Short Sword in slot 0
        Put(12, 5, 14, 3);  // a potion in the bag's first slot

        Assert.That(LegacyFileParsers.ItemSlotSizeOf(blob), Is.EqualTo(32));
        var items = LegacyFileParsers.ParseLegacyBinaryStorage(blob, LegacyFileParsers.ItemSlotSizeOf(blob));
        Assert.That(items.Select(i => (i.Slot, i.Group, i.Number, i.Durability)), Is.EqualTo(new (byte, byte, short, byte)[] { (0, 0, 1, 22), (12, 14, 5, 3) }));

        Assert.Multiple(() =>
        {
            Assert.That(LegacyFileParsers.ItemSlotSizeOf(new byte[7680]), Is.EqualTo(32), "a vault of 240 slots of 32 bytes");
            Assert.That(LegacyFileParsers.ItemSlotSizeOf(new byte[3776]), Is.EqualTo(16), "a 16-byte inventory of 236 slots");
            Assert.That(LegacyFileParsers.ItemSlotSizeOf(new byte[1920]), Is.EqualTo(16), "a 16-byte vault of 120 slots");
            Assert.That(LegacyFileParsers.ItemSlotSizeOf(null), Is.EqualTo(16));
        });
    }

    [Test]
    public void ParsesSeasonSixAndNineConfigurationSchemas()
    {
        Directory.CreateDirectory(Path.Combine(this._tempDir, "ItemBags"));
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Events"));
        Directory.CreateDirectory(Path.Combine(this._tempDir, "Monsters"));

        File.WriteAllText(
            Path.Combine(this._tempDir, "Server_SetItemDropManager.xml"),
            "<SetItemDropManager DropUseRate=\"10000\"><Rule MonsterIndex=\"2\" MonsterMinLevel=\"1\" MonsterMaxLevel=\"150\" MapNumber=\"31\"><ItemDrop Cat=\"0\" Index=\"1\" MinLevel=\"0\" MaxLevel=\"0\" Exc=\"0\" DropRate=\"500\" /></Rule></SetItemDropManager>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "ItemBags", "Item_Kundun_Box+1.xml"),
            "<ItemBag><BagConfig Name=\"Box\" ItemRate=\"10000\" /><DropAllow MapNumber=\"-1\"><Drop Rate=\"2500\" Count=\"1\"><Item Cat=\"0\" Index=\"1\" ItemMinLevel=\"3\" Skill=\"1\" Luck=\"1\" Option=\"1\" Exc=\"-1\" SetItem=\"0\" /></Drop></DropAllow></ItemBag>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Events", "Server_BonusEvent.xml"),
            "<BonusEvent Enable=\"1\"><Event DayOfWeek=\"-1\" StartHour=\"14\" EndHour=\"16\" ExpMultiplier=\"1.75\" MasterExpMultiplier=\"1.5\" ItemDropBonus=\"25\" /></BonusEvent>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Events", "Server_InvasionManager.xml"),
            "<InvasionManager Enable=\"1\"><Invasion Type=\"0\" Name=\"Red Dragon Invasion\"><Start DayOfWeek=\"-1\" Hour=\"13\" Minute=\"0\" /><Start DayOfWeek=\"-1\" Hour=\"23\" Minute=\"30\" /></Invasion></InvasionManager>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Events", "Server_IllusionTemple_Renewal.xml"),
            "<IllusionTempleRenewal Enable=\"1\"><EventSchedule><Start Hour=\"1\" Monute=\"15\" /><Start Hour=\"10\" Minute=\"30\" /></EventSchedule></IllusionTempleRenewal>");
        File.WriteAllText(
            Path.Combine(this._tempDir, "Monsters", "Server_MonsterSpawn.xml"),
            "<MonsterSpawn><Map Number=\"0\" Name=\"Lorencia\"><Spot Type=\"1\"><Spawn Index=\"2\" StartX=\"100\" StartY=\"100\" EndX=\"120\" EndY=\"120\" Dir=\"3\" Count=\"4\" /></Spot><Spot Type=\"3\"><Spawn Index=\"598\" StartX=\"80\" StartY=\"80\" Dir=\"1\" Element=\"1\" /></Spot></Map></MonsterSpawn>");

        var drops = LegacyFileParsers.ParseDropLists(this._tempDir);
        Assert.That(drops.Any(d => d.IsAncient && d.MapNumber == 31), Is.True);
        Assert.That(drops.Any(d => d.MonsterNumber == -1 && d.MinItemLevel == 3 && d.IsExcellent), Is.True);

        var events = LegacyFileParsers.ParseEventSchedules(this._tempDir);
        var bonus = events.Single(e => e.EventId == "imported-bonus-1");
        Assert.That(bonus.ExpBonusPercent, Is.EqualTo(75));
        Assert.That(bonus.DropBonusPercent, Is.EqualTo(25));
        Assert.That(bonus.ScheduleTimes, Is.EqualTo("14:00"));
        var inv = events.Single(e => e.Name == "Red Dragon Invasion");
        Assert.That(inv.ScheduleTimes, Is.EqualTo("13:00, 23:30"));
        var it = events.Single(e => e.EventId == "s9-illusion-temple-renewal");
        Assert.That(it.ScheduleTimes, Is.EqualTo("01:15, 10:30"));

        var spawns = LegacyFileParsers.ParseMonsterSpawns(this._tempDir);
        Assert.That(spawns.Count, Is.EqualTo(2));
        Assert.That(spawns[0].SpawnGroup, Is.EqualTo(1));
        Assert.That(spawns[0].Quantity, Is.EqualTo(4));
        Assert.That(spawns[1].SpawnGroup, Is.EqualTo(5));
    }

    private sealed class StubDatabaseReader(LegacyDatabaseSnapshot snapshot) : ILegacyDatabaseReader
    {
        public Task<LegacyDatabaseSnapshot> ReadAsync(string connectionString, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}

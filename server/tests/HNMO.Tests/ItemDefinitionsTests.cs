// <copyright file="ItemDefinitionsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.ItemDefinitions;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;

/// <summary>
/// Unit tests for <see cref="ItemDefinitionStateStore"/> and <see cref="ItemClientDataSync"/>.
/// </summary>
[TestFixture]
public class ItemDefinitionsTests
{
    private string? _statePath;

    /// <summary>Sets up an isolated temporary save file for <see cref="HNMO.GameLogic.PlugIns.HnmoStateStore"/>.</summary>
    [SetUp]
    public void SetUp()
    {
        this._statePath = Path.Combine(Path.GetTempPath(), $"hnmo-items-state-{Guid.NewGuid():N}.json");
        ItemDefinitionStateStore.FilePath = this._statePath;
    }

    /// <summary>Cleans up temporary test state.</summary>
    [TearDown]
    public void TearDown()
    {
        ItemDefinitionStateStore.FilePath = ItemDefinitionStateStore.DefaultFilePath;
        if (this._statePath is not null && File.Exists(this._statePath))
        {
            File.Delete(this._statePath);
        }
    }

    /// <summary>Verifies glow color normalization for preset names, hex codes, and invalid values.</summary>
    [Test]
    public void NormalizesGlowColorsCorrectly()
    {
        Assert.That(ItemClientDataSync.NormalizeGlowColor(" GOLD "), Is.EqualTo("gold"));
        Assert.That(ItemClientDataSync.NormalizeGlowColor("#FF8800"), Is.EqualTo("#ff8800"));
        Assert.That(ItemClientDataSync.NormalizeGlowColor("invalid-color"), Is.EqualTo(string.Empty));
    }

    /// <summary>Verifies server <see cref="ItemDefinition"/> updates preserve existing non-target translations and sync requirements.</summary>
    [Test]
    public void AppliesAndExtractsMetadataOnItemDefinition()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var context = persistence.CreateNewContext();
        var config = context.CreateNew<GameConfiguration>();
        foreach (var stat in new[] { Stats.Level, Stats.TotalStrength, Stats.TotalAgility, Stats.TotalVitality, Stats.TotalEnergy, Stats.TotalLeadership })
        {
            var def = context.CreateNew<AttributeDefinition>();
            def.Id = stat.Id;
            def.Designation = stat.Designation;
            config.Attributes.Add(def);
        }

        var item = context.CreateNew<ItemDefinition>();
        item.Group = 0;
        item.Number = 0;
        item.Width = 1;
        item.Height = 2;
        item.DropLevel = 6;
        item.Name = new LocalizedString("Kris||de=KrisDe");
        config.Items.Add(item);

        var edit = new ItemClientMetadata
        {
            Group = 0,
            Number = 0,
            NameEn = "Crimson Kris",
            NameVi = "Dao Găm Đỏ",
            Width = 2,
            Height = 3,
            DropLevel = 25,
            MaximumItemLevel = 13,
            Durability = 40,
            BuyPrice = 55000,
            LevelRequirement = 120,
            StrengthRequirement = 240,
            AgilityRequirement = 180,
            GlowColor = "#ff4411",
            TooltipTextEn = "Boss forged blade",
            TooltipTextVi = "Vũ khí rèn từ Boss",
        };

        ItemDefinitionStateStore.ApplyToDefinition(item, edit, config, context);
        ItemDefinitionStateStore.Upsert(edit);

        Assert.That(item.Width, Is.EqualTo(2));
        Assert.That(item.Height, Is.EqualTo(3));
        Assert.That(item.DropLevel, Is.EqualTo(25));
        Assert.That(item.Name.ValueInNeutralLanguage, Is.EqualTo("Crimson Kris"));
        Assert.That(item.Name.GetTranslation(CultureInfo.GetCultureInfo("vi"), fallbackToNeutral: false), Is.EqualTo("Dao Găm Đỏ"));
        Assert.That(item.Name.GetTranslation(CultureInfo.GetCultureInfo("de"), fallbackToNeutral: false), Is.EqualTo("KrisDe"));

        var loaded = ItemDefinitionStateStore.GetAll();
        Assert.That(loaded, Has.Count.EqualTo(1));
        var preview = ItemDefinitionStateStore.FromDefinition(item, loaded[0]);
        Assert.That(preview.StrengthRequirement, Is.EqualTo(240));
        Assert.That(preview.AgilityRequirement, Is.EqualTo(180));
        Assert.That(preview.LevelRequirement, Is.EqualTo(120));
        Assert.That(preview.TooltipTextVi, Is.EqualTo("Vũ khí rèn từ Boss"));

        edit.AgilityRequirement = 0;
        ItemDefinitionStateStore.ApplyToDefinition(item, edit, config, context);
        Assert.That(item.Requirements.Any(r => r.Attribute?.Id == Stats.TotalAgility.Id), Is.False);
    }

    /// <summary>Verifies JSON sync and ZIP export write valid EN/VI item, model glow, and GlowColors files.</summary>
    [Test]
    public void SyncsDataDirectoryAndBuildsDesktopExportZip()
    {
        var tempData = Path.Combine(Path.GetTempPath(), $"hnmo-itemdata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempData, "Items", "Models"));
        Directory.CreateDirectory(Path.Combine(tempData, "Effects"));
        try
        {
            File.WriteAllText(
                Path.Combine(tempData, "Effects", "GlowColors.json"),
                """{"formatVersion":1,"colors":{"gold":[1,0.7,0.2]}}""");
            File.WriteAllText(
                Path.Combine(tempData, "Items", "Group00_Sword.json"),
                """{"formatVersion":1,"group":0,"items":[{"number":0,"name":{"en":"Kris","de":"KrisDe"},"width":1,"height":2,"level":6}]}""");
            File.WriteAllText(
                Path.Combine(tempData, "Items", "Models", "Group00_Sword.json"),
                """{"formatVersion":1,"group":0,"models":[{"number":0,"file":"Data/Item/Sword01.bmd","textureFolders":["Item"]}]}""");

            var overrides = new List<ItemClientMetadata>
            {
                new()
                {
                    Group = 0,
                    Number = 0,
                    NameEn = "Sacred Kris",
                    NameVi = "Kris Thần",
                    Width = 2,
                    Height = 2,
                    DropLevel = 42,
                    StrengthRequirement = 300,
                    GlowColor = "#ff2288",
                    TooltipTextEn = "Admin forged",
                    TooltipTextVi = "Rèn bởi Admin",
                },
                new()
                {
                    Group = 0,
                    Number = 99,
                    NameEn = "Void Blade",
                    NameVi = "Kiếm Hư Không",
                    Width = 1,
                    Height = 4,
                    DropLevel = 80,
                    GlowColor = "gold",
                    ModelSourceGroup = 0,
                    ModelSourceNumber = 0,
                },
            };

            var written = ItemClientDataSync.ApplyOverridesToDataDirectory(tempData, overrides);
            Assert.That(written, Does.Contain("Effects/GlowColors.json"));
            Assert.That(written, Does.Contain("Items/Group00_Sword.json"));
            Assert.That(written, Does.Contain("Items/Models/Group00_Sword.json"));

            var swordRoot = JsonNode.Parse(File.ReadAllText(Path.Combine(tempData, "Items", "Group00_Sword.json")))!.AsObject();
            var items = swordRoot["items"]!.AsArray();
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0]!["name"]!["vi"]!.GetValue<string>(), Is.EqualTo("Kris Thần"));
            Assert.That(items[0]!["name"]!["de"]!.GetValue<string>(), Is.EqualTo("KrisDe"));
            Assert.That(items[0]!["tooltip"]!["vi"]!.GetValue<string>(), Is.EqualTo("Rèn bởi Admin"));

            var modelRoot = JsonNode.Parse(File.ReadAllText(Path.Combine(tempData, "Items", "Models", "Group00_Sword.json")))!.AsObject();
            var models = modelRoot["models"]!.AsArray();
            Assert.That(models.Count, Is.EqualTo(2));
            Assert.That(models[0]!["glow"]!["color"]!.GetValue<string>(), Is.EqualTo("custom_ff2288"));
            Assert.That(models[1]!["file"]!.GetValue<string>(), Is.EqualTo("Data/Item/Sword01.bmd"));

            var zipBytes = ItemClientDataSync.BuildExportZip(tempData, overrides);
            using var ms = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.That(zip.GetEntry("Data/Items/Group00_Sword.json"), Is.Not.Null);
            Assert.That(zip.GetEntry("Data/Items/Models/Group00_Sword.json"), Is.Not.Null);
            Assert.That(zip.GetEntry("Data/Effects/GlowColors.json"), Is.Not.Null);
            Assert.That(zip.GetEntry("Data/Items/hnmo-item-overrides.json"), Is.Not.Null);
        }
        finally
        {
            if (Directory.Exists(tempData))
            {
                Directory.Delete(tempData, recursive: true);
            }
        }
    }
}

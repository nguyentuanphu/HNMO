// <copyright file="HnmoSaveFolderTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// <see cref="HnmoSaveFolder"/>: every runtime state file lives in the HNMO folder's <c>save</c> folder, and the files
/// older versions wrote into the <c>server</c> folder are moved there once, merged, with copies in <c>save/old/</c>.
/// </summary>
[TestFixture]
public class HnmoSaveFolderTests
{
    private string _root = string.Empty;

    private string _server = string.Empty;

    private string _save = string.Empty;

    private HnmoSaveFolder _folder = null!;

    /// <summary>Creates an HNMO folder with an empty <c>server</c> and <c>save</c> folder.</summary>
    [SetUp]
    public void SetUp()
    {
        this._root = Path.Combine(Path.GetTempPath(), $"hnmo-save-folder-{Guid.NewGuid():N}");
        this._server = Path.Combine(this._root, "server");
        this._save = Path.Combine(this._root, "save");
        Directory.CreateDirectory(this._server);
        Directory.CreateDirectory(this._save);
        this._folder = new HnmoSaveFolder(this._save, [this._server], this._server);
    }

    /// <summary>Deletes the folder.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._root))
        {
            Directory.Delete(this._root, recursive: true);
        }
    }

    /// <summary>The configured paths of the plug-ins all lead into the save folder; an absolute path elsewhere stays.</summary>
    [Test]
    public void ConfiguredPathsLeadIntoTheSaveFolder()
    {
        var admin = Path.Combine(this._root, "elsewhere", "my-state.json");
        Assert.Multiple(() =>
        {
            Assert.That(this.Resolve("hnmo-state.json", "s6"), Is.EqualTo(Path.Combine(this._save, "hnmo-state.json")));
            Assert.That(this.Resolve("save/hnmo-state.json", "s6"), Is.EqualTo(Path.Combine(this._save, "hnmo-state.json")));
            Assert.That(this.Resolve("save/hnmo-escrow.json", "s6"), Is.EqualTo(Path.Combine(this._save, "hnmo-escrow.json")));
            Assert.That(this.Resolve("../save/onboarding.json", "s6"), Is.EqualTo(Path.Combine(this._save, "hnmo-state.json")));
            Assert.That(this.Resolve(Path.Combine(this._server, "save", "hnmo-state.json"), "s6"), Is.EqualTo(Path.Combine(this._save, "hnmo-state.json")));
            Assert.That(this.Resolve(Path.Combine(this._server, "gift-codes.json"), "s6"), Is.EqualTo(Path.Combine(this._save, "hnmo-state.json")));
            Assert.That(this.Resolve(admin, "s6"), Is.EqualTo(admin));
            Assert.That(this.Resolve(Path.Combine(this._server, "custom.json"), "s6"), Is.EqualTo(Path.Combine(this._server, "custom.json")), "an absolute path an admin chose stays");
            Assert.That(this._folder.MapPath("hnmo-worlds.json"), Is.EqualTo(Path.Combine(this._save, "hnmo-worlds.json")));
        });
    }

    /// <summary>Season 9 and the Arena keep their files in <c>save/s9/</c> and <c>save/arena/</c>.</summary>
    [Test]
    public void SeasonNineAndTheArenaKeepTheirOwnFolders()
    {
        Assert.Multiple(() =>
        {
            Assert.That(this.Resolve("hnmo-state.json", "s9"), Is.EqualTo(Path.Combine(this._save, "s9", "hnmo-state.json")));
            Assert.That(this.Resolve("save/hnmo-qol.json", "s9"), Is.EqualTo(Path.Combine(this._save, "s9", "hnmo-qol.json")));
            Assert.That(this.Resolve("hnmo-state.json", "arena"), Is.EqualTo(Path.Combine(this._save, "arena", "hnmo-state.json")));
            Assert.That(this.Resolve(this.Resolve("hnmo-state.json", "s9")!, "s9"), Is.EqualTo(Path.Combine(this._save, "s9", "hnmo-state.json")), "resolving twice changes nothing");
        });
    }

    /// <summary>A fresh install writes its state into the save folder and nothing into the server folder.</summary>
    [Test]
    public void AFreshInstallWritesIntoTheSaveFolder()
    {
        Assert.That(this._folder.MoveOldStateFiles("s6"), Is.Empty);

        var store = new HnmoStateStore(this.Resolve("hnmo-state.json", "s6")!);
        store.WriteSection("jewelBank", new { accounts = 1 }, new JsonSerializerOptions());

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(this._save, "hnmo-state.json")), Is.True);
            Assert.That(Directory.GetFiles(this._server, "*", SearchOption.AllDirectories), Is.Empty);
        });
    }

    /// <summary>
    /// The state of an older version in <c>server/</c> and <c>server/save/</c> is moved into the save folder: sections
    /// only the old file has are taken, sections both have keep the save folder's, every original is copied to
    /// <c>save/old/</c>, and a second start changes nothing.
    /// </summary>
    [Test]
    public void AnOlderInstallIsMovedOnceAndNothingIsLost()
    {
        WriteState(Path.Combine(this._save, "hnmo-state.json"), new() { ["onboarding"] = "new", ["antiCheat"] = "save" });
        WriteState(Path.Combine(this._server, "hnmo-state.json"), new() { ["jewelBank"] = "server", ["onboarding"] = "old" });
        WriteState(Path.Combine(this._server, "save", "hnmo-state.json"), new() { ["gmPermissions"] = "server-save", ["jewelBank"] = "server-save" });
        WriteState(Path.Combine(this._server, "save", "hnmo-escrow.json"), new() { ["escrow"] = "contracts" });
        File.WriteAllText(Path.Combine(this._server, "hnmo-worlds.json"), """[{"id":1}]""");
        File.WriteAllText(Path.Combine(this._server, "hnmo-state.json.bak"), "backup");

        var log = this._folder.MoveOldStateFiles("s6");

        var merged = ReadState(Path.Combine(this._save, "hnmo-state.json"));
        Assert.Multiple(() =>
        {
            Assert.That(merged["onboarding"], Is.EqualTo("new"), "a section both have keeps the save folder's");
            Assert.That(merged["antiCheat"], Is.EqualTo("save"));
            Assert.That(merged["jewelBank"], Is.EqualTo("server"), "server/hnmo-state.json is merged before server/save/hnmo-state.json");
            Assert.That(merged["gmPermissions"], Is.EqualTo("server-save"));
            Assert.That(ReadState(Path.Combine(this._save, "hnmo-escrow.json"))["escrow"], Is.EqualTo("contracts"));
            Assert.That(File.ReadAllText(Path.Combine(this._save, "hnmo-worlds.json")), Is.EqualTo("""[{"id":1}]"""));
            Assert.That(Directory.GetFiles(this._server, "*", SearchOption.AllDirectories), Is.Empty, "nothing is left in the server folder");
            Assert.That(ReadState(Path.Combine(this._save, "old", "server", "hnmo-state.json"))["onboarding"], Is.EqualTo("old"));
            Assert.That(ReadState(Path.Combine(this._save, "old", "server", "save", "hnmo-state.json"))["jewelBank"], Is.EqualTo("server-save"));
            Assert.That(File.Exists(Path.Combine(this._save, "old", "server", "save", "hnmo-escrow.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(this._save, "old", "server", "hnmo-worlds.json")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(this._save, "old", "server", "hnmo-state.json.bak")), Is.EqualTo("backup"));
            Assert.That(log, Has.Some.Contains("Section 'jewelBank': taken from"));
            Assert.That(log, Has.Some.Contains("Section 'onboarding': kept the one of"));
            Assert.That(log, Has.Some.Contains("Section 'jewelBank': kept the one of"));
        });

        var before = Snapshot(this._root);
        Assert.Multiple(() =>
        {
            Assert.That(this._folder.MoveOldStateFiles("s6"), Is.Empty);
            Assert.That(Snapshot(this._root), Is.EqualTo(before), "a second start changes nothing");
        });
    }

    /// <summary>Season 9 and the Arena move their own folders only, into <c>save/s9/</c> and <c>save/arena/</c>.</summary>
    [Test]
    public void SeasonNineAndTheArenaMoveTheirOwnFiles()
    {
        WriteState(Path.Combine(this._server, "hnmo-state.json"), new() { ["jewelBank"] = "s6" });
        WriteState(Path.Combine(this._server, "s9", "hnmo-state.json"), new() { ["jewelBank"] = "s9" });
        WriteState(Path.Combine(this._server, "save", "s9", "hnmo-qol.json"), new() { ["qol"] = "s9" });
        WriteState(Path.Combine(this._server, "arena", "hnmo-state.json"), new() { ["arena"] = "arena" });

        this._folder.MoveOldStateFiles("s9");
        Assert.Multiple(() =>
        {
            Assert.That(ReadState(Path.Combine(this._save, "s9", "hnmo-state.json"))["jewelBank"], Is.EqualTo("s9"));
            Assert.That(ReadState(Path.Combine(this._save, "s9", "hnmo-qol.json"))["qol"], Is.EqualTo("s9"));
            Assert.That(File.Exists(Path.Combine(this._save, "old", "server", "s9", "hnmo-state.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(this._server, "hnmo-state.json")), Is.True, "Season 6.3's file is left to its own program");
            Assert.That(File.Exists(Path.Combine(this._server, "arena", "hnmo-state.json")), Is.True);
        });

        this._folder.MoveOldStateFiles("arena");
        this._folder.MoveOldStateFiles("s6");
        Assert.Multiple(() =>
        {
            Assert.That(ReadState(Path.Combine(this._save, "arena", "hnmo-state.json"))["arena"], Is.EqualTo("arena"));
            Assert.That(ReadState(Path.Combine(this._save, "hnmo-state.json"))["jewelBank"], Is.EqualTo("s6"));
            Assert.That(Directory.GetFiles(this._server, "*", SearchOption.AllDirectories), Is.Empty);
        });
    }

    /// <summary>A save folder inside the program folder (a build run from source) is not moved into itself.</summary>
    [Test]
    public void ASaveFolderInsideTheProgramFolderStays()
    {
        var inner = Path.Combine(this._server, "save");
        WriteState(Path.Combine(inner, "hnmo-state.json"), new() { ["jewelBank"] = "inner" });
        var folder = new HnmoSaveFolder(inner, [this._server], this._server);

        Assert.Multiple(() =>
        {
            Assert.That(folder.MoveOldStateFiles("s6"), Is.Empty);
            Assert.That(ReadState(Path.Combine(inner, "hnmo-state.json"))["jewelBank"], Is.EqualTo("inner"));
            Assert.That(folder.MapPath("save/hnmo-state.json"), Is.EqualTo(Path.Combine(inner, "hnmo-state.json")));
        });
    }

    private static void WriteState(string path, Dictionary<string, string> sections)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var document = new JsonObject { ["version"] = 1, ["sections"] = new JsonObject(sections.Select(s => KeyValuePair.Create(s.Key, (JsonNode?)JsonValue.Create(s.Value)))) };
        File.WriteAllText(path, document.ToJsonString());
    }

    private static Dictionary<string, string> ReadState(string path) =>
        HnmoStateStore.TryReadSections(path)!.ToDictionary(s => s.Key, s => s.Value.GetString()!);

    private static Dictionary<string, string> Snapshot(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText);

    private string? Resolve(string path, string season) => HnmoStateStore.ResolveStateFilePath(path, this._folder, season);
}

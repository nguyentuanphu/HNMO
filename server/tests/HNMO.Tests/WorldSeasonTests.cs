// <copyright file="WorldSeasonTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using Moq;
using HNMO.Interfaces;
using HNMO.Persistence.Worlds;

/// <summary>
/// Tests the season of a world: Season 6.3 unless set, kept in the registry file, taken from the template,
/// and shown to players in the worlds list.
/// </summary>
[TestFixture]
public sealed class WorldSeasonTests
{
    private string _file = string.Empty;

    /// <summary>Picks a fresh registry file.</summary>
    [SetUp]
    public void SetUp() => this._file = Path.Combine(Path.GetTempPath(), $"hnmo-worlds-{Guid.NewGuid():N}.json");

    /// <summary>Removes the registry file.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>The first world, and every world of a save from before worlds had a season, is Season 6.3.</summary>
    [Test]
    public void WorldsAreSeasonSixUnlessSet()
    {
        var registry = new WorldRegistryService(this._file);
        registry.EnsureDefaultWorld([0]);
        Assert.That(registry.GetWorlds().Single().Season, Is.EqualTo(WorldSeason.Season6));

        // A registry file written before the season existed has no such field.
        var oldFile = JsonSerializer.Serialize(new { Worlds = new[] { new { WorldId = "main", NameEn = "World 1", ServerIds = new[] { 0 }, IsDefault = true } } });
        File.WriteAllText(this._file, oldFile);
        var reopened = new WorldRegistryService(this._file);
        Assert.That(reopened.GetWorlds().Single().Season, Is.EqualTo(WorldSeason.Season6));
    }

    /// <summary>A Season 9 world keeps its season when the registry is read again from its file.</summary>
    [Test]
    public void SeasonIsKeptInTheRegistryFile()
    {
        var registry = new WorldRegistryService(this._file);
        registry.EnsureDefaultWorld([0]);
        registry.UpsertWorld(new WorldDefinition { WorldId = "nine", NameEn = "World 9", Season = WorldSeason.Season9, ServerIds = [5] });

        var reopened = new WorldRegistryService(this._file);
        Assert.That(reopened.GetWorlds().Single(w => w.WorldId == "nine").Season, Is.EqualTo(WorldSeason.Season9));
        Assert.That(reopened.GetWorlds().Single(w => w.WorldId == "main").Season, Is.EqualTo(WorldSeason.Season6));
        Assert.That(File.ReadAllText(this._file), Does.Contain("\"Season9\""), "The file names the season, so it reads well and survives a renumbering.");
    }

    /// <summary>A world made from a template plays the asked season, or the template's when none is asked.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewWorldTakesTheAskedSeasonOrTheTemplatesAsync()
    {
        var registry = new WorldRegistryService(this._file);
        registry.EnsureDefaultWorld([0]);

        var nine = await registry.CreateWorldFromTemplateAsync(new WorldTemplateCreateRequest { SourceWorldId = "main", NewWorldId = "nine", Season = WorldSeason.Season9, ServerIds = [5] }).ConfigureAwait(false);
        var six = await registry.CreateWorldFromTemplateAsync(new WorldTemplateCreateRequest { SourceWorldId = "main", NewWorldId = "six", ServerIds = [6] }).ConfigureAwait(false);
        var nineAgain = await registry.CreateWorldFromTemplateAsync(new WorldTemplateCreateRequest { SourceWorldId = "nine", NewWorldId = "nine-two", ServerIds = [7] }).ConfigureAwait(false);

        Assert.That(nine.Season, Is.EqualTo(WorldSeason.Season9));
        Assert.That(six.Season, Is.EqualTo(WorldSeason.Season6));
        Assert.That(nineAgain.Season, Is.EqualTo(WorldSeason.Season9));
    }

    /// <summary>The worlds list for players carries each world's season key and badge.</summary>
    [Test]
    public void PlayersSeeEachWorldsSeason()
    {
        var registry = new WorldRegistryService(this._file);
        registry.EnsureDefaultWorld([0]);
        registry.UpsertWorld(new WorldDefinition { WorldId = "nine", NameEn = "World 9", Season = WorldSeason.Season9, ServerIds = [5] });

        var snapshot = registry.BuildGroupedSnapshot([Server(0), Server(5)], DateTimeOffset.UtcNow);

        var main = snapshot.Single(w => w.WorldId == "main");
        var nine = snapshot.Single(w => w.WorldId == "nine");
        Assert.That((main.Season, main.SeasonBadge), Is.EqualTo(("s6", "S6.3")));
        Assert.That((nine.Season, nine.SeasonBadge), Is.EqualTo(("s9", "S9")));
        Assert.That(nine.Servers.Select(s => s.Id), Is.EqualTo(new[] { 5 }));
        Assert.That(main.Servers.Select(s => s.Id), Is.EqualTo(new[] { 0 }), "A Season 9 world's server isn't also listed under the default world.");
    }

    /// <summary>
    /// The host's text for the game's server choice (admin panel, Worlds), in English and Vietnamese, is kept in the
    /// registry file and sent to players with the world; a world without one sends it empty (the game's own text).
    /// </summary>
    [Test]
    public void ServerChoiceTextIsKeptAndSentToPlayers()
    {
        var registry = new WorldRegistryService(this._file);
        registry.EnsureDefaultWorld([0]);
        registry.UpsertWorld(new WorldDefinition
        {
            WorldId = "nine",
            NameEn = "World 9",
            Season = WorldSeason.Season9,
            ServerIds = [5],
            ServerChoiceTextEn = "The newer worlds, weekly sieges",
            ServerChoiceTextVi = "Thế giới mới, công thành hằng tuần",
        });

        var reopened = new WorldRegistryService(this._file);
        var snapshot = reopened.BuildGroupedSnapshot([Server(0), Server(5)], DateTimeOffset.UtcNow);

        var nine = snapshot.Single(w => w.WorldId == "nine");
        var main = snapshot.Single(w => w.WorldId == "main");
        Assert.That((nine.ServerChoiceTextEn, nine.ServerChoiceTextVi), Is.EqualTo(("The newer worlds, weekly sieges", "Thế giới mới, công thành hằng tuần")));
        Assert.That((main.ServerChoiceTextEn, main.ServerChoiceTextVi), Is.EqualTo((string.Empty, string.Empty)));
        var json = JsonSerializer.Serialize(nine, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(json, Does.Contain("\"serverChoiceTextEn\":\"The newer worlds, weekly sieges\""), "The web game reads the camel-case name.");
    }

    /// <summary>
    /// Season 9's server program keeps its own worlds list, which starts with a Season 9 world holding its servers:
    /// the start screen names them as it names Season 6.3's ("World 1 — Origin — Server 0"), not "Server 1".
    /// </summary>
    [Test]
    public void SeasonNineProgramsDefaultWorldIsSeasonNine()
    {
        var registry = new WorldRegistryService(this._file, WorldSeason.Season9);
        registry.EnsureDefaultWorld([0, 1, 2]);

        var world = registry.BuildGroupedSnapshot([Server(0), Server(1), Server(2)], DateTimeOffset.UtcNow).Single();
        Assert.That((world.WorldId, world.Season, world.SeasonBadge), Is.EqualTo(("main", "s9", "S9")));
        Assert.That(world.NameEn, Is.EqualTo("World 1 — Origin"));
        Assert.That(world.Servers.Select(s => s.Id), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(new WorldRegistryService(this._file).GetWorlds().Single().Season, Is.EqualTo(WorldSeason.Season9), "The season is saved with the list.");
    }

    /// <summary>Each season's program keeps its worlds list in its own save folder.</summary>
    [Test]
    public void EachSeasonKeepsItsOwnWorldsFile()
    {
        Assert.That(HnmoSeasonInstance.SaveFileFor(HnmoSeasonInstance.SeasonSix, "hnmo-worlds.json"), Is.EqualTo("hnmo-worlds.json"));
        Assert.That(HnmoSeasonInstance.SaveFileFor(HnmoSeasonInstance.SeasonNine, "hnmo-worlds.json"), Is.EqualTo(Path.Combine("s9", "hnmo-worlds.json")));
    }

    /// <summary>Season keys from addresses and data are read safely.</summary>
    [Test]
    public void SeasonKeysAreReadSafely()
    {
        Assert.That(WorldSeasonExtensions.ParseSeasonKey("s9"), Is.EqualTo(WorldSeason.Season9));
        Assert.That(WorldSeasonExtensions.ParseSeasonKey(" S9 "), Is.EqualTo(WorldSeason.Season9));
        Assert.That(WorldSeasonExtensions.ParseSeasonKey("s6"), Is.EqualTo(WorldSeason.Season6));
        Assert.That(WorldSeasonExtensions.ParseSeasonKey(null), Is.EqualTo(WorldSeason.Season6));
        Assert.That(WorldSeasonExtensions.ParseSeasonKey("../s9"), Is.EqualTo(WorldSeason.Season6));
    }

    private static IGameServer Server(int id)
    {
        var server = new Mock<IGameServer>();
        server.SetupGet(s => s.Id).Returns(id);
        server.SetupGet(s => s.ServerState).Returns(ServerState.Started);
        return server.Object;
    }
}

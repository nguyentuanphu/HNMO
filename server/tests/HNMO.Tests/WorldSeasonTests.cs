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

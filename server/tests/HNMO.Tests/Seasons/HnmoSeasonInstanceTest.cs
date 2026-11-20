// <copyright file="HnmoSeasonInstanceTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Seasons;

using System.IO;
using HNMO.Interfaces;
using HNMO.Persistence.EntityFramework;

/// <summary>
/// A Season 9 server program runs next to Season 6.3's with its own database, ports and save files
/// (<see cref="HnmoSeasonInstance"/>); Season 6.3 keeps everything as it is.
/// </summary>
[TestFixture]
public class HnmoSeasonInstanceTest
{
    /// <summary>Only "s9" is Season 9 and "arena" the Arena; anything else, or nothing, is Season 6.3.</summary>
    /// <param name="value">The environment variable's value.</param>
    /// <param name="expected">The season.</param>
    [TestCase(null, "s6")]
    [TestCase("", "s6")]
    [TestCase("s6", "s6")]
    [TestCase("S9", "s9")]
    [TestCase(" s9 ", "s9")]
    [TestCase("season9", "s6")]
    [TestCase("arena", "arena")]
    [TestCase(" ARENA ", "arena")]
    public void ReadsTheSeason(string? value, string expected)
    {
        Assert.That(HnmoSeasonInstance.Parse(value), Is.EqualTo(expected));
    }

    /// <summary>Season 6.3 keeps its database, ports and files; Season 9 gets its own.</summary>
    [Test]
    public void SeasonSixKeepsEverythingAsItIs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HnmoSeasonInstance.DatabaseNameFor("s6", "hnmo"), Is.EqualTo("hnmo"));
            Assert.That(HnmoSeasonInstance.PortOffsetFor("s6"), Is.Zero);
            var file = Path.Combine("save", "hnmo-state.json");
            Assert.That(HnmoSeasonInstance.SaveFileFor("s6", file), Is.EqualTo(file));
        });
    }

    /// <summary>Season 9's database, ports and files never meet Season 6.3's.</summary>
    [Test]
    public void SeasonNineHasItsOwnDatabasePortsAndFiles()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HnmoSeasonInstance.DatabaseNameFor("s9", "hnmo"), Is.EqualTo("hnmo_s9"));
            Assert.That(HnmoSeasonInstance.DatabaseNameFor("s9", "hnmo_s9"), Is.EqualTo("hnmo_s9"), "never twice");
            Assert.That(HnmoSeasonInstance.PortOffsetFor("s9"), Is.EqualTo(100));
            var file = Path.Combine("x", "save", "hnmo-state.json");
            var moved = HnmoSeasonInstance.SaveFileFor("s9", file);
            Assert.That(moved, Is.EqualTo(Path.Combine("x", "save", "s9", "hnmo-state.json")));
            Assert.That(HnmoSeasonInstance.SaveFileFor("s9", moved), Is.EqualTo(moved), "never twice");
        });
    }

    /// <summary>Season 9's connection string names its database and keeps the rest.</summary>
    [Test]
    public void SeasonNineConnectsToItsOwnDatabase()
    {
        const string seasonSix = "Server=localhost;Port=55432;User Id=postgres;Password=admin;Database=hnmo;Command Timeout=120;";
        Assert.That(ConfigFileDatabaseConnectionStringProvider.SeasonConnectionString("s6", seasonSix), Is.EqualTo(seasonSix));
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(ConfigFileDatabaseConnectionStringProvider.SeasonConnectionString("s9", seasonSix));
        Assert.Multiple(() =>
        {
            Assert.That(builder.Database, Is.EqualTo("hnmo_s9"));
            Assert.That(builder.Port, Is.EqualTo(55432));
            Assert.That(builder.Username, Is.EqualTo("postgres"));
            Assert.That(builder.Password, Is.EqualTo("admin"));
            Assert.That(builder.CommandTimeout, Is.EqualTo(120));
        });
    }

    /// <summary>
    /// The Arena has its own database, ports 200 higher and its own save folder, and shares Season 6.3's accounts.
    /// </summary>
    [Test]
    public void TheArenaHasItsOwnDatabasePortsAndFiles()
    {
        var file = Path.Combine("x", "save", "hnmo-state.json");
        var moved = HnmoSeasonInstance.SaveFileFor("arena", file);
        Assert.Multiple(() =>
        {
            Assert.That(HnmoSeasonInstance.DatabaseNameFor("arena", "hnmo"), Is.EqualTo("hnmo_arena"));
            Assert.That(HnmoSeasonInstance.DatabaseNameFor("arena", "hnmo_arena"), Is.EqualTo("hnmo_arena"), "never twice");
            Assert.That(HnmoSeasonInstance.SharedDatabaseName("hnmo_arena"), Is.EqualTo("hnmo"));
            Assert.That(HnmoSeasonInstance.SharedDatabaseName("hnmo_s9"), Is.EqualTo("hnmo"));
            Assert.That(HnmoSeasonInstance.SharedDatabaseName("hnmo"), Is.EqualTo("hnmo"));
            Assert.That(HnmoSeasonInstance.PortOffsetFor("arena"), Is.EqualTo(200));
            Assert.That(moved, Is.EqualTo(Path.Combine("x", "save", "arena", "hnmo-state.json")));
            Assert.That(HnmoSeasonInstance.SaveFileFor("arena", moved), Is.EqualTo(moved), "never twice");
            Assert.That(new Npgsql.NpgsqlConnectionStringBuilder(ConfigFileDatabaseConnectionStringProvider.SeasonConnectionString("arena", "Server=localhost;Port=55432;User Id=postgres;Password=admin;Database=hnmo;")).Database, Is.EqualTo("hnmo_arena"));
        });
    }
}

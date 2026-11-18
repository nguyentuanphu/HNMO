// <copyright file="PartyOptionsEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.PartyOptions;

/// <summary>
/// Unit tests for <see cref="PartyOptionsEngine"/> and <see cref="PartyOptionsStateWriter"/>.
/// </summary>
[TestFixture]
public class PartyOptionsEngineTest
{
    private string _tempDir = string.Empty;

    /// <summary>Creates temp directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-po-{Guid.NewGuid():N}");
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

    /// <summary>Verifies password match, auto-party spot join rules, level filter, and no password leak on public snapshot.</summary>
    [Test]
    public void PasswordAndSpotJoinValidateCorrectlyWithoutLeakingSecrets()
    {
        var cfg = new PartyOptionsConfiguration { MaxPasswordLength = 12 };
        var store = PartyOptionsStore.Open(Path.Combine(this._tempDir, "state.json"));
        var engine = new PartyOptionsEngine(cfg, store, enabled: true);

        engine.SetAdvertisement(new PartyAdvertisement
        {
            LeaderName = "Knight1",
            MapName = "LostTower",
            SpotName = "LostTower 7",
            Password = "secret77",
            MinLevel = 100,
            MaxLevel = 350,
            MemberCount = 2,
            MaxSize = 5,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        engine.SetAdvertisement(new PartyAdvertisement
        {
            LeaderName = "ElfOpen",
            MapName = "Devias",
            SpotName = "Devias 3",
            Password = string.Empty,
            MinLevel = 1,
            MaxLevel = 0,
            MemberCount = 3,
            MaxSize = 5,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        Assert.That(engine.FindByPassword("wrong", 150, hasParty: false, out _), Is.EqualTo(PartyJoinCheckResult.NotFound));
        Assert.That(engine.FindByPassword("secret77", 50, hasParty: false, out _), Is.EqualTo(PartyJoinCheckResult.LevelOutOfRange));
        Assert.That(engine.FindByPassword("secret77", 150, hasParty: true, out _), Is.EqualTo(PartyJoinCheckResult.AlreadyInParty));
        Assert.That(engine.FindByPassword("secret77", 150, hasParty: false, out var match), Is.EqualTo(PartyJoinCheckResult.Ok));
        Assert.That(match?.LeaderName, Is.EqualTo("Knight1"));

        Assert.That(engine.PlanSpotJoin("ElfOpen", string.Empty, 200, hasParty: false, out var openMatch), Is.EqualTo(PartyJoinCheckResult.Ok));
        Assert.That(openMatch?.SpotName, Is.EqualTo("Devias 3"));

        var pubJson = PartyOptionsStateWriter.WritePublic(cfg, store);
        Assert.That(pubJson, Does.Contain("LostTower 7"));
        Assert.That(pubJson, Does.Not.Contain("secret77"));
    }
}

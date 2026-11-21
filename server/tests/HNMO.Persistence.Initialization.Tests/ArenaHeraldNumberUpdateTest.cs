// <copyright file="ArenaHeraldNumberUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// A save made while the Arena Herald was NPC 595 gets it moved to 900 (the web game knows it there); another monster
/// numbered 595 stays as it is, and a save without the Herald gets one.
/// </summary>
[TestFixture]
internal class ArenaHeraldNumberUpdateTest
{
    /// <summary>The early Herald moves to 900 with its spawns; nothing is added twice.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheEarlyHeraldMovesTo900Async()
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var early = new HNMO.Persistence.BasicModel.MonsterDefinition { Number = 595, Designation = "Arena Herald||vi=Sứ giả Đấu trường" };
        configuration.Monsters.Add(early);

        await new RenumberArenaHeraldUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);
        await new RenumberArenaHeraldUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);

        Assert.That(configuration.Monsters.Select(m => m.Number), Is.EqualTo(new short[] { HnmoArenaHeraldInitializer.Number }));
        Assert.That(early.Number, Is.EqualTo(900));
        Assert.That(early.Designation.ValueInNeutralLanguage, Is.EqualTo("Arena Herald"));
    }

    /// <summary>A real monster 595 (the Season 9 data) is not the Herald: it stays, and the Herald is added at 900.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AnotherMonster595StaysAsync()
    {
        var configuration = new HNMO.Persistence.BasicModel.GameConfiguration();
        var monster = new HNMO.Persistence.BasicModel.MonsterDefinition { Number = 595, Designation = "Some monster" };
        configuration.Monsters.Add(monster);

        await new RenumberArenaHeraldUpdatePlugIn().ApplyUpdateAsync(new HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider().CreateNewContext(), configuration).ConfigureAwait(false);

        Assert.That(monster.Number, Is.EqualTo(595));
        Assert.That(configuration.Monsters.Select(m => m.Number), Is.EquivalentTo(new short[] { 595, 900 }));
    }
}

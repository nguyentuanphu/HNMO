// <copyright file="Dungeon.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Maps;

using HNMO.DataModel.Configuration;

/// <summary>
/// The initialization for the Dungeon map.
/// </summary>
internal class Dungeon : Version075.Maps.Dungeon
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Dungeon"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public Dungeon(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc/>
    protected override string TerrainVersionPrefix => string.Empty;

    /// <summary>
    /// Gets the spawns of the older version which Season 6 leaves out: the Season 6/9 server suites don't have these spots.
    /// </summary>
    protected override IReadOnlySet<short> LeftOutSpawnNumbers { get; } = new HashSet<short> { 101, 107, 108, 109, 110 };

    /// <inheritdoc/>
    protected override IEnumerable<MonsterSpawnArea> CreateMonsterSpawns()
    {
        foreach (var spawn in base.CreateMonsterSpawns())
        {
            yield return spawn;
        }

        // The spots of the Season 6/9 server suites which the older version doesn't have.
        yield return this.CreateMonsterSpawn(769, this.NpcDictionary[8], 030, 122); // Poison Bull
        yield return this.CreateMonsterSpawn(770, this.NpcDictionary[8], 040, 098); // Poison Bull
    }
}
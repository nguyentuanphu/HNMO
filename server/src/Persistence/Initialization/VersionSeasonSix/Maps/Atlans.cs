// <copyright file="Atlans.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Maps;

using HNMO.DataModel.Configuration;

/// <summary>
/// Initialization for the Atlans map.
/// </summary>
internal class Atlans : Version075.Maps.Atlans
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Atlans"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public Atlans(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    protected override string TerrainVersionPrefix => string.Empty;

    /// <summary>
    /// Gets the spawns of the older version which Season 6 leaves out: the Season 6/9 server suites don't have these spots.
    /// </summary>
    protected override IReadOnlySet<short> LeftOutSpawnNumbers { get; } = new HashSet<short> { 297 };

    /// <inheritdoc/>
    protected override IEnumerable<MonsterSpawnArea> CreateMonsterSpawns()
    {
        foreach (var spawn in base.CreateMonsterSpawns())
        {
            yield return spawn;
        }

        // The spots of the Season 6/9 server suites which the older version doesn't have.
        yield return this.CreateMonsterSpawn(438, this.NpcDictionary[51], 251, 051); // Great Bahamut
        yield return this.CreateMonsterSpawn(439, this.NpcDictionary[51], 007, 052); // Great Bahamut
    }

    /// <inheritdoc />
    protected override IEnumerable<MonsterSpawnArea> CreateNpcSpawns()
    {
        foreach (var npc in base.CreateNpcSpawns())
        {
            yield return npc;
        }

        yield return this.CreateMonsterSpawn(10, this.NpcDictionary[229], 17, 35, Direction.SouthEast, SpawnTrigger.Wandering); // Marlon
    }
}
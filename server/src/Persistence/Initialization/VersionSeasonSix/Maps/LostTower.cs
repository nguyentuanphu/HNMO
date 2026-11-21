// <copyright file="LostTower.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Maps;

using HNMO.DataModel.Configuration;

/// <summary>
/// The initialization for the Lost Tower map.
/// </summary>
internal class LostTower : Version075.Maps.LostTower
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LostTower"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public LostTower(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    protected override string TerrainVersionPrefix => string.Empty;

    /// <summary>
    /// Gets the spawns of the older version which Season 6 leaves out: the Season 6/9 server suites don't have these spots.
    /// </summary>
    protected override IReadOnlySet<short> LeftOutSpawnNumbers { get; } = new HashSet<short> { 101, 102, 103, 104, 105, 106, 216 };

    /// <inheritdoc/>
    protected override IEnumerable<MonsterSpawnArea> CreateMonsterSpawns()
    {
        foreach (var spawn in base.CreateMonsterSpawns())
        {
            yield return spawn;
        }

        // The spots of the Season 6/9 server suites which the older version doesn't have.
        yield return this.CreateMonsterSpawn(698, this.NpcDictionary[39], 200, 245); // Poison Shadow
        yield return this.CreateMonsterSpawn(699, this.NpcDictionary[39], 203, 050); // Poison Shadow
        yield return this.CreateMonsterSpawn(700, this.NpcDictionary[39], 236, 127); // Poison Shadow
    }
}
// <copyright file="KalimaBase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Maps;

using HNMO.DataModel.Configuration;

/// <summary>
/// The base class for the initialization of the Kalima maps.
/// </summary>
internal abstract class KalimaBase : BaseMapInitializer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KalimaBase"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    protected KalimaBase(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    protected override void AdditionalInitialization(GameMapDefinition mapDefinition)
    {
        base.AdditionalInitialization(mapDefinition);
        this.AddUnderwaterMovementPowerUp();
    }

    /// <inheritdoc/>
    protected override IEnumerable<MonsterSpawnArea> CreateNpcSpawns()
    {
        yield return this.CreateMonsterSpawn(1, this.NpcDictionary[259], 007, 019, Direction.South); // Oracle Layla
    }
}

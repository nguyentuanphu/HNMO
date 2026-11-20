// <copyright file="HnmoArenaHeraldInitializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix;

using HNMO.DataModel.Configuration;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.Version075.Maps;

/// <summary>
/// The HNMO Arena Herald (owner's choice, 2026-11-20): an NPC in every main town, next to where players appear, which
/// opens the Arena window (the web game opens it when the player talks to the Herald). It looks like the Arena Guard.
/// Used for new saves and, through the "Add the Arena Herald" update, for existing ones; adds nothing twice.
/// </summary>
public sealed class HnmoArenaHeraldInitializer
{
    /// <summary>The Herald's NPC number (free in the game data; the web game's monster data knows it).</summary>
    public const short Number = 900;

    private readonly IContext _context;
    private readonly GameConfiguration _gameConfiguration;

    /// <summary>Initializes a new instance of the <see cref="HnmoArenaHeraldInitializer"/> class.</summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public HnmoArenaHeraldInitializer(IContext context, GameConfiguration gameConfiguration)
    {
        this._context = context;
        this._gameConfiguration = gameConfiguration;
    }

    /// <summary>Gets the towns and tiles the Herald stands on: Lorencia, Devias, Noria and Elbeland, next to the spawn.</summary>
    internal static IReadOnlyList<(short Map, byte X, byte Y)> Spots { get; } =
    [
        (Lorencia.Number, 133, 117),
        (Devias.Number, 199, 34),
        (Noria.Number, 170, 108),
        (Maps.Elvenland.Number, 48, 222),
    ];

    /// <summary>Adds the Herald and its spots (when missing).</summary>
    public void Initialize()
    {
        var herald = this._gameConfiguration.Monsters.FirstOrDefault(m => m.Number == Number);
        if (herald is null)
        {
            herald = this._context.CreateNew<MonsterDefinition>();
            herald.Number = Number;
            herald.Designation = LocalizedString.FromResource(() => MonsterNames.ArenaHerald);
            herald.ObjectKind = NpcObjectKind.PassiveNpc;
            herald.SetGuid(herald.Number);
            this._gameConfiguration.Monsters.Add(herald);
        }

        foreach (var (mapNumber, x, y) in Spots)
        {
            if (this._gameConfiguration.Maps.FirstOrDefault(m => m.Number == mapNumber) is not { } map
                || map.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == Number))
            {
                continue;
            }

            var spawn = this._context.CreateNew<MonsterSpawnArea>();
            spawn.SetGuid((short)(mapNumber + 4000), Number);
            spawn.GameMap = map;
            spawn.MonsterDefinition = herald;
            spawn.Quantity = 1;
            spawn.SpawnTrigger = SpawnTrigger.Automatic;
            spawn.Direction = Direction.South;
            spawn.X1 = x;
            spawn.X2 = x;
            spawn.Y1 = y;
            spawn.Y2 = y;
            map.MonsterSpawns.Add(spawn);
        }
    }
}

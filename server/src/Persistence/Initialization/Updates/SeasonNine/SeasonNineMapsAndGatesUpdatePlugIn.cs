// <copyright file="SeasonNineMapsAndGatesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Adds the Season 9 maps, embedded terrain data, exit/enter gates, and Season 9 spawn areas when running as Season 9.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineMapsAndGatesUpdatePlugIn_Name), Description = nameof(PlugInResources.SeasonNineMapsAndGatesUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B5C9D3E2-4F8A-4B1C-92D6-7E3F1A5C8045")]
public class SeasonNineMapsAndGatesUpdatePlugIn : SeasonNineUpdatePlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineMapsAndGatesUpdatePlugIn"/> class.
    /// </summary>
    public SeasonNineMapsAndGatesUpdatePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineMapsAndGatesUpdatePlugIn"/> class with an explicit season check.
    /// </summary>
    /// <param name="isSeasonNine">Returns <c>true</c> when running in Season 9 mode.</param>
    internal SeasonNineMapsAndGatesUpdatePlugIn(Func<bool> isSeasonNine)
        : base(isSeasonNine)
    {
    }

    /// <inheritdoc />
    public override string Name => PlugInResources.SeasonNineMapsAndGatesUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SeasonNineMapsAndGatesUpdatePlugIn_Description;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 0, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var mapsByNumber = gameConfiguration.Maps
            .GroupBy(m => m.Number)
            .ToDictionary(g => g.Key, g => g.First());
        mapsByNumber.TryGetValue(0, out var lorencia);

        var addedMaps = new List<(GameMapDefinition Map, byte SafezoneMapNumber)>();
        foreach (var spec in SeasonNineMapsCatalog.Maps)
        {
            if (mapsByNumber.ContainsKey(spec.Number))
            {
                continue;
            }

            var map = context.CreateNew<GameMapDefinition>();
            map.SetGuid(spec.Number, 0);
            map.Number = spec.Number;
            map.Name = LocalizedString.FromResource(spec.NameExpression);
            map.ExpMultiplier = 1;
            map.TerrainData = TerrainUpdateHelper.ReadTerrainResource(spec.TerrainFileName);
            if (lorencia is not null)
            {
                foreach (var group in lorencia.DropItemGroups)
                {
                    map.DropItemGroups.Add(group);
                }
            }

            gameConfiguration.Maps.Add(map);
            mapsByNumber[spec.Number] = map;
            addedMaps.Add((map, spec.SafezoneMapNumber));
        }

        foreach (var (map, safezoneNumber) in addedMaps)
        {
            if (mapsByNumber.TryGetValue(safezoneNumber, out var safezone))
            {
                map.SafezoneMap = safezone;
            }
        }

        var targetGates = new Dictionary<short, ExitGate>();
        foreach (var spec in SeasonNineMapsCatalog.ExitGates)
        {
            if (!mapsByNumber.TryGetValue(spec.MapNumber, out var map))
            {
                continue;
            }

            var existing = map.ExitGates.FirstOrDefault(g =>
                g.IsSpawnGate == spec.IsSpawnGate
                && g.X1 == spec.X1 && g.Y1 == spec.Y1 && g.X2 == spec.X2 && g.Y2 == spec.Y2);
            if (existing is not null)
            {
                targetGates[spec.Index] = existing;
                continue;
            }

            var gate = context.CreateNew<ExitGate>();
            gate.SetGuid(spec.Index);
            gate.Map = map;
            gate.X1 = spec.X1;
            gate.Y1 = spec.Y1;
            gate.X2 = spec.X2;
            gate.Y2 = spec.Y2;
            gate.Direction = spec.Direction;
            gate.IsSpawnGate = spec.IsSpawnGate;
            map.ExitGates.Add(gate);
            targetGates[spec.Index] = gate;
        }

        foreach (var spec in SeasonNineMapsCatalog.EnterGates)
        {
            if (!mapsByNumber.TryGetValue(spec.MapNumber, out var map)
                || !targetGates.TryGetValue(spec.TargetGateIndex, out var targetGate)
                || map.EnterGates.Any(g => g.Number == spec.Number))
            {
                continue;
            }

            var enterGate = context.CreateNew<EnterGate>();
            enterGate.SetGuid(spec.Number);
            enterGate.Number = spec.Number;
            enterGate.TargetGate = targetGate;
            enterGate.LevelRequirement = spec.LevelRequirement;
            enterGate.X1 = spec.X1;
            enterGate.Y1 = spec.Y1;
            enterGate.X2 = spec.X2;
            enterGate.Y2 = spec.Y2;
            map.EnterGates.Add(enterGate);
        }

        var monstersByNumber = gameConfiguration.Monsters
            .GroupBy(m => m.Number)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var spec in SeasonNineMapsCatalog.Spawns)
        {
            if (!mapsByNumber.TryGetValue(spec.MapNumber, out var map)
                || !monstersByNumber.TryGetValue(spec.MonsterNumber, out var monster))
            {
                continue;
            }

            if (map.MonsterSpawns.Any(s =>
                    s.MonsterDefinition?.Number == spec.MonsterNumber
                    && s.X1 == spec.X1 && s.Y1 == spec.Y1
                    && s.X2 == spec.X2 && s.Y2 == spec.Y2))
            {
                continue;
            }

            var spawn = context.CreateNew<MonsterSpawnArea>();
            spawn.SetGuid(spec.MapNumber, spec.Number);
            spawn.GameMap = map;
            spawn.MonsterDefinition = monster;
            spawn.Quantity = spec.Quantity;
            spawn.Direction = spec.Direction;
            spawn.SpawnTrigger = SpawnTrigger.Automatic;
            spawn.X1 = spec.X1;
            spawn.Y1 = spec.Y1;
            spawn.X2 = spec.X2;
            spawn.Y2 = spec.Y2;
            map.MonsterSpawns.Add(spawn);
        }

        return ValueTask.CompletedTask;
    }
}

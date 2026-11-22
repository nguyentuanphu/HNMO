// <copyright file="SeasonNineJinShopsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates.SeasonNine;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Equips the Season 9 Jin merchants (583 on Acheron and 604 on Acheron Arca War) with their 32-item potion,
/// bolt/arrow, Town Portal Scroll, and Armor of Guardsman shop (omitting 13-7) and ensures their safezone spawns
/// when running in Season 9 mode.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonNineJinShopsUpdatePlugIn_Name), Description = nameof(PlugInResources.SeasonNineJinShopsUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E4A91F30-8C62-4D7E-A51B-9F2D4C6E8103")]
public class SeasonNineJinShopsUpdatePlugIn : SeasonNineUpdatePlugInBase
{
    private static readonly (short NpcNumber, byte MapNumber, short SpawnNumber, byte X, byte Y)[] JinTargets =
    [
        (583, 91, 231, 55, 195),
        (604, 92, 1, 56, 202),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineJinShopsUpdatePlugIn"/> class.
    /// </summary>
    public SeasonNineJinShopsUpdatePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonNineJinShopsUpdatePlugIn"/> class with an explicit season check.
    /// </summary>
    /// <param name="isSeasonNine">Returns <c>true</c> when running in Season 9 mode.</param>
    internal SeasonNineJinShopsUpdatePlugIn(Func<bool> isSeasonNine)
        : base(isSeasonNine)
    {
    }

    /// <inheritdoc />
    public override string Name => PlugInResources.SeasonNineJinShopsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SeasonNineJinShopsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 12, 46, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplySeasonNineAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (npcNumber, mapNumber, spawnNumber, x, y) in JinTargets)
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == npcNumber) is not { } jin)
            {
                continue;
            }

            if (jin.MerchantStore is null)
            {
                jin.NpcWindow = NpcWindow.Merchant;
                jin.MerchantStore = PotionShopStorageBuilder.Build(context, gameConfiguration, npcNumber);
            }

            if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == mapNumber) is { } map
                && !map.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == npcNumber))
            {
                var spawn = context.CreateNew<MonsterSpawnArea>();
                spawn.SetGuid(mapNumber, spawnNumber);
                spawn.GameMap = map;
                spawn.MonsterDefinition = jin;
                spawn.Quantity = 1;
                spawn.Direction = Direction.South;
                spawn.SpawnTrigger = SpawnTrigger.Automatic;
                spawn.X1 = x;
                spawn.X2 = x;
                spawn.Y1 = y;
                spawn.Y2 = y;
                map.MonsterSpawns.Add(spawn);
            }
        }

        return ValueTask.CompletedTask;
    }
}

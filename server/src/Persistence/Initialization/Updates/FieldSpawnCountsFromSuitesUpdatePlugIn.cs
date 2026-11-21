// <copyright file="FieldSpawnCountsFromSuitesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update gives the field maps the monster spawns of the Season 6/9 server suites where the count of a monster
/// differs (22 monsters on Dungeon, Lost Tower, Atlans, Icarus, Kalima 3/6/7, Land of Trials, Aida, Crywolf and the
/// Barracks of Balgass): the spots which the suites don't have are removed, the ones which HNMO doesn't have are added.
/// A spot is only removed while it is still HNMO's spot (same monster, place and quantity), and only added while
/// its number is free, so spawns the host changed stay.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("507FCA9B-E559-42C8-B17D-392C2BD17BB7")]
public class FieldSpawnCountsFromSuitesUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Field monster spawns as in the suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The spawn spots of 22 field monsters whose count differed from the Season 6/9 server suites, e.g. 13 instead of 6 Queen Rainers in Icarus.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 50, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets the changed spots: map, spawn number, monster, area, quantity, and whether the spot is added (or removed).
    /// </summary>
    internal static IReadOnlyList<SpawnChange> Changes { get; } =
    [
        new(1, 101, 8, 42, 122, 42, 122, 1, false), // Poison Bull
        new(1, 769, 8, 30, 122, 30, 122, 1, true), // Poison Bull
        new(1, 770, 8, 40, 98, 40, 98, 1, true), // Poison Bull
        new(1, 107, 14, 98, 221, 98, 221, 1, false), // Skeleton Warrior
        new(1, 108, 14, 100, 220, 100, 220, 1, false), // Skeleton Warrior
        new(1, 109, 14, 100, 225, 100, 225, 1, false), // Skeleton Warrior
        new(1, 110, 14, 108, 232, 108, 232, 1, false), // Skeleton Warrior
        new(4, 216, 39, 198, 245, 198, 245, 1, false), // Poison Shadow
        new(4, 698, 39, 200, 245, 200, 245, 1, true), // Poison Shadow
        new(4, 699, 39, 203, 50, 203, 50, 1, true), // Poison Shadow
        new(4, 700, 39, 236, 127, 236, 127, 1, true), // Poison Shadow
        new(4, 101, 40, 10, 110, 10, 110, 1, false), // Death Knight
        new(4, 102, 40, 5, 110, 5, 110, 1, false), // Death Knight
        new(4, 103, 40, 15, 105, 15, 105, 1, false), // Death Knight
        new(4, 104, 40, 12, 99, 12, 99, 1, false), // Death Knight
        new(4, 105, 40, 10, 100, 10, 100, 1, false), // Death Knight
        new(4, 106, 40, 12, 100, 12, 100, 1, false), // Death Knight
        new(7, 297, 51, 240, 51, 240, 51, 1, false), // Great Bahamut
        new(7, 438, 51, 251, 51, 251, 51, 1, true), // Great Bahamut
        new(7, 439, 51, 7, 52, 7, 52, 1, true), // Great Bahamut
        new(10, 132, 69, 21, 39, 21, 39, 1, false), // Alquamos
        new(10, 164, 69, 36, 29, 36, 29, 1, true), // Alquamos
        new(10, 165, 69, 37, 41, 37, 41, 1, true), // Alquamos
        new(10, 166, 69, 26, 41, 26, 41, 1, true), // Alquamos
        new(10, 167, 69, 21, 24, 21, 24, 1, true), // Alquamos
        new(10, 168, 69, 21, 36, 21, 36, 1, true), // Alquamos
        new(10, 105, 70, 60, 84, 60, 84, 1, false), // Queen Rainer
        new(10, 169, 70, 84, 36, 84, 36, 1, true), // Queen Rainer
        new(10, 170, 70, 90, 48, 90, 48, 1, true), // Queen Rainer
        new(10, 171, 70, 95, 56, 95, 56, 1, true), // Queen Rainer
        new(10, 172, 70, 74, 84, 74, 84, 1, true), // Queen Rainer
        new(10, 173, 70, 74, 72, 74, 72, 1, true), // Queen Rainer
        new(10, 174, 70, 57, 71, 57, 71, 1, true), // Queen Rainer
        new(10, 175, 70, 64, 84, 64, 84, 1, true), // Queen Rainer
        new(10, 176, 70, 69, 77, 69, 77, 1, true), // Queen Rainer
        new(10, 177, 71, 50, 33, 50, 33, 1, true), // Mega Crust
        new(10, 178, 71, 59, 27, 59, 27, 1, true), // Mega Crust
        new(10, 179, 71, 74, 38, 74, 38, 1, true), // Mega Crust
        new(10, 103, 73, 70, 78, 70, 78, 1, false), // Drakan
        new(10, 119, 73, 55, 73, 55, 73, 1, false), // Drakan
        new(10, 180, 73, 28, 67, 28, 67, 1, true), // Drakan
        new(10, 154, 75, 30, 231, 30, 231, 1, false), // Great Drakan
        new(10, 181, 75, 32, 231, 32, 231, 1, true), // Great Drakan
        new(10, 182, 75, 45, 173, 45, 173, 1, true), // Great Drakan
        new(10, 183, 75, 59, 163, 59, 163, 1, true), // Great Drakan
        new(10, 184, 75, 52, 168, 52, 168, 1, true), // Great Drakan
        new(26, 159, 187, 35, 87, 35, 87, 1, false), // Necron 3
        new(26, 161, 188, 35, 87, 35, 87, 1, true), // Schriker 3
        new(29, 161, 274, 32, 77, 32, 77, 1, true), // Schriker 6
        new(29, 162, 274, 41, 78, 41, 78, 1, true), // Schriker 6
        new(29, 163, 274, 37, 67, 37, 67, 1, true), // Schriker 6
        new(29, 164, 274, 46, 86, 46, 86, 1, true), // Schriker 6
        new(29, 165, 274, 23, 85, 23, 85, 1, true), // Schriker 6
        new(29, 166, 274, 23, 70, 23, 70, 1, true), // Schriker 6
        new(31, 601, 293, 192, 127, 192, 127, 1, true), // Poison Golem
        new(31, 602, 293, 180, 117, 180, 117, 1, true), // Poison Golem
        new(31, 603, 293, 191, 112, 191, 112, 1, true), // Poison Golem
        new(31, 604, 294, 14, 43, 14, 43, 1, true), // Axe Warrior
        new(33, 919, 305, 94, 158, 94, 158, 1, true), // Blue Golem
        new(33, 920, 306, 126, 156, 126, 156, 1, true), // Death Rider
        new(33, 921, 550, 104, 203, 104, 203, 1, true), // Bloody Death Rider
        new(33, 922, 550, 99, 203, 99, 203, 1, true), // Bloody Death Rider
        new(33, 923, 550, 75, 228, 75, 228, 1, true), // Bloody Death Rider
        new(34, 408, 313, 88, 144, 188, 144, 1, false), // Werewolf
        new(34, 1264, 313, 88, 144, 88, 144, 1, true), // Werewolf
        new(34, 1265, 313, 204, 130, 204, 130, 1, true), // Werewolf
        new(36, 161, 337, 32, 77, 32, 77, 1, true), // Schriker 7
        new(36, 162, 337, 41, 78, 41, 78, 1, true), // Schriker 7
        new(36, 163, 337, 37, 67, 37, 67, 1, true), // Schriker 7
        new(36, 164, 337, 46, 86, 46, 86, 1, true), // Schriker 7
        new(36, 165, 337, 23, 85, 23, 85, 1, true), // Schriker 7
        new(36, 166, 337, 23, 70, 23, 70, 1, true), // Schriker 7
        new(41, 309, 409, 71, 101, 71, 101, 1, true), // Balram (Trainee Soldier)
        new(41, 301, 411, 126, 164, 166, 164, 1, false), // Soram (Trainee Soldier)
        new(41, 307, 411, 117, 98, 117, 98, 1, false), // Soram (Trainee Soldier)
        new(41, 308, 411, 113, 109, 113, 109, 1, false), // Soram (Trainee Soldier)
        new(41, 310, 411, 126, 164, 126, 164, 1, true), // Soram (Trainee Soldier)
    ];

    /// <summary>
    /// Determines whether the spawn is the one of the change: same number, monster, area and quantity.
    /// </summary>
    /// <param name="spawn">The spawn.</param>
    /// <param name="change">The change.</param>
    /// <returns><c>true</c>, if the spawn is the one of the change.</returns>
    internal static bool IsSpawnOf(MonsterSpawnArea spawn, SpawnChange change)
        => spawn.GetId() == change.Id
           && spawn.MonsterDefinition?.Number == change.Monster
           && spawn.X1 == change.X1 && spawn.Y1 == change.Y1 && spawn.X2 == change.X2 && spawn.Y2 == change.Y2
           && spawn.Quantity == change.Quantity;

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var change in Changes)
        {
            if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == change.Map && m.Discriminator == 0) is not { } map)
            {
                continue;
            }

            var existing = map.MonsterSpawns.FirstOrDefault(s => s.GetId() == change.Id);
            if (!change.Added)
            {
                if (existing is not null && IsSpawnOf(existing, change))
                {
                    map.MonsterSpawns.Remove(existing);
                }

                continue;
            }

            if (existing is not null
                || gameConfiguration.Monsters.FirstOrDefault(m => m.Number == change.Monster) is not { } monster)
            {
                continue;
            }

            var spawn = context.CreateNew<MonsterSpawnArea>();
            spawn.SetGuid(change.Map, change.Number);
            spawn.GameMap = map;
            spawn.MonsterDefinition = monster;
            spawn.X1 = change.X1;
            spawn.Y1 = change.Y1;
            spawn.X2 = change.X2;
            spawn.Y2 = change.Y2;
            spawn.Quantity = change.Quantity;
            spawn.Direction = Direction.Undefined;
            spawn.SpawnTrigger = SpawnTrigger.Automatic;
            map.MonsterSpawns.Add(spawn);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A spawn spot which is added or removed.
    /// </summary>
    /// <param name="Map">The map number.</param>
    /// <param name="Number">The spawn number on the map.</param>
    /// <param name="Monster">The monster number.</param>
    /// <param name="X1">The x1 coordinate.</param>
    /// <param name="Y1">The y1 coordinate.</param>
    /// <param name="X2">The x2 coordinate.</param>
    /// <param name="Y2">The y2 coordinate.</param>
    /// <param name="Quantity">The quantity.</param>
    /// <param name="Added"><c>true</c>, if the spot is added; <c>false</c>, if it's removed.</param>
    internal readonly record struct SpawnChange(byte Map, short Number, short Monster, byte X1, byte Y1, byte X2, byte Y2, short Quantity, bool Added)
    {
        /// <summary>
        /// Gets the identifier of the spawn.
        /// </summary>
        public Guid Id => GuidHelper.CreateGuid<MonsterSpawnArea>(this.Map, this.Number);
    }
}

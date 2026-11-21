// <copyright file="MonsterResistancesFromSuitesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update gives the monsters the ice, poison, lightning and fire resistance of the Season 6/9 server suites.
/// Many monsters had their lightning resistance stored as water resistance, which no lightning skill reads; the suites' monsters have no water resistance,
/// so it is removed and the lightning resistance is set. A value is only changed while it is still HNMO's old value.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("2F7DD718-34F2-4937-96AC-1C549877452A")]
public class MonsterResistancesFromSuitesUpdatePlugIn : MonsterValuesUpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Monster resistances as in the suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Ice, poison, lightning and fire resistance of the monsters as in the Season 6/9 server suites; a lightning resistance stored as water resistance becomes the lightning resistance.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 20, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IReadOnlyList<MonsterValueChange> Changes => ChangeList;

    /// <summary>
    /// Gets the changes: monster number, value, the value HNMO had, the value of the suites.
    /// </summary>
    internal static IReadOnlyList<MonsterValueChange> ChangeList { get; } =
    [
        new(0, MonsterValue.IceResistance, float.NaN, 0), // Bull Fighter
        new(0, MonsterValue.PoisonResistance, float.NaN, 0),
        new(0, MonsterValue.LightningResistance, float.NaN, 0),
        new(0, MonsterValue.FireResistance, float.NaN, 0),
        new(1, MonsterValue.IceResistance, float.NaN, 0), // Hound
        new(1, MonsterValue.PoisonResistance, float.NaN, 0),
        new(1, MonsterValue.LightningResistance, float.NaN, 0),
        new(1, MonsterValue.FireResistance, float.NaN, 0),
        new(2, MonsterValue.IceResistance, float.NaN, 0), // Budge Dragon
        new(2, MonsterValue.PoisonResistance, float.NaN, 0),
        new(2, MonsterValue.LightningResistance, float.NaN, 0),
        new(2, MonsterValue.FireResistance, float.NaN, 0),
        new(3, MonsterValue.IceResistance, float.NaN, 0), // Spider
        new(3, MonsterValue.PoisonResistance, float.NaN, 0),
        new(3, MonsterValue.LightningResistance, float.NaN, 0),
        new(3, MonsterValue.FireResistance, float.NaN, 0),
        new(4, MonsterValue.IceResistance, float.NaN, 0), // Elite Bull Fighter
        new(4, MonsterValue.PoisonResistance, float.NaN, 0),
        new(4, MonsterValue.LightningResistance, float.NaN, 0),
        new(4, MonsterValue.FireResistance, float.NaN, 0),
        new(5, MonsterValue.IceResistance, float.NaN, 1), // Hell Hound
        new(5, MonsterValue.PoisonResistance, float.NaN, 1),
        new(5, MonsterValue.LightningResistance, float.NaN, 1),
        new(6, MonsterValue.IceResistance, float.NaN, 0), // Lich
        new(6, MonsterValue.PoisonResistance, float.NaN, 0),
        new(6, MonsterValue.LightningResistance, float.NaN, 0),
        new(7, MonsterValue.IceResistance, float.NaN, 0), // Giant
        new(7, MonsterValue.PoisonResistance, float.NaN, 0),
        new(7, MonsterValue.LightningResistance, float.NaN, 0),
        new(7, MonsterValue.FireResistance, float.NaN, 0),
        new(8, MonsterValue.IceResistance, float.NaN, 6), // Poison Bull
        new(8, MonsterValue.PoisonResistance, 6, 2),
        new(8, MonsterValue.LightningResistance, float.NaN, 2),
        new(8, MonsterValue.FireResistance, float.NaN, 2),
        new(9, MonsterValue.IceResistance, float.NaN, 2), // Thunder Lich
        new(9, MonsterValue.PoisonResistance, float.NaN, 2),
        new(9, MonsterValue.LightningResistance, float.NaN, 3),
        new(9, MonsterValue.FireResistance, float.NaN, 2),
        new(9, MonsterValue.WaterResistance, 3, float.NaN),
        new(10, MonsterValue.LightningResistance, float.NaN, 3), // Dark Knight
        new(10, MonsterValue.FireResistance, 3, 5),
        new(10, MonsterValue.WaterResistance, 3, float.NaN),
        new(11, MonsterValue.LightningResistance, float.NaN, 2), // Ghost
        new(11, MonsterValue.WaterResistance, 2, float.NaN),
        new(12, MonsterValue.IceResistance, float.NaN, 2), // Larva
        new(12, MonsterValue.PoisonResistance, float.NaN, 0),
        new(12, MonsterValue.LightningResistance, float.NaN, 0),
        new(12, MonsterValue.FireResistance, float.NaN, 0),
        new(13, MonsterValue.IceResistance, float.NaN, 3), // Hell Spider
        new(13, MonsterValue.PoisonResistance, 3, 1),
        new(13, MonsterValue.LightningResistance, float.NaN, 1),
        new(13, MonsterValue.FireResistance, float.NaN, 1),
        new(14, MonsterValue.IceResistance, float.NaN, 0), // Skeleton Warrior
        new(14, MonsterValue.PoisonResistance, float.NaN, 0),
        new(14, MonsterValue.LightningResistance, float.NaN, 0),
        new(14, MonsterValue.FireResistance, float.NaN, 0),
        new(15, MonsterValue.IceResistance, float.NaN, 1), // Skeleton Archer
        new(15, MonsterValue.PoisonResistance, float.NaN, 1),
        new(15, MonsterValue.LightningResistance, float.NaN, 1),
        new(15, MonsterValue.FireResistance, float.NaN, 1),
        new(16, MonsterValue.IceResistance, float.NaN, 1), // Elite Skeleton
        new(16, MonsterValue.PoisonResistance, float.NaN, 1),
        new(16, MonsterValue.LightningResistance, float.NaN, 1),
        new(16, MonsterValue.FireResistance, float.NaN, 1),
        new(17, MonsterValue.IceResistance, float.NaN, 2), // Cyclops
        new(17, MonsterValue.PoisonResistance, 2, 0),
        new(17, MonsterValue.LightningResistance, float.NaN, 0),
        new(17, MonsterValue.FireResistance, float.NaN, 0),
        new(18, MonsterValue.LightningResistance, float.NaN, 6), // Gorgon
        new(18, MonsterValue.WaterResistance, 6, float.NaN),
        new(19, MonsterValue.IceResistance, 3, 0), // Yeti
        new(19, MonsterValue.PoisonResistance, float.NaN, 3),
        new(19, MonsterValue.LightningResistance, float.NaN, 0),
        new(19, MonsterValue.FireResistance, float.NaN, 0),
        new(20, MonsterValue.IceResistance, 4, 1), // Elite Yeti
        new(20, MonsterValue.PoisonResistance, 1, 4),
        new(20, MonsterValue.LightningResistance, float.NaN, 1),
        new(20, MonsterValue.WaterResistance, 1, float.NaN),
        new(21, MonsterValue.IceResistance, float.NaN, 0), // Assassin
        new(21, MonsterValue.PoisonResistance, float.NaN, 0),
        new(21, MonsterValue.LightningResistance, float.NaN, 0),
        new(21, MonsterValue.FireResistance, float.NaN, 0),
        new(22, MonsterValue.IceResistance, 3, 0), // Ice Monster
        new(22, MonsterValue.PoisonResistance, float.NaN, 3),
        new(22, MonsterValue.LightningResistance, float.NaN, 0),
        new(22, MonsterValue.FireResistance, float.NaN, 0),
        new(23, MonsterValue.IceResistance, 3, 0), // Hommerd
        new(23, MonsterValue.PoisonResistance, float.NaN, 3),
        new(23, MonsterValue.LightningResistance, float.NaN, 0),
        new(23, MonsterValue.FireResistance, float.NaN, 0),
        new(24, MonsterValue.IceResistance, 2, 0), // Worm
        new(24, MonsterValue.PoisonResistance, float.NaN, 2),
        new(24, MonsterValue.LightningResistance, float.NaN, 0),
        new(24, MonsterValue.FireResistance, float.NaN, 0),
        new(25, MonsterValue.IceResistance, 5, 4), // Ice Queen
        new(25, MonsterValue.PoisonResistance, 4, 5),
        new(25, MonsterValue.LightningResistance, float.NaN, 4),
        new(25, MonsterValue.WaterResistance, 4, float.NaN),
        new(26, MonsterValue.LightningResistance, float.NaN, 0), // Goblin
        new(26, MonsterValue.WaterResistance, 0, float.NaN),
        new(27, MonsterValue.IceResistance, float.NaN, 0), // Chain Scorpion
        new(27, MonsterValue.PoisonResistance, float.NaN, 0),
        new(27, MonsterValue.LightningResistance, float.NaN, 0),
        new(27, MonsterValue.FireResistance, float.NaN, 0),
        new(28, MonsterValue.IceResistance, float.NaN, 0), // Beetle Monster
        new(28, MonsterValue.PoisonResistance, float.NaN, 0),
        new(28, MonsterValue.LightningResistance, float.NaN, 0),
        new(28, MonsterValue.FireResistance, float.NaN, 0),
        new(29, MonsterValue.IceResistance, float.NaN, 0), // Hunter
        new(29, MonsterValue.PoisonResistance, float.NaN, 0),
        new(29, MonsterValue.LightningResistance, float.NaN, 0),
        new(29, MonsterValue.FireResistance, float.NaN, 0),
        new(30, MonsterValue.IceResistance, float.NaN, 0), // Forest Monster
        new(30, MonsterValue.PoisonResistance, float.NaN, 0),
        new(30, MonsterValue.LightningResistance, float.NaN, 0),
        new(30, MonsterValue.FireResistance, float.NaN, 0),
        new(31, MonsterValue.IceResistance, float.NaN, 0), // Agon
        new(31, MonsterValue.PoisonResistance, float.NaN, 0),
        new(31, MonsterValue.LightningResistance, float.NaN, 0),
        new(31, MonsterValue.FireResistance, float.NaN, 0),
        new(32, MonsterValue.IceResistance, float.NaN, 0), // Stone Golem
        new(32, MonsterValue.PoisonResistance, float.NaN, 0),
        new(32, MonsterValue.LightningResistance, float.NaN, 0),
        new(32, MonsterValue.FireResistance, float.NaN, 0),
        new(33, MonsterValue.IceResistance, float.NaN, 0), // Elite Goblin
        new(33, MonsterValue.PoisonResistance, float.NaN, 0),
        new(33, MonsterValue.LightningResistance, float.NaN, 0),
        new(33, MonsterValue.FireResistance, float.NaN, 0),
        new(34, MonsterValue.LightningResistance, float.NaN, 7), // Cursed Wizard
        new(34, MonsterValue.WaterResistance, 7, float.NaN),
        new(35, MonsterValue.LightningResistance, float.NaN, 6), // Death Gorgon
        new(35, MonsterValue.WaterResistance, 6, float.NaN),
        new(36, MonsterValue.LightningResistance, float.NaN, 3), // Shadow
        new(36, MonsterValue.WaterResistance, 3, float.NaN),
        new(37, MonsterValue.LightningResistance, float.NaN, 5), // Devil
        new(37, MonsterValue.WaterResistance, 5, float.NaN),
        new(38, MonsterValue.LightningResistance, float.NaN, 10), // Balrog
        new(38, MonsterValue.WaterResistance, 10, float.NaN),
        new(39, MonsterValue.IceResistance, 4, 6), // Poison Shadow
        new(39, MonsterValue.PoisonResistance, 6, 4),
        new(39, MonsterValue.LightningResistance, float.NaN, 4),
        new(39, MonsterValue.WaterResistance, 4, float.NaN),
        new(40, MonsterValue.LightningResistance, float.NaN, 6), // Death Knight
        new(40, MonsterValue.WaterResistance, 6, float.NaN),
        new(41, MonsterValue.LightningResistance, float.NaN, 5), // Death Cow
        new(41, MonsterValue.WaterResistance, 5, float.NaN),
        new(43, MonsterValue.LightningResistance, float.NaN, 2), // Golden Budge Dragon
        new(43, MonsterValue.WaterResistance, 2, float.NaN),
        new(44, MonsterValue.IceResistance, 7, 9), // Red Dragon
        new(44, MonsterValue.PoisonResistance, 9, 7),
        new(44, MonsterValue.LightningResistance, float.NaN, 9),
        new(44, MonsterValue.WaterResistance, 9, float.NaN),
        new(45, MonsterValue.LightningResistance, float.NaN, 1), // Bahamut
        new(45, MonsterValue.WaterResistance, 1, float.NaN),
        new(46, MonsterValue.LightningResistance, float.NaN, 3), // Vepar
        new(46, MonsterValue.WaterResistance, 3, float.NaN),
        new(47, MonsterValue.IceResistance, 2, 6), // Valkyrie
        new(47, MonsterValue.PoisonResistance, 6, 2),
        new(47, MonsterValue.LightningResistance, float.NaN, 2),
        new(47, MonsterValue.WaterResistance, 2, float.NaN),
        new(48, MonsterValue.LightningResistance, float.NaN, 7), // Lizard King
        new(48, MonsterValue.WaterResistance, 7, float.NaN),
        new(49, MonsterValue.LightningResistance, float.NaN, 12), // Hydra
        new(49, MonsterValue.WaterResistance, 12, float.NaN),
        new(50, MonsterValue.LightningResistance, float.NaN, 12), // Sea Worm
        new(50, MonsterValue.WaterResistance, 12, float.NaN),
        new(51, MonsterValue.LightningResistance, float.NaN, 6), // Great Bahamut
        new(51, MonsterValue.WaterResistance, 6, float.NaN),
        new(52, MonsterValue.LightningResistance, float.NaN, 7), // Silver Valkyrie
        new(52, MonsterValue.WaterResistance, 7, float.NaN),
        new(53, MonsterValue.LightningResistance, float.NaN, 5), // Golden Titan
        new(53, MonsterValue.WaterResistance, 5, float.NaN),
        new(54, MonsterValue.LightningResistance, float.NaN, 3), // Golden Soldier
        new(54, MonsterValue.WaterResistance, 3, float.NaN),
        new(57, MonsterValue.LightningResistance, float.NaN, 9), // Iron Wheel
        new(57, MonsterValue.WaterResistance, 9, float.NaN),
        new(58, MonsterValue.LightningResistance, float.NaN, 9), // Tantallos
        new(58, MonsterValue.WaterResistance, 9, float.NaN),
        new(59, MonsterValue.LightningResistance, float.NaN, 13), // Zaikan
        new(59, MonsterValue.WaterResistance, 13, float.NaN),
        new(60, MonsterValue.LightningResistance, float.NaN, 8), // Bloody Wolf
        new(60, MonsterValue.WaterResistance, 8, float.NaN),
        new(61, MonsterValue.LightningResistance, float.NaN, 10), // Beam Knight
        new(61, MonsterValue.WaterResistance, 10, float.NaN),
        new(62, MonsterValue.LightningResistance, float.NaN, 8), // Mutant
        new(62, MonsterValue.WaterResistance, 8, float.NaN),
        new(63, MonsterValue.LightningResistance, float.NaN, 13), // Death Beam Knight
        new(63, MonsterValue.WaterResistance, 13, float.NaN),
        new(64, MonsterValue.LightningResistance, float.NaN, 7), // Orc Archer
        new(64, MonsterValue.WaterResistance, 7, float.NaN),
        new(65, MonsterValue.LightningResistance, float.NaN, 8), // Elite Orc
        new(65, MonsterValue.WaterResistance, 8, float.NaN),
        new(66, MonsterValue.LightningResistance, float.NaN, 17), // Cursed King
        new(66, MonsterValue.WaterResistance, 17, float.NaN),
        new(67, MonsterValue.LightningResistance, float.NaN, 9), // Metal Balrog
        new(67, MonsterValue.WaterResistance, 9, float.NaN),
        new(69, MonsterValue.LightningResistance, float.NaN, 11), // Alquamos
        new(69, MonsterValue.WaterResistance, 11, float.NaN),
        new(70, MonsterValue.IceResistance, 11, 9), // Queen Rainer
        new(70, MonsterValue.PoisonResistance, 9, 11),
        new(70, MonsterValue.LightningResistance, float.NaN, 12),
        new(70, MonsterValue.WaterResistance, 12, float.NaN),
        new(71, MonsterValue.LightningResistance, float.NaN, 12), // Mega Crust
        new(71, MonsterValue.WaterResistance, 12, float.NaN),
        new(72, MonsterValue.LightningResistance, float.NaN, 16), // Phantom Knight
        new(72, MonsterValue.WaterResistance, 16, float.NaN),
        new(73, MonsterValue.LightningResistance, float.NaN, 13), // Drakan
        new(73, MonsterValue.WaterResistance, 13, float.NaN),
        new(74, MonsterValue.LightningResistance, float.NaN, 14), // Alpha Crust
        new(74, MonsterValue.WaterResistance, 14, float.NaN),
        new(75, MonsterValue.LightningResistance, float.NaN, 17), // Great Drakan
        new(75, MonsterValue.WaterResistance, 17, float.NaN),
        new(76, MonsterValue.LightningResistance, float.NaN, 35), // Dark Phoenix Shield
        new(76, MonsterValue.WaterResistance, 35, float.NaN),
        new(77, MonsterValue.LightningResistance, float.NaN, 35), // Dark Phoenix
        new(77, MonsterValue.WaterResistance, 35, float.NaN),
        new(78, MonsterValue.LightningResistance, float.NaN, 2), // Golden Goblin
        new(78, MonsterValue.WaterResistance, 2, float.NaN),
        new(79, MonsterValue.IceResistance, 7, 9), // Golden Dragon
        new(79, MonsterValue.PoisonResistance, 9, 7),
        new(79, MonsterValue.LightningResistance, float.NaN, 9),
        new(79, MonsterValue.WaterResistance, 9, float.NaN),
        new(80, MonsterValue.IceResistance, 7, 9), // Golden Lizard King
        new(80, MonsterValue.PoisonResistance, 9, 7),
        new(80, MonsterValue.LightningResistance, float.NaN, 9),
        new(80, MonsterValue.WaterResistance, 9, float.NaN),
        new(81, MonsterValue.IceResistance, float.NaN, 0), // Golden Vepar
        new(81, MonsterValue.PoisonResistance, float.NaN, 0),
        new(81, MonsterValue.LightningResistance, float.NaN, 3),
        new(81, MonsterValue.FireResistance, float.NaN, 0),
        new(81, MonsterValue.WaterResistance, 3, float.NaN),
        new(82, MonsterValue.IceResistance, 7, 9), // Golden Tantallos
        new(82, MonsterValue.PoisonResistance, 9, 7),
        new(82, MonsterValue.LightningResistance, float.NaN, 9),
        new(82, MonsterValue.WaterResistance, 9, float.NaN),
        new(83, MonsterValue.IceResistance, 7, 9), // Golden Wheel
        new(83, MonsterValue.PoisonResistance, 9, 7),
        new(83, MonsterValue.LightningResistance, float.NaN, 9),
        new(83, MonsterValue.WaterResistance, 9, float.NaN),
        new(84, MonsterValue.IceResistance, 2, 0), // Chief Skeleton Warrior 1
        new(84, MonsterValue.PoisonResistance, 2, 0),
        new(84, MonsterValue.LightningResistance, 2, 0),
        new(84, MonsterValue.FireResistance, 2, 0),
        new(85, MonsterValue.IceResistance, 2, 0), // Chief Skeleton Archer 1
        new(85, MonsterValue.PoisonResistance, 2, 0),
        new(85, MonsterValue.LightningResistance, 2, 0),
        new(85, MonsterValue.FireResistance, 2, 0),
        new(86, MonsterValue.IceResistance, 2, 0), // Dark Skull Soldier 1
        new(86, MonsterValue.PoisonResistance, 2, 0),
        new(86, MonsterValue.LightningResistance, 2, 0),
        new(86, MonsterValue.FireResistance, 2, 0),
        new(87, MonsterValue.IceResistance, 2, 0), // Giant Ogre 1
        new(87, MonsterValue.PoisonResistance, 2, 0),
        new(87, MonsterValue.LightningResistance, 2, 0),
        new(87, MonsterValue.FireResistance, 2, 0),
        new(88, MonsterValue.IceResistance, 2, 0), // Red Skeleton Knight 1
        new(88, MonsterValue.PoisonResistance, 2, 0),
        new(88, MonsterValue.LightningResistance, 2, 0),
        new(88, MonsterValue.FireResistance, 2, 0),
        new(89, MonsterValue.IceResistance, 5, 3), // Magic Skeleton 1
        new(89, MonsterValue.PoisonResistance, 5, 3),
        new(89, MonsterValue.LightningResistance, 5, 3),
        new(89, MonsterValue.FireResistance, 5, 3),
        new(90, MonsterValue.IceResistance, 4, 1), // Chief Skeleton Warrior 2
        new(90, MonsterValue.PoisonResistance, 4, 1),
        new(90, MonsterValue.LightningResistance, 4, 1),
        new(90, MonsterValue.FireResistance, 4, 1),
        new(91, MonsterValue.IceResistance, 4, 1), // Chief Skeleton Archer 2
        new(91, MonsterValue.PoisonResistance, 4, 1),
        new(91, MonsterValue.LightningResistance, 4, 1),
        new(91, MonsterValue.FireResistance, 4, 1),
        new(92, MonsterValue.IceResistance, 4, 1), // Dark Skull Soldier 2
        new(92, MonsterValue.PoisonResistance, 4, 1),
        new(92, MonsterValue.LightningResistance, 4, 1),
        new(92, MonsterValue.FireResistance, 4, 1),
        new(93, MonsterValue.IceResistance, 4, 1), // Giant Ogre 2
        new(93, MonsterValue.PoisonResistance, 4, 1),
        new(93, MonsterValue.LightningResistance, 4, 1),
        new(93, MonsterValue.FireResistance, 4, 1),
        new(94, MonsterValue.IceResistance, 4, 1), // Red Skeleton Knight 2
        new(94, MonsterValue.PoisonResistance, 4, 1),
        new(94, MonsterValue.LightningResistance, 4, 1),
        new(94, MonsterValue.FireResistance, 4, 1),
        new(95, MonsterValue.IceResistance, 7, 4), // Magic Skeleton 2
        new(95, MonsterValue.PoisonResistance, 7, 4),
        new(95, MonsterValue.LightningResistance, 7, 4),
        new(95, MonsterValue.FireResistance, 7, 4),
        new(96, MonsterValue.IceResistance, 5, 2), // Chief Skeleton Warrior 3
        new(96, MonsterValue.PoisonResistance, 5, 2),
        new(96, MonsterValue.LightningResistance, 5, 2),
        new(96, MonsterValue.FireResistance, 5, 2),
        new(97, MonsterValue.IceResistance, 5, 2), // Chief Skeleton Archer 3
        new(97, MonsterValue.PoisonResistance, 5, 2),
        new(97, MonsterValue.LightningResistance, 5, 2),
        new(97, MonsterValue.FireResistance, 5, 2),
        new(98, MonsterValue.IceResistance, 5, 2), // Dark Skull Soldier 3
        new(98, MonsterValue.PoisonResistance, 5, 2),
        new(98, MonsterValue.LightningResistance, 5, 2),
        new(98, MonsterValue.FireResistance, 5, 2),
        new(99, MonsterValue.IceResistance, 5, 2), // Giant Ogre 3
        new(99, MonsterValue.PoisonResistance, 5, 2),
        new(99, MonsterValue.LightningResistance, 5, 2),
        new(99, MonsterValue.FireResistance, 5, 2),
        new(111, MonsterValue.IceResistance, 5, 2), // Red Skeleton Knight 3
        new(111, MonsterValue.PoisonResistance, 5, 2),
        new(111, MonsterValue.LightningResistance, 5, 2),
        new(111, MonsterValue.FireResistance, 5, 2),
        new(112, MonsterValue.IceResistance, 8, 5), // Magic Skeleton 3
        new(112, MonsterValue.PoisonResistance, 8, 5),
        new(112, MonsterValue.LightningResistance, 8, 5),
        new(112, MonsterValue.FireResistance, 8, 5),
        new(113, MonsterValue.IceResistance, 6, 4), // Chief Skeleton Warrior 4
        new(113, MonsterValue.PoisonResistance, 6, 4),
        new(113, MonsterValue.LightningResistance, 6, 4),
        new(113, MonsterValue.FireResistance, 6, 4),
        new(114, MonsterValue.IceResistance, 6, 4), // Chief Skeleton Archer 4
        new(114, MonsterValue.PoisonResistance, 6, 4),
        new(114, MonsterValue.LightningResistance, 6, 4),
        new(114, MonsterValue.FireResistance, 6, 4),
        new(115, MonsterValue.IceResistance, 6, 4), // Dark Skull Soldier 4
        new(115, MonsterValue.PoisonResistance, 6, 4),
        new(115, MonsterValue.LightningResistance, 6, 4),
        new(115, MonsterValue.FireResistance, 6, 4),
        new(116, MonsterValue.IceResistance, 6, 4), // Giant Ogre 4
        new(116, MonsterValue.PoisonResistance, 6, 4),
        new(116, MonsterValue.LightningResistance, 6, 4),
        new(116, MonsterValue.FireResistance, 6, 4),
        new(117, MonsterValue.IceResistance, 6, 4), // Red Skeleton Knight 4
        new(117, MonsterValue.PoisonResistance, 6, 4),
        new(117, MonsterValue.LightningResistance, 6, 4),
        new(117, MonsterValue.FireResistance, 6, 4),
        new(118, MonsterValue.IceResistance, 9, 7), // Magic Skeleton 4
        new(118, MonsterValue.PoisonResistance, 9, 7),
        new(118, MonsterValue.LightningResistance, 9, 7),
        new(118, MonsterValue.FireResistance, 9, 7),
        new(119, MonsterValue.IceResistance, 7, 5), // Chief Skeleton Warrior 5
        new(119, MonsterValue.PoisonResistance, 7, 5),
        new(119, MonsterValue.LightningResistance, 7, 5),
        new(119, MonsterValue.FireResistance, 7, 5),
        new(120, MonsterValue.IceResistance, 7, 5), // Chief Skeleton Archer 5
        new(120, MonsterValue.PoisonResistance, 7, 5),
        new(120, MonsterValue.LightningResistance, 7, 5),
        new(120, MonsterValue.FireResistance, 7, 5),
        new(121, MonsterValue.IceResistance, 7, 5), // Dark Skull Soldier 5
        new(121, MonsterValue.PoisonResistance, 7, 5),
        new(121, MonsterValue.LightningResistance, 7, 5),
        new(121, MonsterValue.FireResistance, 7, 5),
        new(122, MonsterValue.IceResistance, 7, 5), // Giant Ogre 5
        new(122, MonsterValue.PoisonResistance, 7, 5),
        new(122, MonsterValue.LightningResistance, 7, 5),
        new(122, MonsterValue.FireResistance, 7, 5),
        new(123, MonsterValue.IceResistance, 7, 5), // Red Skeleton Knight 5
        new(123, MonsterValue.PoisonResistance, 7, 5),
        new(123, MonsterValue.LightningResistance, 7, 5),
        new(123, MonsterValue.FireResistance, 7, 5),
        new(124, MonsterValue.IceResistance, 10, 8), // Magic Skeleton 5
        new(124, MonsterValue.PoisonResistance, 10, 8),
        new(124, MonsterValue.LightningResistance, 10, 8),
        new(124, MonsterValue.FireResistance, 10, 8),
        new(138, MonsterValue.IceResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.PoisonResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.LightningResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.FireResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.IceResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.PoisonResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.LightningResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.FireResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.IceResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.PoisonResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.LightningResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.FireResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.IceResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.PoisonResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.LightningResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.FireResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.IceResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.PoisonResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.LightningResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.FireResistance, 8, 7), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.IceResistance, 11, 10), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.PoisonResistance, 11, 10), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.LightningResistance, 11, 10), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.FireResistance, 11, 10), // without the Blood Castle 7/8 swap
        new(150, MonsterValue.LightningResistance, float.NaN, 6), // Bali
        new(150, MonsterValue.WaterResistance, 6, float.NaN),
        new(151, MonsterValue.LightningResistance, float.NaN, 6), // Soldier
        new(151, MonsterValue.WaterResistance, 6, float.NaN),
        new(162, MonsterValue.IceResistance, 1, 0), // Chaos Castle 1
        new(162, MonsterValue.PoisonResistance, 1, 0),
        new(162, MonsterValue.LightningResistance, 1, 0),
        new(162, MonsterValue.FireResistance, 1, 0),
        new(163, MonsterValue.IceResistance, 1, 0), // Chaos Castle 2
        new(163, MonsterValue.PoisonResistance, 1, 0),
        new(163, MonsterValue.LightningResistance, 1, 0),
        new(163, MonsterValue.FireResistance, 1, 0),
        new(164, MonsterValue.IceResistance, 2, 1), // Chaos Castle 3
        new(164, MonsterValue.PoisonResistance, 2, 1),
        new(164, MonsterValue.LightningResistance, 2, 1),
        new(164, MonsterValue.FireResistance, 2, 1),
        new(165, MonsterValue.IceResistance, 2, 1), // Chaos Castle 4
        new(165, MonsterValue.PoisonResistance, 2, 1),
        new(165, MonsterValue.LightningResistance, 2, 1),
        new(165, MonsterValue.FireResistance, 2, 1),
        new(166, MonsterValue.IceResistance, 3, 2), // Chaos Castle 5
        new(166, MonsterValue.PoisonResistance, 3, 2),
        new(166, MonsterValue.LightningResistance, 3, 2),
        new(166, MonsterValue.FireResistance, 3, 2),
        new(167, MonsterValue.IceResistance, 3, 2), // Chaos Castle 6
        new(167, MonsterValue.PoisonResistance, 3, 2),
        new(167, MonsterValue.LightningResistance, 3, 2),
        new(167, MonsterValue.FireResistance, 3, 2),
        new(304, MonsterValue.LightningResistance, float.NaN, 22), // Witch Queen
        new(305, MonsterValue.LightningResistance, float.NaN, 21), // Blue Golem
        new(306, MonsterValue.LightningResistance, float.NaN, 20), // Death Rider
        new(307, MonsterValue.LightningResistance, float.NaN, 19), // Forest Orc
        new(308, MonsterValue.LightningResistance, float.NaN, 18), // Death Tree
        new(309, MonsterValue.LightningResistance, float.NaN, 50), // Hell Maine
        new(310, MonsterValue.LightningResistance, float.NaN, 15), // Hammer Scout
        new(311, MonsterValue.LightningResistance, float.NaN, 17), // Lance Scout
        new(312, MonsterValue.LightningResistance, float.NaN, 19), // Bow Scout
        new(313, MonsterValue.LightningResistance, float.NaN, 21), // Werewolf
        new(314, MonsterValue.LightningResistance, float.NaN, 23), // Scout(Hero)
        new(315, MonsterValue.LightningResistance, float.NaN, 25), // Werewolf(Hero)
        new(316, MonsterValue.LightningResistance, float.NaN, 27), // Balram
        new(317, MonsterValue.LightningResistance, float.NaN, 29), // Soram
        new(361, MonsterValue.IceResistance, 50, 150), // Nightmare
        new(361, MonsterValue.PoisonResistance, 50, 255),
        new(361, MonsterValue.LightningResistance, 50, 150),
        new(361, MonsterValue.FireResistance, 50, 150),
        new(362, MonsterValue.IceResistance, 40, 100), // Maya (Hand Left)
        new(362, MonsterValue.PoisonResistance, 40, 100),
        new(362, MonsterValue.LightningResistance, 40, 100),
        new(362, MonsterValue.FireResistance, 40, 100),
        new(363, MonsterValue.IceResistance, 40, 100), // Maya (Hand Right)
        new(363, MonsterValue.PoisonResistance, 40, 100),
        new(363, MonsterValue.LightningResistance, 40, 100),
        new(363, MonsterValue.FireResistance, 40, 100),
        new(364, MonsterValue.IceResistance, 50, 100), // Maya
        new(364, MonsterValue.PoisonResistance, 50, 100),
        new(364, MonsterValue.LightningResistance, 50, 100),
        new(364, MonsterValue.FireResistance, 50, 100),
        new(386, MonsterValue.IceResistance, float.NaN, 4), // Illusion Sorc. Spirit
        new(386, MonsterValue.PoisonResistance, float.NaN, 4),
        new(386, MonsterValue.LightningResistance, float.NaN, 4),
        new(386, MonsterValue.FireResistance, float.NaN, 4),
        new(387, MonsterValue.IceResistance, float.NaN, 4), // Illusion Sorc. Spirit
        new(387, MonsterValue.PoisonResistance, float.NaN, 4),
        new(387, MonsterValue.LightningResistance, float.NaN, 4),
        new(387, MonsterValue.FireResistance, float.NaN, 4),
        new(388, MonsterValue.IceResistance, float.NaN, 4), // Illusion Sorc. Spirit
        new(388, MonsterValue.PoisonResistance, float.NaN, 4),
        new(388, MonsterValue.LightningResistance, float.NaN, 4),
        new(388, MonsterValue.FireResistance, float.NaN, 4),
        new(389, MonsterValue.IceResistance, float.NaN, 4), // Illusion Sorc. Spirit
        new(389, MonsterValue.PoisonResistance, float.NaN, 4),
        new(389, MonsterValue.LightningResistance, float.NaN, 4),
        new(389, MonsterValue.FireResistance, float.NaN, 4),
        new(390, MonsterValue.IceResistance, float.NaN, 4), // Illusion Sorc. Spirit
        new(390, MonsterValue.PoisonResistance, float.NaN, 4),
        new(390, MonsterValue.LightningResistance, float.NaN, 4),
        new(390, MonsterValue.FireResistance, float.NaN, 4),
        new(391, MonsterValue.IceResistance, float.NaN, 7), // Illusion Sorc. Spirit
        new(391, MonsterValue.PoisonResistance, float.NaN, 7),
        new(391, MonsterValue.LightningResistance, float.NaN, 7),
        new(391, MonsterValue.FireResistance, float.NaN, 7),
        new(392, MonsterValue.IceResistance, float.NaN, 6), // Illusion Sorc. Spirit
        new(392, MonsterValue.PoisonResistance, float.NaN, 6),
        new(392, MonsterValue.LightningResistance, float.NaN, 6),
        new(392, MonsterValue.FireResistance, float.NaN, 6),
        new(393, MonsterValue.IceResistance, float.NaN, 6), // Illusion Sorc. Spirit
        new(393, MonsterValue.PoisonResistance, float.NaN, 6),
        new(393, MonsterValue.LightningResistance, float.NaN, 6),
        new(393, MonsterValue.FireResistance, float.NaN, 6),
        new(394, MonsterValue.IceResistance, float.NaN, 6), // Illusion Sorc. Spirit
        new(394, MonsterValue.PoisonResistance, float.NaN, 6),
        new(394, MonsterValue.LightningResistance, float.NaN, 6),
        new(394, MonsterValue.FireResistance, float.NaN, 6),
        new(395, MonsterValue.IceResistance, float.NaN, 6), // Illusion Sorc. Spirit
        new(395, MonsterValue.PoisonResistance, float.NaN, 6),
        new(395, MonsterValue.LightningResistance, float.NaN, 6),
        new(395, MonsterValue.FireResistance, float.NaN, 6),
        new(396, MonsterValue.IceResistance, float.NaN, 6), // Illusion Sorc. Spirit
        new(396, MonsterValue.PoisonResistance, float.NaN, 6),
        new(396, MonsterValue.LightningResistance, float.NaN, 6),
        new(396, MonsterValue.FireResistance, float.NaN, 6),
        new(397, MonsterValue.IceResistance, float.NaN, 9), // Illusion Sorc. Spirit
        new(397, MonsterValue.PoisonResistance, float.NaN, 9),
        new(397, MonsterValue.LightningResistance, float.NaN, 9),
        new(397, MonsterValue.FireResistance, float.NaN, 9),
        new(398, MonsterValue.IceResistance, float.NaN, 26), // Illusion Sorc. Spirit
        new(398, MonsterValue.PoisonResistance, float.NaN, 26),
        new(398, MonsterValue.LightningResistance, float.NaN, 26),
        new(398, MonsterValue.FireResistance, float.NaN, 26),
        new(399, MonsterValue.IceResistance, float.NaN, 27), // Illusion Sorc. Spirit
        new(399, MonsterValue.PoisonResistance, float.NaN, 27),
        new(399, MonsterValue.LightningResistance, float.NaN, 27),
        new(399, MonsterValue.FireResistance, float.NaN, 27),
        new(418, MonsterValue.IceResistance, float.NaN, 0), // Strange Rabbit
        new(418, MonsterValue.PoisonResistance, float.NaN, 0),
        new(418, MonsterValue.LightningResistance, float.NaN, 0),
        new(418, MonsterValue.FireResistance, float.NaN, 0),
        new(419, MonsterValue.IceResistance, float.NaN, 0), // Hideous Rabbit
        new(419, MonsterValue.PoisonResistance, float.NaN, 0),
        new(419, MonsterValue.LightningResistance, float.NaN, 0),
        new(419, MonsterValue.FireResistance, float.NaN, 0),
        new(420, MonsterValue.IceResistance, float.NaN, 0), // Werewolf
        new(420, MonsterValue.PoisonResistance, float.NaN, 0),
        new(420, MonsterValue.LightningResistance, float.NaN, 0),
        new(420, MonsterValue.FireResistance, float.NaN, 0),
        new(421, MonsterValue.IceResistance, float.NaN, 0), // Polluted Butterfly
        new(421, MonsterValue.PoisonResistance, float.NaN, 0),
        new(421, MonsterValue.LightningResistance, float.NaN, 0),
        new(421, MonsterValue.FireResistance, float.NaN, 0),
        new(422, MonsterValue.IceResistance, 4, 0), // Cursed Lich
        new(422, MonsterValue.PoisonResistance, 4, 2),
        new(422, MonsterValue.LightningResistance, float.NaN, 0),
        new(422, MonsterValue.FireResistance, 4, 0),
        new(422, MonsterValue.WaterResistance, 4, float.NaN),
        new(423, MonsterValue.IceResistance, float.NaN, 0), // Totem Golem
        new(423, MonsterValue.PoisonResistance, float.NaN, 0),
        new(423, MonsterValue.LightningResistance, float.NaN, 0),
        new(423, MonsterValue.FireResistance, float.NaN, 0),
        new(424, MonsterValue.IceResistance, float.NaN, 0), // Grizzly
        new(424, MonsterValue.PoisonResistance, float.NaN, 3),
        new(424, MonsterValue.LightningResistance, float.NaN, 0),
        new(424, MonsterValue.FireResistance, float.NaN, 0),
        new(425, MonsterValue.IceResistance, float.NaN, 3), // Captain Grizzly
        new(425, MonsterValue.PoisonResistance, float.NaN, 3),
        new(425, MonsterValue.LightningResistance, float.NaN, 3),
        new(425, MonsterValue.FireResistance, float.NaN, 5),
        new(427, MonsterValue.IceResistance, 6, 7), // Chaos Castle 14
        new(427, MonsterValue.PoisonResistance, 6, 7),
        new(427, MonsterValue.LightningResistance, 6, 7),
        new(427, MonsterValue.FireResistance, 6, 7),
        new(428, MonsterValue.IceResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(428, MonsterValue.PoisonResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(428, MonsterValue.LightningResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(428, MonsterValue.FireResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(429, MonsterValue.IceResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(429, MonsterValue.PoisonResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(429, MonsterValue.LightningResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(429, MonsterValue.FireResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(430, MonsterValue.IceResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(430, MonsterValue.PoisonResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(430, MonsterValue.LightningResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(430, MonsterValue.FireResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(431, MonsterValue.IceResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(431, MonsterValue.PoisonResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(431, MonsterValue.LightningResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(431, MonsterValue.FireResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(432, MonsterValue.IceResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(432, MonsterValue.PoisonResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(432, MonsterValue.LightningResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(432, MonsterValue.FireResistance, 7, 8), // without the Blood Castle 7/8 swap
        new(433, MonsterValue.IceResistance, 10, 11), // without the Blood Castle 7/8 swap
        new(433, MonsterValue.PoisonResistance, 10, 11), // without the Blood Castle 7/8 swap
        new(433, MonsterValue.LightningResistance, 10, 11), // without the Blood Castle 7/8 swap
        new(433, MonsterValue.FireResistance, 10, 11), // without the Blood Castle 7/8 swap
        new(434, MonsterValue.LightningResistance, float.NaN, 25), // Gigantis
        new(434, MonsterValue.WaterResistance, 25, float.NaN),
        new(435, MonsterValue.LightningResistance, float.NaN, 50), // Berserk
        new(435, MonsterValue.WaterResistance, 50, float.NaN),
        new(436, MonsterValue.LightningResistance, float.NaN, 23), // Balram (Trainee)
        new(436, MonsterValue.WaterResistance, 23, float.NaN),
        new(437, MonsterValue.LightningResistance, float.NaN, 25), // Soram (Trainee)
        new(437, MonsterValue.WaterResistance, 25, float.NaN),
        new(438, MonsterValue.LightningResistance, float.NaN, 29), // Persona
        new(438, MonsterValue.WaterResistance, 29, float.NaN),
        new(439, MonsterValue.LightningResistance, float.NaN, 27), // Dreadfear
        new(439, MonsterValue.WaterResistance, 27, float.NaN),
        new(440, MonsterValue.LightningResistance, float.NaN, 23), // Dark_Elf
        new(440, MonsterValue.WaterResistance, 23, float.NaN),
        new(441, MonsterValue.IceResistance, float.NaN, 0), // Sapi-Unus
        new(441, MonsterValue.LightningResistance, float.NaN, 0),
        new(441, MonsterValue.FireResistance, float.NaN, 0),
        new(442, MonsterValue.IceResistance, float.NaN, 0), // Sapi-Duo
        new(442, MonsterValue.LightningResistance, float.NaN, 0),
        new(442, MonsterValue.FireResistance, float.NaN, 0),
        new(443, MonsterValue.IceResistance, float.NaN, 0), // Sapi-Tres
        new(443, MonsterValue.LightningResistance, float.NaN, 0),
        new(443, MonsterValue.FireResistance, float.NaN, 0),
        new(444, MonsterValue.LightningResistance, float.NaN, 0), // Shadow Pawn
        new(444, MonsterValue.FireResistance, float.NaN, 0),
        new(445, MonsterValue.LightningResistance, float.NaN, 0), // Shadow Knight
        new(445, MonsterValue.FireResistance, float.NaN, 0),
        new(446, MonsterValue.LightningResistance, float.NaN, 0), // Shadow Look
        new(446, MonsterValue.FireResistance, float.NaN, 0),
        new(447, MonsterValue.IceResistance, float.NaN, 0), // Thunder Napin
        new(447, MonsterValue.PoisonResistance, float.NaN, 0),
        new(447, MonsterValue.FireResistance, float.NaN, 0),
        new(448, MonsterValue.IceResistance, float.NaN, 0), // Ghost Napin
        new(448, MonsterValue.LightningResistance, float.NaN, 0),
        new(448, MonsterValue.FireResistance, float.NaN, 0),
        new(449, MonsterValue.IceResistance, float.NaN, 0), // Blaze Napin
        new(449, MonsterValue.PoisonResistance, float.NaN, 0),
        new(449, MonsterValue.LightningResistance, float.NaN, 0),
        new(459, MonsterValue.LightningResistance, float.NaN, 150), // Selupan
        new(459, MonsterValue.WaterResistance, 150, float.NaN),
        new(460, MonsterValue.LightningResistance, float.NaN, 50), // Spider Eggs 1
        new(460, MonsterValue.WaterResistance, 50, float.NaN),
        new(461, MonsterValue.LightningResistance, float.NaN, 50), // Spider Eggs 2
        new(461, MonsterValue.WaterResistance, 50, float.NaN),
        new(462, MonsterValue.LightningResistance, float.NaN, 50), // Spider Eggs 3
        new(462, MonsterValue.WaterResistance, 50, float.NaN),
        new(480, MonsterValue.LightningResistance, float.NaN, 200), // Zombie Fighter
        new(481, MonsterValue.LightningResistance, float.NaN, 200), // Zombie Fighter
        new(481, MonsterValue.FireResistance, 200, 250),
        new(482, MonsterValue.LightningResistance, float.NaN, 200), // Resurrected Gladiator
        new(483, MonsterValue.LightningResistance, float.NaN, 200), // Resurrected Gladiator
        new(484, MonsterValue.LightningResistance, float.NaN, 200), // Ash Slaughterer
        new(485, MonsterValue.LightningResistance, float.NaN, 200), // Ash Slaughterer
        new(486, MonsterValue.LightningResistance, float.NaN, 200), // Blood Assassin
        new(487, MonsterValue.LightningResistance, float.NaN, 200), // Cruel Blood Assassin
        new(488, MonsterValue.LightningResistance, float.NaN, 200), // Cruel Blood Assassin
        new(489, MonsterValue.LightningResistance, float.NaN, 200), // Burning Lava Giant
        new(490, MonsterValue.LightningResistance, float.NaN, 200), // Ruthless Lava Giant
        new(491, MonsterValue.LightningResistance, float.NaN, 200), // Ruthless Lava Giant
        new(504, MonsterValue.LightningResistance, float.NaN, 29), // Gayion The Gladiator
        new(504, MonsterValue.WaterResistance, 29, float.NaN),
        new(505, MonsterValue.LightningResistance, float.NaN, 150), // Jerry
        new(505, MonsterValue.WaterResistance, 150, float.NaN),
        new(506, MonsterValue.LightningResistance, float.NaN, 150), // Raymond
        new(506, MonsterValue.WaterResistance, 150, float.NaN),
        new(507, MonsterValue.LightningResistance, float.NaN, 150), // Lucas
        new(507, MonsterValue.WaterResistance, 150, float.NaN),
        new(508, MonsterValue.LightningResistance, float.NaN, 150), // Fred
        new(508, MonsterValue.WaterResistance, 150, float.NaN),
        new(509, MonsterValue.LightningResistance, float.NaN, 150), // Hammerize
        new(509, MonsterValue.WaterResistance, 150, float.NaN),
        new(510, MonsterValue.LightningResistance, float.NaN, 150), // Dual Berserker
        new(510, MonsterValue.WaterResistance, 150, float.NaN),
        new(511, MonsterValue.LightningResistance, float.NaN, 150), // Devil Lord
        new(511, MonsterValue.WaterResistance, 150, float.NaN),
        new(512, MonsterValue.LightningResistance, float.NaN, 23), // Quarter Master
        new(512, MonsterValue.WaterResistance, 23, float.NaN),
        new(513, MonsterValue.LightningResistance, float.NaN, 29), // Combat Instructor
        new(513, MonsterValue.WaterResistance, 29, float.NaN),
        new(514, MonsterValue.LightningResistance, float.NaN, 23), // Aticle's Head
        new(514, MonsterValue.WaterResistance, 23, float.NaN),
        new(515, MonsterValue.LightningResistance, float.NaN, 25), // Dark Ghost
        new(515, MonsterValue.WaterResistance, 25, float.NaN),
        new(516, MonsterValue.LightningResistance, float.NaN, 29), // Banshee
        new(516, MonsterValue.WaterResistance, 29, float.NaN),
        new(517, MonsterValue.LightningResistance, float.NaN, 60), // Head Mounter
        new(517, MonsterValue.WaterResistance, 60, float.NaN),
        new(518, MonsterValue.LightningResistance, float.NaN, 29), // Defender
        new(518, MonsterValue.WaterResistance, 29, float.NaN),
        new(519, MonsterValue.LightningResistance, float.NaN, 50), // Forsaker
        new(519, MonsterValue.WaterResistance, 50, float.NaN),
        new(520, MonsterValue.LightningResistance, float.NaN, 25), // Ocelot the Lord
        new(520, MonsterValue.WaterResistance, 25, float.NaN),
        new(521, MonsterValue.LightningResistance, float.NaN, 26), // Eric the Guard
        new(521, MonsterValue.WaterResistance, 26, float.NaN),
        new(523, MonsterValue.LightningResistance, float.NaN, 5), // Trap
        new(523, MonsterValue.WaterResistance, 5, float.NaN),
        new(549, MonsterValue.LightningResistance, float.NaN, 20), // Bloody Orc
        new(550, MonsterValue.LightningResistance, float.NaN, 20), // Bloody Death Rider
        new(551, MonsterValue.LightningResistance, float.NaN, 20), // Bloody Golem
        new(552, MonsterValue.LightningResistance, float.NaN, 20), // Bloody Witch Queen
        new(569, MonsterValue.LightningResistance, float.NaN, 100), // Venomous Chain Scorpion
        new(570, MonsterValue.LightningResistance, float.NaN, 100), // Bone Scorpion
        new(571, MonsterValue.LightningResistance, float.NaN, 100), // Orcus
        new(573, MonsterValue.LightningResistance, float.NaN, 100), // Crypta
        new(574, MonsterValue.LightningResistance, float.NaN, 100), // Crypos
    ];
}

// <copyright file="MonsterStatsFromSuitesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update gives the monsters the level, health, damage, defense, attack rate and defense rate of the Season 6/9 server suites:
/// Blood Castle 1-8 (it replaces the earlier swap of Blood Castle 7 and 8), the Kanturu bosses, Raklion, Chaos Castle 14, Kalima,
/// the golden invasion monsters and a few field monsters which were off by some points.
/// A value is only changed while it is still HNMO's old value (for Blood Castle 7 and 8 also the values from before the swap).
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("8B3D6E21-4C7A-4F19-A5E2-91D0C3B7F648")]
public class MonsterStatsFromSuitesUpdatePlugIn : MonsterValuesUpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Monster stats as in the suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Level, health, damage, defense, attack and defense rate of the Blood Castle 1-8, Kanturu, Raklion, Chaos Castle, Kalima, golden and other monsters as in the Season 6/9 server suites.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IReadOnlyList<MonsterValueChange> Changes => ChangeList;

    /// <summary>
    /// Gets the changes: monster number, value, the value HNMO had, the value of the suites.
    /// </summary>
    internal static IReadOnlyList<MonsterValueChange> ChangeList { get; } =
    [
        new(43, MonsterValue.MaximumHealth, 2500, 2200), // Golden Budge Dragon
        new(44, MonsterValue.Level, 47, 60), // Red Dragon
        new(44, MonsterValue.AttackRate, 400, 300),
        new(53, MonsterValue.MaximumHealth, 6500, 8000), // Golden Titan
        new(54, MonsterValue.MaximumHealth, 5000, 3500), // Golden Soldier
        new(78, MonsterValue.MaximumHealth, 3200, 3100), // Golden Goblin
        new(78, MonsterValue.DefenseRate, 50, 30),
        new(79, MonsterValue.MaximumHealth, 22000, 23000), // Golden Dragon
        new(81, MonsterValue.MaximumHealth, 10000, 6300), // Golden Vepar
        new(82, MonsterValue.MaximumHealth, 32000, 30000), // Golden Tantallos
        new(84, MonsterValue.Level, 56, 33), // Chief Skeleton Warrior 1
        new(84, MonsterValue.MaximumHealth, 5000, 1100),
        new(84, MonsterValue.MinimumDamage, 160, 100),
        new(84, MonsterValue.MaximumDamage, 180, 120),
        new(84, MonsterValue.Defense, 110, 55),
        new(84, MonsterValue.AttackRate, 300, 190),
        new(84, MonsterValue.DefenseRate, 85, 45),
        new(85, MonsterValue.Level, 61, 37), // Chief Skeleton Archer 1
        new(85, MonsterValue.MaximumHealth, 6500, 1400),
        new(85, MonsterValue.MinimumDamage, 180, 110),
        new(85, MonsterValue.MaximumDamage, 200, 130),
        new(85, MonsterValue.Defense, 120, 65),
        new(85, MonsterValue.AttackRate, 330, 210),
        new(85, MonsterValue.DefenseRate, 93, 49),
        new(86, MonsterValue.Level, 66, 41), // Dark Skull Soldier 1
        new(86, MonsterValue.MaximumHealth, 8000, 1700),
        new(86, MonsterValue.MinimumDamage, 190, 120),
        new(86, MonsterValue.MaximumDamage, 220, 140),
        new(86, MonsterValue.Defense, 160, 75),
        new(86, MonsterValue.AttackRate, 360, 230),
        new(86, MonsterValue.DefenseRate, 98, 61),
        new(87, MonsterValue.Level, 70, 44), // Giant Ogre 1
        new(87, MonsterValue.MaximumHealth, 9500, 2100),
        new(87, MonsterValue.MinimumDamage, 210, 130),
        new(87, MonsterValue.MaximumDamage, 240, 150),
        new(87, MonsterValue.Defense, 180, 85),
        new(87, MonsterValue.AttackRate, 400, 250),
        new(87, MonsterValue.DefenseRate, 115, 73),
        new(88, MonsterValue.Level, 74, 47), // Red Skeleton Knight 1
        new(88, MonsterValue.MaximumHealth, 12000, 2800),
        new(88, MonsterValue.MinimumDamage, 220, 140),
        new(88, MonsterValue.MaximumDamage, 260, 160),
        new(88, MonsterValue.Defense, 190, 95),
        new(88, MonsterValue.AttackRate, 440, 270),
        new(88, MonsterValue.DefenseRate, 130, 77),
        new(89, MonsterValue.Level, 79, 51), // Magic Skeleton 1
        new(89, MonsterValue.MaximumHealth, 15000, 3700),
        new(89, MonsterValue.MinimumDamage, 230, 150),
        new(89, MonsterValue.MaximumDamage, 280, 180),
        new(89, MonsterValue.Defense, 240, 115),
        new(89, MonsterValue.AttackRate, 500, 300),
        new(89, MonsterValue.DefenseRate, 180, 88),
        new(90, MonsterValue.Level, 64, 49), // Chief Skeleton Warrior 2
        new(90, MonsterValue.MaximumHealth, 7500, 3500),
        new(90, MonsterValue.MinimumDamage, 180, 140),
        new(90, MonsterValue.MaximumDamage, 220, 170),
        new(90, MonsterValue.Defense, 150, 90),
        new(90, MonsterValue.AttackRate, 340, 250),
        new(90, MonsterValue.DefenseRate, 98, 76),
        new(91, MonsterValue.Level, 69, 53), // Chief Skeleton Archer 2
        new(91, MonsterValue.MaximumHealth, 9500, 4100),
        new(91, MonsterValue.MinimumDamage, 200, 150),
        new(91, MonsterValue.MaximumDamage, 240, 180),
        new(91, MonsterValue.Defense, 170, 100),
        new(91, MonsterValue.AttackRate, 380, 275),
        new(91, MonsterValue.DefenseRate, 110, 80),
        new(92, MonsterValue.Level, 75, 56), // Dark Skull Soldier 2
        new(92, MonsterValue.MaximumHealth, 12000, 5000),
        new(92, MonsterValue.MinimumDamage, 220, 160),
        new(92, MonsterValue.MaximumDamage, 260, 190),
        new(92, MonsterValue.Defense, 190, 115),
        new(92, MonsterValue.AttackRate, 440, 300),
        new(92, MonsterValue.DefenseRate, 130, 88),
        new(93, MonsterValue.Level, 79, 60), // Giant Ogre 2
        new(93, MonsterValue.MaximumHealth, 15000, 6000),
        new(93, MonsterValue.MinimumDamage, 250, 175),
        new(93, MonsterValue.MaximumDamage, 290, 210),
        new(93, MonsterValue.Defense, 210, 130),
        new(93, MonsterValue.AttackRate, 500, 320),
        new(93, MonsterValue.DefenseRate, 150, 95),
        new(94, MonsterValue.Level, 82, 64), // Red Skeleton Knight 2
        new(94, MonsterValue.MaximumHealth, 18000, 7000),
        new(94, MonsterValue.MinimumDamage, 270, 185),
        new(94, MonsterValue.MaximumDamage, 320, 220),
        new(94, MonsterValue.Defense, 290, 170),
        new(94, MonsterValue.AttackRate, 560, 360),
        new(94, MonsterValue.DefenseRate, 170, 110),
        new(95, MonsterValue.Level, 88, 69), // Magic Skeleton 2
        new(95, MonsterValue.MaximumHealth, 24000, 8000),
        new(95, MonsterValue.MinimumDamage, 300, 200),
        new(95, MonsterValue.MaximumDamage, 350, 240),
        new(95, MonsterValue.Defense, 360, 190),
        new(95, MonsterValue.AttackRate, 640, 400),
        new(95, MonsterValue.DefenseRate, 200, 130),
        new(96, MonsterValue.Level, 70, 56), // Chief Skeleton Warrior 3
        new(96, MonsterValue.MaximumHealth, 12000, 5000),
        new(96, MonsterValue.MinimumDamage, 210, 160),
        new(96, MonsterValue.MaximumDamage, 270, 180),
        new(96, MonsterValue.Defense, 185, 110),
        new(96, MonsterValue.AttackRate, 380, 300),
        new(96, MonsterValue.DefenseRate, 125, 85),
        new(97, MonsterValue.Level, 75, 61), // Chief Skeleton Archer 3
        new(97, MonsterValue.MaximumHealth, 15000, 6500),
        new(97, MonsterValue.MinimumDamage, 240, 180),
        new(97, MonsterValue.MaximumDamage, 300, 200),
        new(97, MonsterValue.Defense, 190, 120),
        new(97, MonsterValue.AttackRate, 420, 330),
        new(97, MonsterValue.DefenseRate, 130, 93),
        new(98, MonsterValue.Level, 80, 66), // Dark Skull Soldier 3
        new(98, MonsterValue.MaximumHealth, 19000, 8000),
        new(98, MonsterValue.MinimumDamage, 280, 190),
        new(98, MonsterValue.MaximumDamage, 340, 220),
        new(98, MonsterValue.Defense, 220, 150),
        new(98, MonsterValue.AttackRate, 470, 360),
        new(98, MonsterValue.DefenseRate, 175, 98),
        new(99, MonsterValue.Level, 85, 70), // Giant Ogre 3
        new(99, MonsterValue.MaximumHealth, 23000, 9500),
        new(99, MonsterValue.MinimumDamage, 340, 210),
        new(99, MonsterValue.MaximumDamage, 390, 240),
        new(99, MonsterValue.Defense, 300, 180),
        new(99, MonsterValue.AttackRate, 520, 400),
        new(99, MonsterValue.DefenseRate, 190, 115),
        new(111, MonsterValue.Level, 87, 74), // Red Skeleton Knight 3
        new(111, MonsterValue.MaximumHealth, 26000, 12000),
        new(111, MonsterValue.MinimumDamage, 370, 220),
        new(111, MonsterValue.MaximumDamage, 410, 260),
        new(111, MonsterValue.Defense, 400, 190),
        new(111, MonsterValue.AttackRate, 650, 440),
        new(111, MonsterValue.DefenseRate, 200, 130),
        new(112, MonsterValue.Level, 90, 79), // Magic Skeleton 3
        new(112, MonsterValue.MaximumHealth, 32000, 15000),
        new(112, MonsterValue.MinimumDamage, 400, 230),
        new(112, MonsterValue.MaximumDamage, 460, 280),
        new(112, MonsterValue.Defense, 450, 240),
        new(112, MonsterValue.AttackRate, 720, 500),
        new(112, MonsterValue.DefenseRate, 260, 180),
        new(113, MonsterValue.Level, 76, 64), // Chief Skeleton Warrior 4
        new(113, MonsterValue.MaximumHealth, 17000, 7500),
        new(113, MonsterValue.MinimumDamage, 260, 180),
        new(113, MonsterValue.MaximumDamage, 290, 220),
        new(113, MonsterValue.Defense, 210, 150),
        new(113, MonsterValue.AttackRate, 460, 340),
        new(113, MonsterValue.DefenseRate, 150, 98),
        new(114, MonsterValue.Level, 81, 69), // Chief Skeleton Archer 4
        new(114, MonsterValue.MaximumHealth, 20000, 9500),
        new(114, MonsterValue.MinimumDamage, 320, 200),
        new(114, MonsterValue.MaximumDamage, 350, 240),
        new(114, MonsterValue.Defense, 270, 170),
        new(114, MonsterValue.AttackRate, 520, 380),
        new(114, MonsterValue.DefenseRate, 160, 110),
        new(115, MonsterValue.Level, 86, 75), // Dark Skull Soldier 4
        new(115, MonsterValue.MaximumHealth, 25000, 12000),
        new(115, MonsterValue.MinimumDamage, 390, 220),
        new(115, MonsterValue.MaximumDamage, 420, 260),
        new(115, MonsterValue.Defense, 360, 190),
        new(115, MonsterValue.AttackRate, 580, 440),
        new(115, MonsterValue.DefenseRate, 195, 130),
        new(116, MonsterValue.Level, 90, 79), // Giant Ogre 4
        new(116, MonsterValue.MaximumHealth, 32000, 15000),
        new(116, MonsterValue.MinimumDamage, 440, 250),
        new(116, MonsterValue.MaximumDamage, 500, 290),
        new(116, MonsterValue.Defense, 400, 210),
        new(116, MonsterValue.AttackRate, 660, 500),
        new(116, MonsterValue.DefenseRate, 230, 150),
        new(117, MonsterValue.Level, 94, 82), // Red Skeleton Knight 4
        new(117, MonsterValue.MaximumHealth, 38000, 18000),
        new(117, MonsterValue.MinimumDamage, 480, 270),
        new(117, MonsterValue.MaximumDamage, 540, 320),
        new(117, MonsterValue.Defense, 450, 290),
        new(117, MonsterValue.AttackRate, 720, 560),
        new(117, MonsterValue.DefenseRate, 260, 170),
        new(118, MonsterValue.Level, 99, 84), // Magic Skeleton 4
        new(118, MonsterValue.MaximumHealth, 47000, 24000),
        new(118, MonsterValue.MinimumDamage, 530, 300),
        new(118, MonsterValue.MaximumDamage, 580, 350),
        new(118, MonsterValue.Defense, 500, 360),
        new(118, MonsterValue.AttackRate, 840, 640),
        new(118, MonsterValue.DefenseRate, 280, 200),
        new(119, MonsterValue.Level, 85, 70), // Chief Skeleton Warrior 5
        new(119, MonsterValue.MaximumHealth, 23000, 12000),
        new(119, MonsterValue.MinimumDamage, 340, 210),
        new(119, MonsterValue.MaximumDamage, 390, 270),
        new(119, MonsterValue.Defense, 340, 185),
        new(119, MonsterValue.AttackRate, 520, 380),
        new(119, MonsterValue.DefenseRate, 190, 125),
        new(120, MonsterValue.Level, 87, 75), // Chief Skeleton Archer 5
        new(120, MonsterValue.MaximumHealth, 26000, 15000),
        new(120, MonsterValue.MinimumDamage, 390, 240),
        new(120, MonsterValue.MaximumDamage, 430, 300),
        new(120, MonsterValue.Defense, 380, 190),
        new(120, MonsterValue.AttackRate, 590, 420),
        new(120, MonsterValue.DefenseRate, 200, 130),
        new(121, MonsterValue.Level, 90, 80), // Dark Skull Soldier 5
        new(121, MonsterValue.MaximumHealth, 32000, 19000),
        new(121, MonsterValue.MinimumDamage, 430, 280),
        new(121, MonsterValue.MaximumDamage, 480, 340),
        new(121, MonsterValue.Defense, 400, 220),
        new(121, MonsterValue.AttackRate, 660, 470),
        new(121, MonsterValue.DefenseRate, 230, 175),
        new(122, MonsterValue.Level, 93, 85), // Giant Ogre 5
        new(122, MonsterValue.MaximumHealth, 37000, 23000),
        new(122, MonsterValue.MinimumDamage, 470, 340),
        new(122, MonsterValue.MaximumDamage, 520, 390),
        new(122, MonsterValue.Defense, 440, 300),
        new(122, MonsterValue.AttackRate, 730, 520),
        new(122, MonsterValue.DefenseRate, 250, 190),
        new(123, MonsterValue.Level, 98, 87), // Red Skeleton Knight 5
        new(123, MonsterValue.MaximumHealth, 45000, 26000),
        new(123, MonsterValue.MinimumDamage, 510, 370),
        new(123, MonsterValue.MaximumDamage, 560, 410),
        new(123, MonsterValue.Defense, 490, 400),
        new(123, MonsterValue.AttackRate, 800, 650),
        new(123, MonsterValue.DefenseRate, 270, 200),
        new(124, MonsterValue.Level, 102, 90), // Magic Skeleton 5
        new(124, MonsterValue.MaximumHealth, 55000, 32000),
        new(124, MonsterValue.MinimumDamage, 560, 400),
        new(124, MonsterValue.MaximumDamage, 600, 460),
        new(124, MonsterValue.Defense, 550, 450),
        new(124, MonsterValue.AttackRate, 870, 720),
        new(124, MonsterValue.DefenseRate, 310, 260),
        new(125, MonsterValue.Level, 110, 76), // Chief Skeleton Warrior 6
        new(125, MonsterValue.MaximumHealth, 50000, 17000),
        new(125, MonsterValue.MinimumDamage, 470, 260),
        new(125, MonsterValue.MaximumDamage, 510, 290),
        new(125, MonsterValue.AttackRate, 570, 460),
        new(125, MonsterValue.DefenseRate, 370, 150),
        new(126, MonsterValue.Level, 112, 81), // Chief Skeleton Archer 6
        new(126, MonsterValue.MaximumHealth, 53000, 20000),
        new(126, MonsterValue.MinimumDamage, 530, 320),
        new(126, MonsterValue.MaximumDamage, 556, 350),
        new(126, MonsterValue.Defense, 590, 270),
        new(126, MonsterValue.AttackRate, 640, 520),
        new(126, MonsterValue.DefenseRate, 380, 160),
        new(127, MonsterValue.Level, 118, 86), // Dark Skull Soldier 6
        new(127, MonsterValue.MaximumHealth, 57000, 25000),
        new(127, MonsterValue.MinimumDamage, 560, 390),
        new(127, MonsterValue.MaximumDamage, 581, 420),
        new(127, MonsterValue.Defense, 610, 360),
        new(127, MonsterValue.AttackRate, 710, 580),
        new(127, MonsterValue.DefenseRate, 300, 195),
        new(128, MonsterValue.Level, 120, 90), // Giant Ogre 6
        new(128, MonsterValue.MaximumHealth, 62000, 32000),
        new(128, MonsterValue.MinimumDamage, 580, 440),
        new(128, MonsterValue.MaximumDamage, 599, 500),
        new(128, MonsterValue.Defense, 615, 400),
        new(128, MonsterValue.AttackRate, 780, 660),
        new(128, MonsterValue.DefenseRate, 310, 230),
        new(129, MonsterValue.Level, 127, 94), // Red Skeleton Knight 6
        new(129, MonsterValue.MaximumHealth, 70000, 38000),
        new(129, MonsterValue.MinimumDamage, 600, 480),
        new(129, MonsterValue.MaximumDamage, 618, 540),
        new(129, MonsterValue.Defense, 635, 450),
        new(129, MonsterValue.AttackRate, 850, 720),
        new(129, MonsterValue.DefenseRate, 360, 260),
        new(130, MonsterValue.Level, 135, 99), // Magic Skeleton 6
        new(130, MonsterValue.MaximumHealth, 80000, 47000),
        new(130, MonsterValue.MinimumDamage, 634, 530),
        new(130, MonsterValue.MaximumDamage, 651, 580),
        new(130, MonsterValue.Defense, 680, 500),
        new(130, MonsterValue.AttackRate, 920, 840),
        new(130, MonsterValue.DefenseRate, 370, 280),
        new(138, MonsterValue.Level, 105, 85), // Chief Skeleton Warrior 7
        new(138, MonsterValue.Level, 114, 85), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.MaximumHealth, 29000, 23000),
        new(138, MonsterValue.MaximumHealth, 173500, 23000), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.MinimumDamage, 475, 340),
        new(138, MonsterValue.MinimumDamage, 745, 340), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.MaximumDamage, 510, 390),
        new(138, MonsterValue.MaximumDamage, 800, 390), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.Defense, 440, 340),
        new(138, MonsterValue.Defense, 600, 340), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.AttackRate, 570, 520),
        new(138, MonsterValue.AttackRate, 640, 520), // without the Blood Castle 7/8 swap
        new(138, MonsterValue.DefenseRate, 250, 190),
        new(138, MonsterValue.DefenseRate, 426, 190), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.Level, 106, 87), // Chief Skeleton Archer 7
        new(139, MonsterValue.Level, 117, 87), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.MaximumHealth, 32000, 26000),
        new(139, MonsterValue.MaximumHealth, 175000, 26000), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.MinimumDamage, 510, 390),
        new(139, MonsterValue.MinimumDamage, 825, 390), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.MaximumDamage, 555, 430),
        new(139, MonsterValue.MaximumDamage, 872, 430), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.Defense, 480, 380),
        new(139, MonsterValue.Defense, 615, 380), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.AttackRate, 640, 590),
        new(139, MonsterValue.AttackRate, 690, 590), // without the Blood Castle 7/8 swap
        new(139, MonsterValue.DefenseRate, 260, 200),
        new(139, MonsterValue.DefenseRate, 440, 200), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.Level, 110, 90), // Dark Skull Soldier 7
        new(140, MonsterValue.Level, 125, 90), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.MaximumHealth, 37000, 32000),
        new(140, MonsterValue.MaximumHealth, 184000, 32000), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.MinimumDamage, 600, 430),
        new(140, MonsterValue.MinimumDamage, 890, 430), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.MaximumDamage, 650, 480),
        new(140, MonsterValue.MaximumDamage, 915, 480), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.Defense, 500, 400),
        new(140, MonsterValue.Defense, 622, 400), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.AttackRate, 710, 660),
        new(140, MonsterValue.AttackRate, 760, 660), // without the Blood Castle 7/8 swap
        new(140, MonsterValue.DefenseRate, 300, 230),
        new(140, MonsterValue.DefenseRate, 465, 230), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.Level, 112, 93), // Giant Ogre 7
        new(141, MonsterValue.Level, 129, 93), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.MaximumHealth, 45000, 37000),
        new(141, MonsterValue.MaximumHealth, 208000, 37000), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.MinimumDamage, 645, 470),
        new(141, MonsterValue.MinimumDamage, 920, 470), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.MaximumDamage, 690, 520),
        new(141, MonsterValue.MaximumDamage, 946, 520), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.Defense, 540, 440),
        new(141, MonsterValue.Defense, 635, 440), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.AttackRate, 780, 730),
        new(141, MonsterValue.AttackRate, 830, 730), // without the Blood Castle 7/8 swap
        new(141, MonsterValue.DefenseRate, 310, 250),
        new(141, MonsterValue.DefenseRate, 510, 250), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.Level, 119, 98), // Red Skeleton Knight 7
        new(142, MonsterValue.Level, 132, 98), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.MaximumHealth, 55000, 45000),
        new(142, MonsterValue.MaximumHealth, 208700, 45000), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.MinimumDamage, 780, 510),
        new(142, MonsterValue.MinimumDamage, 995, 510), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.MaximumDamage, 820, 560),
        new(142, MonsterValue.MaximumDamage, 1120, 560), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.Defense, 600, 490),
        new(142, MonsterValue.Defense, 648, 490), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.AttackRate, 850, 800),
        new(142, MonsterValue.AttackRate, 900, 800), // without the Blood Castle 7/8 swap
        new(142, MonsterValue.DefenseRate, 360, 270),
        new(142, MonsterValue.DefenseRate, 585, 270), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.Level, 125, 102), // Magic Skeleton 7
        new(143, MonsterValue.Level, 140, 102), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.MaximumHealth, 60000, 55000),
        new(143, MonsterValue.MaximumHealth, 215000, 55000), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.MinimumDamage, 830, 560),
        new(143, MonsterValue.MinimumDamage, 1500, 560), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.MaximumDamage, 865, 600),
        new(143, MonsterValue.MaximumDamage, 1780, 600), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.Defense, 680, 550),
        new(143, MonsterValue.Defense, 690, 550), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.AttackRate, 920, 870),
        new(143, MonsterValue.AttackRate, 950, 870), // without the Blood Castle 7/8 swap
        new(143, MonsterValue.DefenseRate, 370, 310),
        new(143, MonsterValue.DefenseRate, 750, 310), // without the Blood Castle 7/8 swap
        new(196, MonsterValue.DefenseRate, 245, 215), // Schriker 4
        new(261, MonsterValue.Defense, 460, 430), // Death Centurion 5
        new(265, MonsterValue.AttackRate, 642, 645), // Necron 5
        new(360, MonsterValue.DefenseRate, 906, 905), // Dreadfear
        new(361, MonsterValue.Level, 145, 135), // Nightmare
        new(361, MonsterValue.MinimumDamage, 3000, 1225),
        new(361, MonsterValue.MaximumDamage, 3500, 1468),
        new(361, MonsterValue.Defense, 7500, 1000),
        new(361, MonsterValue.AttackRate, 3000, 2000),
        new(361, MonsterValue.DefenseRate, 2500, 420),
        new(362, MonsterValue.Level, 130, 135), // Maya (Hand Left)
        new(362, MonsterValue.MinimumDamage, 2000, 732),
        new(362, MonsterValue.MaximumDamage, 2500, 932),
        new(362, MonsterValue.Defense, 5000, 315),
        new(362, MonsterValue.DefenseRate, 1500, 1445),
        new(363, MonsterValue.Level, 130, 135), // Maya (Hand Right)
        new(363, MonsterValue.MinimumDamage, 2000, 732),
        new(363, MonsterValue.MaximumDamage, 2500, 932),
        new(363, MonsterValue.Defense, 5000, 800),
        new(363, MonsterValue.AttackRate, 2100, 2000),
        new(363, MonsterValue.DefenseRate, 1600, 550),
        new(364, MonsterValue.Level, 140, 111), // Maya
        new(364, MonsterValue.MaximumHealth, 5000000, 300),
        new(364, MonsterValue.MinimumDamage, 2500, 712),
        new(364, MonsterValue.MaximumDamage, 3000, 752),
        new(364, MonsterValue.Defense, 6500, 584),
        new(364, MonsterValue.AttackRate, 2800, 850),
        new(364, MonsterValue.DefenseRate, 2200, 315),
        new(410, MonsterValue.Level, 119, 118), // Death Spirit (Trainee Soldier)
        new(427, MonsterValue.AttackRate, 480, 520), // Chaos Castle 14
        new(427, MonsterValue.DefenseRate, 190, 200),
        new(428, MonsterValue.Level, 114, 105), // Chief Skeleton Warrior 8
        new(428, MonsterValue.MaximumHealth, 173500, 29000),
        new(428, MonsterValue.MinimumDamage, 745, 475),
        new(428, MonsterValue.MaximumDamage, 800, 510),
        new(428, MonsterValue.Defense, 600, 440),
        new(428, MonsterValue.AttackRate, 640, 570),
        new(428, MonsterValue.DefenseRate, 426, 250),
        new(429, MonsterValue.Level, 117, 106), // Chief Skeleton Archer 8
        new(429, MonsterValue.MaximumHealth, 175000, 32000),
        new(429, MonsterValue.MinimumDamage, 825, 510),
        new(429, MonsterValue.MaximumDamage, 872, 555),
        new(429, MonsterValue.Defense, 615, 480),
        new(429, MonsterValue.AttackRate, 690, 640),
        new(429, MonsterValue.DefenseRate, 440, 260),
        new(430, MonsterValue.Level, 125, 110), // Dark Skull Soldier 8
        new(430, MonsterValue.MaximumHealth, 184000, 37000),
        new(430, MonsterValue.MinimumDamage, 890, 600),
        new(430, MonsterValue.MaximumDamage, 915, 650),
        new(430, MonsterValue.Defense, 622, 500),
        new(430, MonsterValue.AttackRate, 760, 710),
        new(430, MonsterValue.DefenseRate, 465, 300),
        new(431, MonsterValue.Level, 129, 112), // Giant Ogre 8
        new(431, MonsterValue.MaximumHealth, 208000, 45000),
        new(431, MonsterValue.MinimumDamage, 920, 645),
        new(431, MonsterValue.MaximumDamage, 946, 690),
        new(431, MonsterValue.Defense, 635, 540),
        new(431, MonsterValue.AttackRate, 830, 780),
        new(431, MonsterValue.DefenseRate, 510, 310),
        new(432, MonsterValue.Level, 132, 119), // Red Skeleton Knight 8
        new(432, MonsterValue.MaximumHealth, 208700, 55000),
        new(432, MonsterValue.MinimumDamage, 995, 780),
        new(432, MonsterValue.MaximumDamage, 1120, 820),
        new(432, MonsterValue.Defense, 648, 600),
        new(432, MonsterValue.AttackRate, 900, 850),
        new(432, MonsterValue.DefenseRate, 585, 360),
        new(433, MonsterValue.Level, 140, 125), // Magic Skeleton 8
        new(433, MonsterValue.MaximumHealth, 215000, 65000),
        new(433, MonsterValue.MaximumHealth, 60000, 65000), // without the Blood Castle 7/8 swap
        new(433, MonsterValue.MinimumDamage, 1500, 830),
        new(433, MonsterValue.MaximumDamage, 1780, 865),
        new(433, MonsterValue.Defense, 690, 680),
        new(433, MonsterValue.AttackRate, 950, 920),
        new(433, MonsterValue.DefenseRate, 750, 370),
        new(434, MonsterValue.Level, 98, 105), // Gigantis
        new(434, MonsterValue.MaximumHealth, 43000, 30000),
        new(434, MonsterValue.MinimumDamage, 546, 475),
        new(434, MonsterValue.MaximumDamage, 581, 510),
        new(434, MonsterValue.Defense, 430, 360),
        new(434, MonsterValue.DefenseRate, 250, 270),
        new(435, MonsterValue.Level, 100, 106), // Berserk
        new(435, MonsterValue.MaximumHealth, 70000, 33000),
        new(435, MonsterValue.MinimumDamage, 408, 500),
        new(435, MonsterValue.MaximumDamage, 543, 540),
        new(435, MonsterValue.Defense, 430, 410),
        new(435, MonsterValue.AttackRate, 1000, 750),
        new(435, MonsterValue.DefenseRate, 360, 280),
        new(436, MonsterValue.Level, 134, 117), // Balram (Trainee)
        new(436, MonsterValue.MaximumHealth, 90000, 37000),
        new(436, MonsterValue.MinimumDamage, 600, 560),
        new(436, MonsterValue.MaximumDamage, 700, 600),
        new(436, MonsterValue.Defense, 500, 460),
        new(436, MonsterValue.AttackRate, 1500, 800),
        new(436, MonsterValue.DefenseRate, 370, 320),
        new(437, MonsterValue.Level, 134, 119), // Soram (Trainee)
        new(437, MonsterValue.MaximumHealth, 100000, 40000),
        new(437, MonsterValue.MinimumDamage, 600, 610),
        new(437, MonsterValue.MaximumDamage, 700, 650),
        new(437, MonsterValue.Defense, 500, 510),
        new(437, MonsterValue.AttackRate, 1500, 835),
        new(437, MonsterValue.DefenseRate, 397, 330),
        new(438, MonsterValue.Level, 118, 121), // Persona
        new(438, MonsterValue.MaximumHealth, 68000, 50000),
        new(438, MonsterValue.MinimumDamage, 1168, 640),
        new(438, MonsterValue.MaximumDamage, 1213, 680),
        new(438, MonsterValue.Defense, 615, 560),
        new(438, MonsterValue.AttackRate, 1190, 935),
        new(438, MonsterValue.DefenseRate, 485, 370),
        new(439, MonsterValue.Level, 119, 123), // Dreadfear
        new(439, MonsterValue.MaximumHealth, 94000, 55000),
        new(439, MonsterValue.MinimumDamage, 946, 700),
        new(439, MonsterValue.MaximumDamage, 996, 740),
        new(439, MonsterValue.Defense, 783, 610),
        new(439, MonsterValue.DefenseRate, 905, 380),
        new(440, MonsterValue.MaximumHealth, 1000000, 75000), // Dark_Elf
        new(440, MonsterValue.MinimumDamage, 800, 850),
        new(440, MonsterValue.MaximumDamage, 800, 900),
        new(440, MonsterValue.Defense, 650, 690),
        new(440, MonsterValue.AttackRate, 1500, 1300),
        new(440, MonsterValue.DefenseRate, 370, 430),
        new(448, MonsterValue.AttackRate, 983, 980), // Ghost Napin
        new(481, MonsterValue.MaximumHealth, 5000000, 500000), // Zombie Fighter
        new(570, MonsterValue.Defense, 283, 485), // Bone Scorpion
        new(570, MonsterValue.DefenseRate, 363, 283),
        new(575, MonsterValue.DefenseRate, 406, 405), // Condra
    ];
}

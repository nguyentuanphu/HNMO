// <copyright file="MonsterSpeedsAndRangesFromSuitesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update gives the monsters the move speed, attack speed, view range, attack range and move range of the Season 6/9 server suites.
/// A value is only changed while it is still HNMO's old value.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("9B8EF2C9-AE6E-4D12-96D4-02421BA3C1DC")]
public class MonsterSpeedsAndRangesFromSuitesUpdatePlugIn : MonsterValuesUpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Monster speeds and ranges as in the suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Move speed, attack speed, view range, attack range and move range of the monsters as in the Season 6/9 server suites.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IReadOnlyList<MonsterValueChange> Changes => ChangeList;

    /// <summary>
    /// Gets the changes: monster number, value, the value HNMO had, the value of the suites.
    /// </summary>
    internal static IReadOnlyList<MonsterValueChange> ChangeList { get; } =
    [
        new(86, MonsterValue.AttackRange, 4, 1), // Dark Skull Soldier 1
        new(89, MonsterValue.AttackDelay, 1800, 1600), // Magic Skeleton 1
        new(135, MonsterValue.AttackDelay, 1400, 2200), // White Wizard
        new(136, MonsterValue.AttackDelay, 1400, 1500), // Destructive Ogre Soldier
        new(137, MonsterValue.AttackDelay, 1600, 1800), // Destructive Ogre Archer
        new(144, MonsterValue.MoveRange, 2, 3), // Death Angel 1
        new(146, MonsterValue.AttackRange, 2, 6), // Blood Soldier 1
        new(294, MonsterValue.MoveDelay, 500, 400), // Axe Warrior
        new(294, MonsterValue.AttackDelay, 2000, 1400),
        new(348, MonsterValue.MoveRange, 0, 6), // Tanker
        new(355, MonsterValue.AttackRange, 1, 4), // Kentauros
        new(355, MonsterValue.ViewRange, 7, 5),
        new(361, MonsterValue.MoveRange, 3, 6), // Nightmare
        new(361, MonsterValue.AttackRange, 5, 2),
        new(361, MonsterValue.ViewRange, 9, 7),
        new(361, MonsterValue.MoveDelay, 400, 700),
        new(361, MonsterValue.AttackDelay, 1600, 1700),
        new(362, MonsterValue.MoveRange, 3, 6), // Maya (Hand Left)
        new(362, MonsterValue.AttackRange, 5, 3),
        new(362, MonsterValue.ViewRange, 8, 7),
        new(362, MonsterValue.MoveDelay, 400, 700),
        new(362, MonsterValue.AttackDelay, 1600, 1700),
        new(363, MonsterValue.MoveRange, 3, 6), // Maya (Hand Right)
        new(363, MonsterValue.AttackRange, 5, 3),
        new(363, MonsterValue.ViewRange, 8, 7),
        new(363, MonsterValue.MoveDelay, 400, 700),
        new(363, MonsterValue.AttackDelay, 1600, 1700),
        new(364, MonsterValue.AttackRange, 6, 1), // Maya
        new(364, MonsterValue.ViewRange, 9, 7),
        new(364, MonsterValue.MoveDelay, 400, 500),
        new(364, MonsterValue.AttackDelay, 2000, 1500),
        new(386, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(387, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(388, MonsterValue.MoveRange, 3, 5), // Illusion Sorc. Spirit
        new(389, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(390, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(391, MonsterValue.MoveRange, 3, 5), // Illusion Sorc. Spirit
        new(392, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(393, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(394, MonsterValue.MoveRange, 3, 5), // Illusion Sorc. Spirit
        new(395, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(396, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(397, MonsterValue.MoveRange, 3, 5), // Illusion Sorc. Spirit
        new(398, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(399, MonsterValue.MoveRange, 3, 4), // Illusion Sorc. Spirit
        new(409, MonsterValue.AttackDelay, 1700, 1500), // Balram (Trainee Soldier)
        new(410, MonsterValue.AttackDelay, 1700, 1500), // Death Spirit (Trainee Soldier)
        new(411, MonsterValue.AttackDelay, 1700, 1500), // Soram (Trainee Soldier)
        new(436, MonsterValue.AttackRange, 7, 4), // Balram (Trainee)
        new(436, MonsterValue.MoveDelay, 700, 500),
        new(436, MonsterValue.AttackDelay, 1700, 1500),
        new(437, MonsterValue.AttackRange, 4, 2), // Soram (Trainee)
        new(437, MonsterValue.MoveDelay, 700, 500),
        new(437, MonsterValue.AttackDelay, 1700, 1500),
        new(558, MonsterValue.AttackDelay, 2550, 2500), // Ice Napin
        new(559, MonsterValue.AttackDelay, 2550, 2500), // Shadow Master
        new(569, MonsterValue.MoveDelay, 400, 900), // Venomous Chain Scorpion
        new(570, MonsterValue.MoveDelay, 400, 900), // Bone Scorpion
        new(571, MonsterValue.MoveDelay, 400, 700), // Orcus
        new(571, MonsterValue.AttackDelay, 1500, 1800),
        new(573, MonsterValue.MoveDelay, 400, 800), // Crypta
        new(573, MonsterValue.AttackDelay, 1500, 1700),
        new(574, MonsterValue.MoveDelay, 400, 800), // Crypos
        new(574, MonsterValue.AttackDelay, 1500, 1700),
    ];
}

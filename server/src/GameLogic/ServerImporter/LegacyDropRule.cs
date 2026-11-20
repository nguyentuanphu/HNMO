// <copyright file="LegacyDropRule.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Parsed monster drop entry.</summary>
public sealed class LegacyDropRule
{
    /// <summary>Gets or sets the monster number (-1 for global drop pool).</summary>
    public int MonsterNumber { get; set; }

    /// <summary>Gets or sets minimum monster level.</summary>
    public int MonsterMinLevel { get; set; }

    /// <summary>Gets or sets maximum monster level.</summary>
    public int MonsterMaxLevel { get; set; }

    /// <summary>Gets or sets target map number (-1 for any).</summary>
    public int MapNumber { get; set; }

    /// <summary>Gets or sets item group (0-15).</summary>
    public byte ItemGroup { get; set; }

    /// <summary>Gets or sets item number.</summary>
    public short ItemNumber { get; set; }

    /// <summary>Gets or sets item drop chance ratio (0..1).</summary>
    public double Chance { get; set; }

    /// <summary>Gets or sets minimum item enhancement level.</summary>
    public byte MinItemLevel { get; set; }

    /// <summary>Gets or sets a value indicating whether the drop is excellent.</summary>
    public bool IsExcellent { get; set; }

    /// <summary>Gets or sets a value indicating whether the drop is ancient set.</summary>
    public bool IsAncient { get; set; }

    /// <summary>Gets or sets socket count.</summary>
    public int SocketCount { get; set; }

    /// <summary>Gets or sets Season 9 elemental attribute flag (0 if none).</summary>
    public int ElementalAttribute { get; set; }

    /// <summary>Gets or sets optional companion evolution category (Season 9+ only).</summary>
    public int? CompanionEvolutionGroup { get; set; }

    /// <summary>
    /// Gets or sets the chance that the whole group this rule belongs to drops something (0..1), when the source gives
    /// it directly, as an item bag's <c>ItemRate</c> does. Otherwise the group's chance is the sum of its rules' chances.
    /// </summary>
    public double? GroupChance { get; set; }

    /// <summary>
    /// Gets or sets the item bag file name when the rule comes from a bag that is not a monster's drop: a box, an
    /// event or chaos-mix reward, an NPC gift. Such rules are rewards of their own and never become monster drops.
    /// </summary>
    public string? RewardBagName { get; set; }

    /// <summary>
    /// Gets or sets the list the rule comes from (an item bag's file name, "AppointItemDrop" ...): rules of different
    /// lists are separate drops of the monster, each with its own chance.
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// Gets or sets the box the rule is the content of, when it comes from an "Item_(group,number,level)" bag: what a
    /// player gets on opening (dropping) that box.
    /// </summary>
    public (byte Group, short Number, byte Level)? BoxItem { get; set; }
}

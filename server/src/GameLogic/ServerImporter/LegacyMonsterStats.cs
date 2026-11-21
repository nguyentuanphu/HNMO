// <copyright file="LegacyMonsterStats.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

/// <summary>A monster's stats from the server's MonsterList.xml.</summary>
public sealed class LegacyMonsterStats
{
    /// <summary>Gets or sets the monster number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the monster name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the life.</summary>
    public int Health { get; set; }

    /// <summary>Gets or sets the least damage.</summary>
    public int DamageMin { get; set; }

    /// <summary>Gets or sets the most damage.</summary>
    public int DamageMax { get; set; }

    /// <summary>Gets or sets the defense.</summary>
    public int Defense { get; set; }

    /// <summary>Gets or sets the attack rate.</summary>
    public int AttackRate { get; set; }

    /// <summary>Gets or sets the defense (block) rate.</summary>
    public int DefenseRate { get; set; }

    /// <summary>Gets or sets how far the monster walks.</summary>
    public int MoveRange { get; set; }

    /// <summary>Gets or sets how far the monster attacks.</summary>
    public int AttackRange { get; set; }

    /// <summary>Gets or sets how far the monster sees.</summary>
    public int ViewRange { get; set; }

    /// <summary>Gets or sets the time of a step, in milliseconds.</summary>
    public int MoveDelayMilliseconds { get; set; }

    /// <summary>Gets or sets the time of an attack, in milliseconds.</summary>
    public int AttackDelayMilliseconds { get; set; }

    /// <summary>Gets or sets the time until the monster comes back, in seconds.</summary>
    public int RespawnSeconds { get; set; }
}

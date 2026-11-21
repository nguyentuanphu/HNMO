// <copyright file="BackupMonsterStats.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>A monster's stats before an import changed them.</summary>
public sealed class BackupMonsterStats
{
    /// <summary>Gets or sets the monster number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets the attribute values, by attribute id (absent: the monster had no such attribute).</summary>
    public Dictionary<Guid, float> Attributes { get; set; } = [];

    /// <summary>Gets or sets the walking range.</summary>
    public byte MoveRange { get; set; }

    /// <summary>Gets or sets the attack range.</summary>
    public byte AttackRange { get; set; }

    /// <summary>Gets or sets the view range.</summary>
    public short ViewRange { get; set; }

    /// <summary>Gets or sets the time of a step.</summary>
    public TimeSpan MoveDelay { get; set; }

    /// <summary>Gets or sets the time of an attack.</summary>
    public TimeSpan AttackDelay { get; set; }

    /// <summary>Gets or sets the time until the monster comes back.</summary>
    public TimeSpan RespawnDelay { get; set; }
}

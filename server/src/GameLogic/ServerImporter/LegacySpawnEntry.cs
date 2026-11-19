// <copyright file="LegacySpawnEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>Parsed monster or NPC spawn line from MonsterSetBase text format.</summary>
public sealed class LegacySpawnEntry
{
    /// <summary>Gets or sets group type (0=NPC, 1=Area, 2=Point, 3=Golden, 4=Event, 5=Fortress, 6=Elemental).</summary>
    public int SpawnGroup { get; set; }

    /// <summary>Gets or sets monster/NPC number.</summary>
    public int MonsterNumber { get; set; }

    /// <summary>Gets or sets map number.</summary>
    public int MapNumber { get; set; }

    /// <summary>Gets or sets X1 coordinate.</summary>
    public int X1 { get; set; }

    /// <summary>Gets or sets Y1 coordinate.</summary>
    public int Y1 { get; set; }

    /// <summary>Gets or sets X2 coordinate.</summary>
    public int X2 { get; set; }

    /// <summary>Gets or sets Y2 coordinate.</summary>
    public int Y2 { get; set; }

    /// <summary>Gets or sets direction code.</summary>
    public int Direction { get; set; }

    /// <summary>Gets or sets spawn quantity.</summary>
    public int Quantity { get; set; }
}

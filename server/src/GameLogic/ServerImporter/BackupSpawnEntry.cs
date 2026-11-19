// <copyright file="BackupSpawnEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>Identifies a created <see cref="HNMO.DataModel.Configuration.MonsterSpawnArea"/> for undo removal.</summary>
public sealed class BackupSpawnEntry
{
    /// <summary>Gets or sets map number.</summary>
    public short MapNumber { get; set; }

    /// <summary>Gets or sets monster number.</summary>
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets X1.</summary>
    public byte X1 { get; set; }

    /// <summary>Gets or sets Y1.</summary>
    public byte Y1 { get; set; }

    /// <summary>Gets or sets X2.</summary>
    public byte X2 { get; set; }

    /// <summary>Gets or sets Y2.</summary>
    public byte Y2 { get; set; }

    /// <summary>Gets or sets quantity.</summary>
    public short Quantity { get; set; }
}

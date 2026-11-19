// <copyright file="BackupItemSlot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>Saved minimal item snapshot inside a merchant shop backup.</summary>
public sealed class BackupItemSlot
{
    /// <summary>Gets or sets slot index.</summary>
    public byte ItemSlot { get; set; }

    /// <summary>Gets or sets item group.</summary>
    public byte Group { get; set; }

    /// <summary>Gets or sets item number.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets item level.</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets durability.</summary>
    public double Durability { get; set; }

    /// <summary>Gets or sets whether item has skill.</summary>
    public bool HasSkill { get; set; }
}

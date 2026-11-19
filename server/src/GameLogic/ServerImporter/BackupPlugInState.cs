// <copyright file="BackupPlugInState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>Saved state of a single <see cref="HNMO.PlugIns.PlugInConfiguration"/> before import.</summary>
public sealed class BackupPlugInState
{
    /// <summary>Gets or sets plugin GUID.</summary>
    public Guid TypeId { get; set; }

    /// <summary>Gets or sets whether a configuration record existed prior to import.</summary>
    public bool Existed { get; set; }

    /// <summary>Gets or sets previous IsActive flag.</summary>
    public bool PreviousIsActive { get; set; }

    /// <summary>Gets or sets previous JSON configuration payload.</summary>
    public string? PreviousJson { get; set; }
}

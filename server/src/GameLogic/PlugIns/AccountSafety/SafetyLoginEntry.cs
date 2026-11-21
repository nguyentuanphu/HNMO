// <copyright file="SafetyLoginEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// A login of an account (successful or not).
/// </summary>
public sealed class SafetyLoginEntry
{
    /// <summary>Gets or sets when.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the address it came from (full; the website masks it).</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Gets or sets where: "game" or "website".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the password was right.</summary>
    public bool Success { get; set; }
}

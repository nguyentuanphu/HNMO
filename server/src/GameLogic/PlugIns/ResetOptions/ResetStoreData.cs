// <copyright file="ResetStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// The saved state of the reset options, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class ResetStoreData
{
    /// <summary>Gets or sets the characters, by character id.</summary>
    public Dictionary<string, ResetCharacterRecord> Characters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the resets per month (yyyy-MM) and character id.</summary>
    public Dictionary<string, Dictionary<string, int>> Months { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the resets and relifes, newest first.</summary>
    public List<ResetLogEntry> Log { get; set; } = new();
}

// <copyright file="ResetCharacterRecord.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// What the reset options remember of a character.
/// </summary>
public sealed class ResetCharacterRecord
{
    /// <summary>Gets or sets the character's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the character class number.</summary>
    public int Class { get; set; }

    /// <summary>Gets or sets the relifes done.</summary>
    public int Relifes { get; set; }

    /// <summary>Gets or sets the resets after the last reset or relife.</summary>
    public int Resets { get; set; }

    /// <summary>Gets or sets a value indicating whether the delegated reset is on.</summary>
    public bool Delegated { get; set; }

    /// <summary>Gets or sets the day of the counts below (yyyy-MM-dd).</summary>
    public string DayKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the resets of that day (every source).</summary>
    public int DayResets { get; set; }

    /// <summary>Gets or sets the relifes of that day.</summary>
    public int DayRelifes { get; set; }

    /// <summary>Gets or sets the delegated resets of that day.</summary>
    public int DayDelegated { get; set; }

    /// <summary>Gets or sets the delegated resets the player wasn't told about yet.</summary>
    public int DelegatedUntold { get; set; }

    /// <summary>Gets or sets why the last delegated reset couldn't be done (a <see cref="ResetResult"/> name), until one is done.</summary>
    public string DelegatedBlocked { get; set; } = string.Empty;

    /// <summary>Gets or sets when the last reset or relife was done.</summary>
    public DateTime? LastAtUtc { get; set; }
}

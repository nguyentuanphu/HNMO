// <copyright file="SafetyItemLock.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// A locked item of an account.
/// </summary>
public sealed class SafetyItemLock
{
    /// <summary>Gets or sets the item's id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the item's text when it was locked (e.g. "Kris +7 +Luck").</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the character which had it when it was locked (empty for the vault).</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was locked.</summary>
    public DateTime LockedAtUtc { get; set; }

    /// <summary>Gets or sets when an asked unlock happens; <c>null</c> while no unlock was asked.</summary>
    public DateTime? UnlockAtUtc { get; set; }
}

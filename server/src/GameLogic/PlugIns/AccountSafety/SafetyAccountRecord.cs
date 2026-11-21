// <copyright file="SafetyAccountRecord.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// The safety state of an account.
/// </summary>
public sealed class SafetyAccountRecord
{
    /// <summary>Gets or sets the logins, newest first.</summary>
    public List<SafetyLoginEntry> Logins { get; set; } = new();

    /// <summary>Gets or sets the trades, newest first.</summary>
    public List<SafetyTradeEntry> Trades { get; set; } = new();

    /// <summary>Gets or sets the locked items.</summary>
    public List<SafetyItemLock> Locks { get; set; } = new();

    /// <summary>Gets or sets the wrong second passwords since the last right one or lockout.</summary>
    public int WrongTries { get; set; }

    /// <summary>Gets or sets until when the second password can't be entered.</summary>
    public DateTime? LockedOutUntilUtc { get; set; }

    /// <summary>Gets or sets when the second password was last set, changed or cleared.</summary>
    public DateTime? PinChangedAtUtc { get; set; }
}

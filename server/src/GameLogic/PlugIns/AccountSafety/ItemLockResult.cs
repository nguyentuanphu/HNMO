// <copyright file="ItemLockResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Globalization;
using System.Net;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The result of locking or unlocking an item.
/// </summary>
public enum ItemLockResult
{
    /// <summary>The item is locked now.</summary>
    Locked,

    /// <summary>The item was locked already.</summary>
    AlreadyLocked,

    /// <summary>The item's asked unlock was cancelled: it stays locked.</summary>
    UnlockCancelled,

    /// <summary>The unlock was asked; it happens after the wait.</summary>
    UnlockStarted,

    /// <summary>The unlock was asked before; it still waits.</summary>
    UnlockWaiting,

    /// <summary>The item is unlocked now.</summary>
    Unlocked,

    /// <summary>The item isn't locked.</summary>
    NotLocked,

    /// <summary>The account locked as many items as it may.</summary>
    TooMany,

    /// <summary>The item lock is switched off.</summary>
    Off,
}

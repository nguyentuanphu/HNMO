// <copyright file="PinCheckResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Globalization;
using System.Net;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The result of a second password which was entered.
/// </summary>
public enum PinCheckResult
{
    /// <summary>It's right.</summary>
    Ok,

    /// <summary>It's wrong.</summary>
    Wrong,

    /// <summary>Too many wrong ones were entered: wait until the lockout ends.</summary>
    LockedOut,

    /// <summary>The account has no second password.</summary>
    NoPin,
}

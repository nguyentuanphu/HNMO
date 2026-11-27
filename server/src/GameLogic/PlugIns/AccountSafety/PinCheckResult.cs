// <copyright file="PinCheckResult.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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

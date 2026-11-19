// <copyright file="PinCheck.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Globalization;
using System.Net;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The result of a check of a second password, with the tries left and the end of a lockout.
/// </summary>
/// <param name="Result">The result.</param>
/// <param name="TriesLeft">The wrong tries left before the lockout.</param>
/// <param name="LockedOutUntilUtc">The end of the lockout.</param>
public readonly record struct PinCheck(PinCheckResult Result, int TriesLeft, DateTime? LockedOutUntilUtc);

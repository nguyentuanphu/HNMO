// <copyright file="VipMembership.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>A saved account subscription; times are UTC and the exclusive expiry is authoritative.</summary>
/// <param name="LoginName">The canonical account name.</param>
/// <param name="LevelId">The configured level identifier.</param>
/// <param name="FromUtc">The inclusive activation timestamp.</param>
/// <param name="UntilUtc">The exclusive expiry.</param>
public sealed record VipMembership(string LoginName, int LevelId, DateTime FromUtc, DateTime UntilUtc);

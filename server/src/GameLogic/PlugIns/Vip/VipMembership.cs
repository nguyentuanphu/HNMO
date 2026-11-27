// <copyright file="VipMembership.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>A saved account subscription; times are UTC and the exclusive expiry is authoritative.</summary>
/// <param name="LoginName">The canonical account name.</param>
/// <param name="LevelId">The configured level identifier.</param>
/// <param name="FromUtc">The inclusive activation timestamp.</param>
/// <param name="UntilUtc">The exclusive expiry.</param>
public sealed record VipMembership(string LoginName, int LevelId, DateTime FromUtc, DateTime UntilUtc);

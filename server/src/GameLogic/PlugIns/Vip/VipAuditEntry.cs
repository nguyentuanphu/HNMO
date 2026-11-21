// <copyright file="VipAuditEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>An operational record of a subscription change, with no credentials or network addresses.</summary>
/// <param name="AtUtc">The change timestamp.</param>
/// <param name="LoginName">The canonical account name.</param>
/// <param name="Action">The machine-readable action.</param>
/// <param name="Actor">The verified caller or service.</param>
/// <param name="Reason">The bounded operational reason.</param>
public sealed record VipAuditEntry(DateTime AtUtc, string LoginName, string Action, string Actor, string Reason);

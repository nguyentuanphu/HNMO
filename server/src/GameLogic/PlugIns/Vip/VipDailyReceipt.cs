// <copyright file="VipDailyReceipt.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>Metadata for a durable, once-per-Sydney-day pending coin credit.</summary>
/// <param name="Kind">The versioned format.</param>
/// <param name="AccountId">The credited account.</param>
/// <param name="SydneyDay">The eligible local calendar day.</param>
/// <param name="VipOperationId">The paid subscription authorizing this bonus.</param>
/// <param name="NameEn">The purchased English level name.</param>
/// <param name="NameVi">The purchased Vietnamese level name.</param>
public sealed record VipDailyReceipt(string Kind, Guid AccountId, string SydneyDay, Guid VipOperationId, string NameEn, string NameVi);

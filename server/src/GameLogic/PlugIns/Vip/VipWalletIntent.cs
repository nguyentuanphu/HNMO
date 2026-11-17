// <copyright file="VipWalletIntent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>A recovery intent persisted before an account or wallet is changed.</summary>
/// <param name="AccountId">The persistent owner.</param>
/// <param name="OperationId">The caller's stable retry identifier.</param>
/// <param name="Plan">The server-validated price and expiry.</param>
/// <param name="Membership">The unchanged subscription interval.</param>
/// <param name="ExperiencePercent">The purchased experience bonus.</param>
/// <param name="ZenPercent">The purchased zen bonus.</param>
/// <param name="NameEn">The purchased level's English name.</param>
/// <param name="NameVi">The purchased level's Vietnamese name.</param>
/// <param name="PreviousGrantIds">Only grants owned by the preceding VIP purchase.</param>
public sealed record VipWalletIntent(Guid AccountId, Guid OperationId, VipPurchasePlan Plan, VipMembership Membership, int ExperiencePercent, int ZenPercent, string NameEn, string NameVi, List<Guid> PreviousGrantIds);

// <copyright file="GiftBoxReceipt.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using HNMO.DataModel.Configuration;

/// <summary>Delivery proof committed with the actual inventory, Zen and coin changes.</summary>
/// <param name="Kind">The invariant receipt format.</param>
/// <param name="AccountId">The authenticated receiving account.</param>
/// <param name="LetterId">The immutable mail identifier.</param>
/// <param name="CharacterId">The original chosen character.</param>
/// <param name="ItemIds">The actual persistent item identifiers; never recreate them on replay.</param>
/// <param name="Zen">The delivered Zen.</param>
/// <param name="Coins">The delivered coins.</param>
/// <param name="CoinType">The delivered currency.</param>
/// <param name="TitleEn">The English history label.</param>
/// <param name="TitleVi">The Vietnamese history label.</param>
public sealed record GiftBoxReceipt(string Kind, Guid AccountId, Guid LetterId, Guid CharacterId, IReadOnlyList<Guid> ItemIds, int Zen, int Coins, CashShopCoinType CoinType, string TitleEn, string TitleVi);

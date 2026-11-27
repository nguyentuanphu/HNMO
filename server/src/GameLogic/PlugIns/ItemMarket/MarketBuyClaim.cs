// <copyright file="MarketBuyClaim.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

/// <summary>Claimed listing details with calculated fee and net seller payout.</summary>
/// <param name="Result">Atomic claim outcome.</param>
/// <param name="Listing">Original listing if claimed.</param>
/// <param name="FeeAmount">Deducted market tax.</param>
/// <param name="NetAmount">Net payout to seller.</param>
public sealed record MarketBuyClaim(MarketClaimResult Result, MarketListing? Listing, int FeeAmount, int NetAmount);

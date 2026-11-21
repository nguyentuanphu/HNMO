// <copyright file="MarketBuyClaim.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

/// <summary>Claimed listing details with calculated fee and net seller payout.</summary>
/// <param name="Result">Atomic claim outcome.</param>
/// <param name="Listing">Original listing if claimed.</param>
/// <param name="FeeAmount">Deducted market tax.</param>
/// <param name="NetAmount">Net payout to seller.</param>
public sealed record MarketBuyClaim(MarketClaimResult Result, MarketListing? Listing, int FeeAmount, int NetAmount);

// <copyright file="WebsiteVipRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

/// <summary>Own-account VIP action; prices, balances and grant identifiers are never accepted from the client.</summary>
/// <param name="LoginName">The account login.</param>
/// <param name="Password">Its current password.</param>
/// <param name="OperationId">The stable purchase retry identifier, or the purchase whose result is being read.</param>
/// <param name="LevelId">The selected configured level.</param>
/// <param name="Days">The selected configured duration.</param>
/// <param name="OfferToken">The reviewed server offer version.</param>
public sealed record WebsiteVipRequest(string? LoginName, string? Password, Guid? OperationId = null, int LevelId = 0, int Days = 0, string? OfferToken = null);

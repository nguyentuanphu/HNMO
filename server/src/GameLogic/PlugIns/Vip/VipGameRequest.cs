// <copyright file="VipGameRequest.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>An own-account selection from the authenticated game session; prices and identity are never supplied by it.</summary>
/// <param name="Action">State, purchase or daily action.</param>
/// <param name="OperationId">Stable purchase retry identifier.</param>
/// <param name="LevelId">Configured level selection.</param>
/// <param name="Days">Configured duration selection.</param>
/// <param name="OfferToken">The reviewed server offer version.</param>
public sealed record VipGameRequest(string Action, Guid? OperationId = null, int LevelId = 0, int Days = 0, string? OfferToken = null)
{
    /// <summary>Gets a value indicating whether the bounded request has a supported action and a reviewed purchase selection.</summary>
    public bool IsValid => this.Action is "state" or "daily" || (this.Action == "purchase" && this.OperationId is { } id && id != Guid.Empty
        && this.LevelId > 0 && this.Days > 0 && this.OfferToken is { Length: 64 } token && token.All(Uri.IsHexDigit));
}

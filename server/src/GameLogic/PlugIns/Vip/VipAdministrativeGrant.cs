// <copyright file="VipAdministrativeGrant.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>Trusted host attribution saved in the same durable proof as an administrative VIP entitlement.</summary>
/// <param name="Actor">The authenticated host principal; never taken from a player request.</param>
/// <param name="Reason">The required operational reason.</param>
public sealed record VipAdministrativeGrant(string Actor, string Reason)
{
    /// <summary>Gets a value indicating whether bounded, nonempty attribution can be stored safely.</summary>
    public bool IsValid => this.Actor is { Length: > 0 and <= 256 } && !string.IsNullOrWhiteSpace(this.Actor)
        && !this.Actor.Any(char.IsControl) && this.Reason is { Length: > 0 and <= 500 } && !string.IsNullOrWhiteSpace(this.Reason);
}

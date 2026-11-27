// <copyright file="GiftBoxRecipient.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

/// <summary>A host-resolved account snapshot used to review and freeze a mail audience.</summary>
/// <param name="AccountId">The persisted account identifier.</param>
/// <param name="Login">The exact case-sensitive account login.</param>
/// <param name="LastActiveUtc">The last actually recorded activity, or unknown; never an invented date.</param>
/// <param name="Online">Whether the authoritative live snapshot contains this account.</param>
public sealed record GiftBoxRecipient(Guid AccountId, string Login, DateTime? LastActiveUtc, bool Online = false);

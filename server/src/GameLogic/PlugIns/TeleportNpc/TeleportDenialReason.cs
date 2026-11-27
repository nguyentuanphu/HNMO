// <copyright file="TeleportDenialReason.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TeleportNpc;

/// <summary>
/// Reasons why a Teleport NPC warp request may be declined.
/// </summary>
public enum TeleportDenialReason
{
    /// <summary>Allowed to warp.</summary>
    None = 0,

    /// <summary>Teleport NPC plug-in is disabled.</summary>
    Disabled,

    /// <summary>Requested destination id does not exist.</summary>
    DestinationNotFound,

    /// <summary>Requested destination is disabled by admin.</summary>
    DestinationDisabled,

    /// <summary>Player level is below <see cref="TeleportDestinationConfig.MinLevel"/>.</summary>
    LevelTooLow,

    /// <summary>Player level exceeds <see cref="TeleportDestinationConfig.MaxLevel"/>.</summary>
    LevelTooHigh,

    /// <summary>Player resets are below <see cref="TeleportDestinationConfig.MinResets"/>.</summary>
    InsufficientResets,

    /// <summary>VIP status is required.</summary>
    VipRequired,

    /// <summary>PK/Outlaw characters cannot use this destination.</summary>
    PkBlocked,

    /// <summary>Player cannot warp while inside a mini-game event.</summary>
    InMiniGame,

    /// <summary>Player lacks the required Zen.</summary>
    InsufficientZen,
}

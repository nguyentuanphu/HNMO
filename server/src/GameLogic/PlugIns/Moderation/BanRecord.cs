// <copyright file="BanRecord.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Moderation;

/// <summary>The moderation enforcement target type.</summary>
public enum BanTargetKind
{
    /// <summary>Blocks login for a specific account.</summary>
    AccountBan = 0,

    /// <summary>Blocks entering the world with a specific character.</summary>
    CharacterBan = 1,

    /// <summary>Blocks all chat channels for a specific account.</summary>
    AccountMute = 2,

    /// <summary>Blocks all chat channels for a specific character.</summary>
    CharacterMute = 3,

    /// <summary>Blocks logins originating from a salted-hashed IP address.</summary>
    NetworkBan = 4,

    /// <summary>Blocks logins originating from a salted-hashed device fingerprint.</summary>
    DeviceBan = 5,
}

/// <summary>One persisted account, character, network, or device ban/mute record.</summary>
/// <param name="Id">The unique moderation record identifier.</param>
/// <param name="Kind">The enforcement scope.</param>
/// <param name="TargetKey">Normalized account/character name, or HMAC-SHA256 salted hash for network/device bans.</param>
/// <param name="TargetDisplay">Human-readable login/character name, or masked hash prefix for network/device bans.</param>
/// <param name="ReasonEn">English reason displayed to the player.</param>
/// <param name="ReasonVi">Vietnamese reason displayed to the player.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp.</param>
/// <param name="ExpiresAtUtc">UTC expiry timestamp, or <c>null</c> for permanent.</param>
/// <param name="IssuedBy">Administrator identity or automated system label.</param>
/// <param name="Active">Whether the record has not been lifted.</param>
/// <param name="LiftedAtUtc">UTC unban/unmute timestamp if lifted.</param>
/// <param name="LiftedBy">Administrator identity that lifted the record.</param>
/// <param name="UnbanReason">Required internal reason for unbanning/unmuting.</param>
public sealed record BanRecord(
    string Id,
    BanTargetKind Kind,
    string TargetKey,
    string TargetDisplay,
    string ReasonEn,
    string ReasonVi,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    string IssuedBy,
    bool Active,
    DateTime? LiftedAtUtc = null,
    string? LiftedBy = null,
    string? UnbanReason = null)
{
    /// <summary>Returns whether this record is active and unexpired at <paramref name="nowUtc"/>.</summary>
    /// <param name="nowUtc">Current authoritative UTC timestamp.</param>
    /// <returns><c>true</c> if currently enforced.</returns>
    public bool IsEnforcedAt(DateTime nowUtc)
        => this.Active && (!this.ExpiresAtUtc.HasValue || this.ExpiresAtUtc.Value > nowUtc);
}

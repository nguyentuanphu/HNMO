// <copyright file="CommunityAccountState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// What an account got from the community rewards.
/// </summary>
public sealed class CommunityAccountState
{
    /// <summary>Gets or sets the login name, as it was written.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets when the Discord coins were paid.</summary>
    public DateTime? DiscordPaidAtUtc { get; set; }

    /// <summary>Gets or sets the Discord user the coins were paid for.</summary>
    public string? DiscordUserId { get; set; }

    /// <summary>Gets or sets the state of the Zalo claim.</summary>
    public ZaloClaimStatus ZaloStatus { get; set; }

    /// <summary>Gets or sets the code the player shows in the Zalo group (or sends to the admin).</summary>
    public string? ZaloCode { get; set; }

    /// <summary>Gets or sets the name the player has in Zalo.</summary>
    public string? ZaloName { get; set; }

    /// <summary>Gets or sets the character which asked.</summary>
    public string? ZaloCharacter { get; set; }

    /// <summary>Gets or sets when the player asked.</summary>
    public DateTime? ZaloRequestedAtUtc { get; set; }

    /// <summary>Gets or sets when the admin decided.</summary>
    public DateTime? ZaloDecidedAtUtc { get; set; }

    /// <summary>Gets or sets when the Zalo coins were paid.</summary>
    public DateTime? ZaloPaidAtUtc { get; set; }
}

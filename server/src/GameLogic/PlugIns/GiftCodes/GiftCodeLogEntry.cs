// <copyright file="GiftCodeLogEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// A redemption of a gift code (the admin panel's log).
/// </summary>
public sealed class GiftCodeLogEntry
{
    /// <summary>Gets or sets when.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the code's id.</summary>
    public string CodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the code which was entered (normalized).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets where it was entered: "game" or "website".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the reference which makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;
}

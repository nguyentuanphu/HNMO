// <copyright file="CommunityEventEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A reward of a community event which a character got (or waits for).
/// </summary>
public sealed class CommunityEventEntry
{
    /// <summary>Gets or sets the event id.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name.</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the character was a listed winner (else it entered the code).</summary>
    public bool Winner { get; set; }

    /// <summary>Gets or sets when it was recorded.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether the coins were paid.</summary>
    public bool CoinsPaid { get; set; }
}

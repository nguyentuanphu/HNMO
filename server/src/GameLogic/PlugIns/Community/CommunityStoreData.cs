// <copyright file="CommunityStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// The saved state of the community rewards, events and highlights, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class CommunityStoreData
{
    /// <summary>Gets or sets the accounts, by login name in lower case.</summary>
    public Dictionary<string, CommunityAccountState> Accounts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the Discord users whose coins were paid, with the login name (lower case).</summary>
    public Dictionary<string, string> PaidDiscordUsers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the event entries, by "event id|character name (lower case)".</summary>
    public Dictionary<string, CommunityEventEntry> Entries { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the code entries, by "event id|login (lower case)" (a code counts once per account).</summary>
    public Dictionary<string, string> CodeAccounts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the rewards which wait for the character, by character name (lower case).</summary>
    public Dictionary<string, List<CommunityOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the highlights, newest first.</summary>
    public List<CommunityHighlight> Highlights { get; set; } = new();
}

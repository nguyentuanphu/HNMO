// <copyright file="GiftCodeStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// The saved state of the gift codes, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class GiftCodeStoreData
{
    /// <summary>Gets or sets the random secret used to hash network and device identities; retain it with the claims.</summary>
    public string IdentitySalt { get; set; } = string.Empty;

    /// <summary>Gets or sets the codes.</summary>
    public List<GiftCodeDefinition> Codes { get; set; } = new();

    /// <summary>Gets or sets how often each code was used, by code id.</summary>
    public Dictionary<string, int> Uses { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets who used each code ("a:login" and "c:character id"), by code id.</summary>
    public Dictionary<string, HashSet<string>> Claims { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the batch codes which were used (normalized), with the reference of their redemption.</summary>
    public Dictionary<string, string> UsedBatchCodes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the redemptions, newest first.</summary>
    public List<GiftCodeLogEntry> Log { get; set; } = new();

    /// <summary>Gets or sets the coins which weren't given yet.</summary>
    public List<GiftCodePendingCoins> PendingCoins { get; set; } = new();

    /// <summary>Gets or sets the zen and items waiting for the character, by character id.</summary>
    public Dictionary<string, List<GiftCodeOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the times of wrong codes, by login name (lower case).</summary>
    public Dictionary<string, List<DateTime>> WrongTries { get; set; } = new(StringComparer.Ordinal);
}

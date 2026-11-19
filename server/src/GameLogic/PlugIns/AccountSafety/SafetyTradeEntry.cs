// <copyright file="SafetyTradeEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// A trade or a personal store sale of an account's character.
/// </summary>
public sealed class SafetyTradeEntry
{
    /// <summary>Gets or sets when.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets what: "trade", "store-sale" (sold in the own store) or "store-buy" (bought in another's store).</summary>
    public string Kind { get; set; } = "trade";

    /// <summary>Gets or sets the account's character.</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets the other character.</summary>
    public string Partner { get; set; } = string.Empty;

    /// <summary>Gets or sets the other character's account (only shown in the admin panel).</summary>
    public string PartnerLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the items given away.</summary>
    public List<string> Gave { get; set; } = new();

    /// <summary>Gets or sets the items received.</summary>
    public List<string> Got { get; set; } = new();

    /// <summary>Gets or sets the zen given away.</summary>
    public long ZenGave { get; set; }

    /// <summary>Gets or sets the zen received.</summary>
    public long ZenGot { get; set; }
}

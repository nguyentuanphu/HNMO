// <copyright file="MarriageStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.IO;
using System.Text.Json;

/// <summary>
/// Persisted section stored under <c>marriage</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class MarriageStoreData
{
    /// <summary>Gets or sets all active marriages.</summary>
    public List<MarriageCoupleEntry> Couples { get; set; } = [];

    /// <summary>Gets or sets remarriage cooldowns keyed by character name.</summary>
    public Dictionary<string, DateTime> RemarryCooldownUntilUtc { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

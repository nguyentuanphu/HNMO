// <copyright file="TopUpMilestoneStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>
/// Serialized data section for <see cref="TopUpMilestoneStore"/> inside <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class TopUpMilestoneStoreData
{
    /// <summary>Gets or sets accounts keyed by lower-case login name.</summary>
    public Dictionary<string, TopUpAccountState> Accounts { get; set; } = new(StringComparer.Ordinal);
}

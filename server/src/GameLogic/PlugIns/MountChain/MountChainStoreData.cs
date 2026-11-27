// <copyright file="MountChainStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>Persistent state stored under section <c>mountChain</c> in <see cref="HnmoStateStore"/>.</summary>
public sealed class MountChainStoreData
{
    /// <summary>Gets or sets per-account states keyed by login name.</summary>
    public Dictionary<string, MountAccountState> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets owed mount items keyed by character ID when bag was full.</summary>
    public Dictionary<string, List<GiftCodeOwedReward>> Owed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

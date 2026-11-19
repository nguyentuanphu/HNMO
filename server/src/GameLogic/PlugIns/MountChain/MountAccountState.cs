// <copyright file="MountAccountState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>Per-account mount chain progress stored inside <c>mountChain</c>.</summary>
public sealed class MountAccountState
{
    /// <summary>Gets or sets Mount Shards balance earned from Golden Bosses and duplicate salvage.</summary>
    public int Shards { get; set; }

    /// <summary>Gets or sets unlocked mount tier IDs and unlock UTC timestamps.</summary>
    public Dictionary<string, DateTime> Unlocked { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets currently selected collection aura/tier ID for modest PvE bonus.</summary>
    public string ActiveTierId { get; set; } = string.Empty;
}

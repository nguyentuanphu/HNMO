// <copyright file="GiftCodeOwedReward.cs" company="HNMO">
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
/// Zen and items of a redemption which wait for the character to be in the game with room in the bag.
/// </summary>
public sealed class GiftCodeOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the code's id.</summary>
    public string CodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the code which was entered.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the zen.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public List<GiftCodeItem> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the reference of a marker saved together with what the reward was paid with (e.g. materials taken
    /// from the bag), or <c>null</c>. A reward with a marker is delivered only once the marker is saved: without it, the
    /// payment never reached the database.
    /// </summary>
    public string? PaidMarker { get; set; }
}

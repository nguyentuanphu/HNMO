// <copyright file="LuckyNumberOwedReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;

/// <summary>
/// Jewels waiting for an account to claim inside the game.
/// </summary>
public sealed class LuckyNumberOwedReward
{
    /// <summary>Gets or sets the unique claim ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the round date key.</summary>
    public string DateKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the tier (<c>exact</c> or <c>lastDigit</c>).</summary>
    public string Tier { get; set; } = "exact";

    /// <summary>Gets or sets the item group.</summary>
    public int Group { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the item quantity.</summary>
    public int Count { get; set; }
}

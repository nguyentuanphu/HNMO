// <copyright file="RankCoins.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The coins of a rank.
/// </summary>
public class RankCoins
{
    /// <summary>Gets or sets the rank (1 to 10).</summary>
    [Range(1, 10)]
    public int Rank { get; set; }

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }
}

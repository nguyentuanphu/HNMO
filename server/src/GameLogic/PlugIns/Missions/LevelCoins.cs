// <copyright file="LevelCoins.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// The coins of a level.
/// </summary>
public class LevelCoins
{
    /// <summary>Gets or sets the level.</summary>
    [Range(1, 1000)]
    public int Level { get; set; }

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }
}

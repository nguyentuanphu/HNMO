// <copyright file="GuildXpSourceSetting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The guild experience of one source.
/// </summary>
public class GuildXpSourceSetting
{
    /// <summary>Gets or sets a value indicating whether the source gives guild experience.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the guild experience per time (per percent for the world boss damage).</summary>
    [Range(0, 1_000_000)]
    public int Xp { get; set; }
}

// <copyright file="GuildXpResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The result of adding guild experience.
/// </summary>
/// <param name="Granted">The experience the guild got (less than asked when the member's daily cap was reached).</param>
/// <param name="OldLevel">The level before.</param>
/// <param name="NewLevel">The level after.</param>
public sealed record GuildXpResult(int Granted, int OldLevel, int NewLevel)
{
    /// <summary>Gets a value indicating whether the guild reached a new level.</summary>
    public bool LeveledUp => this.NewLevel > this.OldLevel;
}

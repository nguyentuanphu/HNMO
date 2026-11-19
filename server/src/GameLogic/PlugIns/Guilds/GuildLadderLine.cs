// <copyright file="GuildLadderLine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A line of a finished week's ladder.
/// </summary>
public sealed class GuildLadderLine
{
    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the persistent guild id.</summary>
    public Guid GuildId { get; set; }

    /// <summary>Gets or sets the guild's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the ladder points.</summary>
    public long Xp { get; set; }

    /// <summary>Gets or sets the coins each member got.</summary>
    public int CoinsPerMember { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public HNMO.DataModel.Configuration.CashShopCoinType CoinType { get; set; }

    /// <summary>Gets or sets the members who were paid.</summary>
    public int PaidMembers { get; set; }
}

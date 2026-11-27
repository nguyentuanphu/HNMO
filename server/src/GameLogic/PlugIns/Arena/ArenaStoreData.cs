// <copyright file="ArenaStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>What the HNMO Arena keeps in its state file.</summary>
public sealed class ArenaStoreData
{
    /// <summary>Gets or sets the accounts' ranked records, by login name (lower case).</summary>
    public Dictionary<string, ArenaPlayerRecord> Players { get; set; } = [];

    /// <summary>Gets or sets the guild war records, by guild name.</summary>
    public Dictionary<string, ArenaGuildRecord> Guilds { get; set; } = [];

    /// <summary>Gets or sets the Sydney week whose guild war champion got its guild experience.</summary>
    public string ChampionPaidWeek { get; set; } = string.Empty;
}

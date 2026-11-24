// <copyright file="ActivityPvpMode.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ActivityLog;

/// <summary>Combat contexts captured at the time of a player defeat.</summary>
public enum ActivityPvpMode
{
    /// <summary>Regular world combat.</summary>
    Field,

    /// <summary>Active paired duel.</summary>
    Duel,

    /// <summary>Active Arena battle.</summary>
    Arena,

    /// <summary>Active castle siege on its configured map.</summary>
    Siege,

    /// <summary>Combat in another mini-game instance.</summary>
    MiniGame,

    /// <summary>Evaluated guild-war combat outside the Arena.</summary>
    GuildWar,
}

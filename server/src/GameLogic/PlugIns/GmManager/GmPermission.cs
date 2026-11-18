// <copyright file="GmPermission.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GmManager;

/// <summary>Fine-grained permissions granted to a Game Master character (ROADMAP 14.17).</summary>
[Flags]
public enum GmPermission
{
    /// <summary>No GM command permissions.</summary>
    None = 0,

    /// <summary>Server announcements (/goldnotice, /slidenotice, /gmannounce).</summary>
    Announce = 1 << 0,

    /// <summary>Movement, teleport, go-to, and summon commands (/teleport, /trace, /goto, /track, /summon, /move, /guildmove, /hide, /unhide).</summary>
    MoveSummonGoto = 1 << 1,

    /// <summary>Spawning or removing monsters/bosses and world effects (/createmonster, /spawn, /boss, /npc, /removenpc, /killallmonsters, etc.).</summary>
    SpawnMonsterBoss = 1 << 2,

    /// <summary>Starting or skipping scheduled events (/startevent, /startbc, /startcc, /startds, /startit, /startkanturu, /skipminigamewait, /crywolf).</summary>
    StartEvent = 1 << 3,

    /// <summary>Inspecting characters, stats, online status, and inventory (/inspectinv, /charinfo, /get*, /online).</summary>
    InspectInventory = 1 << 4,

    /// <summary>Muting, kicking, and banning players (/mute, /chatban, /chatunban, /kick, /disconnect, /guilddisconnect, /banacc, /banchar, /unban*).</summary>
    MuteKick = 1 << 5,

    /// <summary>Direct GM state changes (/setlevel, /setstat, /setmoney, /setresets, /item, /clearinventory, /openwarehouse, /pk, /pkclear, /skin).</summary>
    EditCharacter = 1 << 6,

    /// <summary>All GM permissions.</summary>
    All = Announce | MoveSummonGoto | SpawnMonsterBoss | StartEvent | InspectInventory | MuteKick | EditCharacter,
}

// <copyright file="CommunityHighlightEvent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// HNMO's community posts: something worth telling happened (a rare drop, a world boss kill, a guild level-up, the
/// guild ladder's winners, the castle siege's winner). The texts are the ones set in the admin panel, with the names
/// already filled in.
/// </summary>
/// <param name="ServerId">The identifier of the game server on which it happened.</param>
/// <param name="TimestampUtc">The timestamp, in UTC, when it happened.</param>
/// <param name="Kind">The kind, e.g. "RareDrop", "WorldBossKill", "GuildLevelUp", "GuildLadderWinner", "CastleSiegeWinner".</param>
/// <param name="TextEn">The English text.</param>
/// <param name="TextVi">The Vietnamese text.</param>
public sealed record CommunityHighlightEvent(byte ServerId, DateTime TimestampUtc, string Kind, string TextEn, string TextVi)
    : GameEvent(ServerId, TimestampUtc);

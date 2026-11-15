// <copyright file="WorldBossAnnouncedEvent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// HNMO's world bosses: a boss comes soon, appeared, was killed or left. The texts are the ones set in the admin panel,
/// with the names already filled in.
/// </summary>
/// <param name="ServerId">The identifier of the game server which hosts the boss.</param>
/// <param name="TimestampUtc">The timestamp, in UTC, when the event happened.</param>
/// <param name="Phase">"soon", "spawn", "kill" or "escape".</param>
/// <param name="BossNameEn">The English name of the boss.</param>
/// <param name="BossNameVi">The Vietnamese name of the boss.</param>
/// <param name="TextEn">The English text.</param>
/// <param name="TextVi">The Vietnamese text.</param>
/// <param name="AtUtc">The spawn time (soon, spawn) or the end time.</param>
public sealed record WorldBossAnnouncedEvent(byte ServerId, DateTime TimestampUtc, string Phase, string BossNameEn, string BossNameVi, string TextEn, string TextVi, DateTime AtUtc)
    : GameEvent(ServerId, TimestampUtc);

// <copyright file="WorldBossAction.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Something the plug-in has to do for a world boss.
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="Boss">The boss (<c>null</c> for the despawn of a boss which isn't configured any more).</param>
/// <param name="AtUtc">The spawn time (Soon, Spawn) or the despawn time.</param>
/// <param name="InstanceId">The appearance which leaves (Despawn).</param>
public sealed record WorldBossAction(WorldBossActionKind Kind, WorldBossDefinition? Boss, DateTime AtUtc, string? InstanceId = null);

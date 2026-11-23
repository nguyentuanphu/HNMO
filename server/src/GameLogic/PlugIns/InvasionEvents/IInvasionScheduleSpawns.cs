// <copyright file="IInvasionScheduleSpawns.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.InvasionEvents;

/// <summary>Exposes the spawn rules the scheduler can use without selecting a random spawn.</summary>
internal interface IInvasionScheduleSpawns
{
    /// <summary>Reads the active monsters and eligible maps from the live configuration.</summary>
    /// <param name="context">The running server context.</param>
    /// <returns>The spawn rules for this server.</returns>
    IReadOnlyList<InvasionSpawnConfiguration> ActiveSpawns(IGameContext context);
}

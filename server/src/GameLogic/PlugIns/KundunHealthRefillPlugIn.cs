// <copyright file="KundunHealthRefillPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using HNMO.GameLogic.NPC;
using HNMO.PlugIns;

/// <summary>
/// Kundun gets health back over time, as in the Season 6/9 server suites (KundunRefillHPSec, KundunRefillHP and
/// KundunRefillHPTime of their common server settings): an amount every second and a bigger amount at an interval.
/// </summary>
[PlugIn]
[Display(Name = "Kundun health refill", Description = "Kundun (Kalima 7) gets health back: 400 every second and 15,000 every 10 minutes, as in the Season 6/9 server suites.")]
[Guid("C0A28D0E-E3A3-4A7C-AF10-500F46982379")]
public class KundunHealthRefillPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<KundunHealthRefillConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private DateTime _nextSecondUtc = DateTime.MinValue;

    private DateTime _nextIntervalUtc = DateTime.MinValue;

    /// <inheritdoc />
    public KundunHealthRefillConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new KundunHealthRefillConfiguration();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        var configuration = this.Configuration ??= new KundunHealthRefillConfiguration();
        if (this._nextIntervalUtc == DateTime.MinValue)
        {
            this._nextIntervalUtc = now + configuration.Interval;
        }

        var amount = 0;
        if (now >= this._nextSecondUtc)
        {
            this._nextSecondUtc = now + Second;
            amount += configuration.HealthPerSecond;
        }

        if (configuration.Interval > TimeSpan.Zero && now >= this._nextIntervalUtc)
        {
            this._nextIntervalUtc = now + configuration.Interval;
            amount += configuration.HealthPerInterval;
        }

        if (amount <= 0)
        {
            return;
        }

        foreach (var map in await gameContext.GetMapsAsync().ConfigureAwait(false))
        {
            Refill(map, configuration.MonsterNumber, amount);
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextSecondUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Gives the monsters of the map with the number the amount of health back, up to their maximum health.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="monsterNumber">The monster number.</param>
    /// <param name="amount">The amount of health.</param>
    /// <returns>The health which was given back in total.</returns>
    internal static int Refill(GameMap map, short monsterNumber, int amount)
    {
        if (!map.Definition.MonsterSpawns.Any(spawn => spawn.MonsterDefinition?.Number == monsterNumber))
        {
            return 0;
        }

        var restored = 0;
        foreach (var monster in map.GetAttackableNpcs().Where(npc => npc.Definition.Number == monsterNumber && npc.IsAlive))
        {
            restored += monster.RestoreHealth(amount);
        }

        return restored;
    }
}

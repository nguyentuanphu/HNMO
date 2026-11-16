// <copyright file="CombatLimitsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.PlugIns;

/// <summary>
/// Enforces server-wide maximum damage cap per hit, PvP damage reduction %, and Castle Siege damage reduction % (ROADMAP 14.3).
/// </summary>
[PlugIn]
[Display(Name = "Combat damage limits", Description = "Caps maximum hit damage and applies PvP and Castle Siege damage reductions.")]
[Guid("4A8D5E12-6C7F-4D90-B123-8F5E0A2C9D41")]
public sealed class CombatLimitsPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<CombatLimitsConfiguration>,
    ISupportDefaultCustomConfiguration
{
    private const short ValleyOfLorenMapNumber = 30;

    /// <inheritdoc />
    public CombatLimitsConfiguration? Configuration { get; set; }

    /// <summary>Applies configured damage reductions and caps for <paramref name="attacker"/> vs <paramref name="defender"/>.</summary>
    /// <param name="attacker">Attacker.</param>
    /// <param name="defender">Defender.</param>
    /// <param name="isPvp">Whether this hit is player-vs-player.</param>
    /// <param name="damage">Raw calculated damage.</param>
    /// <returns>Adjusted damage.</returns>
    public static int ApplyDamageLimits(IAttacker attacker, IAttackable defender, bool isPvp, int damage)
    {
        if (damage <= 0)
        {
            return damage;
        }

        var player = attacker as Player ?? (attacker as IPlayerSurrogate)?.Owner ?? defender as Player;
        var cfg = player?.GameContext.FeaturePlugIns.GetPlugIn<CombatLimitsPlugIn>()?.Configuration;
        if (cfg is not { Enabled: true })
        {
            return damage;
        }

        var result = (double)damage;
        if (isPvp && cfg.PvpDamageReductionPercent > 0)
        {
            var factor = Math.Clamp(1.0 - (cfg.PvpDamageReductionPercent / 100.0), 0.05, 1.0);
            result *= factor;
        }

        var mapNumber = (defender as Player ?? player)?.CurrentMap?.Definition?.Number;
        if (mapNumber == ValleyOfLorenMapNumber && cfg.CastleSiegeDamageReductionPercent > 0)
        {
            var factor = Math.Clamp(1.0 - (cfg.CastleSiegeDamageReductionPercent / 100.0), 0.05, 1.0);
            result *= factor;
        }

        var adjusted = Math.Max(1, (int)Math.Round(result));
        if (cfg.MaxDamagePerHit > 0 && adjusted > cfg.MaxDamagePerHit)
        {
            adjusted = cfg.MaxDamagePerHit;
        }

        return adjusted;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CombatLimitsConfiguration();
}

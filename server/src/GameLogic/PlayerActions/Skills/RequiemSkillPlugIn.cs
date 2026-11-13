// <copyright file="RequiemSkillPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views.World;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Handles the requiem skill (book of neil) of the summoner class. Based on a chance, it may stun the target.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.RequiemSkillPlugIn_Name), Description = nameof(PlugInResources.RequiemSkillPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3F1A4C8E-9B72-4D5F-A1B3-C6E7D8F90123")]
public class RequiemSkillPlugIn : IAreaSkillPlugIn
{
    private const int StunnedMagicEffectNumber = 61; // 0x3D

    private MagicEffectDefinition? _stunEffectDefinition;

    /// <inheritdoc />
    public short Key => 224;

    /// <inheritdoc />
    public async ValueTask AfterTargetGotAttackedAsync(IAttacker attacker, IAttackable target, SkillEntry skillEntry, Point targetAreaCenter, HitInfo? hitInfo)
    {
        this._stunEffectDefinition ??= ((Player)attacker).GameContext.Configuration.MagicEffects.First(m => m.Number == StunnedMagicEffectNumber);

        if (!target.IsAlive || !Rand.NextRandomBool(attacker.Attributes[Stats.MasteryStunChance]))
        {
            return;
        }

        var powerUp = attacker.Attributes.CreateElement(this._stunEffectDefinition.PowerUpDefinitions.First(pu => pu.TargetAttribute == Stats.IsStunned));
        var magicEffect = new MagicEffect(TimeSpan.FromSeconds(3), this._stunEffectDefinition, [new MagicEffect.ElementWithTarget(powerUp, Stats.IsStunned)]);
        await target.MagicEffectList.AddEffectAsync(magicEffect).ConfigureAwait(false);

        if (target is ISupportWalk walkSupporter && walkSupporter.IsWalking)
        {
            await walkSupporter.StopWalkingAsync().ConfigureAwait(false);

            // Since the actual coordinates could be out of sync with the client
            // coordinates, we simply update the position on the client side.
            if (walkSupporter is IObservable observable)
            {
                await observable.ForEachWorldObserverAsync<IObjectMovedPlugIn>(p => p.ObjectMovedAsync(walkSupporter, MoveType.Instant), true).ConfigureAwait(false);
            }
        }
    }
}
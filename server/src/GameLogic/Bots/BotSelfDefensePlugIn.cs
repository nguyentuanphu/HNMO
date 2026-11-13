// <copyright file="BotSelfDefensePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Bots;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Notes when a (human) player attacks a server-side bot, so the bot's combat AI can defend itself.
/// Without this, a bot placidly keeps farming monsters while a player kills it - the most obviously
/// bot-like behavior an observer can trigger. The actual counter-attack happens in the offline
/// <see cref="CombatHandler"/>, which prioritizes a recent aggressor over its monster targets.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.GameLogic.Properties.PlugInResources.BotSelfDefensePlugIn_Name), Description = nameof(HNMO.GameLogic.Properties.PlugInResources.BotSelfDefensePlugIn_Description), ResourceType = typeof(HNMO.GameLogic.Properties.PlugInResources))]
[Guid("7E2B9C41-5A8D-4F36-B190-3D6E84C7F215")]
public class BotSelfDefensePlugIn : IAttackableGotHitPlugIn
{
    /// <inheritdoc />
    public void AttackableGotHit(IAttackable attackable, IAttacker attacker, HitInfo hitInfo)
    {
        if (attackable is OfflinePlayer bot
            && bot.Account?.IsBot == true
            && bot.CurrentMiniGame is null // event fights (Chaos Castle) leave no grudge outside
            && attacker is Player aggressor
            && aggressor is not OfflinePlayer
            && !ReferenceEquals(aggressor, attackable))
        {
            bot.RegisterAggressor(aggressor);
        }
    }
}

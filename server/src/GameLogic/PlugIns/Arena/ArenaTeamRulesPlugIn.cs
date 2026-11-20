// <copyright file="ArenaTeamRulesPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Properties;
using HNMO.PlugIns;

/// <summary>
/// The rules of the Arena's 5v5 team battles inside the game (owner's choice, 2026-11-20): only the other team can be
/// hurt, a kill between the teams carries no player-kill penalty and is counted, and the fallen come back at their
/// team's gate. Only fighters of a running battle are concerned; nothing changes anywhere else.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ArenaTeamRulesPlugIn_Name), Description = nameof(PlugInResources.ArenaTeamRulesPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3F9B6E14-27C8-4A5D-B0E1-8C4D7A2F6B93")]
public sealed class ArenaTeamRulesPlugIn : IPlayerAttackRestrictionPlugIn, IPlayerKillPenaltyExemptionPlugIn, IPlayerSpawnGateSelectionPlugIn, IAttackableGotKilledPlugIn
{
    /// <inheritdoc />
    public void CheckAttack(Player attacker, Player defender, CancelEventArgs eventArgs)
    {
        if ((TeamsOf(attacker) is { } teams && teams.PreventsAttack(attacker, defender))
            || (RoyaleOf(attacker) is { } royale && royale.PreventsAttack(attacker, defender))
            || (GuildWarOf(attacker) is { } war && war.PreventsAttack(attacker, defender)))
        {
            eventArgs.Cancel = true;
        }
    }

    /// <inheritdoc />
    public void CheckExemption(Player attacker, Player defender, PlayerKillPenaltyExemptionArgs eventArgs)
    {
        if ((TeamsOf(attacker) is { } teams && teams.AreOpponents(attacker, defender))
            || (RoyaleOf(attacker) is { } royale && royale.AreOpponents(attacker, defender))
            || (GuildWarOf(attacker) is { } war && war.AreOpponents(attacker, defender)))
        {
            eventArgs.IsExempted = true;
        }
    }

    /// <inheritdoc />
    public ValueTask SelectSpawnGateAsync(Player player, SpawnGateSelectionArgs args)
    {
        if ((TeamsOf(player)?.RespawnGateOf(player) ?? RoyaleOf(player)?.RespawnGateOf(player) ?? GuildWarOf(player)?.RespawnGateOf(player)) is { } gate)
        {
            args.Gate = gate;
        }
        else if (args.Gate is null && ArenaRules.Active && ArenaPlugIn.Get(player.GameContext) is { } arena)
        {
            // On the arena map the fallen (and anyone sent to the safe zone) stand at the Arena's own gate: the map's
            // gates lie partly on blocked tiles, where the game would warp them again and again.
            var cfg = arena.Configuration ?? new ArenaConfiguration();
            var mapNumber = player.CurrentMap?.Definition.Number ?? player.SelectedCharacter?.CurrentMap?.Number;
            if (mapNumber == cfg.MapNumber)
            {
                args.Gate = cfg.HomeGate(player.GameContext.Configuration);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Player fallen)
        {
            return;
        }

        var killerPlayer = killer as Player ?? (killer as IPlayerSurrogate)?.Owner;
        if (killerPlayer is not null && TeamsOf(fallen) is { } teams)
        {
            await teams.KilledAsync(fallen, killerPlayer).ConfigureAwait(false);
        }

        if (killerPlayer is not null && GuildWarOf(fallen) is { } war)
        {
            await war.KilledAsync(fallen, killerPlayer).ConfigureAwait(false);
        }

        if (RoyaleOf(fallen) is { } royale)
        {
            await royale.FellAsync(fallen, killerPlayer).ConfigureAwait(false);
        }
    }

    private static ArenaGuildWar? GuildWarOf(Player player) =>
        ArenaRules.Active && ArenaPlugIn.Get(player.GameContext) is { } arena && arena.GuildWar.Running ? arena.GuildWar : null;

    private static ArenaRoyale? RoyaleOf(Player player) =>
        ArenaRules.Active && ArenaPlugIn.Get(player.GameContext) is { } arena && arena.Royale.Running ? arena.Royale : null;

    private static ArenaTeams? TeamsOf(Player player) =>
        ArenaRules.Active && ArenaPlugIn.Get(player.GameContext) is { } arena && arena.Teams.BattleRunning ? arena.Teams : null;
}

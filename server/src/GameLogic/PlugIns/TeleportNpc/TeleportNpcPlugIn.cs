// <copyright file="TeleportNpcPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TeleportNpc;

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Configurable Teleport NPC plug-in (OFF by default) allowing players to warp to admin-defined maps
/// for Zen with level, reset, VIP, and PK checks.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Teleport NPC", Description = "Lets players talk to configured NPCs to warp to admin-defined maps for Zen with level requirements (OFF by default).")]
[Guid("A8F3E190-2C4B-4D7E-9A61-3D5C7E8F1A24")]
public sealed class TeleportNpcPlugIn :
    IPlayerTalkToNpcPlugIn,
    ISupportCustomConfiguration<TeleportNpcConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public TeleportNpcConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="TeleportNpcPlugIn"/> for <paramref name="gameContext"/>, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static TeleportNpcPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerTalkToNpcPlugIn>().OfType<TeleportNpcPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new TeleportNpcConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (eventArgs.HasBeenHandled || player.SelectedCharacter is null || npc.Definition is null)
        {
            return;
        }

        var cfg = this.Configuration ?? new TeleportNpcConfiguration();
        if (!TeleportNpcEngine.HandlesNpc(cfg, plugInActive: true, npc.Definition.Number))
        {
            return;
        }

        eventArgs.HasBeenHandled = true;
        eventArgs.LeavesDialogOpen = false;
        await this.SendStateAsync(player, requested: true, openDialog: true, actionResult: null).ConfigureAwait(false);
    }

    /// <summary>Handles a menu refresh or warp request from <see cref="HnmoTeleportNpcViewPlugIn"/>.</summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="action">Action byte (<c>0</c> = RequestMenu, <c>1</c> = WarpToDestination).</param>
    /// <param name="destinationId">Destination identifier for warp requests.</param>
    /// <returns>The task.</returns>
    public async ValueTask HandleCommandAsync(Player player, byte action, string? destinationId)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        if (action == 0)
        {
            await this.SendStateAsync(player, requested: true, openDialog: false, actionResult: null).ConfigureAwait(false);
            return;
        }

        var cfg = this.Configuration ?? new TeleportNpcConfiguration();
        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0f);
        var vipLevel = GetVipLevel(player);
        var isPk = player.SelectedCharacter.State >= HeroState.PlayerKiller1stStage;
        var inMiniGame = player.CurrentMiniGame is not null;
        var decision = TeleportNpcEngine.EvaluateWarp(
            cfg,
            plugInActive: true,
            destinationId,
            player.Level,
            resets,
            vipLevel,
            player.Money,
            isPk,
            inMiniGame);

        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        if (!decision.Allowed || decision.Destination is null)
        {
            await player.ShowBlueMessageAsync(isVi ? decision.MessageVi : decision.MessageEn).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, openDialog: false, actionResult: (false, decision.MessageEn, decision.MessageVi)).ConfigureAwait(false);
            return;
        }

        var mapDef = player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == decision.Destination.MapNumber);
        if (mapDef is null)
        {
            const string missingEn = "Target map is not configured on this server.";
            const string missingVi = "Bản đồ đích chưa được cấu hình trên máy chủ này.";
            await player.ShowBlueMessageAsync(isVi ? missingVi : missingEn).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, openDialog: false, actionResult: (false, missingEn, missingVi)).ConfigureAwait(false);
            return;
        }

        if (decision.ZenCost > 0 && !player.TryRemoveMoney(decision.ZenCost))
        {
            const string noZenEn = "Not enough Zen for teleport.";
            const string noZenVi = "Không đủ Zen để dịch chuyển.";
            await player.ShowBlueMessageAsync(isVi ? noZenVi : noZenEn).ConfigureAwait(false);
            await this.SendStateAsync(player, requested: true, openDialog: false, actionResult: (false, noZenEn, noZenVi)).ConfigureAwait(false);
            return;
        }

        var gate = new ExitGate
        {
            Map = mapDef,
            X1 = decision.Destination.TargetX,
            X2 = decision.Destination.TargetX,
            Y1 = decision.Destination.TargetY,
            Y2 = decision.Destination.TargetY,
        };

        await player.WarpToAsync(gate).ConfigureAwait(false);
        await player.ShowBlueMessageAsync(isVi ? decision.MessageVi : decision.MessageEn).ConfigureAwait(false);
        await this.SendStateAsync(
            player,
            requested: true,
            openDialog: false,
            actionResult: (true, decision.MessageEn, decision.MessageVi)).ConfigureAwait(false);
    }

    /// <summary>Sends the Teleport NPC destination snapshot to <paramref name="player"/>.</summary>
    /// <param name="player">Target player.</param>
    /// <param name="requested">Whether triggered by a client interaction.</param>
    /// <param name="openDialog">Whether the web client should open the dialog panel.</param>
    /// <param name="actionResult">Optional last action outcome.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        bool openDialog,
        (bool Ok, string En, string Vi)? actionResult)
    {
        var cfg = this.Configuration ?? new TeleportNpcConfiguration();
        var ch = player.SelectedCharacter;
        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0f);
        var vipLevel = GetVipLevel(player);
        var isPk = ch is not null && ch.State >= HeroState.PlayerKiller1stStage;
        var inMiniGame = player.CurrentMiniGame is not null;

        var destinations = (cfg.Destinations ?? [])
            .Where(d => d.Enabled)
            .Select(d =>
            {
                var decision = TeleportNpcEngine.EvaluateWarp(
                    cfg,
                    plugInActive: true,
                    d.Id,
                    player.Level,
                    resets,
                    vipLevel,
                    player.Money,
                    isPk,
                    inMiniGame);
                return new
                {
                    id = d.Id,
                    nameEn = d.NameEn,
                    nameVi = d.NameVi,
                    mapNumber = d.MapNumber,
                    targetX = d.TargetX,
                    targetY = d.TargetY,
                    minLevel = d.MinLevel,
                    maxLevel = d.MaxLevel,
                    minResets = d.MinResets,
                    zenCost = d.ZenCost,
                    vipOnly = d.VipOnly,
                    allowPk = d.AllowPk,
                    eligible = decision.Allowed,
                    reasonEn = decision.Allowed ? string.Empty : decision.MessageEn,
                    reasonVi = decision.Allowed ? string.Empty : decision.MessageVi,
                };
            })
            .ToList();

        var payload = new
        {
            v = 1,
            enabled = cfg.Enabled,
            open = openDialog,
            closeAfterWarp = cfg.CloseAfterWarp,
            npcNumbers = cfg.NpcNumbers ?? [],
            me = new
            {
                level = player.Level,
                resets,
                vipLevel,
                zen = player.Money,
                isPk,
            },
            destinations,
            @event = actionResult is { } res
                ? new { ok = res.Ok, textEn = res.En, textVi = res.Vi }
                : null,
        };

        var json = EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
        await player.InvokeViewPlugInAsync<IHnmoTeleportNpcViewPlugIn>(p => p.ShowTeleportNpcStateAsync(json, requested)).ConfigureAwait(false);
    }

    private static int GetVipLevel(Player player)
    {
        var fromAttr = (int)(player.Attributes?[Stats.IsVip] ?? 0f);
        var fromAcct = player.Account is { } acc
            ? (int)acc.GetAccountAttributeValue(Stats.IsVip, DateTime.UtcNow)
            : 0;
        return Math.Max(fromAttr, fromAcct);
    }

    private static string EscapeNonAscii(string json)
    {
        var sb = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c <= 0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u").Append(((int)c).ToString("x4"));
            }
        }

        return sb.ToString();
    }
}

// <copyright file="QolPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Qol;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Quality-of-Life &amp; player convenience plug-in (disabled by default):
/// <c>/requests on|off</c> auto-refusal for trade/party/duel, <c>/srvinfo</c> live server summary,
/// and <c>/mine</c> excavation mini-system.
/// </summary>
[PlugIn]
[Display(Name = "QoL Commands & Mining", Description = "/requests on|off auto-refuse, /srvinfo rate/VIP summary, and /mine excavation system.")]
[Guid("6C9E4F12-3A8D-4B7E-9F10-2D5C8E7A1B34")]
public sealed class QolPlugIn :
    IFeaturePlugIn,
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<QolConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private readonly ConcurrentDictionary<Player, bool> _requestsBlocked = new();
    private readonly ConcurrentDictionary<Player, DateTime> _lastMineUtc = new();
    private readonly TimeProvider _timeProvider;
    private QolStore? _store;

    /// <summary>Initializes a new instance of the <see cref="QolPlugIn"/> class.</summary>
    public QolPlugIn()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="QolPlugIn"/> class for deterministic tests.</summary>
    /// <param name="timeProvider">Clock provider.</param>
    internal QolPlugIn(TimeProvider timeProvider)
    {
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public QolConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="QolPlugIn"/> instance on <paramref name="gameContext"/>, or <c>null</c>.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in instance, or <c>null</c>.</returns>
    public static QolPlugIn? Get(IGameContext? gameContext) =>
        gameContext?.FeaturePlugIns.GetPlugIn<QolPlugIn>();

    /// <summary>Returns whether <paramref name="target"/> currently has <c>/requests off</c> enabled.</summary>
    /// <param name="target">Target player receiving a request.</param>
    /// <returns><see langword="true"/> if requests should be automatically refused.</returns>
    public static bool AreRequestsBlocked(Player? target)
    {
        if (target is null || Get(target.GameContext) is not { } plugin)
        {
            return false;
        }

        if (plugin.Configuration is not { Enabled: true, RequestsToggleEnabled: true })
        {
            return false;
        }

        return plugin._requestsBlocked.TryGetValue(target, out var blocked) && blocked;
    }

    /// <summary>Picks a reward from <paramref name="rewards"/> proportionally by weight.</summary>
    /// <param name="rewards">Configured reward rows.</param>
    /// <param name="randomFraction">Uniform random value in [0, 1).</param>
    /// <returns>Chosen reward or <c>null</c> when empty.</returns>
    public static MiningRewardEntry? PickMiningReward(IReadOnlyList<MiningRewardEntry>? rewards, double randomFraction)
    {
        if (rewards is null || rewards.Count == 0)
        {
            return null;
        }

        var totalWeight = rewards.Sum(r => Math.Max(1, r.Weight));
        var target = Math.Clamp(randomFraction, 0.0, 0.999999999) * totalWeight;
        var sum = 0.0;
        foreach (var entry in rewards)
        {
            sum += Math.Max(1, entry.Weight);
            if (target < sum)
            {
                return entry;
            }
        }

        return rewards[^1];
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new QolConfiguration();

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState.IsDisconnectedOrFinished())
        {
            this._requestsBlocked.TryRemove(player, out _);
            this._lastMineUtc.TryRemove(player, out _);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Toggles or inspects <c>/requests on|off</c> for <paramref name="player"/>.</summary>
    /// <param name="player">Issuing player.</param>
    /// <param name="arg">Argument (<c>on</c>, <c>off</c>, or empty to toggle).</param>
    /// <returns>Resulting blocked state (<see langword="true"/> = blocked/off, <see langword="false"/> = allowed/on).</returns>
    public async ValueTask<bool> HandleRequestsCommandAsync(Player player, string? arg)
    {
        if (this.Configuration is not { Enabled: true, RequestsToggleEnabled: true })
        {
            await SendLocalizedAsync(player, "/requests is currently disabled on this server.", "Tính năng /requests hiện đang tắt trên máy chủ.").ConfigureAwait(false);
            return false;
        }

        var trimmed = (arg ?? string.Empty).Trim().ToLowerInvariant();
        var currentBlocked = this._requestsBlocked.TryGetValue(player, out var existing) && existing;
        var nextBlocked = trimmed switch
        {
            "off" or "0" or "disable" => true,
            "on" or "1" or "enable" => false,
            _ => !currentBlocked,
        };

        if (nextBlocked)
        {
            this._requestsBlocked[player] = true;
            await SendLocalizedAsync(
                player,
                "Incoming trade, party, and duel requests are now AUTO-REFUSED (/requests on to re-enable).",
                "Đã TẮT tự động nhận lời mời giao dịch, tổ đội và thách đấu (gõ /requests on để bật lại).").ConfigureAwait(false);
        }
        else
        {
            this._requestsBlocked.TryRemove(player, out _);
            await SendLocalizedAsync(
                player,
                "Incoming trade, party, and duel requests are now ALLOWED.",
                "Đã BẬT cho phép nhận lời mời giao dịch, tổ đội và thách đấu.").ConfigureAwait(false);
        }

        return nextBlocked;
    }

    /// <summary>Displays live server rates, resets, coins, and QoL status via <c>/srvinfo</c>.</summary>
    /// <param name="player">Requesting player.</param>
    /// <returns>A task representing the response.</returns>
    public async ValueTask HandleServerInfoCommandAsync(Player player)
    {
        if (this.Configuration is not { Enabled: true, ServerInfoEnabled: true })
        {
            await SendLocalizedAsync(player, "/srvinfo is currently disabled on this server.", "Lệnh /srvinfo hiện đang tắt.").ConfigureAwait(false);
            return;
        }

        var cfg = player.GameContext.Configuration;
        var expRate = cfg.ExperienceRate;
        var masterExpRate = cfg.MasterExperienceRate;
        var maxLvl = cfg.MaximumLevel;
        var zenMult = RateTuningPlugIn.GetZenDropMultiplier(player);
        var (_, lifeCap) = RateTuningPlugIn.GetEffectiveLifeOptionSettings(player, 0.5);
        var capLabel = lifeCap is { } cap ? $"+{cap * 4}" : "+16/+28";
        var wcc = player.Account?.WCoinC ?? 0;
        var wcp = player.Account?.WCoinP ?? 0;
        var gp = player.Account?.GoblinPoints ?? 0;
        var reqBlocked = AreRequestsBlocked(player);

        await SendLocalizedAsync(
            player,
            $"[Server Info] EXP: {expRate:0.##}x (Master {masterExpRate:0.##}x) | MaxLv: {maxLvl} | Zen: {zenMult:0.##}x | Life Cap: {capLabel} | Coins: {wcc} WCC / {wcp} WCP / {gp} GP | Requests: {(reqBlocked ? "OFF" : "ON")}",
            $"[Thông Tin Máy Chủ] EXP: {expRate:0.##}x (Master {masterExpRate:0.##}x) | Cấp Tối Đa: {maxLvl} | Zen: {zenMult:0.##}x | Giới Hạn Life: {capLabel} | Xu: {wcc} WCC / {wcp} WCP / {gp} GP | Lời mời: {(reqBlocked ? "TẮT" : "BẬT")}")
            .ConfigureAwait(false);
    }

    /// <summary>Executes <c>/mine</c> excavation if allowed on current map, off cooldown, and pick requirement met.</summary>
    /// <param name="player">Mining player.</param>
    /// <returns><see langword="true"/> if mining succeeded and yielded a reward.</returns>
    public async ValueTask<bool> HandleMineCommandAsync(Player player)
    {
        if (this.Configuration is not { Enabled: true, MiningEnabled: true } cfg)
        {
            await SendLocalizedAsync(player, "Excavation (/mine) is not enabled on this server.", "Tính năng đào khoáng (/mine) hiện đang tắt.").ConfigureAwait(false);
            return false;
        }

        var mapNumber = player.CurrentMap?.Definition?.Number ?? -1;
        if (cfg.MiningAllowedMapNumbers.Count > 0 && !cfg.MiningAllowedMapNumbers.Contains(mapNumber))
        {
            await SendLocalizedAsync(player, "You cannot mine in this map.", "Không thể đào khoáng tại bản đồ này.").ConfigureAwait(false);
            return false;
        }

        if (cfg.MiningRequiredItemGroup >= 0)
        {
            var hasPick = player.Inventory?.Items.Any(i =>
                i.Definition is not null
                && i.Definition.Group == cfg.MiningRequiredItemGroup
                && i.Definition.Number == cfg.MiningRequiredItemNumber
                && i.Durability > 0) == true;
            if (!hasPick)
            {
                await SendLocalizedAsync(player, "You need a Mining Pick in your inventory to mine.", "Bạn cần có Cuốc Đào Khoáng (Pick) trong hành trang.").ConfigureAwait(false);
                return false;
            }
        }

        var nowUtc = this._timeProvider.GetUtcNow().UtcDateTime;
        var cooldown = Math.Clamp(cfg.MiningCooldownSeconds, 1, 3600);
        if (this._lastMineUtc.TryGetValue(player, out var prevUtc))
        {
            var waitLeft = cooldown - (nowUtc - prevUtc).TotalSeconds;
            if (waitLeft > 0)
            {
                await SendLocalizedAsync(
                    player,
                    $"Excavation cooling down: wait {Math.Ceiling(waitLeft):0}s.",
                    $"Cuốc đào đang hồi: vui lòng chờ thêm {Math.Ceiling(waitLeft):0} giây.").ConfigureAwait(false);
                return false;
            }
        }

        var reward = PickMiningReward(cfg.MiningRewards, Rand.NextDouble());
        if (reward is null)
        {
            await SendLocalizedAsync(player, "Nothing mined (no reward table entries configured).", "Chưa cấu hình phần thưởng đào khoáng.").ConfigureAwait(false);
            return false;
        }

        if (reward.ItemGroup >= 0)
        {
            var def = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == reward.ItemGroup && d.Number == reward.ItemNumber);
            if (def is not null && player.Inventory is { } inventory)
            {
                var item = player.PersistenceContext.CreateNew<Item>();
                item.Definition = def;
                item.Durability = Math.Max(1, (int)def.Durability);
                if (inventory.CheckInvSpace(item) is not { } slot || !await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
                {
                    player.PersistenceContext.Detach(item);
                    await SendLocalizedAsync(player, "Your inventory is full; free space before mining.", "Hành trang đã đầy, hãy dọn bớt chỗ trước khi đào.").ConfigureAwait(false);
                    return false;
                }

                await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
            }
        }

        if (reward.Zen > 0)
        {
            if (!player.TryAddMoney(reward.Zen))
            {
                await SendLocalizedAsync(player, "Inventory Zen is full; clear space before mining.", "Túi Zen đã đầy, vui lòng gửi bớt trước khi đào.").ConfigureAwait(false);
                return false;
            }

            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        this._lastMineUtc[player] = nowUtc;
        this.GetStore(cfg).RecordMine(player.SelectedCharacter?.Name ?? player.Account?.LoginName ?? "Miner", reward.NameEn, nowUtc);
        await SendLocalizedAsync(
            player,
            $"[Mining] Discovered: {reward.NameEn}!",
            $"[Đào Khoáng] Thu hoạch được: {reward.NameVi}!").ConfigureAwait(false);
        return true;
    }

    private static ValueTask SendLocalizedAsync(Player player, string en, string vi)
    {
        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        return player.ShowBlueMessageAsync(isVi ? vi : en);
    }

    private QolStore GetStore(QolConfiguration cfg) =>
        this._store ??= QolStore.Open(cfg.FileName);
}

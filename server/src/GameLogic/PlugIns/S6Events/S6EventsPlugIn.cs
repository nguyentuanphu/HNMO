// <copyright file="S6EventsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging;

/// <summary>
/// Season 6 event &amp; token exchange plug-in (disabled by default): Last Man Standing scheduled PvP elimination,
/// Cherry Blossom Spirit exchange (<c>/blossom</c>), and Delgado Lucky Coin registration (<c>/luckycoin</c>).
/// </summary>
[PlugIn]
[Display(Name = "Season 6 Events & Exchanges", Description = "Last Man Standing arena event (/lms), Cherry Blossom Spirit (/blossom), and Lucky Coin (/luckycoin) exchanges.")]
[Guid("8D5A31E9-72C4-4A18-9F6B-3E2D4C5A6B71")]
public sealed class S6EventsPlugIn :
    IPeriodicTaskPlugIn,
    IPlayerTalkToNpcPlugIn,
    IAttackableGotKilledPlugIn,
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<S6EventsConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>GrantedBy identifier recorded on CashShopCoinGrant records.</summary>
    public const string GrantedBy = "HNMO S6 Events";

    private readonly object _roundLock = new();
    private readonly ConcurrentDictionary<Player, int> _lmsContestants = new();

    /// <summary>The entry fee each contestant of the current registration paid, to refund it when the round is cancelled.</summary>
    private readonly ConcurrentDictionary<Player, int> _lmsPaidFees = new();
    private LmsPhase _lmsPhase = LmsPhase.Idle;
    private DateTime _phaseDeadlineUtc = DateTime.MinValue;
    private DateTime _lastTickUtc = DateTime.MinValue;
    private string _activeRoundId = string.Empty;
    private S6EventsStore? _store;
    private int _tickRunning;

    /// <summary>Live phase of the Last Man Standing event.</summary>
    public enum LmsPhase
    {
        /// <summary>No event currently open.</summary>
        Idle = 0,

        /// <summary>Registration open via <c>/lms</c>.</summary>
        Registration = 1,

        /// <summary>Combat round active.</summary>
        Fighting = 2,
    }

    /// <inheritdoc />
    public S6EventsConfiguration? Configuration { get; set; }

    /// <summary>Gets a value indicating whether an LMS registration or combat round is live.</summary>
    public bool IsLmsActive => this._lmsPhase != LmsPhase.Idle;

    /// <summary>Gets current LMS phase.</summary>
    public LmsPhase CurrentLmsPhase => this._lmsPhase;

    /// <summary>Gets count of currently surviving LMS contestants.</summary>
    public int CurrentContestantCount => this._lmsContestants.Count;

    /// <summary>Gets active <see cref="S6EventsPlugIn"/> from <paramref name="gameContext"/>, if enabled in plug-in manager.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active instance or <c>null</c>.</returns>
    public static S6EventsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<S6EventsPlugIn>().FirstOrDefault();

    /// <summary>Immediately opens LMS registration from the Admin Panel if enabled.</summary>
    /// <param name="gameContexts">Running game server contexts.</param>
    /// <returns>Status code (<c>Ok</c>, <c>AlreadyRunning</c>, <c>Disabled</c>).</returns>
    public static async ValueTask<string> TriggerLmsNowAsync(IEnumerable<IGameContext> gameContexts)
    {
        foreach (var ctx in gameContexts)
        {
            if (Get(ctx) is { } plugin && plugin.Configuration is { Enabled: true, LmsEnabled: true } cfg)
            {
                if (plugin._lmsPhase != LmsPhase.Idle)
                {
                    return "AlreadyRunning";
                }

                await plugin.StartLmsRegistrationAsync(ctx, cfg).ConfigureAwait(false);
                return "Ok";
            }
        }

        return "Disabled";
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new S6EventsConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._lastTickUtc = DateTime.MinValue;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (this.Configuration is not { Enabled: true, LmsEnabled: true } cfg)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - this._lastTickUtc < TimeSpan.FromSeconds(5))
        {
            return;
        }

        this._lastTickUtc = nowUtc;
        if (Interlocked.Exchange(ref this._tickRunning, 1) == 1)
        {
            return;
        }

        try
        {
            if (this._lmsPhase == LmsPhase.Idle)
            {
                var slotKey = S6EventsEngine.ResolveDueScheduleSlot(cfg.LmsDailyTimes, cfg.TimeZone, nowUtc);
                if (slotKey is not null && this.GetStore(cfg).TryClaimScheduleSlot(slotKey))
                {
                    await this.StartLmsRegistrationAsync(gameContext, cfg).ConfigureAwait(false);
                }
            }
            else if (this._lmsPhase == LmsPhase.Registration && nowUtc >= this._phaseDeadlineUtc)
            {
                await this.BeginLmsFightOrCancelAsync(gameContext, cfg).ConfigureAwait(false);
            }
            else if (this._lmsPhase == LmsPhase.Fighting)
            {
                await this.CheckLmsCompletionAsync(gameContext, cfg, timedOut: nowUtc >= this._phaseDeadlineUtc).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref this._tickRunning, 0);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (eventArgs.HasBeenHandled || this.Configuration is not { Enabled: true } cfg || npc.Definition is null)
        {
            return;
        }

        if (cfg.CherryBlossomEnabled && npc.Definition.Number == cfg.CherryBlossomNpcNumber)
        {
            await this.ExchangeTokensAsync(player, cfg, isCherryBlossom: true, requestedCount: 0).ConfigureAwait(false);
            eventArgs.HasBeenHandled = true;
            eventArgs.LeavesDialogOpen = false;
        }
        else if (cfg.LuckyCoinEnabled && npc.Definition.Number == cfg.LuckyCoinNpcNumber)
        {
            await this.ExchangeTokensAsync(player, cfg, isCherryBlossom: false, requestedCount: 0).ConfigureAwait(false);
            eventArgs.HasBeenHandled = true;
            eventArgs.LeavesDialogOpen = false;
        }
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (this.Configuration is not { Enabled: true, LmsEnabled: true } cfg || this._lmsPhase != LmsPhase.Fighting)
        {
            return;
        }

        if (killed is not Player victim || !this._lmsContestants.TryRemove(victim, out _))
        {
            return;
        }

        if (killer is Player killerPlayer && this._lmsContestants.ContainsKey(killerPlayer))
        {
            this._lmsContestants.AddOrUpdate(killerPlayer, 1, (_, count) => count + 1);
        }

        await this.SendLocalizedBlueAsync(
            victim,
            "You have been eliminated from Last Man Standing!",
            "Bạn đã bị loại khỏi Chiến Trường Sinh Tử (LMS)!").ConfigureAwait(false);
        await this.WarpBackToSafezoneAsync(victim, cfg).ConfigureAwait(false);
        await this.CheckLmsCompletionAsync(victim.GameContext, cfg, timedOut: false).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration is not { Enabled: true } cfg)
        {
            return;
        }

        if (currentState == PlayerState.EnteredWorld)
        {
            await this.DeliverPendingOwedRewardsAsync(player, cfg).ConfigureAwait(false);
            return;
        }

        if (currentState.IsDisconnectedOrFinished() && this._lmsContestants.TryRemove(player, out _))
        {
            // A contestant who leaves gives up the entry, like at the gate of a mini game.
            this._lmsPaidFees.TryRemove(player, out _);

            if (this._lmsPhase == LmsPhase.Fighting)
            {
                await this.CheckLmsCompletionAsync(player.GameContext, cfg, timedOut: false).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Handles <c>/lms</c> chat command (registration or live status).</summary>
    /// <param name="player">Issuing player.</param>
    /// <returns>A task representing the command execution.</returns>
    public async ValueTask HandleLmsCommandAsync(Player player)
    {
        if (this.Configuration is not { Enabled: true, LmsEnabled: true } cfg)
        {
            await this.SendLocalizedBlueAsync(player, "Last Man Standing is not enabled.", "Chiến Trường Sinh Tử chưa được bật.").ConfigureAwait(false);
            return;
        }

        if (this._lmsPhase == LmsPhase.Idle)
        {
            var next = string.Join(", ", cfg.LmsDailyTimes);
            await this.SendLocalizedBlueAsync(
                player,
                $"Last Man Standing is closed. Daily times ({cfg.TimeZone}): {next}.",
                $"Chiến Trường Sinh Tử đang đóng. Lịch hàng ngày ({cfg.TimeZone}): {next}.").ConfigureAwait(false);
            return;
        }

        if (this._lmsPhase == LmsPhase.Fighting)
        {
            await this.SendLocalizedBlueAsync(
                player,
                $"Last Man Standing is already in progress ({this._lmsContestants.Count} survivors left).",
                $"Chiến Trường Sinh Tử đang diễn ra (còn {this._lmsContestants.Count} người sống sót).").ConfigureAwait(false);
            return;
        }

        var level = Math.Max(
            (int)(player.Attributes?[Attributes.Stats.TotalLevel] ?? 0),
            (int)(player.Attributes?[Attributes.Stats.Level] ?? 0));
        if (level < cfg.LmsMinLevel)
        {
            await this.SendLocalizedBlueAsync(
                player,
                $"You need at least level {cfg.LmsMinLevel} to enter Last Man Standing.",
                $"Cần đạt ít nhất cấp {cfg.LmsMinLevel} để tham gia Chiến Trường Sinh Tử.").ConfigureAwait(false);
            return;
        }

        if (this._lmsContestants.ContainsKey(player))
        {
            await this.SendLocalizedBlueAsync(player, "You are already registered for Last Man Standing.", "Bạn đã đăng ký Chiến Trường Sinh Tử rồi.").ConfigureAwait(false);
            return;
        }

        if (cfg.LmsEntryFeeZen > 0)
        {
            if (!player.TryRemoveMoney(cfg.LmsEntryFeeZen))
            {
                await this.SendLocalizedBlueAsync(
                    player,
                    $"Need {cfg.LmsEntryFeeZen:N0} Zen to join Last Man Standing.",
                    $"Cần {cfg.LmsEntryFeeZen:N0} Zen để vào Chiến Trường Sinh Tử.").ConfigureAwait(false);
                return;
            }

            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            this._lmsPaidFees[player] = cfg.LmsEntryFeeZen;
        }

        this._lmsContestants.TryAdd(player, 0);
        await this.WarpToArenaAsync(player, cfg).ConfigureAwait(false);
        await this.SendLocalizedBlueAsync(
            player,
            $"Entered Last Man Standing! Contestants waiting: {this._lmsContestants.Count}.",
            $"Đã vào Chiến Trường Sinh Tử! Số người tham gia: {this._lmsContestants.Count}.").ConfigureAwait(false);
    }

    /// <summary>Performs a Cherry Blossom (<c>isCherryBlossom == true</c>) or Lucky Coin exchange for <paramref name="player"/>.</summary>
    /// <param name="player">Requesting player.</param>
    /// <param name="isCherryBlossom">True for Cherry Blossom, false for Delgado Lucky Coin.</param>
    /// <param name="requestedCount">Optional explicit tier quantity (0 = auto highest affordable).</param>
    /// <returns>True when an exchange succeeded and items were consumed.</returns>
    public async ValueTask<bool> HandleExchangeCommandAsync(Player player, bool isCherryBlossom, int requestedCount)
    {
        if (this.Configuration is not { Enabled: true } cfg)
        {
            await this.SendLocalizedBlueAsync(player, "Season 6 exchanges are disabled on this server.", "Tính năng đổi thưởng Season 6 đang tắt.").ConfigureAwait(false);
            return false;
        }

        return await this.ExchangeTokensAsync(player, cfg, isCherryBlossom, requestedCount).ConfigureAwait(false);
    }

    /// <summary>Transitions LMS from Registration into Fighting (or cancels if under min contestants).</summary>
    /// <param name="gameContext">Game context.</param>
    /// <param name="cfg">Active configuration.</param>
    /// <returns>A task representing the state transition.</returns>
    internal async ValueTask BeginLmsFightOrCancelAsync(IGameContext gameContext, S6EventsConfiguration cfg)
    {
        if (this._lmsContestants.Count < Math.Max(2, cfg.LmsMinPlayers))
        {
            this._lmsPhase = LmsPhase.Idle;
            var contestants = this._lmsContestants.Keys.ToList();
            this._lmsContestants.Clear();
            await this.BroadcastGoldenAsync(
                gameContext,
                "Last Man Standing cancelled due to insufficient contestants.",
                "Chiến Trường Sinh Tử bị hủy do chưa đủ số người tham gia.").ConfigureAwait(false);

            // Like a mini game which can't start, the round gives the entry fee back, and the
            // contestants don't stay behind in the arena they were warped into.
            foreach (var contestant in contestants)
            {
                if (this._lmsPaidFees.TryRemove(contestant, out var fee) && fee > 0 && contestant.TryAddMoney(fee))
                {
                    await contestant.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
                }

                await this.WarpBackToSafezoneAsync(contestant, cfg).ConfigureAwait(false);
            }

            this._lmsPaidFees.Clear();
            return;
        }

        this._lmsPaidFees.Clear();
        this._lmsPhase = LmsPhase.Fighting;
        this._phaseDeadlineUtc = DateTime.UtcNow.AddMinutes(Math.Clamp(cfg.LmsDurationMinutes, 1, 60));
        await this.BroadcastGoldenAsync(
            gameContext,
            $"Last Man Standing BEGINS! {this._lmsContestants.Count} fighters — last survivor wins!",
            $"Chiến Trường Sinh Tử BẮT ĐẦU! {this._lmsContestants.Count} chiến binh — người cuối cùng sẽ chiến thắng!").ConfigureAwait(false);
    }

    private async ValueTask<bool> ExchangeTokensAsync(Player player, S6EventsConfiguration cfg, bool isCherryBlossom, int requestedCount)
    {
        var enabled = isCherryBlossom ? cfg.CherryBlossomEnabled : cfg.LuckyCoinEnabled;
        if (!enabled)
        {
            await this.SendLocalizedBlueAsync(player, "This exchange is currently disabled.", "Tính năng đổi thưởng này hiện đang tắt.").ConfigureAwait(false);
            return false;
        }

        var group = isCherryBlossom ? cfg.CherryBlossomItemGroup : cfg.LuckyCoinItemGroup;
        var number = isCherryBlossom ? cfg.CherryBlossomItemNumber : cfg.LuckyCoinItemNumber;
        var tiers = isCherryBlossom ? cfg.CherryBlossomTiers : cfg.LuckyCoinTiers;

        var available = S6EventsEngine.CountStackedItems(player.Inventory, group, number);
        var tier = S6EventsEngine.SelectAffordableTier(tiers, available, requestedCount);
        if (tier is null)
        {
            var requiredHints = tiers.Count == 0
                ? "-"
                : string.Join("/", tiers.Select(t => t.RequiredCount).OrderBy(x => x));
            await this.SendLocalizedBlueAsync(
                player,
                $"You have {available} tokens. Need tier count: {requiredHints}.",
                $"Bạn hiện có {available} vật phẩm. Các mốc đổi: {requiredHints}.").ConfigureAwait(false);
            return false;
        }

        var reward = S6EventsEngine.PickWeightedReward(tier.Rewards, Rand.NextDouble());
        if (reward is null)
        {
            await this.SendLocalizedBlueAsync(player, "No rewards are configured for that tier.", "Chưa cấu hình phần thưởng cho mốc này.").ConfigureAwait(false);
            return false;
        }

        if (!await S6EventsEngine.TryConsumeStackedItemsAsync(player, group, number, tier.RequiredCount).ConfigureAwait(false))
        {
            await this.SendLocalizedBlueAsync(player, "Failed to consume required exchange items.", "Không thể trừ đủ vật phẩm đổi thưởng.").ConfigureAwait(false);
            return false;
        }

        await this.DeliverRewardAsync(
            player.GameContext,
            player,
            player.Account?.LoginName ?? string.Empty,
            player.SelectedCharacter?.Name ?? string.Empty,
            reward.Zen,
            reward.Coins,
            reward.CoinType,
            reward.ItemGroup,
            reward.ItemNumber,
            reward.ItemLevel,
            reward.ItemCount,
            reward.NameEn,
            reward.NameVi,
            cfg,
            $"s6-ex-{(isCherryBlossom ? "cb" : "lc")}-{Guid.NewGuid():N}").ConfigureAwait(false);

        await this.SendLocalizedBlueAsync(
            player,
            $"Exchanged {tier.RequiredCount} tokens -> Received: {reward.NameEn}!",
            $"Đã đổi {tier.RequiredCount} vật phẩm -> Nhận: {reward.NameVi}!").ConfigureAwait(false);
        return true;
    }

    private async ValueTask StartLmsRegistrationAsync(IGameContext gameContext, S6EventsConfiguration cfg)
    {
        lock (this._roundLock)
        {
            this._lmsContestants.Clear();
            this._lmsPaidFees.Clear();
            this._activeRoundId = Guid.NewGuid().ToString("N");
            this._lmsPhase = LmsPhase.Registration;
            this._phaseDeadlineUtc = DateTime.UtcNow.AddMinutes(Math.Clamp(cfg.LmsRegistrationMinutes, 1, 15));
        }

        await this.BroadcastGoldenAsync(
            gameContext,
            $"Last Man Standing registration is OPEN for {cfg.LmsRegistrationMinutes} min(s)! Type /lms to join!",
            $"Chiến Trường Sinh Tử (LMS) ĐÃ MỞ đăng ký trong {cfg.LmsRegistrationMinutes} phút! Gõ /lms để tham gia!").ConfigureAwait(false);
    }

    private async ValueTask CheckLmsCompletionAsync(IGameContext gameContext, S6EventsConfiguration cfg, bool timedOut)
    {
        if (this._lmsPhase != LmsPhase.Fighting)
        {
            return;
        }

        var survivors = this._lmsContestants.ToArray();
        if (survivors.Length > 1 && !timedOut)
        {
            return;
        }

        this._lmsPhase = LmsPhase.Idle;
        this._lmsContestants.Clear();

        if (survivors.Length == 0)
        {
            await this.BroadcastGoldenAsync(
                gameContext,
                "Last Man Standing ended with no survivors.",
                "Chiến Trường Sinh Tử kết thúc không có người sống sót.").ConfigureAwait(false);
            return;
        }

        var winnerPair = survivors.OrderByDescending(kv => kv.Value).First();
        var winner = winnerPair.Key;
        var kills = winnerPair.Value;
        var charName = winner.SelectedCharacter?.Name ?? "Hero";
        var loginName = winner.Account?.LoginName ?? string.Empty;

        await this.DeliverRewardAsync(
            gameContext,
            winner,
            loginName,
            charName,
            cfg.LmsRewardZen,
            cfg.LmsRewardCoins,
            cfg.LmsRewardCoinType,
            cfg.LmsRewardItemGroup,
            cfg.LmsRewardItemNumber,
            cfg.LmsRewardItemLevel,
            cfg.LmsRewardItemCount,
            "Last Man Standing Winner Reward",
            "Phần Thưởng Vô Địch Chiến Trường Sinh Tử",
            cfg,
            $"s6-lms-{this._activeRoundId}").ConfigureAwait(false);

        this.GetStore(cfg).RecordLmsWin(new S6EventsLmsWinEntry
        {
            CompletedAtUtc = DateTime.UtcNow,
            LoginName = loginName,
            CharacterName = charName,
            Participants = Math.Max(1, survivors.Length + kills),
            Kills = kills,
            RewardSummary = $"{cfg.LmsRewardCoins} {cfg.LmsRewardCoinType}, {cfg.LmsRewardZen:N0} Zen",
        });

        await this.BroadcastGoldenAsync(
            gameContext,
            $"[LMS] {charName} is the Last Man Standing champion ({kills} KOs)!",
            $"[LMS] {charName} đã vô địch Chiến Trường Sinh Tử ({kills} hạ gục)!").ConfigureAwait(false);

        await this.WarpBackToSafezoneAsync(winner, cfg).ConfigureAwait(false);
    }

    private async ValueTask DeliverRewardAsync(
        IGameContext gameContext,
        Player? player,
        string loginName,
        string characterName,
        uint zen,
        int coins,
        CashShopCoinType coinType,
        int itemGroup,
        short itemNumber,
        byte itemLevel,
        int itemCount,
        string reasonEn,
        string reasonVi,
        S6EventsConfiguration cfg,
        string reference)
    {
        if (coins > 0 && !string.IsNullOrWhiteSpace(loginName))
        {
            await MissionCoinGranter.GrantAsync(
                gameContext,
                new CoinGrant(loginName, coinType, coins, reasonEn, reference),
                GrantedBy).ConfigureAwait(false);
        }

        if (zen > 0 || (itemGroup >= 0 && itemCount > 0))
        {
            var owed = new S6EventsOwedReward
            {
                LoginName = loginName,
                CharacterName = characterName,
                ReasonEn = reasonEn,
                ReasonVi = reasonVi,
                Zen = zen,
                ItemGroup = itemGroup,
                ItemNumber = itemNumber,
                ItemLevel = itemLevel,
                ItemCount = itemCount,
            };

            if (player is null || !await S6EventsEngine.TryDeliverGoodsAsync(player, owed).ConfigureAwait(false))
            {
                this.GetStore(cfg).QueueOwedReward(owed);
                if (player is not null)
                {
                    await this.SendLocalizedBlueAsync(
                        player,
                        "Bag full or Zen capped — your event reward has been saved to Owed Rewards and will deliver automatically once space is free.",
                        "Túi đầy hoặc kịch Zen — phần thưởng đã được lưu chờ nhận và sẽ tự phát khi còn chỗ trống.").ConfigureAwait(false);
                }
            }
        }
    }

    private async ValueTask DeliverPendingOwedRewardsAsync(Player player, S6EventsConfiguration cfg)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var store = this.GetStore(cfg);
        var owedList = store.GetOwedRewardsFor(login, player.SelectedCharacter?.Name);
        foreach (var owed in owedList)
        {
            if (await S6EventsEngine.TryDeliverGoodsAsync(player, owed).ConfigureAwait(false))
            {
                store.RemoveOwedReward(owed.RewardId);
                await this.SendLocalizedBlueAsync(
                    player,
                    $"Delivered queued event reward: {owed.ReasonEn}.",
                    $"Đã phát phần thưởng sự kiện còn nợ: {owed.ReasonVi}.").ConfigureAwait(false);
            }
        }
    }

    private async ValueTask WarpToArenaAsync(Player player, S6EventsConfiguration cfg)
    {
        var mapDef = player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == cfg.LmsArenaMapNumber)
            ?? player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == 0);
        if (mapDef is null)
        {
            return;
        }

        await player.WarpToAsync(new ExitGate { Map = mapDef, X1 = cfg.LmsArenaX, X2 = cfg.LmsArenaX, Y1 = cfg.LmsArenaY, Y2 = cfg.LmsArenaY }).ConfigureAwait(false);
    }

    private async ValueTask WarpBackToSafezoneAsync(Player player, S6EventsConfiguration cfg)
    {
        var mapDef = player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == cfg.LmsReturnMapNumber)
            ?? player.GameContext.Configuration.Maps.FirstOrDefault(m => m.Number == 0);
        if (mapDef is null)
        {
            return;
        }

        await player.WarpToAsync(new ExitGate { Map = mapDef, X1 = cfg.LmsReturnX, X2 = cfg.LmsReturnX, Y1 = cfg.LmsReturnY, Y2 = cfg.LmsReturnY }).ConfigureAwait(false);
    }

    private async ValueTask BroadcastGoldenAsync(IGameContext gameContext, string en, string vi)
    {
        await gameContext.ForEachPlayerAsync(async recipient =>
        {
            var isVi = string.Equals(recipient.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            await recipient.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(isVi ? vi : en, MessageType.GoldenCenter)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private ValueTask SendLocalizedBlueAsync(Player player, string en, string vi)
    {
        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        return player.ShowBlueMessageAsync(isVi ? vi : en);
    }

    private S6EventsStore GetStore(S6EventsConfiguration cfg) =>
        this._store ??= S6EventsStore.Open(cfg.FileName);
}

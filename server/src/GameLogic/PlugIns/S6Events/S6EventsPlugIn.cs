// <copyright file="S6EventsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Pathfinding;
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
    private readonly ConcurrentDictionary<Monster, byte> _lorenMobs = new();
    private LmsPhase _lmsPhase = LmsPhase.Idle;
    private DateTime _phaseDeadlineUtc = DateTime.MinValue;
    private DateTime _lastTickUtc = DateTime.MinValue;
    private string _activeRoundId = string.Empty;
    private bool _lorenActive;
    private LorenDeepEngine.AssaultType _lorenType;
    private int _lorenWave;
    private DateTime _lorenStartedUtc = DateTime.MinValue;
    private DateTime _lorenDeadlineUtc = DateTime.MinValue;
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

    /// <summary>Gets a value indicating whether a Loren Deep assault event is currently active.</summary>
    public bool IsLorenDeepActive => this._lorenActive;

    /// <summary>Gets active <see cref="S6EventsPlugIn"/> from <paramref name="gameContext"/>, if enabled in plug-in manager.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active instance or <c>null</c>.</returns>
    public static S6EventsPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<S6EventsPlugIn>().FirstOrDefault();

    /// <summary>Immediately starts a Loren Deep assault event from the Admin Panel if enabled.</summary>
    /// <param name="gameContexts">Running game server contexts.</param>
    /// <returns>Status code (<c>Ok</c>, <c>AlreadyRunning</c>, <c>Disabled</c>).</returns>
    public static async ValueTask<string> TriggerLorenDeepNowAsync(IEnumerable<IGameContext> gameContexts)
    {
        foreach (var ctx in gameContexts)
        {
            if (Get(ctx) is { } plugin && plugin.Configuration is { Enabled: true, LorenDeepEnabled: true } cfg)
            {
                if (plugin._lorenActive)
                {
                    return "AlreadyRunning";
                }

                await plugin.StartLorenDeepAsync(ctx, cfg, DateTime.UtcNow).ConfigureAwait(false);
                return "Ok";
            }
        }

        return "Disabled";
    }

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
        if (this.Configuration is not { Enabled: true } cfg || (!cfg.LmsEnabled && !cfg.LorenDeepEnabled))
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
            if (cfg.LmsEnabled)
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

            if (cfg.LorenDeepEnabled)
            {
                await this.TickLorenDeepAsync(gameContext, cfg, nowUtc).ConfigureAwait(false);
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
        else if (cfg.MiningEnabled && npc.Definition.Number is 605 or 606 or 607)
        {
            await this.PerformMiningAttemptAsync(player, cfg, (MiningTwinkleType)npc.Definition.Number, stage: 1).ConfigureAwait(false);
            eventArgs.HasBeenHandled = true;
            eventArgs.LeavesDialogOpen = false;
        }
        else if (cfg.XMasEnabled && npc.Definition.Number == 467)
        {
            await this.HandleXMasCommandAsync(player).ConfigureAwait(false);
            eventArgs.HasBeenHandled = true;
            eventArgs.LeavesDialogOpen = false;
        }
        else if (cfg.XMasEnabled && npc.Definition.Number is >= 468 and <= 475)
        {
            await this.ApplySantaBuffAsync(player, cfg, npc.Definition.Number).ConfigureAwait(false);
            eventArgs.HasBeenHandled = true;
            eventArgs.LeavesDialogOpen = false;
        }
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (this.Configuration is not { Enabled: true } cfg)
        {
            return;
        }

        if (cfg.MuRummyEnabled
            && killed is Monster killedMonster
            && killedMonster.CurrentMap is { } map
            && killer is Player monsterKiller
            && cfg.MuRummyCardPieceDropRatePer10k > 0
            && Rand.NextInt(0, 10_000) < cfg.MuRummyCardPieceDropRatePer10k
            && monsterKiller.GameContext.Configuration.Items.FirstOrDefault(i => i.Group == 14 && i.Number == 215) is { } cardPieceDef)
        {
            var droppedCard = new TemporaryItem { Definition = cardPieceDef, Durability = 1 };
            var owners = monsterKiller.Party?.PartyList.AsEnumerable() ?? monsterKiller.GetAsEnumerable();
            await map.AddAsync(new DroppedItem(droppedCard, killedMonster.Position, map, null, owners)).ConfigureAwait(false);
        }

        if (!cfg.LmsEnabled || this._lmsPhase != LmsPhase.Fighting)
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

    /// <summary>Handles <c>/loren</c> command reporting Loren Deep schedule and wave state.</summary>
    /// <param name="player">Issuing player.</param>
    /// <returns>A task representing the command handling.</returns>
    public async ValueTask HandleLorenCommandAsync(Player player)
    {
        if (this.Configuration is not { Enabled: true, LorenDeepEnabled: true } cfg)
        {
            await this.SendLocalizedBlueAsync(player, "Loren Deep is disabled on this server.", "Sự kiện Loren Deep chưa được bật.").ConfigureAwait(false);
            return;
        }

        if (this._lorenActive)
        {
            var typeEn = this._lorenType == LorenDeepEngine.AssaultType.SpecialRaid ? "Special Raid" : "Common Assault";
            var typeVi = this._lorenType == LorenDeepEngine.AssaultType.SpecialRaid ? "Tập Kích Đặc Biệt" : "Tổng Tấn Công";
            await this.SendLocalizedBlueAsync(
                player,
                $"Loren Deep active ({typeEn}) — Wave {this._lorenWave}/3 on Valley of Loren!",
                $"Loren Deep đang diễn ra ({typeVi}) — Đợt {this._lorenWave}/3 tại Thung Lũng Loren!").ConfigureAwait(false);
            return;
        }

        var next = string.Join(", ", cfg.LorenDeepDailyTimes);
        await this.SendLocalizedBlueAsync(
            player,
            $"Loren Deep schedule ({cfg.TimeZone}): {next}.",
            $"Lịch Loren Deep ({cfg.TimeZone}): {next}.").ConfigureAwait(false);
    }

    /// <summary>Handles <c>/mine [small|medium|large] [stage]</c> and <c>/mine repair</c>.</summary>
    /// <param name="player">Player.</param>
    /// <param name="command">Raw command text.</param>
    /// <returns>True if handled.</returns>
    public async ValueTask<bool> HandleMineCommandAsync(Player player, string command)
    {
        if (this.Configuration is not { Enabled: true, MiningEnabled: true } cfg)
        {
            await this.SendLocalizedBlueAsync(player, "Mining System is disabled on this server.", "Hệ thống Đào Khoáng đang tắt.").ConfigureAwait(false);
            return false;
        }

        var tokens = (command ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 1 && string.Equals(tokens[1], "repair", StringComparison.OrdinalIgnoreCase))
        {
            return await this.RepairPandoraPickAsync(player, cfg).ConfigureAwait(false);
        }

        var twinkle = MiningTwinkleType.Small;
        var stage = 1;
        for (var i = 1; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], "medium", StringComparison.OrdinalIgnoreCase))
            {
                twinkle = MiningTwinkleType.Medium;
            }
            else if (string.Equals(tokens[i], "large", StringComparison.OrdinalIgnoreCase))
            {
                twinkle = MiningTwinkleType.Large;
            }
            else if (int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStage))
            {
                stage = Math.Clamp(parsedStage, 1, 5);
            }
        }

        return await this.PerformMiningAttemptAsync(player, cfg, twinkle, stage).ConfigureAwait(false);
    }

    /// <summary>Handles <c>/rummy</c> subcommands: combine, start, status, match i j k, finish.</summary>
    /// <param name="player">Player.</param>
    /// <param name="command">Raw command text.</param>
    /// <returns>True when command mutated game/inventory state.</returns>
    public async ValueTask<bool> HandleRummyCommandAsync(Player player, string command)
    {
        if (this.Configuration is not { Enabled: true, MuRummyEnabled: true } cfg)
        {
            await this.SendLocalizedBlueAsync(player, "Mu Rummy is disabled on this server.", "Sự kiện Mu Rummy đang tắt.").ConfigureAwait(false);
            return false;
        }

        var tokens = (command ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sub = tokens.Length > 1 ? tokens[1].ToLowerInvariant() : "status";
        var charName = player.SelectedCharacter?.Name ?? string.Empty;
        var store = this.GetStore(cfg);

        switch (sub)
        {
            case "combine":
            {
                var pieces = S6EventsEngine.CountStackedItems(player.Inventory, 14, 215);
                if (pieces < cfg.MuRummyRequiredPieces)
                {
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Need {cfg.MuRummyRequiredPieces} Card Pieces (have {pieces}).",
                        $"Cần {cfg.MuRummyRequiredPieces} Mảnh bài (hiện có {pieces}).").ConfigureAwait(false);
                    return false;
                }

                if (!await S6EventsEngine.TryConsumeStackedItemsAsync(player, 14, 215, cfg.MuRummyRequiredPieces).ConfigureAwait(false))
                {
                    return false;
                }

                await this.DeliverRewardAsync(
                    player.GameContext,
                    player,
                    player.Account?.LoginName ?? string.Empty,
                    charName,
                    0,
                    0,
                    CashShopCoinType.WCoinC,
                    14,
                    216,
                    0,
                    1,
                    "Card Deck combined from 24 Card Pieces",
                    "Ghép Bộ bài Rummy từ 24 Mảnh bài",
                    cfg,
                    $"s6-rummy-deck-{Guid.NewGuid():N}").ConfigureAwait(false);
                await this.SendLocalizedBlueAsync(player, "Combined 24 Card Pieces into 1 Card Deck!", "Đã ghép 24 Mảnh bài thành 1 Bộ bài Rummy!").ConfigureAwait(false);
                return true;
            }

            case "start":
            {
                if (store.GetMuRummyGame(charName) is { } active)
                {
                    await this.SendRummyStatusAsync(player, active).ConfigureAwait(false);
                    return false;
                }

                if (!await S6EventsEngine.TryConsumeStackedItemsAsync(player, 14, 216, 1).ConfigureAwait(false))
                {
                    await this.SendLocalizedBlueAsync(player, "Need 1 Card Deck (14-216) to start Mu Rummy (/rummy combine).", "Cần 1 Bộ bài Rummy (14-216) để bắt đầu (/rummy combine).").ConfigureAwait(false);
                    return false;
                }

                var fresh = MuRummyEngine.CreateNewGame(max => Rand.NextInt(0, max));
                store.SaveMuRummyGame(charName, fresh);
                await this.SendRummyStatusAsync(player, fresh).ConfigureAwait(false);
                return true;
            }

            case "match":
            {
                var game = store.GetMuRummyGame(charName);
                if (game is null)
                {
                    await this.SendLocalizedBlueAsync(player, "No active Mu Rummy game. Type /rummy start.", "Chưa có ván Mu Rummy nào. Gõ /rummy start.").ConfigureAwait(false);
                    return false;
                }

                if (tokens.Length < 5
                    || !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var i0)
                    || !int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var i1)
                    || !int.TryParse(tokens[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var i2))
                {
                    await this.SendLocalizedBlueAsync(player, "Usage: /rummy match <1..5> <1..5> <1..5>", "Cú pháp: /rummy match <1..5> <1..5> <1..5>").ConfigureAwait(false);
                    return false;
                }

                var pts = MuRummyEngine.TryPlayMeld(game, i0 - 1, i1 - 1, i2 - 1);
                if (pts <= 0)
                {
                    await this.SendLocalizedBlueAsync(player, "Selected cards do not form a valid meld.", "3 lá bài đã chọn chưa tạo thành bộ hợp lệ.").ConfigureAwait(false);
                    return false;
                }

                store.SaveMuRummyGame(charName, game);
                await this.SendLocalizedBlueAsync(player, $"Meld matched for +{pts} pts! Total score: {game.Score}.", $"Ăn bộ +{pts} điểm! Tổng điểm: {game.Score}.").ConfigureAwait(false);
                await this.SendRummyStatusAsync(player, game).ConfigureAwait(false);
                return true;
            }

            case "finish":
            {
                var game = store.GetMuRummyGame(charName);
                if (game is null)
                {
                    await this.SendLocalizedBlueAsync(player, "No active Mu Rummy game.", "Chưa có ván Mu Rummy đang chơi.").ConfigureAwait(false);
                    return false;
                }

                var finalScore = game.Score;
                store.SaveMuRummyGame(charName, null);
                var reward = MuRummyEngine.ResolveReward(finalScore, cfg.MuRummyRewardScoreFirst, cfg.MuRummyRewardScoreSecond, Rand.NextDouble());
                await this.DeliverRewardAsync(
                    player.GameContext,
                    player,
                    player.Account?.LoginName ?? string.Empty,
                    charName,
                    reward.Zen,
                    0,
                    CashShopCoinType.WCoinC,
                    reward.ItemGroup,
                    reward.ItemNumber,
                    reward.ItemLevel,
                    reward.ItemCount,
                    reward.ReasonEn,
                    reward.ReasonVi,
                    cfg,
                    $"s6-rummy-fin-{Guid.NewGuid():N}").ConfigureAwait(false);
                await this.SendLocalizedBlueAsync(
                    player,
                    $"Mu Rummy finished (Score {finalScore}) -> Reward: {reward.ReasonEn}!",
                    $"Kết thúc Mu Rummy (Điểm {finalScore}) -> Phần thưởng: {reward.ReasonVi}!").ConfigureAwait(false);
                return true;
            }

            default:
            {
                var game = store.GetMuRummyGame(charName);
                if (game is null)
                {
                    var pieces = S6EventsEngine.CountStackedItems(player.Inventory, 14, 215);
                    await this.SendLocalizedBlueAsync(
                        player,
                        $"Mu Rummy: {pieces}/{cfg.MuRummyRequiredPieces} Card Pieces. Use /rummy combine or /rummy start.",
                        $"Mu Rummy: có {pieces}/{cfg.MuRummyRequiredPieces} Mảnh bài. Dùng /rummy combine hoặc /rummy start.").ConfigureAwait(false);
                    return false;
                }

                await this.SendRummyStatusAsync(player, game).ConfigureAwait(false);
                return false;
            }
        }
    }

    /// <summary>Handles <c>/xmas</c> or Snowman (467) talk for Christmas seasonal gift and buff.</summary>
    /// <param name="player">Player.</param>
    /// <returns>True when gift was claimed.</returns>
    public async ValueTask<bool> HandleXMasCommandAsync(Player player)
    {
        if (this.Configuration is not { Enabled: true, XMasEnabled: true } cfg)
        {
            await this.SendLocalizedBlueAsync(player, "XMas event is disabled on this server.", "Sự kiện Giáng Sinh đang tắt.").ConfigureAwait(false);
            return false;
        }

        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0);
        if (resets < cfg.XMasMinReset)
        {
            await this.SendLocalizedBlueAsync(
                player,
                $"Need at least {cfg.XMasMinReset} Reset(s) for Santa gifts.",
                $"Cần tối thiểu {cfg.XMasMinReset} lần Reset để nhận quà Giáng Sinh.").ConfigureAwait(false);
            return false;
        }

        var charName = player.SelectedCharacter?.Name ?? string.Empty;
        if (!this.GetStore(cfg).TryClaimXMasGift(charName, cfg.XMasGiftCooldownMinutes, DateTime.UtcNow, out _))
        {
            await this.SendLocalizedBlueAsync(
                player,
                $"Santa gift is on cooldown ({cfg.XMasGiftCooldownMinutes} min).",
                $"Quà Giáng Sinh đang hồi ({cfg.XMasGiftCooldownMinutes} phút).").ConfigureAwait(false);
            return false;
        }

        await this.ApplySantaBuffAsync(player, cfg, 474).ConfigureAwait(false);
        var gift = XMasSantaEngine.PickSnowmanGift(Rand.NextDouble());
        await this.DeliverRewardAsync(
            player.GameContext,
            player,
            player.Account?.LoginName ?? string.Empty,
            charName,
            gift.Zen,
            0,
            CashShopCoinType.WCoinC,
            gift.ItemGroup,
            gift.ItemNumber,
            gift.ItemLevel,
            gift.ItemCount,
            gift.ReasonEn,
            gift.ReasonVi,
            cfg,
            $"s6-xmas-{Guid.NewGuid():N}").ConfigureAwait(false);
        await this.SendLocalizedBlueAsync(
            player,
            $"Snowman Gift claimed -> {gift.ReasonEn}!",
            $"Đã nhận Quà Người Tuyết -> {gift.ReasonVi}!").ConfigureAwait(false);
        return true;
    }

    /// <summary>Applies timed buff from Little Santa NPC (468..475).</summary>
    /// <param name="player">Player.</param>
    /// <param name="cfg">Active configuration.</param>
    /// <param name="npcNumber">NPC ID (468..475).</param>
    /// <returns>Task.</returns>
    internal async ValueTask ApplySantaBuffAsync(Player player, S6EventsConfiguration cfg, short npcNumber)
    {
        var kind = (npcNumber % 3) switch
        {
            0 => XMasBuffKind.SantaHealing,
            1 => XMasBuffKind.SantaProtection,
            _ => XMasBuffKind.BlessingOfChristmas,
        };
        var level = (int)(player.Attributes?[Stats.Level] ?? 1);
        var duration = TimeSpan.FromSeconds(Math.Clamp(cfg.XMasBuffDurationSeconds, 60, 86400));
        var def = player.GameContext.Configuration.MagicEffects?.FirstOrDefault(e => e.Number == (byte)kind)
            ?? new MagicEffectDefinition { Number = (byte)kind, Name = kind.ToString(), StopByDeath = true, SendDuration = true };
        var powerUps = XMasSantaEngine.BuildBuffPowerUps(kind, level);
        await player.MagicEffectList.AddEffectAsync(new MagicEffect(duration, def, powerUps)).ConfigureAwait(false);

        var (en, vi) = kind switch
        {
            XMasBuffKind.SantaHealing => ("Santa Healing (+500 Max Life) applied!", "Đã nhận Chúc Phúc Hồi Sinh (+500 Sinh Lực tối đa)!"),
            XMasBuffKind.SantaProtection => ("Santa Protection (+500 Max Mana) applied!", "Đã nhận Bảo Hộ Giáng Sinh (+500 Mana tối đa)!"),
            _ => ("Blessing of Christmas (+Damage & Defense) applied!", "Đã nhận Phép Lành Giáng Sinh (+Sát thương & Phòng thủ)!"),
        };
        await this.SendLocalizedBlueAsync(player, en, vi).ConfigureAwait(false);
    }

    /// <summary>Executes one Mining System attempt using Pandora Pick (0-41).</summary>
    internal async ValueTask<bool> PerformMiningAttemptAsync(Player player, S6EventsConfiguration cfg, MiningTwinkleType twinkle, int stage)
    {
        if (player.Inventory is not { } inventory)
        {
            return false;
        }

        var pick = inventory.Items.FirstOrDefault(i => i.Definition is { Group: 0, Number: 41 });
        if (pick is null)
        {
            await this.SendLocalizedBlueAsync(player, "Equip or carry Pandora Pick (0-41) to mine.", "Cần mang theo Cuốc Pandora (0-41) để đào khoáng.").ConfigureAwait(false);
            return false;
        }

        var dec = MiningSystemEngine.GetDurabilityCost(twinkle);
        if (pick.Durability < dec)
        {
            await this.SendLocalizedBlueAsync(player, $"Pandora Pick needs {dec} durability (has {(int)pick.Durability}). Use /mine repair.", $"Cuốc Pandora cần {dec} độ bền (hiện còn {(int)pick.Durability}). Dùng /mine repair để sửa.").ConfigureAwait(false);
            return false;
        }

        pick.Durability -= dec;
        await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(pick, true)).ConfigureAwait(false);

        var denom = Math.Max(1, cfg.MiningChanceDenominator);
        var rate = MiningSystemEngine.GetStageSuccessRate(cfg, twinkle, stage);
        if (Rand.NextInt(0, denom) >= rate)
        {
            await this.SendLocalizedBlueAsync(player, $"Mining ({twinkle}, Stage {stage}) yielded no jewels.", $"Đào khoáng ({twinkle}, Tầng {stage}) chưa trúng ngọc.").ConfigureAwait(false);
            return false;
        }

        var jackpot = stage == 5 && Rand.NextInt(0, denom) < MiningSystemEngine.GetPandoraJackpotRate(cfg, twinkle);
        var totalBless = MiningSystemEngine.GetRewardBlessTotal(twinkle, stage, jackpot);
        foreach (var reward in MiningSystemEngine.BuildBlessPayoutItems(totalBless))
        {
            await this.DeliverRewardAsync(
                player.GameContext,
                player,
                player.Account?.LoginName ?? string.Empty,
                player.SelectedCharacter?.Name ?? string.Empty,
                0,
                0,
                CashShopCoinType.WCoinC,
                reward.ItemGroup,
                reward.ItemNumber,
                reward.ItemLevel,
                reward.ItemCount,
                reward.ReasonEn,
                reward.ReasonVi,
                cfg,
                $"s6-mine-{Guid.NewGuid():N}").ConfigureAwait(false);
        }

        if (jackpot)
        {
            var winner = player.SelectedCharacter?.Name ?? "Miner";
            await this.BroadcastGoldenAsync(
                player.GameContext,
                $"[Mining] {winner} struck Pandora Jackpot: {totalBless} Jewel of Bless!",
                $"[Đào Khoáng] {winner} trúng Nổ Hũ Pandora: {totalBless} Ngọc Ước Nguyện!").ConfigureAwait(false);
        }
        else
        {
            await this.SendLocalizedBlueAsync(
                player,
                $"Mining succeeded ({twinkle}, Stage {stage}) -> {totalBless} Jewel of Bless!",
                $"Đào khoáng thành công ({twinkle}, Tầng {stage}) -> Nhận {totalBless} Ngọc Ước Nguyện!").ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Starts Loren Deep waves and schedules wave 2/3.</summary>
    internal async ValueTask StartLorenDeepAsync(IGameContext gameContext, S6EventsConfiguration cfg, DateTime nowUtc)
    {
        this._lorenType = LorenDeepEngine.SelectAssaultType(cfg.LorenDeepCommonAssaultWeight, cfg.LorenDeepSpecialRaidWeight, Rand.NextDouble());
        this._lorenWave = 1;
        this._lorenStartedUtc = nowUtc;
        this._lorenDeadlineUtc = nowUtc.AddMinutes(Math.Clamp(cfg.LorenDeepDurationMinutes, 5, 120));
        this._lorenActive = true;

        var typeEn = this._lorenType == LorenDeepEngine.AssaultType.SpecialRaid ? "Special Raid" : "Common Assault";
        var typeVi = this._lorenType == LorenDeepEngine.AssaultType.SpecialRaid ? "Tập Kích Đặc Biệt" : "Tổng Tấn Công";
        await this.BroadcastGoldenAsync(
            gameContext,
            $"Loren Deep ({typeEn}) has begun in Valley of Loren — Wave 1!",
            $"Loren Deep ({typeVi}) đã bắt đầu tại Thung Lũng Loren — Đợt 1!").ConfigureAwait(false);
        await this.SpawnLorenWaveAsync(gameContext, cfg, 1).ConfigureAwait(false);
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
            await MissionCoinGranter.GrantRewardAsync(
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

    private async ValueTask<bool> RepairPandoraPickAsync(Player player, S6EventsConfiguration cfg)
    {
        if (player.Inventory is not { } inventory)
        {
            return false;
        }

        var pick = inventory.Items.FirstOrDefault(i => i.Definition is { Group: 0, Number: 41 });
        if (pick is null)
        {
            await this.SendLocalizedBlueAsync(player, "No Pandora Pick found to repair.", "Không tìm thấy Cuốc Pandora để sửa.").ConfigureAwait(false);
            return false;
        }

        var maxDur = Math.Clamp(cfg.MiningRepairMaxDurability, 1, 255);
        if (pick.Durability >= maxDur)
        {
            await this.SendLocalizedBlueAsync(player, $"Pandora Pick is already at cap durability ({maxDur}).", $"Cuốc Pandora đã đạt độ bền tối đa ({maxDur}).").ConfigureAwait(false);
            return false;
        }

        foreach (var candidate in inventory.Items)
        {
            if (candidate.Definition is null)
            {
                continue;
            }

            var amount = MiningSystemEngine.GetRepairAmount(candidate.Definition.Group, candidate.Definition.Number, candidate.Level);
            if (amount <= 0)
            {
                continue;
            }

            if (!await S6EventsEngine.TryConsumeStackedItemsAsync(player, candidate.Definition.Group, candidate.Definition.Number, 1).ConfigureAwait(false))
            {
                continue;
            }

            pick.Durability = (byte)Math.Min(maxDur, (int)pick.Durability + amount);
            await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(pick, true)).ConfigureAwait(false);
            await this.SendLocalizedBlueAsync(
                player,
                $"Repaired Pandora Pick +{amount} (durability {(int)pick.Durability}/{maxDur}).",
                $"Đã sửa Cuốc Pandora +{amount} (độ bền {(int)pick.Durability}/{maxDur}).").ConfigureAwait(false);
            return true;
        }

        await this.SendLocalizedBlueAsync(player, "Need Jewel of Bless or Bless Bundle in inventory to repair.", "Cần Ngọc Ước Nguyện hoặc Cụm Ngọc trong túi để sửa Cuốc Pandora.").ConfigureAwait(false);
        return false;
    }

    private ValueTask SendRummyStatusAsync(Player player, MuRummyGameState game)
    {
        var cards = string.Join(", ", game.Hand.Select((c, i) => $"[{i + 1}] C{c.Color}-#{c.Number}"));
        return this.SendLocalizedBlueAsync(
            player,
            $"Mu Rummy Score {game.Score} | Deck {game.Deck.Count} | Hand: {cards}",
            $"Mu Rummy Điểm {game.Score} | Bộ {game.Deck.Count} | Bài: {cards}");
    }

    private async ValueTask TickLorenDeepAsync(IGameContext gameContext, S6EventsConfiguration cfg, DateTime nowUtc)
    {
        if (!this._lorenActive)
        {
            var slotKey = S6EventsEngine.ResolveDueScheduleSlot(cfg.LorenDeepDailyTimes, cfg.TimeZone, nowUtc);
            if (slotKey is not null && this.GetStore(cfg).TryClaimLorenScheduleSlot(slotKey))
            {
                await this.StartLorenDeepAsync(gameContext, cfg, nowUtc).ConfigureAwait(false);
            }

            return;
        }

        if (nowUtc >= this._lorenDeadlineUtc)
        {
            await this.EndLorenDeepAsync(gameContext).ConfigureAwait(false);
            return;
        }

        var elapsedMinutes = (nowUtc - this._lorenStartedUtc).TotalMinutes;
        if (this._lorenWave < 2 && elapsedMinutes >= 10)
        {
            this._lorenWave = 2;
            await this.BroadcastGoldenAsync(gameContext, "Loren Deep — Wave 2 monsters arriving!", "Loren Deep — Đợt quái thứ 2 đã xuất hiện!").ConfigureAwait(false);
            await this.SpawnLorenWaveAsync(gameContext, cfg, 2).ConfigureAwait(false);
        }
        else if (this._lorenWave < 3 && elapsedMinutes >= 20)
        {
            this._lorenWave = 3;
            await this.BroadcastGoldenAsync(gameContext, "Loren Deep — Final Wave 3 has arrived!", "Loren Deep — Đợt cuối cùng thứ 3 đã xuất hiện!").ConfigureAwait(false);
            await this.SpawnLorenWaveAsync(gameContext, cfg, 3).ConfigureAwait(false);
        }
    }

    private async ValueTask SpawnLorenWaveAsync(IGameContext gameContext, S6EventsConfiguration cfg, int wave)
    {
        var map = await gameContext.GetMapAsync((ushort)cfg.LorenDeepMapNumber).ConfigureAwait(false);
        if (map is null)
        {
            return;
        }

        foreach (var entry in LorenDeepEngine.GetWaveSpawns(this._lorenType, wave))
        {
            var def = gameContext.Configuration.Monsters.FirstOrDefault(m => m.Number == entry.MonsterNumber);
            if (def is null)
            {
                continue;
            }

            for (var i = 0; i < entry.Count; i++)
            {
                var x = (byte)Rand.NextInt(LorenDeepEngine.SpawnMinX, LorenDeepEngine.SpawnMaxX + 1);
                var y = (byte)Rand.NextInt(LorenDeepEngine.SpawnMinY, LorenDeepEngine.SpawnMaxY + 1);
                var pt = map.Terrain.AIgrid[x, y] == 1 ? new Point(x, y) : map.Terrain.RandomWalkableCoordinate;
                if (pt is null)
                {
                    continue;
                }

                var area = new MonsterSpawnArea
                {
                    GameMap = map.Definition,
                    MonsterDefinition = def,
                    SpawnTrigger = SpawnTrigger.OnceAtEventStart,
                    Quantity = 1,
                    X1 = pt.Value.X,
                    X2 = pt.Value.X,
                    Y1 = pt.Value.Y,
                    Y2 = pt.Value.Y,
                };

                var monster = new Monster(
                    area,
                    def,
                    map,
                    gameContext.DropGenerator,
                    new BasicMonsterIntelligence(),
                    gameContext.PlugInManager,
                    gameContext.PathFinderPool);
                monster.Initialize();
                await map.AddAsync(monster).ConfigureAwait(false);
                monster.OnSpawn();
                this._lorenMobs.TryAdd(monster, 0);
            }
        }
    }

    private async ValueTask EndLorenDeepAsync(IGameContext gameContext)
    {
        this._lorenActive = false;
        this._lorenWave = 0;
        foreach (var mob in this._lorenMobs.Keys.ToList())
        {
            if (this._lorenMobs.TryRemove(mob, out _))
            {
                await mob.CurrentMap.RemoveAsync(mob).ConfigureAwait(false);
                mob.Dispose();
            }
        }

        await this.BroadcastGoldenAsync(
            gameContext,
            "Loren Deep assault has ended.",
            "Sự kiện Quái Xâm Lăng Loren Deep đã kết thúc.").ConfigureAwait(false);
    }
}

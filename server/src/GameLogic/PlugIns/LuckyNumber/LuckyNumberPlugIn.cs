// <copyright file="LuckyNumberPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Daily lucky number plug-in (OFF by default): players pick a number in game (<kbd>L</kbd>) or on the website,
/// a draw runs daily at the configured Sydney wall-clock time, and winners receive coins and jewels.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Daily Lucky Number", Description = "Daily number draw with configurable prizes, odds, website picks, and jewel rewards (OFF by default).")]
[Guid("2B8F7E31-1D94-4C05-B87E-442A691E73D0")]
public sealed class LuckyNumberPlugIn :
    IPeriodicTaskPlugIn,
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<LuckyNumberConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private (LuckyNumberConfiguration Config, LuckyNumberEngine Engine)? _engine;
    private DateTime _lastDrawCheckUtc = DateTime.MinValue;

    /// <inheritdoc />
    public LuckyNumberConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="LuckyNumberPlugIn"/> for a game context, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static LuckyNumberPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<LuckyNumberPlugIn>().FirstOrDefault();

    /// <summary>Lookup helper for item names.</summary>
    /// <param name="configuration">The game configuration.</param>
    /// <returns>Item name resolver.</returns>
    public static Func<short, short, string?> ItemNames(GameConfiguration? configuration) =>
        (group, number) => configuration?.Items?.FirstOrDefault(i => i.Group == group && i.Number == number)?.Name.ValueInNeutralLanguage;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new LuckyNumberConfiguration();

    /// <summary>Gets or creates the engine for the current configuration.</summary>
    /// <returns>The engine.</returns>
    public LuckyNumberEngine GetEngine()
    {
        var cfg = this.Configuration ??= new LuckyNumberConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new LuckyNumberEngine(cfg, LuckyNumberStore.Open(cfg.FileName), enabled: true);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var utcNow = DateTime.UtcNow;
        if (utcNow - this._lastDrawCheckUtc < TimeSpan.FromSeconds(15))
        {
            return;
        }

        this._lastDrawCheckUtc = utcNow;
        var engine = this.GetEngine();
        var outcomes = engine.DrawDueRounds(utcNow);
        if (outcomes.Count == 0)
        {
            return;
        }

        foreach (var outcome in outcomes)
        {
            foreach (var winner in outcome.Winners.Where(w => w.Coins > 0 && !w.CoinsGranted))
            {
                var reference = $"lucky:{outcome.DateKey}:{winner.Number}:{winner.LoginName}";
                var granted = await MissionCoinGranter.GrantRewardAsync(
                    gameContext,
                    new CoinGrant(winner.LoginName, winner.CoinType, winner.Coins, $"Daily lucky number {outcome.DateKey} ({winner.Tier})", reference),
                    "HNMO Lucky Number").ConfigureAwait(false);
                if (granted)
                {
                    engine.MarkCoinsGranted(outcome.DateKey, winner.LoginName, winner.Number);
                }
            }

            await gameContext.ForEachPlayerAsync(async player =>
            {
                var msg = player.Culture.TwoLetterISOLanguageName == "vi"
                    ? $"[Con số may mắn {outcome.DateKey}] Kết quả hôm nay: {outcome.DrawnNumber:00} ({outcome.Winners.Count} người trúng giải)!"
                    : $"[Lucky Number {outcome.DateKey}] Winning number: {outcome.DrawnNumber:00} ({outcome.Winners.Count} winner(s))!";
                await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(msg, MessageType.GoldenCenter)).ConfigureAwait(false);
                if (player.SelectedCharacter is not null)
                {
                    await this.SendStateAsync(player, requested: false).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            await this.SendStateAsync(player, requested: false).ConfigureAwait(false);
        }
    }

    /// <summary>Sends the player's current lucky number state via <c>F5 35</c>.</summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether requested explicitly by the client.</param>
    /// <param name="actionResult">Optional action result code.</param>
    /// <param name="messageEn">Optional English message.</param>
    /// <param name="messageVi">Optional Vietnamese message.</param>
    /// <returns>A task representing the send.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var json = LuckyNumberStateWriter.WriteAccountState(
            this.GetEngine(),
            login,
            DateTime.UtcNow,
            ItemNames(player.GameContext.Configuration),
            actionResult,
            messageEn,
            messageVi);
        await player.InvokeViewPlugInAsync<IHnmoLuckyNumberViewPlugIn>(p => p.ShowLuckyNumberAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Handles an in-game pick request.</summary>
    /// <param name="player">The player.</param>
    /// <param name="number">The picked number.</param>
    /// <returns>A task representing the action.</returns>
    public async ValueTask PickAsync(Player player, int number)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var engine = this.GetEngine();
        var cost = Math.Max(0, engine.Configuration.EntryCostZen);
        if (cost > 0 && player.Money < cost)
        {
            var en = $"Need {cost:N0} Zen to pick a lucky number.";
            var vi = $"Cần {cost:N0} Zen để chọn số may mắn.";
            await this.NotifyAndSendAsync(player, "notEnoughZen", en, vi).ConfigureAwait(false);
            return;
        }

        var outcome = engine.Pick(login, player.SelectedCharacter?.Name ?? login, number, DateTime.UtcNow, "game");
        if (outcome == LuckyNumberPickResult.Success && cost > 0 && !player.TryRemoveMoney(cost))
        {
            // Refund edge case if balance changed concurrently; cost check passed above.
            await this.NotifyAndSendAsync(player, "notEnoughZen", "Not enough Zen.", "Không đủ Zen.").ConfigureAwait(false);
            return;
        }

        var (enMsg, viMsg) = outcome switch
        {
            LuckyNumberPickResult.Success => ($"Picked lucky number {number:00}!", $"Đã chọn số may mắn {number:00}!"),
            LuckyNumberPickResult.OutOfRange => ($"Number must be between {engine.Configuration.MinNumber} and {engine.Configuration.MaxNumber}.", $"Số phải từ {engine.Configuration.MinNumber} đến {engine.Configuration.MaxNumber}."),
            LuckyNumberPickResult.DailyLimitReached => ("You already reached today's pick limit.", "Bạn đã đạt giới hạn chọn số hôm nay."),
            LuckyNumberPickResult.DuplicateNumber => ("You already picked that number for this draw.", "Bạn đã chọn số này cho kỳ quay hôm nay."),
            _ => ("Daily lucky number is currently turned off.", "Tính năng Con số may mắn hiện đang tắt."),
        };

        await this.NotifyAndSendAsync(player, outcome.ToString(), enMsg, viMsg).ConfigureAwait(false);
    }

    /// <summary>Delivers owed lucky-number jewels into the player's inventory.</summary>
    /// <param name="player">The player.</param>
    /// <returns>A task representing the claim.</returns>
    public async ValueTask ClaimOwedAsync(Player player)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var engine = this.GetEngine();
        var owed = engine.PeekOwed(login);
        if (owed.Count == 0)
        {
            await this.NotifyAndSendAsync(player, "noneOwed", "No waiting rewards to claim.", "Không có quà chờ nhận.").ConfigureAwait(false);
            return;
        }

        var claimed = 0;
        var bagFull = false;
        foreach (var entry in owed)
        {
            var giftReward = new GiftCodeOwedReward
            {
                Items =
                [
                    new GiftCodeItem { Group = (byte)entry.Group, Number = (short)entry.Number, Count = Math.Max(1, entry.Count) },
                ],
            };

            if (await GiftCodeRewards.GiveGoodsAsync(player, giftReward).ConfigureAwait(false))
            {
                engine.RemoveOwed(login, entry.Id);
                claimed++;
            }
            else
            {
                bagFull = true;
                break;
            }
        }

        var en = bagFull
            ? $"Claimed {claimed} reward(s); make more room in your bag for the rest."
            : $"Claimed {claimed} lucky number reward(s)!";
        var vi = bagFull
            ? $"Đã nhận {claimed} phần quà; hãy dọn thêm chỗ trong túi đồ."
            : $"Đã nhận {claimed} phần quà Con số may mắn!";

        await this.NotifyAndSendAsync(player, bagFull ? "bagFull" : "claimed", en, vi).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ForceStart()
    {
    }

    private async ValueTask NotifyAndSendAsync(Player player, string code, string en, string vi)
    {
        var notice = player.Culture.TwoLetterISOLanguageName == "vi" ? vi : en;
        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(notice, MessageType.BlueNormal)).ConfigureAwait(false);
        await this.SendStateAsync(player, requested: true, actionResult: code, messageEn: en, messageVi: vi).ConfigureAwait(false);
    }
}

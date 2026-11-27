// <copyright file="GiftCodesPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// HNMO's gift codes: the admin makes codes (or batches of random single-use codes) with a reward (coins, zen, items),
/// a number of uses, once per account or per character, start and end on the Sydney wall clock and level, reset and
/// class limits. Players enter them in the game (<c>/code CODE</c> or the gift code window) or on the website's account
/// page. Coins go to the account once (by the redemption's reference), zen and items to the character's bag, waiting
/// when it's full. Wrong codes are limited per account. The state is kept in a JSON file in the save folder.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GiftCodesPlugIn_Name), Description = nameof(PlugInResources.GiftCodesPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6B2E9F41-3C7D-4A85-B1E0-92D4F57A6C13")]
public class GiftCodesPlugIn : IPeriodicTaskPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<GiftCodesConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(20);
    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();

    private DateTime _nextTickUtc = DateTime.UtcNow + TimeSpan.FromSeconds(10);
    private int _running;
    private (GiftCodesConfiguration Configuration, GiftCodeEngine Engine)? _engine;

    /// <inheritdoc />
    public GiftCodesConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static GiftCodesPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<GiftCodesPlugIn>().FirstOrDefault();

    /// <summary>
    /// Gets the game contexts of this process which run the plug-in (for the website's redemptions).
    /// </summary>
    /// <returns>The contexts.</returns>
    public static IReadOnlyCollection<IGameContext> KnownContexts() => Contexts.Values.Distinct().ToList();

    /// <summary>
    /// The text of a redemption's result for the player (English or Vietnamese).
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="outcome">The outcome.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <param name="waiting">Whether the zen and items wait for room in the bag.</param>
    /// <returns>The text.</returns>
    public static string ResultText(GiftCodeEngine engine, GiftCodeOutcome outcome, bool vietnamese, Func<short, short, string?>? itemName, bool waiting)
    {
        var culture = CultureInfo.GetCultureInfo(vietnamese ? "vi" : "en-AU");
        string When(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), engine.Clock.TimeZone).ToString("ddd d MMM HH:mm", culture);
        var code = outcome.Code;
        switch (outcome.Result)
        {
            case GiftCodeResult.Ok:
                var reward = RewardText(code?.Reward ?? new GiftCodeReward(), vietnamese, itemName);
                var text = vietnamese ? $"Đã nhận gift code {outcome.Entry?.Code}: {reward}." : $"Gift code {outcome.Entry?.Code} accepted: {reward}.";
                return waiting
                    ? text + (vietnamese ? " Túi đồ đã đầy: vật phẩm đang chờ, hãy dọn chỗ trống rồi nhận trong cửa sổ Gift code." : " Your bag is full: the items wait. Make room, then take them in the gift code window.")
                    : text;
            case GiftCodeResult.Invalid:
                return vietnamese ? $"Gift code không đúng (còn {outcome.TriesLeft} lần thử)." : $"That gift code doesn't exist ({outcome.TriesLeft} tries left).";
            case GiftCodeResult.TooManyTries:
                var retry = outcome.RetryAtUtc is { } at ? When(at) : "-";
                return vietnamese ? $"Bạn đã nhập sai quá nhiều lần. Hãy thử lại sau {retry} (giờ Sydney)." : $"Too many wrong codes. Try again after {retry} (Sydney time).";
            case GiftCodeResult.Disabled:
                return vietnamese ? "Gift code này đang tạm khóa." : "This gift code is switched off.";
            case GiftCodeResult.NotStarted:
                var start = code is not null && engine.StartUtc(code) is { } s ? When(s) : "-";
                return vietnamese ? $"Gift code này dùng được từ {start} (giờ Sydney)." : $"This gift code works from {start} (Sydney time).";
            case GiftCodeResult.Ended:
                return vietnamese ? "Gift code này đã hết hạn." : "This gift code has expired.";
            case GiftCodeResult.UsedUp:
                return vietnamese ? "Gift code này đã hết lượt dùng." : "This gift code has been used up.";
            case GiftCodeResult.AlreadyUsedNetwork:
                return vietnamese ? "Gift code này đã được dùng từ mạng này." : "This code was already used from this network.";
            case GiftCodeResult.AlreadyUsedDevice:
                return vietnamese ? "Gift code này đã được dùng trên thiết bị này." : "This code was already used on this device.";
            case GiftCodeResult.NetworkAddressUnknown:
                return vietnamese ? "Chưa xác định được địa chỉ mạng. Hãy kết nối lại rồi thử lại." : "Your network address is unavailable. Reconnect and try again.";
            case GiftCodeResult.AlreadyUsedAccount:
                return vietnamese ? "Tài khoản của bạn đã dùng gift code này rồi." : "Your account already used this gift code.";
            case GiftCodeResult.AlreadyUsedCharacter:
                return vietnamese ? "Nhân vật này đã dùng gift code này rồi." : "This character already used this gift code.";
            case GiftCodeResult.BatchCodeUsed:
                return vietnamese ? "Gift code này đã được dùng." : "This gift code was already used.";
            case GiftCodeResult.LevelTooLow:
                return vietnamese ? $"Gift code này cần cấp {code?.MinLevel}." : $"This gift code needs level {code?.MinLevel}.";
            case GiftCodeResult.LevelTooHigh:
                return vietnamese ? $"Gift code này dành cho cấp {code?.MaxLevel} trở xuống." : $"This gift code is for level {code?.MaxLevel} or lower.";
            case GiftCodeResult.ResetsTooLow:
                return vietnamese ? $"Gift code này cần {code?.MinResets} lần reset." : $"This gift code needs {code?.MinResets} resets.";
            case GiftCodeResult.ResetsTooHigh:
                return vietnamese ? $"Gift code này dành cho nhân vật có tối đa {code?.MaxResets} lần reset." : $"This gift code is for characters with {code?.MaxResets} resets or fewer.";
            default:
                return vietnamese ? "Gift code này không dành cho lớp nhân vật của bạn." : "This gift code is not for your class.";
        }
    }

    /// <summary>
    /// The text of a reward, e.g. "100 Goblin Points + 50,000 zen + Jewel of Bless x2".
    /// </summary>
    /// <param name="reward">The reward.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <param name="itemName">Gives the name of an item.</param>
    /// <returns>The text.</returns>
    public static string RewardText(GiftCodeReward reward, bool vietnamese, Func<short, short, string?>? itemName)
    {
        var parts = new List<string>();
        if (reward.Coins > 0)
        {
            var coin = reward.CoinType switch
            {
                CashShopCoinType.WCoinC => "W Coin (C)",
                CashShopCoinType.WCoinP => "W Coin (P)",
                _ => "Goblin Points",
            };
            parts.Add($"{reward.Coins.ToString("N0", CultureInfo.InvariantCulture)} {coin}");
        }

        if (reward.Zen > 0)
        {
            parts.Add($"{reward.Zen.ToString("N0", CultureInfo.InvariantCulture)} zen");
        }

        foreach (var item in reward.Items.Where(i => i.Count > 0))
        {
            var name = itemName?.Invoke(item.Group, item.Number) ?? $"{item.Group}/{item.Number}";
            parts.Add($"{name}{(item.Level > 0 ? $" +{item.Level}" : string.Empty)} x{item.Count}");
        }

        return parts.Count == 0 ? (vietnamese ? "không có gì" : "nothing") : string.Join(" + ", parts);
    }

    /// <summary>
    /// Gets the name of an item of a game configuration.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <returns>The function.</returns>
    public static Func<short, short, string?> ItemNames(GameConfiguration configuration) =>
        (group, number) => configuration.Items.FirstOrDefault(i => i.Group == group && i.Number == number)?.Name.ValueInNeutralLanguage;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new GiftCodesConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public GiftCodeEngine GetEngine()
    {
        var configuration = this.Configuration ??= new GiftCodesConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var engine = new GiftCodeEngine(configuration, GiftCodeStore.Open(configuration.FileName));
        this._engine = (configuration, engine);
        return engine;
    }

    /// <summary>
    /// Sends the state to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client asked for it.</param>
    /// <param name="outcome">The outcome of a code the player entered.</param>
    /// <param name="eventType">What happened.</param>
    /// <param name="eventResult">The result of a "goods" or "claim" event.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested, GiftCodeOutcome? outcome = null, string? eventType = null, string? eventResult = null)
    {
        if (RedeemerOf(player) is not { } who)
        {
            return;
        }

        var json = GiftCodeStateWriter.WriteState(this.GetEngine(), who.Login, who.CharacterId, outcome, eventType, eventResult, ItemNames(player.GameContext.Configuration));
        await player.InvokeViewPlugInAsync<IHnmoGiftCodesViewPlugIn>(p => p.ShowGiftCodesAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// Enters a code for the player's character.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="text">The code.</param>
    /// <param name="fromWindow">Whether it came from the gift code window (else the chat command, which answers in the chat).</param>
    /// <param name="deviceId">The optional browser device id; legacy clients and chat commands leave it unknown.</param>
    /// <returns>The outcome; <c>null</c>, if the player can't enter codes.</returns>
    public async ValueTask<GiftCodeOutcome?> RedeemAsync(Player player, string text, bool fromWindow, string? deviceId = null)
    {
        if (RedeemerOf(player) is not { } who)
        {
            return null;
        }

        var engine = this.GetEngine();
        var outcome = engine.Redeem(text, who with { NetworkAddress = (player as IHasIpAddress)?.IpAddress, DeviceId = deviceId }, DateTime.UtcNow, "game");
        if (outcome.Result == GiftCodeResult.Ok)
        {
            player.Logger.LogInformation("Gift codes: {Player} used {Code} ({Reference}).", player, outcome.Entry?.Code, outcome.Entry?.Reference);
            if (outcome.Coins is { } coins)
            {
                await this.GiveCoinsAsync(player.GameContext, coins).ConfigureAwait(false);
            }

            await this.DeliverAsync(player).ConfigureAwait(false);
        }
        else if (outcome.Result is GiftCodeResult.TooManyTries)
        {
            player.Logger.LogWarning("Gift codes: {Player} entered too many wrong codes.", player);
        }

        this.Save();
        var waiting = outcome.Owed is { } owed && engine.Owed(who.CharacterId).Any(o => o.Reference == owed.Reference);
        if (!fromWindow)
        {
            await player.ShowBlueMessageAsync(ResultText(engine, outcome, IsVietnamese(player), ItemNames(player.GameContext.Configuration), waiting)).ConfigureAwait(false);
        }

        await this.SendStateAsync(player, fromWindow, outcome, fromWindow ? "redeem" : "command").ConfigureAwait(false);
        return outcome;
    }

    /// <summary>
    /// Gives the waiting zen and items which fit (the window's button).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask ClaimAsync(Player player)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        this.Save();
        await this.SendStateAsync(player, true, null, "claim", left > 0 ? "BagFull" : given > 0 ? "Ok" : "Nothing").ConfigureAwait(false);
    }

    /// <summary>
    /// Gives coins of a redemption and applies them at once when the account is in the game.
    /// </summary>
    /// <param name="gameContext">A game context.</param>
    /// <param name="coins">The coins.</param>
    /// <returns>The result.</returns>
    public async ValueTask<GiftCodeCoinsResult> GiveCoinsAsync(IGameContext gameContext, GiftCodePendingCoins coins)
    {
        var result = await GiftCodeRewards.GrantCoinsAsync(gameContext.PersistenceContextProvider, gameContext.Configuration, coins, gameContext.LoggerFactory.CreateLogger<GiftCodesPlugIn>()).ConfigureAwait(false);
        if (result == GiftCodeCoinsResult.Failed)
        {
            return result;
        }

        this.GetEngine().CoinsGiven(coins.Reference);
        if (result == GiftCodeCoinsResult.Given)
        {
            foreach (var context in Contexts.Values.Append(gameContext).Distinct())
            {
                var player = (await context.GetPlayersAsync().ConfigureAwait(false))
                    .FirstOrDefault(p => p is not OfflinePlayer && string.Equals(p.Account?.LoginName, coins.Login, StringComparison.OrdinalIgnoreCase));
                if (player is not null)
                {
                    await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
                    break;
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        var serverId = (gameContext as IGameServerContext)?.Id ?? 0;
        Contexts[serverId] = gameContext;
        if (now < this._nextTickUtc || Interlocked.Exchange(ref this._running, 1) == 1)
        {
            return;
        }

        try
        {
            this._nextTickUtc = now + TickInterval;
            var engine = this.GetEngine();
            if (serverId == Contexts.Keys.Min())
            {
                foreach (var coins in engine.PendingCoins())
                {
                    await this.GiveCoinsAsync(gameContext, coins).ConfigureAwait(false);
                }
            }

            foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
            {
                try
                {
                    if (RedeemerOf(player) is { } who && player.PlayerState.CurrentState == PlayerState.EnteredWorld && engine.Owed(who.CharacterId).Count > 0)
                    {
                        await this.DeliverAndTellAsync(player, false).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    player.Logger.LogDebug(ex, "Gift codes: the tick of {Player} failed.", player);
                }
            }

            this.Save();
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<GiftCodesPlugIn>().LogError(ex, "Gift codes: the tick failed.");
        }
        finally
        {
            Interlocked.Exchange(ref this._running, 0);
        }
    }

    /// <inheritdoc />
    public void ForceStart() => this._nextTickUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld || previousState != PlayerState.CharacterSelection || RedeemerOf(player) is null)
        {
            return ValueTask.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await this.DeliverAndTellAsync(player).ConfigureAwait(false);
                this.Save();
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Gift codes: the waiting rewards of {Player} weren't given.", player);
            }
        });
        return ValueTask.CompletedTask;
    }

    private static bool IsVietnamese(Player player) => player.Culture.TwoLetterISOLanguageName == "vi";

    private static GiftCodeRedeemer? RedeemerOf(Player player)
    {
        if (player is OfflinePlayer || player.IsTemplatePlayer || player.Account is not { IsBot: false, LoginName: { Length: > 0 } login }
            || player.SelectedCharacter is not { } character || character.Id == Guid.Empty)
        {
            return null;
        }

        var resets = player.Attributes is { } attributes ? (int)attributes[Stats.Resets] : 0;
        return new GiftCodeRedeemer(login, character.Id.ToString(), character.Name, player.Level, resets, character.CharacterClass?.Number ?? 0);
    }

    /// <summary>
    /// Gives the waiting zen and items of a player when they fit.
    /// </summary>
    private async ValueTask<(int Given, int Left)> DeliverAsync(Player player)
    {
        if (RedeemerOf(player) is not { } who)
        {
            return (0, 0);
        }

        var engine = this.GetEngine();
        var keep = new List<GiftCodeOwedReward>();
        var given = 0;
        foreach (var reward in engine.TakeOwed(who.CharacterId))
        {
            try
            {
                if (await GiftCodeRewards.GiveGoodsAsync(player, reward).ConfigureAwait(false))
                {
                    given++;
                    player.Logger.LogInformation("Gift codes: {Player} got the reward {Reference}.", player, reward.Reference);
                }
                else
                {
                    keep.Add(reward);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Gift codes: the reward {Reference} of {Player} wasn't given; it waits.", reward.Reference, player);
                keep.Add(reward);
            }
        }

        engine.ReturnOwed(who.CharacterId, keep);
        return (given, keep.Count);
    }

    private async ValueTask DeliverAndTellAsync(Player player, bool tellWaiting = true)
    {
        var (given, left) = await this.DeliverAsync(player).ConfigureAwait(false);
        if (given > 0)
        {
            await player.ShowBlueMessageAsync(IsVietnamese(player) ? "Đã nhận phần thưởng gift code đang chờ." : "The waiting gift code rewards are in your bag.").ConfigureAwait(false);
        }

        if (given > 0 || (left > 0 && tellWaiting))
        {
            await this.SendStateAsync(player, false, null, "goods", left > 0 ? "BagFull" : "Ok").ConfigureAwait(false);
        }
    }

    private void Save()
    {
        var store = this.GetEngine().Store;
        lock (store.Lock)
        {
            store.SaveIfChanged();
        }
    }
}

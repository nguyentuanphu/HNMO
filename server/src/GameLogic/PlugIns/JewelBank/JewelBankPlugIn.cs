// <copyright file="JewelBankPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.Runtime.InteropServices;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Jewel Bank + in-game Master Skill Reset plug-in (OFF by default, hotkey <kbd>F7</kbd>).
/// Deposits single or all loose jewels into account-wide integer counters and withdraws them cleanly to the bag,
/// with optional Master Skill clearing integrated with <see cref="ResetApplier"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Jewel Bank", Description = "Deposit and withdraw Bless, Soul, Life, Creation, Chaos, and custom jewels per account, plus Master Skill clearing (OFF by default).")]
[Guid("B918E2F4-40A9-4D17-9F1C-8A0E5B4C2391")]
public sealed class JewelBankPlugIn :
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<JewelBankConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>Serializes the deposits and withdrawals of one account, so one can't be finished as left by a stop while it runs.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> OperationLocks = new(StringComparer.OrdinalIgnoreCase);

    private (JewelBankConfiguration Config, JewelBankEngine Engine)? _engine;

    /// <inheritdoc />
    public JewelBankConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="JewelBankPlugIn"/> for a game context.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in or <c>null</c>.</returns>
    public static JewelBankPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<JewelBankPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new JewelBankConfiguration();

    /// <summary>Gets or creates the engine for current configuration.</summary>
    /// <returns>The engine.</returns>
    public JewelBankEngine GetEngine()
    {
        var cfg = this.Configuration ??= new JewelBankConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new JewelBankEngine(cfg, JewelBankStore.Open(cfg.FileName), enabled: true);
        this._engine = (cfg, engine);
        return engine;
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && player.SelectedCharacter is not null)
        {
            return this.ClaimPendingAndSendAsync(player, requested: false);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Claims any pending website withdrawals if room exists and pushes updated Jewel Bank JSON state.</summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether opened by user hotkey/button.</param>
    /// <param name="actionResult">Optional action result code.</param>
    /// <param name="messageEn">Optional English notice.</param>
    /// <param name="messageVi">Optional Vietnamese notice.</param>
    /// <returns>A task representing delivery &amp; send.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login) || player.SelectedCharacter is null)
        {
            return;
        }

        var engine = this.GetEngine();
        var bagJewels = CollectBagJewels(player, engine);
        var masterFree = player.SelectedCharacter.MasterLevelUpPoints;
        var masterSpent = ResetApplier.CountSpentMasterPoints(player.SelectedCharacter);
        var json = JewelBankStateWriter.WriteAccountState(engine, login, masterFree, masterSpent, bagJewels, actionResult, messageEn, messageVi);
        await player.InvokeViewPlugInAsync<IHnmoJewelBankViewPlugIn>(p => p.ShowJewelBankAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Deposits either one bag slot (<paramref name="slot"/> &gt;= 12) or all matching jewels when <paramref name="depositAll"/> is true.</summary>
    /// <param name="player">The player.</param>
    /// <param name="slot">Inventory slot (ignored when <paramref name="depositAll"/>).</param>
    /// <param name="depositAll">Whether to sweep all unlocked bag jewels.</param>
    /// <returns>A task representing deposit.</returns>
    public async ValueTask DepositAsync(Player player, byte slot, bool depositAll)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login) || player.Inventory is null)
        {
            return;
        }

        var operationLock = OperationLocks.GetOrAdd(login, static _ => new SemaphoreSlim(1, 1));
        await operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await this.FinishPendingOperationsAsync(player, login).ConfigureAwait(false);
            await this.DepositLockedAsync(player, login, slot, depositAll).ConfigureAwait(false);
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async ValueTask DepositLockedAsync(Player player, string login, byte slot, bool depositAll)
    {
        var engine = this.GetEngine();
        if (!await this.EnsureSafeZoneAsync(player, engine).ConfigureAwait(false))
        {
            return;
        }

        var fee = Math.Max(0, engine.Configuration.DepositZenFee);
        if (fee > 0 && player.Money < fee)
        {
            await this.NotifyAsync(player, "NotEnoughZen", $"Need {fee:N0} Zen deposit fee.", $"Cần {fee:N0} Zen phí gửi ngọc.").ConfigureAwait(false);
            return;
        }

        var targets = new List<(Item Item, JewelBankEntryConfig Cfg)>();
        var safety = AccountSafetyPlugIn.Get(player.GameContext);
        if (depositAll)
        {
            foreach (var item in player.Inventory!.Items.ToList())
            {
                if (item.ItemSlot < InventoryConstants.EquippableSlotsCount
                    || item.ItemSlot >= InventoryConstants.FirstStoreItemSlotIndex
                    || item.Definition is null)
                {
                    continue;
                }

                if (engine.FindByItem(item.Definition.Group, item.Definition.Number) is not { } cfg)
                {
                    continue;
                }

                if (safety is not null && await safety.RefuseLockedItemAsync(player, item).ConfigureAwait(false))
                {
                    continue;
                }

                targets.Add((item, cfg));
            }
        }
        else
        {
            var item = player.Inventory!.GetItem(slot);
            if (item?.Definition is null
                || slot < InventoryConstants.EquippableSlotsCount
                || slot >= InventoryConstants.FirstStoreItemSlotIndex
                || engine.FindByItem(item.Definition.Group, item.Definition.Number) is not { } cfg)
            {
                await this.NotifyAsync(player, "InvalidJewel", "Select a supported jewel in your bag.", "Hãy chọn một viên ngọc hợp lệ trong túi đồ.").ConfigureAwait(false);
                return;
            }

            if (safety is not null && await safety.RefuseLockedItemAsync(player, item).ConfigureAwait(false))
            {
                await this.SendStateAsync(player, true).ConfigureAwait(false);
                return;
            }

            targets.Add((item, cfg));
        }

        if (targets.Count == 0)
        {
            await this.NotifyAsync(player, "NoJewels", "No supported jewels found in your bag.", "Không tìm thấy ngọc phù hợp trong túi đồ.").ConfigureAwait(false);
            return;
        }

        if (fee > 0 && !player.TryRemoveMoney(fee))
        {
            await this.NotifyAsync(player, "NotEnoughZen", $"Need {fee:N0} Zen deposit fee.", $"Cần {fee:N0} Zen phí gửi ngọc.").ConfigureAwait(false);
            return;
        }

        // The jewels leave the bag and that is saved first, with a marker of this deposit; the bank is credited
        // afterwards. A stop in between leaves the deposit written in the bank's file, and the next login credits it when
        // the marker was saved (FinishPendingOperationsAsync): no jewel is doubled or lost.
        var planned = new List<(JewelBankEntryConfig Config, int Count)>();
        var balances = engine.GetBalances(login);
        foreach (var (item, cfg) in targets)
        {
            var stackCount = Math.Max(1, (int)item.Durability);
            balances.TryGetValue(cfg.Key, out var currentBalance);
            var availableCapacity = Math.Max(0, Math.Max(1, cfg.MaxCapacity) - currentBalance);
            var toDeposit = Math.Min(stackCount, availableCapacity);
            if (toDeposit <= 0)
            {
                continue;
            }

            balances[cfg.Key] = currentBalance + toDeposit;
            planned.Add((cfg, toDeposit));
            if (toDeposit < stackCount)
            {
                item.Durability = (byte)(stackCount - toDeposit);
                await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(item, false)).ConfigureAwait(false);
                continue;
            }

            var itemSlot = item.ItemSlot;
            await player.Inventory!.RemoveItemAsync(item).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(itemSlot)).ConfigureAwait(false);
        }

        if (planned.Count == 0)
        {
            if (fee > 0)
            {
                player.TryAddMoney(fee);
            }

            await this.NotifyAsync(player, "CapacityExceeded", "Your Jewel Bank capacity for that jewel is full.", "Ngân hàng ngọc đã đạt giới hạn cho loại ngọc này.").ConfigureAwait(false);
            return;
        }

        var jewels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (cfg, count) in planned)
        {
            jewels[cfg.Key] = jewels.GetValueOrDefault(cfg.Key) + count;
        }

        var operation = engine.BeginDeposit(login, jewels, DateTime.UtcNow);
        var marker = CreateMarker(player, login, operation);
        if (!await TrySaveAsync(player, "deposit").ConfigureAwait(false))
        {
            // Nothing is banked when the bag couldn't be saved: the jewels and the fee go back, so the running game
            // matches the saved bag again.
            await RemoveMarkerAsync(player, marker).ConfigureAwait(false);
            engine.DropOperation(operation);
            await GiveJewelsAsync(player, engine, jewels).ConfigureAwait(false);
            if (fee > 0)
            {
                player.TryAddMoney(fee);
            }

            await this.NotifyAsync(player, "SaveFailed", "The deposit couldn't be saved; your jewels are back in your bag. Please try again.", "Không lưu được lần gửi ngọc, ngọc đã trở lại túi đồ. Vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        this.StopAfterBagSaved?.Invoke();
        var refused = engine.CommitDeposit(operation, DateTime.UtcNow) ?? [];
        await RemoveMarkerAsync(player, marker).ConfigureAwait(false);

        // The bank refused some after all (a website deposit filled it meanwhile): those jewels go back into the bag.
        await GiveJewelsAsync(player, engine, refused).ConfigureAwait(false);
        await TrySaveAsync(player, "deposit").ConfigureAwait(false);

        var depositedCount = jewels.Sum(j => j.Value) - refused.Sum(r => r.Value);
        if (depositedCount > 0)
        {
            await this.NotifyAsync(player, "Deposited", $"Deposited {depositedCount} jewel(s) into your Jewel Bank.", $"Đã gửi {depositedCount} viên ngọc vào Ngân hàng ngọc.").ConfigureAwait(false);
        }
        else
        {
            await this.NotifyAsync(player, "CapacityExceeded", "Your Jewel Bank capacity for that jewel is full.", "Ngân hàng ngọc đã đạt giới hạn cho loại ngọc này.").ConfigureAwait(false);
        }
    }

    /// <summary>Withdraws <paramref name="quantity"/> units of <paramref name="jewelKey"/> into the player's bag (or claims pending website withdrawals).</summary>
    /// <param name="player">The player.</param>
    /// <param name="jewelKey">Jewel key.</param>
    /// <param name="quantity">Number of jewels to withdraw.</param>
    /// <returns>A task representing withdrawal.</returns>
    public async ValueTask WithdrawAsync(Player player, string jewelKey, int quantity)
    {
        var login = player.Account?.LoginName;
        if (string.IsNullOrWhiteSpace(login) || player.Inventory is null)
        {
            return;
        }

        var operationLock = OperationLocks.GetOrAdd(login, static _ => new SemaphoreSlim(1, 1));
        await operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await this.FinishPendingOperationsAsync(player, login).ConfigureAwait(false);
            await this.WithdrawLockedAsync(player, login, jewelKey, quantity).ConfigureAwait(false);
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <summary>
    /// Finishes the deposits and withdrawals a stop left half done, by whether their bag save (with its marker) went
    /// through: a saved deposit is credited, an unsaved one dropped (its jewels are still in the saved bag); a saved
    /// withdrawal is kept, an unsaved one credited back (its jewels never reached the saved bag).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="login">The account login name.</param>
    /// <returns>A task.</returns>
    internal async ValueTask FinishPendingOperationsAsync(Player player, string login)
    {
        var engine = this.GetEngine();
        var operations = engine.PeekOperations(login);
        if (operations.Count == 0)
        {
            return;
        }

        var refusedAll = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var operation in operations)
        {
            var marker = await player.PersistenceContext.GetCashShopCoinGrantByReferenceAsync(MarkerReference(login, operation.Id)).ConfigureAwait(false);
            var saved = marker is not null;
            if (operation.Kind == JewelBankPendingOperation.DepositKind)
            {
                if (saved)
                {
                    foreach (var (key, count) in engine.CommitDeposit(operation.Id, DateTime.UtcNow) ?? [])
                    {
                        refusedAll[key] = refusedAll.GetValueOrDefault(key) + count;
                    }
                }
                else
                {
                    engine.DropOperation(operation.Id);
                }
            }
            else
            {
                engine.SettleWithdraw(operation.Id, saved ? int.MaxValue : 0, finish: true, DateTime.UtcNow);
            }

            await RemoveMarkerAsync(player, marker).ConfigureAwait(false);
            player.Logger.LogWarning("Jewel Bank: finished the {Kind} {Operation} of {Login} left by a stop (bag saved: {Saved}).", operation.Kind, operation.Id, login, saved);
        }

        await GiveJewelsAsync(player, engine, refusedAll).ConfigureAwait(false);
        await TrySaveAsync(player, "recovery").ConfigureAwait(false);
    }

    /// <summary>A stop right after the bag of a deposit or withdrawal was saved (tests only): throwing there stops it.</summary>
    internal Action? StopAfterBagSaved { get; set; }

    private static string MarkerReference(string login, string operation) => $"hnmo-jewel-bank:{login}:{operation}";

    // A zero-coin receipt saved with the bag: after a stop, its presence tells that the bag was saved.
    private static CashShopCoinGrant CreateMarker(Player player, string login, string operation)
    {
        var now = DateTime.UtcNow;
        var marker = player.PersistenceContext.CreateNew<CashShopCoinGrant>();
        marker.AccountId = player.Account!.GetId();
        marker.CoinType = CashShopCoinType.WCoinC;
        marker.Amount = 0;
        marker.CreatedAt = now;
        marker.AppliedAt = now;
        marker.Reference = MarkerReference(login, operation);
        marker.Reason = "jewel-bank";
        return marker;
    }

    private static async ValueTask RemoveMarkerAsync(Player player, CashShopCoinGrant? marker)
    {
        if (marker is null)
        {
            return;
        }

        try
        {
            await player.PersistenceContext.DeleteAsync(marker).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A marker left behind only says that a finished operation's bag was saved.
            player.Logger.LogDebug(ex, "Jewel Bank: a marker couldn't be removed.");
        }
    }

    private static async ValueTask<bool> TrySaveAsync(Player player, string what)
    {
        try
        {
            if (await player.SaveProgressAsync().ConfigureAwait(false))
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            player.Logger.LogWarning(ex, "Jewel Bank: the bag of {Player} couldn't be saved ({What}).", player.Name, what);
            return false;
        }

        player.Logger.LogWarning("Jewel Bank: the bag of {Player} couldn't be saved ({What}).", player.Name, what);
        return false;
    }

    private static async ValueTask GiveJewelsAsync(Player player, JewelBankEngine engine, IReadOnlyDictionary<string, int> jewels)
    {
        foreach (var (key, count) in jewels)
        {
            if (count <= 0 || engine.Configuration.Jewels.FirstOrDefault(j => string.Equals(j.Key, key, StringComparison.OrdinalIgnoreCase)) is not { } cfg)
            {
                continue;
            }

            var back = new GiftCodeOwedReward { Items = [new GiftCodeItem { Group = cfg.Group, Number = cfg.Number, Count = count, Durability = 1 }] };
            if (!await GiftCodeRewards.GiveGoodsAsync(player, back).ConfigureAwait(false))
            {
                player.Logger.LogError("Jewel Bank: {Count} x {Jewel} of {Player} could neither be banked nor given back.", count, cfg.Key, player.Name);
            }
        }
    }

    // Takes jewels given by a withdrawal back out of the bag: the items it added and what it added to stacks.
    private static async ValueTask TakeBackAsync(Player player, IReadOnlyDictionary<Item, double> before)
    {
        foreach (var item in player.Inventory!.Items.ToList())
        {
            if (!before.TryGetValue(item, out var durability))
            {
                var itemSlot = item.ItemSlot;
                await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
                await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(itemSlot)).ConfigureAwait(false);
            }
            else if (item.Durability > durability)
            {
                item.Durability = durability;
                await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(item, false)).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask WithdrawLockedAsync(Player player, string login, string jewelKey, int quantity)
    {
        var engine = this.GetEngine();
        if (!await this.EnsureSafeZoneAsync(player, engine).ConfigureAwait(false))
        {
            return;
        }

        if (engine.FindByKey(jewelKey) is not { } cfg)
        {
            await this.NotifyAsync(player, "JewelNotAllowed", "That jewel type is not enabled.", "Loại ngọc này hiện không khả dụng.").ConfigureAwait(false);
            return;
        }

        var count = Math.Clamp(quantity, 1, 50);
        var fee = Math.Max(0, engine.Configuration.WithdrawZenFee);
        if (fee > 0 && player.Money < fee)
        {
            await this.NotifyAsync(player, "NotEnoughZen", $"Need {fee:N0} Zen withdrawal fee.", $"Cần {fee:N0} Zen phí rút ngọc.").ConfigureAwait(false);
            return;
        }

        // The bank is debited and the withdrawal written in one step; the bag with the jewels is saved with a marker of
        // it. A stop before that save credits the bank back at the next login; a stop after it keeps the withdrawal.
        var (withdrawOutcome, operation) = engine.BeginWithdraw(login, cfg.Key, count, DateTime.UtcNow);
        if (withdrawOutcome != JewelBankResult.Ok || operation is null)
        {
            await this.NotifyAsync(player, withdrawOutcome.ToString(), "Insufficient banked jewels.", "Số dư ngọc trong ngân hàng không đủ.").ConfigureAwait(false);
            return;
        }

        var before = player.Inventory!.Items.ToDictionary(i => i, i => i.Durability);
        var given = 0;
        for (var i = 0; i < count; i++)
        {
            var singleReward = new GiftCodeOwedReward
            {
                Items = [new GiftCodeItem { Group = cfg.Group, Number = cfg.Number, Count = 1, Durability = 1 }],
            };
            if (!await GiftCodeRewards.GiveGoodsAsync(player, singleReward).ConfigureAwait(false))
            {
                break;
            }

            given++;
        }

        // What didn't fit goes back into the bank.
        engine.SettleWithdraw(operation, given, finish: given == 0, DateTime.UtcNow);
        if (given == 0)
        {
            await this.NotifyAsync(player, "BagFull", "Make space in your bag before withdrawing jewels.", "Túi đồ đầy! Hãy dọn chỗ trống trước khi rút ngọc.").ConfigureAwait(false);
            return;
        }

        if (fee > 0)
        {
            player.TryRemoveMoney(fee);
        }

        var marker = CreateMarker(player, login, operation);
        if (!await TrySaveAsync(player, "withdrawal").ConfigureAwait(false))
        {
            // Nothing is withdrawn when the bag couldn't be saved: the jewels leave the bag again, the bank gets them
            // back and the fee is returned.
            await RemoveMarkerAsync(player, marker).ConfigureAwait(false);
            await TakeBackAsync(player, before).ConfigureAwait(false);
            engine.SettleWithdraw(operation, 0, finish: true, DateTime.UtcNow);
            if (fee > 0)
            {
                player.TryAddMoney(fee);
            }

            await this.NotifyAsync(player, "SaveFailed", "The withdrawal couldn't be saved; your jewels stay in the bank. Please try again.", "Không lưu được lần rút ngọc, ngọc vẫn ở trong ngân hàng. Vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        this.StopAfterBagSaved?.Invoke();
        engine.SettleWithdraw(operation, given, finish: true, DateTime.UtcNow);
        await RemoveMarkerAsync(player, marker).ConfigureAwait(false);
        await TrySaveAsync(player, "withdrawal").ConfigureAwait(false);
        await this.NotifyAsync(
            player,
            "Withdrawn",
            $"Withdrew {given} × {cfg.NameEn} into your bag.",
            $"Đã rút {given} × {cfg.NameVi} vào túi đồ.").ConfigureAwait(false);
    }

    /// <summary>Clears and refunds all learned master skill points for the active online character.</summary>
    /// <param name="player">Online player.</param>
    /// <param name="fromPanel">Whether invoked from the Jewel Bank modal.</param>
    /// <returns>A task representing the reset.</returns>
    public async ValueTask ClearMasterSkillsAsync(Player player, bool fromPanel = true)
    {
        var login = player.Account?.LoginName;
        var character = player.SelectedCharacter;
        if (string.IsNullOrWhiteSpace(login) || character is null || player.Account is null)
        {
            return;
        }

        var engine = this.GetEngine();
        if (!engine.Configuration.AllowClearMasterSkills)
        {
            await this.SendNoticeAsync(player, fromPanel, "Disabled", "Clearing master skills is disabled by the admin.", "Tính năng tẩy điểm master hiện đang tắt.").ConfigureAwait(false);
            return;
        }

        if (ResetApplier.CountSpentMasterPoints(character) <= 0)
        {
            await this.SendNoticeAsync(player, fromPanel, "NothingToClear", "You have not spent any master skill points.", "Bạn chưa cộng điểm kỹ năng master nào.").ConfigureAwait(false);
            return;
        }

        var zen = Math.Max(0, engine.Configuration.ClearMasterZen);
        if (zen > 0 && player.Money < zen)
        {
            await this.SendNoticeAsync(player, fromPanel, "NotEnoughZen", $"Need {zen:N0} Zen to clear master skills.", $"Cần {zen:N0} Zen để tẩy điểm master.").ConfigureAwait(false);
            return;
        }

        var coins = Math.Max(0, engine.Configuration.ClearMasterCoins);
        var coinType = engine.Configuration.ClearMasterCoinType;
        var priorBalance = 0;
        CashShopCoinGrant? coinReceipt = null;
        if (coins > 0)
        {
            await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
            priorBalance = player.Account.GetCashShopCoins(coinType);
            if (priorBalance < coins)
            {
                await this.SendNoticeAsync(player, fromPanel, "NotEnoughCoins", $"Need {coins:N0} {coinType} to clear master skills.", $"Cần {coins:N0} {coinType} để tẩy điểm master.").ConfigureAwait(false);
                return;
            }

            player.Account.SetCashShopCoins(coinType, priorBalance - coins);
            coinReceipt = CashShopBalanceReceipt.Record(player.PersistenceContext, player.Account, coinType, -coins, CashShopBalanceReason.ClearMaster);
        }

        if (zen > 0)
        {
            player.TryRemoveMoney(zen);
        }

        var refunded = player.SkillList is SkillList concrete
            ? await concrete.ClearMasterSkillsAsync().ConfigureAwait(false)
            : await ResetApplier.ClearMasterSkillsOfflineAsync(character, player.PersistenceContext).ConfigureAwait(false);

        if (!await player.SaveProgressAsync().ConfigureAwait(false))
        {
            if (coins > 0)
            {
                player.Account.SetCashShopCoins(coinType, priorBalance);
                await CashShopBalanceReceipt.RemoveAsync(player.PersistenceContext, coinReceipt).ConfigureAwait(false);
            }

            if (zen > 0)
            {
                player.TryAddMoney(zen);
            }

            await this.SendNoticeAsync(player, fromPanel, "SaveFailed", "Could not save master skill reset; please try again.", "Không lưu được thao tác tẩy điểm master; vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        engine.RecordClearMaster(login, character.Name, refunded, "game", DateTime.UtcNow);
        await this.SendNoticeAsync(
            player,
            fromPanel,
            "ClearedMaster",
            $"Cleared master skills and refunded {refunded} master point(s)!",
            $"Đã tẩy điểm master và hoàn lại {refunded} điểm master!").ConfigureAwait(false);
    }

    private static IReadOnlyList<(byte Slot, string Key, string NameEn, string NameVi)> CollectBagJewels(Player player, JewelBankEngine engine)
    {
        var list = new List<(byte, string, string, string)>();
        if (player.Inventory is null)
        {
            return list;
        }

        foreach (var item in player.Inventory.Items)
        {
            if (item.ItemSlot < InventoryConstants.EquippableSlotsCount
                || item.ItemSlot >= InventoryConstants.FirstStoreItemSlotIndex
                || item.Definition is null)
            {
                continue;
            }

            if (engine.FindByItem(item.Definition.Group, item.Definition.Number) is { } cfg)
            {
                list.Add((item.ItemSlot, cfg.Key, cfg.NameEn, cfg.NameVi));
            }
        }

        return list.OrderBy(x => x.Item1).ToList();
    }

    private async ValueTask ClaimPendingAndSendAsync(Player player, bool requested)
    {
        var login = player.Account?.LoginName;
        if (!string.IsNullOrWhiteSpace(login) && player.Inventory is not null)
        {
            var operationLock = OperationLocks.GetOrAdd(login, static _ => new SemaphoreSlim(1, 1));
            await operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await this.FinishPendingOperationsAsync(player, login).ConfigureAwait(false);
            }
            finally
            {
                operationLock.Release();
            }

            var engine = this.GetEngine();
            var pending = engine.PeekPending(login);
            foreach (var p in pending)
            {
                if (engine.FindByKey(p.JewelKey) is not { } cfg)
                {
                    continue;
                }

                var delivered = 0;
                for (var i = 0; i < p.Quantity; i++)
                {
                    var ok = await GiftCodeRewards.GiveGoodsAsync(player, new GiftCodeOwedReward
                    {
                        Items = [new GiftCodeItem { Group = cfg.Group, Number = cfg.Number, Count = 1, Durability = 1 }],
                    }).ConfigureAwait(false);
                    if (!ok)
                    {
                        break;
                    }

                    delivered++;
                }

                if (delivered > 0)
                {
                    engine.ConsumePending(p.Id, delivered);
                    await player.SaveProgressAsync().ConfigureAwait(false);
                }
            }
        }

        await this.SendStateAsync(player, requested).ConfigureAwait(false);
    }

    private async ValueTask<bool> EnsureSafeZoneAsync(Player player, JewelBankEngine engine)
    {
        if (!engine.Configuration.SafeZoneOnly || player.CurrentMap is null)
        {
            return true;
        }

        if (player.CurrentMap.Terrain.SafezoneMap[player.Position.X, player.Position.Y])
        {
            return true;
        }

        await this.NotifyAsync(player, "NotInSafeZone", "Enter a town safe zone to deposit or withdraw jewels.", "Hãy vào vùng an toàn trong thành để gửi/rút ngọc.").ConfigureAwait(false);
        return false;
    }

    private ValueTask NotifyAsync(Player player, string code, string en, string vi) =>
        this.SendNoticeAsync(player, sendPanel: true, code, en, vi);

    private async ValueTask SendNoticeAsync(Player player, bool sendPanel, string code, string en, string vi)
    {
        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(isVi ? vi : en, MessageType.BlueNormal)).ConfigureAwait(false);
        if (sendPanel)
        {
            await this.SendStateAsync(player, requested: true, actionResult: code, messageEn: en, messageVi: vi).ConfigureAwait(false);
        }
    }
}

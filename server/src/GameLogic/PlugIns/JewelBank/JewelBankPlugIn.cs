// <copyright file="JewelBankPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
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
            foreach (var item in player.Inventory.Items.ToList())
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
            var item = player.Inventory.GetItem(slot);
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

        // The jewels leave the bag and that is saved first; the bank is credited afterwards. A stop in between can't
        // leave a jewel both in the saved bag and in the bank (a doubled deposit); the worst case is a lost deposit.
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
            await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(itemSlot)).ConfigureAwait(false);
        }

        if (planned.Count == 0 && fee > 0)
        {
            player.TryAddMoney(fee);
        }

        // A failed save keeps the change for the next one: the bank is credited all the same, so the running game holds
        // every jewel exactly once.
        try
        {
            if (!await player.SaveProgressAsync().ConfigureAwait(false))
            {
                player.Logger.LogWarning("Jewel Bank: the bag of {Player} couldn't be saved before the deposit; the next save tries again.", player.Name);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            player.Logger.LogWarning(ex, "Jewel Bank: the bag of {Player} couldn't be saved before the deposit; the next save tries again.", player.Name);
        }

        var depositedCount = 0;
        foreach (var (cfg, count) in planned)
        {
            if (engine.Deposit(login, cfg.Key, count, "game", DateTime.UtcNow) == JewelBankResult.Ok)
            {
                depositedCount += count;
                continue;
            }

            // The bank refused it after all (e.g. a website deposit filled it meanwhile): the jewels go back into the bag.
            var back = new GiftCodeOwedReward { Items = [new GiftCodeItem { Group = cfg.Group, Number = cfg.Number, Count = count, Durability = 1 }] };
            if (!await GiftCodeRewards.GiveGoodsAsync(player, back).ConfigureAwait(false))
            {
                player.Logger.LogError("Jewel Bank: {Count} x {Jewel} of {Player} could neither be banked nor given back.", count, cfg.Key, player.Name);
            }

            await player.SaveProgressAsync().ConfigureAwait(false);
        }

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

        var withdrawOutcome = engine.TryWithdraw(login, cfg.Key, count, "game", DateTime.UtcNow, enqueuePending: false);
        if (withdrawOutcome != JewelBankResult.Ok)
        {
            await this.NotifyAsync(player, withdrawOutcome.ToString(), "Insufficient banked jewels.", "Số dư ngọc trong ngân hàng không đủ.").ConfigureAwait(false);
            return;
        }

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

        if (given < count)
        {
            // Return undelivered jewels back into bank atomically.
            engine.Deposit(login, cfg.Key, count - given, "rollback", DateTime.UtcNow);
        }

        if (given == 0)
        {
            await this.NotifyAsync(player, "BagFull", "Make space in your bag before withdrawing jewels.", "Túi đồ đầy! Hãy dọn chỗ trống trước khi rút ngọc.").ConfigureAwait(false);
            return;
        }

        if (fee > 0)
        {
            player.TryRemoveMoney(fee);
        }

        await player.SaveProgressAsync().ConfigureAwait(false);
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

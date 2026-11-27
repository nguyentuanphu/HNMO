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
    /// <summary>The kind of a website withdrawal, whose jewels wait for room in a bag.</summary>
    public const string WebsiteWithdrawKind = "jewel-web-withdraw";

    /// <summary>The accounts (by state file and account id) whose JSON entries were imported in this process.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> ImportedLogins = new(StringComparer.OrdinalIgnoreCase);

    private (JewelBankConfiguration Config, JewelBankEngine Engine)? _engine;

    /// <inheritdoc />
    public JewelBankConfiguration? Configuration { get; set; }

    /// <summary>Gets or sets a hook which runs right before a commit (tests: a stop before it).</summary>
    internal Func<AssetOperationRequest, ValueTask>? BeforeCommit { get; set; }

    /// <summary>Gets or sets a hook which runs right after a commit (tests: a stop after it).</summary>
    internal Func<AssetOperationRequest, ValueTask>? AfterCommit { get; set; }

    /// <summary>Gets the active <see cref="JewelBankPlugIn"/> for a game context.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in or <c>null</c>.</returns>
    public static JewelBankPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<JewelBankPlugIn>().FirstOrDefault();

    /// <summary>A short, stable hash of what an operation does (see <see cref="AssetOperationCommitter.PayloadHash"/>).</summary>
    /// <param name="payload">The payload text.</param>
    /// <returns>The hash.</returns>
    public static string PayloadHash(string payload) => AssetOperationCommitter.PayloadHash(payload);

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
        await this.ImportJsonOnceAsync(player).ConfigureAwait(false);
        var (balances, pending) = await ReadBankAsync(player.PersistenceContext, player.Account!.GetId()).ConfigureAwait(false);
        var json = JewelBankStateWriter.WriteAccountState(engine, balances, pending, masterFree, masterSpent, bagJewels, actionResult, messageEn, messageVi);
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

        // The bag change and the bank change commit together; no other save of this player may write the bag between.
        await player.RunPersistenceExclusiveAsync(() => this.DepositLockedAsync(player, login, slot, depositAll)).ConfigureAwait(false);
    }

    /// <summary>Withdraws <paramref name="quantity"/> units of <paramref name="jewelKey"/> into the player's bag.</summary>
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

        await player.RunPersistenceExclusiveAsync(() => this.WithdrawLockedAsync(player, jewelKey, quantity)).ConfigureAwait(false);
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

    /// <summary>Commits an asset operation of the Jewel Bank (see <see cref="AssetOperationCommitter.CommitAsync"/>).</summary>
    /// <param name="player">The player.</param>
    /// <param name="request">The operation.</param>
    /// <returns>The outcome; <c>null</c> when it couldn't be read.</returns>
    internal ValueTask<AssetCommitStatus?> CommitAsync(Player player, AssetOperationRequest request)
        => AssetOperationCommitter.CommitAsync(player, request, this.BeforeCommit, this.AfterCommit);

    private static async ValueTask<(IReadOnlyDictionary<string, int> Balances, IReadOnlyList<(string Id, string Key, int Quantity)> Pending)> ReadBankAsync(IPlayerContext context, Guid accountId)
    {
        var balances = await context.GetJewelBankBalancesAsync(accountId).ConfigureAwait(false);
        var pending = (await context.GetPendingAssetOperationsAsync(accountId).ConfigureAwait(false))
            .Where(operation => operation.Kind == WebsiteWithdrawKind && operation.PendingKey is not null)
            .Select(operation => (operation.Id.ToString("N"), operation.PendingKey!, operation.PendingCount))
            .ToList();
        return (balances, pending);
    }

    private static async ValueTask GiveJewelsAsync(Player player, JewelBankEngine engine, IEnumerable<KeyValuePair<string, int>> jewels)
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

    // Takes jewels given by a withdrawal or delivery back out of the bag: the items it added and what it added to stacks.
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

    private static async ValueTask<int> GiveSinglesAsync(Player player, JewelBankEntryConfig cfg, int count)
    {
        var given = 0;
        for (var i = 0; i < count; i++)
        {
            var single = new GiftCodeOwedReward { Items = [new GiftCodeItem { Group = cfg.Group, Number = cfg.Number, Count = 1, Durability = 1 }] };
            if (!await GiftCodeRewards.GiveGoodsAsync(player, single).ConfigureAwait(false))
            {
                break;
            }

            given++;
        }

        return given;
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

    /// <summary>
    /// Imports the account's entries of the JSON state file into the database once (each entry has a fixed operation id,
    /// so this is cheap and safe to repeat).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>A task.</returns>
    private async ValueTask ImportJsonOnceAsync(Player player)
    {
        if (player.Account?.LoginName is not { } login)
        {
            return;
        }

        var memo = $"{this.GetEngine().Configuration.FileName}|{player.Account.GetId():N}";
        if (!ImportedLogins.TryAdd(memo, true))
        {
            return;
        }

        try
        {
            var report = await JewelBankDatabaseImport.RunAsync(player.GameContext.PersistenceContextProvider, player.GameContext.Configuration, this.GetEngine().Store, dryRun: false, login, player.Account.GetId()).ConfigureAwait(false);
            if (report.Imported.Count > 0 || report.Conflicts.Count > 0)
            {
                player.Logger.LogInformation("Jewel Bank: imported {Count} JSON entries of {Login} into the database ({Conflicts} conflicts).", report.Imported.Count, login, report.Conflicts.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ImportedLogins.TryRemove(memo, out _);
            player.Logger.LogWarning(ex, "Jewel Bank: the JSON entries of {Login} couldn't be imported yet.", login);
        }
    }

    private bool IsUncertain(Player player) => AssetOperationCommitter.IsUncertain(player.Account!.GetId());

    private ValueTask NotifyUncertainAsync(Player player) =>
        this.NotifyAsync(player, "Checking", "Your last Jewel Bank action is still being checked; please try again in a moment.", "Thao tác Ngân hàng ngọc trước đang được kiểm tra; vui lòng thử lại sau ít phút.");

    private async ValueTask DepositLockedAsync(Player player, string login, byte slot, bool depositAll)
    {
        var engine = this.GetEngine();
        await this.ImportJsonOnceAsync(player).ConfigureAwait(false);
        if (this.IsUncertain(player))
        {
            await this.NotifyUncertainAsync(player).ConfigureAwait(false);
            return;
        }

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

        // The jewels leave the bag, and the bank is credited in the same database transaction as that bag (and the fee):
        // both are written or neither is. A refusal (the bank filled meanwhile) or a failure puts the jewels back.
        var accountId = player.Account!.GetId();
        var planned = new List<(JewelBankEntryConfig Config, int Count)>();
        var balances = new Dictionary<string, int>(await player.PersistenceContext.GetJewelBankBalancesAsync(accountId).ConfigureAwait(false), StringComparer.OrdinalIgnoreCase);
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

        var jewels = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (cfg, count) in planned)
        {
            jewels[cfg.Key] = jewels.GetValueOrDefault(cfg.Key) + count;
        }

        var payload = "deposit|" + string.Join(",", jewels.Select(j => $"{j.Key}:{j.Value}")) + $"|fee:{fee}";
        var request = new AssetOperationRequest(
            Guid.NewGuid(),
            accountId,
            "jewel-deposit",
            PayloadHash(payload),
            "game",
            payload,
            jewels.Select(j => new JewelBankDelta(j.Key, j.Value, Math.Max(1, engine.FindByKey(j.Key)?.MaxCapacity ?? 1))).ToList());
        var status = await this.CommitAsync(player, request).ConfigureAwait(false);
        if (status is null)
        {
            await this.NotifyUncertainAsync(player).ConfigureAwait(false);
            return;
        }

        if (status != AssetCommitStatus.Committed)
        {
            // Nothing was written: the jewels and the fee go back, so the running game matches the database again.
            await GiveJewelsAsync(player, engine, jewels).ConfigureAwait(false);
            if (fee > 0)
            {
                player.TryAddMoney(fee);
            }

            await this.NotifyAsync(player, "CapacityExceeded", "The deposit couldn't be made; your jewels are back in your bag. Please try again.", "Không gửi được ngọc, ngọc đã trở lại túi đồ. Vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        var depositedCount = jewels.Sum(j => j.Value);
        await this.NotifyAsync(player, "Deposited", $"Deposited {depositedCount} jewel(s) into your Jewel Bank.", $"Đã gửi {depositedCount} viên ngọc vào Ngân hàng ngọc.").ConfigureAwait(false);
    }

    private async ValueTask WithdrawLockedAsync(Player player, string jewelKey, int quantity)
    {
        var engine = this.GetEngine();
        await this.ImportJsonOnceAsync(player).ConfigureAwait(false);
        if (!await this.EnsureSafeZoneAsync(player, engine).ConfigureAwait(false))
        {
            return;
        }

        if (this.IsUncertain(player))
        {
            await this.NotifyUncertainAsync(player).ConfigureAwait(false);
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

        var accountId = player.Account!.GetId();
        var balance = (await player.PersistenceContext.GetJewelBankBalancesAsync(accountId).ConfigureAwait(false)).GetValueOrDefault(cfg.Key);
        if (balance < count)
        {
            await this.NotifyAsync(player, nameof(JewelBankResult.InsufficientBalance), "Insufficient banked jewels.", "Số dư ngọc trong ngân hàng không đủ.").ConfigureAwait(false);
            return;
        }

        // The jewels go into the bag, and the bank is debited in the same transaction as that bag; a refusal (the website
        // took some meanwhile) or a failure takes them out again.
        var before = player.Inventory!.Items.ToDictionary(i => i, i => i.Durability);
        var given = await GiveSinglesAsync(player, cfg, count).ConfigureAwait(false);
        if (given == 0)
        {
            await this.NotifyAsync(player, "BagFull", "Make space in your bag before withdrawing jewels.", "Túi đồ đầy! Hãy dọn chỗ trống trước khi rút ngọc.").ConfigureAwait(false);
            return;
        }

        if (fee > 0)
        {
            player.TryRemoveMoney(fee);
        }

        var payload = $"withdraw|{cfg.Key}:{given}|fee:{fee}";
        var request = new AssetOperationRequest(Guid.NewGuid(), accountId, "jewel-withdraw", PayloadHash(payload), "game", payload, [new JewelBankDelta(cfg.Key, -given, int.MaxValue)]);
        var status = await this.CommitAsync(player, request).ConfigureAwait(false);
        if (status is null)
        {
            await this.NotifyUncertainAsync(player).ConfigureAwait(false);
            return;
        }

        if (status != AssetCommitStatus.Committed)
        {
            await TakeBackAsync(player, before).ConfigureAwait(false);
            if (fee > 0)
            {
                player.TryAddMoney(fee);
            }

            await this.NotifyAsync(player, nameof(JewelBankResult.InsufficientBalance), "The withdrawal couldn't be made; your jewels stay in the bank. Please try again.", "Không rút được ngọc, ngọc vẫn ở trong ngân hàng. Vui lòng thử lại.").ConfigureAwait(false);
            return;
        }

        await this.NotifyAsync(
            player,
            "Withdrawn",
            $"Withdrew {given} × {cfg.NameEn} into your bag.",
            $"Đã rút {given} × {cfg.NameVi} vào túi đồ.").ConfigureAwait(false);
    }

    private async ValueTask ClaimPendingAndSendAsync(Player player, bool requested)
    {
        if (player.Account is not null && player.Inventory is not null)
        {
            await player.RunPersistenceExclusiveAsync(() => this.DeliverPendingLockedAsync(player)).ConfigureAwait(false);
        }

        await this.SendStateAsync(player, requested).ConfigureAwait(false);
    }

    // Website withdrawals whose jewels wait: each part delivered into the bag is taken from the waiting count in the same
    // transaction as that bag.
    private async ValueTask DeliverPendingLockedAsync(Player player)
    {
        await this.ImportJsonOnceAsync(player).ConfigureAwait(false);
        if (this.IsUncertain(player))
        {
            return;
        }

        var engine = this.GetEngine();
        var accountId = player.Account!.GetId();
        var pending = await player.PersistenceContext.GetPendingAssetOperationsAsync(accountId).ConfigureAwait(false);
        foreach (var waiting in pending.Where(p => p.Kind == WebsiteWithdrawKind))
        {
            if (waiting.PendingKey is not { } key || engine.FindByKey(key) is not { } cfg)
            {
                continue;
            }

            var before = player.Inventory!.Items.ToDictionary(i => i, i => i.Durability);
            var delivered = await GiveSinglesAsync(player, cfg, waiting.PendingCount).ConfigureAwait(false);
            if (delivered == 0)
            {
                break;
            }

            var payload = $"delivery|{waiting.Id:N}|{key}:{delivered}";
            var request = new AssetOperationRequest(
                Guid.NewGuid(),
                accountId,
                "jewel-delivery",
                PayloadHash(payload),
                "game",
                payload,
                [],
                DeliveredOperationId: waiting.Id,
                DeliveredCount: delivered);
            var status = await this.CommitAsync(player, request).ConfigureAwait(false);
            if (status is null)
            {
                return;
            }

            if (status != AssetCommitStatus.Committed)
            {
                await TakeBackAsync(player, before).ConfigureAwait(false);
            }
        }
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

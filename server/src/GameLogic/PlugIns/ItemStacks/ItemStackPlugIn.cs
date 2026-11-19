// <copyright file="ItemStackPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemStacks;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.Views.Inventory;
using HNMO.GameLogic.Views.Vault;
using HNMO.PlugIns;

/// <summary>
/// HNMO's bigger stacks (owner's choice, 2026-11-20): potions, scrolls, event tickets and keys stack up to 255 in one slot
/// for everybody; jewels, boxes and quest items, and crafting materials after the account unlocked them with coins.
/// In the bag and the vault alike. The kinds, limits and prices are set in the admin panel.
/// </summary>
[PlugIn]
[Display(Name = "Item stacks", Description = "Bigger stacks (up to 255 in one slot): free kinds and kinds an account unlocks with coins.")]
[Guid("C7E43A18-5B92-4D06-8F1A-3E7D9B2C6A54")]
public sealed class ItemStackPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<ItemStackConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default };

    private readonly SemaphoreSlim _purchaseGate = new(1, 1);
    private ItemStackConfiguration? _configuration;

    /// <summary>Initializes a new instance of the <see cref="ItemStackPlugIn"/> class.</summary>
    public ItemStackPlugIn()
    {
        ItemStackRules.Configure(new ItemStackConfiguration());
    }

    /// <inheritdoc />
    public ItemStackConfiguration? Configuration
    {
        get => this._configuration;
        set
        {
            this._configuration = value;
            ItemStackRules.Configure(value ?? new ItemStackConfiguration());
        }
    }

    /// <summary>Gets the active plug-in of a game.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in, or <c>null</c>.</returns>
    public static ItemStackPlugIn? Get(IGameContext? gameContext) => gameContext?.FeaturePlugIns.GetPlugIn<ItemStackPlugIn>();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ItemStackConfiguration();

    /// <summary>
    /// Splits <paramref name="count"/> pieces off a stack into a free slot of the same storage (the bag, or the open vault).
    /// The new item is made new (its own id) with the pieces taken off the stack; nothing changes when there's no room
    /// or the save fails. Refused while trading or with a personal store open.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="inVault">Whether the stack is in the vault (else the bag).</param>
    /// <param name="slot">The stack's slot.</param>
    /// <param name="count">How many pieces to split off (at least 1, fewer than the stack holds).</param>
    /// <returns>Whether it worked, and the message in the player's language.</returns>
    public async ValueTask<(bool Success, string Message)> SplitAsync(Player player, bool inVault, byte slot, int count)
    {
        ArgumentNullException.ThrowIfNull(player);
        var vi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        var state = player.PlayerState.CurrentState;
        if ((state != PlayerState.EnteredWorld && state != PlayerState.NpcDialogOpened)
            || player.TradingPartner is not null
            || player.ShopStorage?.StoreOpen is true)
        {
            return (false, vi ? "Không tách được lúc này (đang giao dịch hoặc mở cửa hàng)." : "You can't split now (trading or a store is open).");
        }

        IStorage? storage = inVault ? player.Vault : player.Inventory;
        if (storage is null || (!inVault && slot <= InventoryConstants.LastEquippableItemSlotIndex))
        {
            return (false, vi ? "Không tìm thấy chồng đồ." : "The stack wasn't found.");
        }

        if (storage.GetItem(slot) is not { Definition: { } definition } stack || !stack.IsStackable() || count < 1 || count >= stack.Durability)
        {
            return (false, vi ? "Số lượng tách không hợp lệ." : "That can't be split off this stack.");
        }

        var part = player.PersistenceContext.CreateNew<Item>();
        part.Definition = definition;
        part.Level = stack.Level;
        part.HasSkill = stack.HasSkill;
        part.Durability = count;
        if (storage.CheckInvSpace(part) is not { } freeSlot)
        {
            await player.PersistenceContext.DeleteAsync(part).ConfigureAwait(false);
            return (false, vi ? "Không còn chỗ trống để tách." : "There's no free slot for the split.");
        }

        part.ItemSlot = freeSlot;
        stack.Durability -= count;
        if (!await storage.AddItemAsync(freeSlot, part).ConfigureAwait(false) || !await player.SaveProgressAsync().ConfigureAwait(false))
        {
            // Back as it was: the stack keeps its pieces and the new item is gone.
            if (storage.GetItem(freeSlot) == part)
            {
                await storage.RemoveItemAsync(part).ConfigureAwait(false);
            }

            await player.PersistenceContext.DeleteAsync(part).ConfigureAwait(false);
            stack.Durability += count;
            return (false, vi ? "Không lưu được, chồng đồ vẫn như cũ." : "The split couldn't be saved; the stack is unchanged.");
        }

        if (inVault)
        {
            await player.InvokeViewPlugInAsync<IShowVaultPlugIn>(p => p.ShowVaultAsync()).ConfigureAwait(false);
        }
        else
        {
            await player.InvokeViewPlugInAsync<IUpdateInventoryListPlugIn>(p => p.UpdateInventoryListAsync()).ConfigureAwait(false);
        }

        return (true, vi ? $"Đã tách {count} món." : $"Split off {count}.");
    }

    /// <summary>Builds the state for the web game: the kinds, which are unlocked, and the result of the last action.</summary>
    /// <param name="player">The player.</param>
    /// <param name="success">The result of the last split or unlock, if any.</param>
    /// <param name="message">Its message, in the player's language.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public string BuildStateJson(Player player, bool? success, string? message)
    {
        var cfg = this._configuration ?? new ItemStackConfiguration();
        var vi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        var login = player.Account?.LoginName ?? string.Empty;
        var state = new
        {
            enabled = cfg.Enabled,
            coinType = cfg.UnlockCoinType.ToString(),
            balance = player.Account?.GetCashShopCoins(cfg.UnlockCoinType) ?? 0,
            kinds = cfg.Kinds.Select(k => new
            {
                key = k.Key,
                name = vi ? k.NameVi : k.NameEn,
                free = k.Free,
                unlocked = k.Free || ItemStackRules.IsUnlocked(login, k.Key),
                limit = Math.Clamp(k.Limit, 1, byte.MaxValue),
                price = k.UnlockPrice,
                items = k.Items,
            }),
            success,
            message,
        };
        return System.Text.Json.JsonSerializer.Serialize(state, JsonOptions);
    }

    /// <summary>Unlocks a kind for the player's account, for its price in coins.</summary>
    /// <param name="player">The player.</param>
    /// <param name="kindKey">The kind key.</param>
    /// <returns>Whether it worked, and the message in the player's language.</returns>
    public async ValueTask<(bool Success, string Message)> UnlockAsync(Player player, string kindKey)
    {
        ArgumentNullException.ThrowIfNull(player);
        var vi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        var cfg = this._configuration ?? new ItemStackConfiguration();
        var kind = cfg.Kinds.FirstOrDefault(k => string.Equals(k.Key, kindKey, StringComparison.OrdinalIgnoreCase));
        if (!cfg.Enabled || kind is null)
        {
            return (false, vi ? "Không có loại chồng đồ này." : "There's no such stack kind.");
        }

        var name = vi ? kind.NameVi : kind.NameEn;
        if (player.Account is not { LoginName: { Length: > 0 } login } account)
        {
            return (false, vi ? "Không tìm thấy tài khoản." : "Account not loaded.");
        }

        if (kind.Free || ItemStackRules.IsUnlocked(login, kind.Key))
        {
            return (true, vi ? $"{name} đã chồng được." : $"{name} already stack.");
        }

        if (kind.UnlockPrice <= 0)
        {
            return (false, vi ? $"{name} không mở khoá bằng xu được." : $"{name} can't be unlocked with coins.");
        }

        await this._purchaseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
            var balance = account.GetCashShopCoins(cfg.UnlockCoinType);
            if (balance < kind.UnlockPrice)
            {
                return (false, vi ? $"Cần {kind.UnlockPrice:N0} {cfg.UnlockCoinType} để mở khoá {name}." : $"Unlocking {name} needs {kind.UnlockPrice:N0} {cfg.UnlockCoinType}.");
            }

            account.SetCashShopCoins(cfg.UnlockCoinType, balance - kind.UnlockPrice);
            CashShopBalanceReceipt.Record(player.PersistenceContext, account, cfg.UnlockCoinType, -kind.UnlockPrice, CashShopBalanceReason.StackUnlock);
            if (!await player.SaveProgressAsync().ConfigureAwait(false))
            {
                // Nothing is bought when the payment couldn't be saved: the coins go back.
                account.SetCashShopCoins(cfg.UnlockCoinType, account.GetCashShopCoins(cfg.UnlockCoinType) + kind.UnlockPrice);
                return (false, vi ? "Không lưu được giao dịch, chưa trừ xu. Vui lòng thử lại." : "The purchase couldn't be saved; no coins were taken. Please try again.");
            }

            ItemStackRules.AddUnlock(login, kind.Key);
            return (true, vi ? $"Đã mở khoá: {name} giờ chồng được tới {kind.Limit} món một ô." : $"Unlocked: {name} now stack up to {kind.Limit} in one slot.");
        }
        finally
        {
            this._purchaseGate.Release();
        }
    }
}

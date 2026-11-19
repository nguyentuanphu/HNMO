// <copyright file="MultiVaultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence;
using HNMO.PlugIns;
using PersistenceIdentifiable = HNMO.Persistence.IIdentifiable;

/// <summary>
/// Manages multiple numbered vaults per account persisted in <c>hnmo.player_feature_state</c>
/// (with default free vaults, VIP bonus vaults, and coin-purchased extra vaults) (ROADMAP 14.5).
/// </summary>
[PlugIn]
[Display(Name = "Multi-vault storage", Description = "Extra numbered vaults per account (/vault N and /vault buy) backed by PostgreSQL rows.")]
[Guid("9D1E4F28-7A3B-4C95-B812-6F0E3C7D9A24")]
public sealed class MultiVaultPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<MultiVaultConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <summary>Row feature key for per-account vault metadata.</summary>
    public const string AccountFeatureKey = "multiVault.account";

    /// <summary>Row feature key for numbered vault item snapshots.</summary>
    public const string StorageFeatureKey = "multiVault.storage";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object _gate = new();

    private readonly SemaphoreSlim _switchGate = new(1, 1);

    /// <inheritdoc />
    public MultiVaultConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="MultiVaultPlugIn"/> from <paramref name="gameContext"/>.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static MultiVaultPlugIn? Get(IGameContext? gameContext) =>
        gameContext?.FeaturePlugIns.GetPlugIn<MultiVaultPlugIn>();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MultiVaultConfiguration();

    /// <summary>Computes total unlocked vaults for <paramref name="player"/> (at least 1).</summary>
    /// <param name="player">The player.</param>
    /// <returns>Unlocked vault count.</returns>
    public int GetUnlockedVaultCount(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled || player.Account is not { LoginName: { Length: > 0 } login })
        {
            return 1;
        }

        var state = this.LoadAccountState(login);
        var isVip = (player.Attributes?[Stats.IsVip] ?? 0) > 0;
        var total = cfg.DefaultVaultCount + (isVip ? cfg.VipExtraVaults : 0) + state.PurchasedExtraVaults;
        return Math.Clamp(total, 1, Math.Max(1, cfg.MaxVaultsPerAccount));
    }

    /// <summary>Gets the currently active vault index (0-based) for <paramref name="login"/>.</summary>
    /// <param name="login">Account login name.</param>
    /// <returns>Active 0-based vault index.</returns>
    public int GetActiveVaultIndex(string login)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        return Math.Max(0, this.LoadAccountState(login).ActiveVaultIndex);
    }

    /// <summary>
    /// Switches <paramref name="player"/>'s active vault to <paramref name="targetIndex"/>; see <see cref="SwitchVaultCoreAsync"/>.
    /// </summary>
    /// <param name="player">The player (must have warehouse closed).</param>
    /// <param name="targetIndex">0-based vault index.</param>
    /// <returns>Outcome tuple `(Success, ReasonKey)`.</returns>
    public async ValueTask<(bool Success, string Message)> SwitchVaultAsync(Player player, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(player);
        var isVi = IsVi(player);
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled)
        {
            return (false, isVi ? "Tính năng nhiều rương chứa đồ đang tắt." : "Multi-vault is currently disabled.");
        }

        if (player.Account is not { LoginName: { Length: > 0 } login })
        {
            return (false, isVi ? "Không tìm thấy tài khoản." : "Account not loaded.");
        }

        if (player.Vault is not null || player.OpenedNpc is not null)
        {
            return (false, isVi ? "Vui lòng đóng cửa sổ rương/NPC trước khi đổi rương." : "Please close your vault/NPC window before switching vaults.");
        }

        var unlocked = this.GetUnlockedVaultCount(player);
        if (targetIndex < 0 || targetIndex >= unlocked)
        {
            return (false, isVi ? $"Rương #{targetIndex} chưa mở khóa (đã mở 0..{unlocked - 1})." : $"Vault #{targetIndex} is locked (unlocked: 0..{unlocked - 1}).");
        }

        await this._switchGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await this.SwitchVaultCoreAsync(player, login, targetIndex, isVi).ConfigureAwait(false);
        }
        finally
        {
            this._switchGate.Release();
        }
    }

    /// <summary>Purchases one permanent extra vault slot for <paramref name="player"/> using configured coins.</summary>
    /// <param name="player">The player.</param>
    /// <returns>Outcome tuple `(Success, Message)`.</returns>
    public async ValueTask<(bool Success, string Message)> BuyExtraVaultAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var isVi = IsVi(player);
        var cfg = this.EffectiveConfig();
        if (!cfg.Enabled)
        {
            return (false, isVi ? "Tính năng nhiều rương chứa đồ đang tắt." : "Multi-vault is currently disabled.");
        }

        if (player.Account is not { LoginName: { Length: > 0 } login })
        {
            return (false, isVi ? "Không tìm thấy tài khoản." : "Account not loaded.");
        }

        if (cfg.PurchaseExtraVaultCost <= 0)
        {
            return (false, isVi ? "Máy chủ không bật mua thêm rương bằng xu." : "Purchasing extra vaults is disabled.");
        }

        var currentUnlocked = this.GetUnlockedVaultCount(player);
        if (currentUnlocked >= cfg.MaxVaultsPerAccount)
        {
            return (false, isVi ? $"Bạn đã đạt giới hạn tối đa ({cfg.MaxVaultsPerAccount} rương)." : $"You already reached the maximum vault limit ({cfg.MaxVaultsPerAccount}).");
        }

        await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
        var balance = player.Account.GetCashShopCoins(cfg.PurchaseCoinType);
        if (balance < cfg.PurchaseExtraVaultCost)
        {
            return (false, isVi ? $"Cần {cfg.PurchaseExtraVaultCost} {cfg.PurchaseCoinType} để mở thêm rương." : $"Need {cfg.PurchaseExtraVaultCost} {cfg.PurchaseCoinType} to unlock an extra vault.");
        }

        player.Account.SetCashShopCoins(cfg.PurchaseCoinType, balance - cfg.PurchaseExtraVaultCost);
        CashShopBalanceReceipt.Record(player.PersistenceContext, player.Account, cfg.PurchaseCoinType, -cfg.PurchaseExtraVaultCost, CashShopBalanceReason.ExtraVault);
        if (!await player.SaveProgressAsync().ConfigureAwait(false))
        {
            // Nothing is bought when the payment couldn't be saved: the coins go back.
            player.Account.SetCashShopCoins(cfg.PurchaseCoinType, player.Account.GetCashShopCoins(cfg.PurchaseCoinType) + cfg.PurchaseExtraVaultCost);
            return (false, isVi ? "Không lưu được giao dịch, chưa trừ xu. Vui lòng thử lại." : "The purchase couldn't be saved; no coins were taken. Please try again.");
        }

        var state = this.LoadAccountState(login);
        state.PurchasedExtraVaults++;
        this.SaveAccountState(login, state);

        var nextCount = this.GetUnlockedVaultCount(player);
        return (true, isVi ? $"Đã mở khóa thêm rương! Tổng số rương hiện có: {nextCount}." : $"Unlocked an extra vault! Total unlocked: {nextCount}.");
    }

    private static bool IsVi(Player player) =>
        player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fills a new storage from a vault saved by the earlier version of this feature, which kept a vault's items as a
    /// list of values. Only vaults which were never opened since are read this way, once.
    /// </summary>
    private static void FillFromLegacySnapshot(Player player, ItemStorage vault, MultiVaultStorageSnapshot snapshot)
    {
        vault.Money = snapshot.Money;
        var config = player.GameContext.Configuration;
        var allOptions = config.ItemOptions.SelectMany(o => o.PossibleOptions).ToList();

        foreach (var itemSnap in snapshot.Items)
        {
            var def = config.Items.FirstOrDefault(d => d.Group == itemSnap.Group && d.Number == itemSnap.Number);
            if (def is null)
            {
                continue;
            }

            var entity = player.PersistenceContext.CreateNew<Item>();
            entity.Definition = def;
            entity.Level = itemSnap.Level;
            entity.Durability = itemSnap.Durability;
            entity.HasSkill = itemSnap.HasSkill;
            entity.SocketCount = itemSnap.SocketCount;
            entity.ItemSlot = itemSnap.Slot;

            foreach (var optSnap in itemSnap.Options)
            {
                var optionDef = allOptions.FirstOrDefault(o => o is PersistenceIdentifiable id && id.Id == optSnap.OptionId);
                if (optionDef is null)
                {
                    continue;
                }

                var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
                link.ItemOption = optionDef;
                link.Level = optSnap.Level;
                link.Index = optSnap.Index;
                entity.ItemOptions.Add(link);
            }

            vault.Items.Add(entity);
        }
    }

    private static Guid IdOf(ItemStorage storage) => storage is PersistenceIdentifiable identifiable ? identifiable.Id : Guid.Empty;

    /// <summary>
    /// Switches vaults. Each vault is an item storage in the database and the account's vault is the one in use, so a
    /// switch is one change (which storage the account's vault is) saved in one step: the items themselves are never
    /// copied, so they keep all their detail, and a failed save leaves the vault as it was. Which vault is in use is
    /// read from the account's storage, not from the state file, so the two can't disagree.
    /// </summary>
    private async ValueTask<(bool Success, string Message)> SwitchVaultCoreAsync(Player player, string login, int targetIndex, bool isVi)
    {
        var account = player.Account!;
        var state = this.LoadAccountState(login);
        var current = account.Vault;
        if (current is null)
        {
            current = player.PersistenceContext.CreateNew<ItemStorage>();
            account.Vault = current;
        }

        var currentId = IdOf(current);
        if (currentId == Guid.Empty)
        {
            return (false, isVi ? "Rương hiện tại chưa được lưu. Vui lòng thử lại sau." : "Your vault isn't saved yet. Please try again later.");
        }

        var currentIndex = state.StorageIds.FirstOrDefault(p => p.Value == currentId) is { Value: var found } pair && found != Guid.Empty
            ? pair.Key
            : Math.Max(0, state.ActiveVaultIndex);
        if (currentIndex == targetIndex)
        {
            return (true, isVi ? $"Bạn đang ở rương #{targetIndex}." : $"You are already on vault #{targetIndex}.");
        }

        ItemStorage target;
        var fromLegacy = false;
        var isNew = false;
        if (state.StorageIds.TryGetValue(targetIndex, out var targetId) && targetId != Guid.Empty)
        {
            if (await player.PersistenceContext.GetByIdAsync<ItemStorage>(targetId).ConfigureAwait(false) is not { } loaded)
            {
                // Never replace a vault which can't be read right now by an empty one.
                return (false, isVi ? $"Không đọc được rương #{targetIndex}. Vui lòng thử lại sau." : $"Vault #{targetIndex} couldn't be read. Please try again later.");
            }

            target = loaded;
        }
        else
        {
            target = player.PersistenceContext.CreateNew<ItemStorage>();
            isNew = true;
            if (this.LoadStorageSnapshot(login, targetIndex) is { } legacy)
            {
                FillFromLegacySnapshot(player, target, legacy);
                fromLegacy = true;
            }
        }

        // Record both storages before the save, so whichever one the account has afterwards is known.
        var previousActive = state.ActiveVaultIndex;
        state.StorageIds[currentIndex] = currentId;
        state.StorageIds[targetIndex] = IdOf(target);
        state.ActiveVaultIndex = targetIndex;
        this.SaveAccountState(login, state);

        account.Vault = target;
        if (!await player.SaveProgressAsync().ConfigureAwait(false))
        {
            account.Vault = current;
            if (isNew)
            {
                // The new storage was never saved: forget it, so the vault is created again next time.
                state.StorageIds.Remove(targetIndex);
                await player.PersistenceContext.DeleteAsync(target).ConfigureAwait(false);
            }

            state.ActiveVaultIndex = previousActive;
            this.SaveAccountState(login, state);
            return (false, isVi ? "Không lưu được, rương vẫn như cũ. Vui lòng thử lại." : "The switch couldn't be saved; your vault is unchanged. Please try again.");
        }

        if (fromLegacy)
        {
            this.RemoveStorageSnapshot(login, targetIndex);
        }

        return (true, isVi ? $"Đã chuyển sang rương #{targetIndex}." : $"Switched to vault #{targetIndex}.");
    }

    private MultiVaultConfiguration EffectiveConfig() => this.Configuration ?? new MultiVaultConfiguration();

    private IHnmoPlayerStateRowBackend GetBackend()
    {
        var cfg = this.EffectiveConfig();
        var key = string.IsNullOrWhiteSpace(cfg.FileName) ? HnmoStateStore.StateFileName : cfg.FileName.Trim();
        var resolved = HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(HnmoStateStore.StateFileName);
        return HnmoPlayerStateRowStoreRegistry.GetBackend(resolved);
    }

    private MultiVaultAccountState LoadAccountState(string login)
    {
        lock (this._gate)
        {
            var backend = this.GetBackend();
            var row = backend.LoadAll(AccountFeatureKey).FirstOrDefault(r => string.Equals(r.ScopeId, login, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                return new MultiVaultAccountState();
            }

            return JsonSerializer.Deserialize<MultiVaultAccountState>(row.PayloadJson, JsonOptions) ?? new MultiVaultAccountState();
        }
    }

    private void SaveAccountState(string login, MultiVaultAccountState state)
    {
        lock (this._gate)
        {
            var backend = this.GetBackend();
            backend.EnsureSchema();
            var row = new HnmoFeatureRow(AccountFeatureKey, login, JsonSerializer.Serialize(state, JsonOptions), DateTime.UtcNow);
            backend.UpsertBatch([row], []);
        }
    }

    private MultiVaultStorageSnapshot? LoadStorageSnapshot(string login, int vaultIndex)
    {
        var key = $"{login}:{vaultIndex}";
        lock (this._gate)
        {
            var backend = this.GetBackend();
            var row = backend.LoadAll(StorageFeatureKey).FirstOrDefault(r => string.Equals(r.ScopeId, key, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                return null;
            }

            return JsonSerializer.Deserialize<MultiVaultStorageSnapshot>(row.PayloadJson, JsonOptions);
        }
    }

    private void RemoveStorageSnapshot(string login, int vaultIndex)
    {
        var key = $"{login}:{vaultIndex}";
        lock (this._gate)
        {
            var backend = this.GetBackend();
            backend.EnsureSchema();
            backend.UpsertBatch([], [(StorageFeatureKey, key)]);
        }
    }
}

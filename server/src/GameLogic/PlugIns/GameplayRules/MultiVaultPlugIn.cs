// <copyright file="MultiVaultPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    /// Switches <paramref name="player"/>'s active vault to <paramref name="targetIndex"/> after persisting the current vault snapshot.
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

        var state = this.LoadAccountState(login);
        if (state.ActiveVaultIndex == targetIndex)
        {
            return (true, isVi ? $"Bạn đang ở rương #{targetIndex}." : $"You are already on vault #{targetIndex}.");
        }

        player.Account.Vault ??= player.PersistenceContext.CreateNew<ItemStorage>();
        var currentSnapshot = SnapshotFromStorage(player.Account.Vault);
        this.SaveStorageSnapshot(login, state.ActiveVaultIndex, currentSnapshot);

        var nextSnapshot = this.LoadStorageSnapshot(login, targetIndex) ?? new MultiVaultStorageSnapshot();
        await ApplySnapshotToStorageAsync(player, player.Account.Vault, nextSnapshot).ConfigureAwait(false);

        state.ActiveVaultIndex = targetIndex;
        this.SaveAccountState(login, state);
        await player.SaveProgressAsync().ConfigureAwait(false);

        return (true, isVi ? $"Đã chuyển sang rương #{targetIndex}." : $"Switched to vault #{targetIndex}.");
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
        var state = this.LoadAccountState(login);
        state.PurchasedExtraVaults++;
        this.SaveAccountState(login, state);
        await player.SaveProgressAsync().ConfigureAwait(false);

        var nextCount = this.GetUnlockedVaultCount(player);
        return (true, isVi ? $"Đã mở khóa thêm rương! Tổng số rương hiện có: {nextCount}." : $"Unlocked an extra vault! Total unlocked: {nextCount}.");
    }

    private static bool IsVi(Player player) =>
        player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

    private static MultiVaultStorageSnapshot SnapshotFromStorage(ItemStorage storage)
    {
        var snap = new MultiVaultStorageSnapshot { Money = storage.Money };
        foreach (var item in storage.Items)
        {
            if (item.Definition is null)
            {
                continue;
            }

            var itemSnap = new MultiVaultItemSnapshot
            {
                Group = item.Definition.Group,
                Number = item.Definition.Number,
                Level = item.Level,
                Durability = item.Durability,
                HasSkill = item.HasSkill,
                SocketCount = item.SocketCount,
                Slot = item.ItemSlot,
            };

            foreach (var link in item.ItemOptions)
            {
                if (link.ItemOption is PersistenceIdentifiable id && id.Id != Guid.Empty)
                {
                    itemSnap.Options.Add(new MultiVaultOptionSnapshot
                    {
                        OptionId = id.Id,
                        Level = link.Level,
                        Index = link.Index,
                    });
                }
            }

            snap.Items.Add(itemSnap);
        }

        return snap;
    }

    private static async ValueTask ApplySnapshotToStorageAsync(Player player, ItemStorage vault, MultiVaultStorageSnapshot snapshot)
    {
        var existing = vault.Items.ToList();
        foreach (var old in existing)
        {
            vault.Items.Remove(old);
            await player.PersistenceContext.DeleteAsync(old).ConfigureAwait(false);
        }

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

    private void SaveStorageSnapshot(string login, int vaultIndex, MultiVaultStorageSnapshot snapshot)
    {
        var key = $"{login}:{vaultIndex}";
        lock (this._gate)
        {
            var backend = this.GetBackend();
            backend.EnsureSchema();
            var row = new HnmoFeatureRow(StorageFeatureKey, key, JsonSerializer.Serialize(snapshot, JsonOptions), DateTime.UtcNow);
            backend.UpsertBatch([row], []);
        }
    }
}

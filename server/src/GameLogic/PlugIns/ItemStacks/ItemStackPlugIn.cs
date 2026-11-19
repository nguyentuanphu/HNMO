// <copyright file="ItemStackPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemStacks;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.GameLogic.CashShop;
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

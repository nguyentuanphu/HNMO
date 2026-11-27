// <copyright file="OfflineTradePlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.OfflineTrade;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlayerShopCurrencies;
using HNMO.PlugIns;

/// <summary>
/// Configurable offline personal shop plug-in (<c>/offtrade</c>, OFF by default).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Offline Trade (/offtrade)", Description = "Lets players leave their personal shop open in town while offline, selling for Zen or coins (OFF by default).")]
[Guid("6C9D4A10-8F3B-4B7E-A2D5-91E40C8A7F12")]
public sealed class OfflineTradePlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<OfflineTradeConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <inheritdoc />
    public OfflineTradeConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active <see cref="OfflineTradePlugIn"/> from <paramref name="gameContext"/>, or <c>null</c> when disabled.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static OfflineTradePlugIn? Get(IGameContext gameContext) =>
        gameContext.FeaturePlugIns.GetPlugIn<OfflineTradePlugIn>();

    /// <summary>
    /// Attempts to validate, charge the optional Zen fee, and transition <paramref name="player"/> into an offline shop session.
    /// </summary>
    /// <param name="player">The player executing <c>/offtrade</c>.</param>
    /// <returns><c>true</c> if transitioned cleanly; otherwise <c>false</c> after sending an EN/VI notice.</returns>
    public static async ValueTask<bool> TryStartAsync(Player player)
    {
        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        var plugIn = Get(player.GameContext);
        if (plugIn is null)
        {
            await player.ShowBlueMessageAsync(isVi
                ? "Tính năng treo cửa hàng offline (/offtrade) hiện chưa được bật."
                : "Offline trade (/offtrade) is currently disabled by the administrator.").ConfigureAwait(false);
            return false;
        }

        var cfg = plugIn.Configuration ?? new OfflineTradeConfiguration();
        var character = player.SelectedCharacter;
        var loginName = player.Account?.LoginName;
        var hasCharacter = character is not null && player.CurrentMap is not null && !string.IsNullOrEmpty(loginName);
        var manager = player.GameContext.OfflinePlayerManager;
        var items = player.ShopStorage?.Items.ToArray() ?? [];
        var usesNonZen = items.Any(i => i.GetStorePrice() is { Kind: not ShopPriceKind.Zen });
        var isSafeZone = player.CurrentMap?.Terrain.SafezoneMap[player.Position.X, player.Position.Y] ?? false;

        var decision = OfflineTradeEngine.EvaluateStart(
            cfg,
            true,
            hasCharacter,
            player.IsAlive,
            player.CurrentMiniGame is not null,
            !string.IsNullOrEmpty(loginName) && manager.IsActive(loginName),
            player.ShopStorage?.StoreOpen ?? false,
            items.Length,
            isSafeZone,
            player.CurrentMap?.Definition.Number ?? 0,
            player.Level,
            player.Money,
            usesNonZen);

        if (!decision.Allowed)
        {
            await player.ShowBlueMessageAsync(FormatDenial(decision.Reason, cfg, decision.ZenFee, isVi)).ConfigureAwait(false);
            return false;
        }

        if (decision.ZenFee > 0 && !player.TryRemoveMoney(decision.ZenFee))
        {
            await player.ShowBlueMessageAsync(FormatDenial(OfflineTradeDenialReason.NotEnoughZen, cfg, decision.ZenFee, isVi)).ConfigureAwait(false);
            return false;
        }

        await player.ShowBlueMessageAsync(isVi
            ? "Đang bật treo cửa hàng offline (/offtrade). Cửa hàng sẽ tiếp tục mở sau khi bạn thoát game."
            : "Starting offline trade (/offtrade). Your personal shop will stay open while you disconnect.").ConfigureAwait(false);

        if (!await manager.StartOffTradeAsync(player, loginName!, decision.MaxDurationHours, decision.StopWhenSoldOut).ConfigureAwait(false))
        {
            if (decision.ZenFee > 0)
            {
                player.TryAddMoney(decision.ZenFee);
            }

            await player.ShowBlueMessageAsync(isVi
                ? "Không thể khởi tạo cửa hàng offline. Vui lòng thử lại."
                : "Could not start offline trade session. Please try again.").ConfigureAwait(false);
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new OfflineTradeConfiguration();

    private static string FormatDenial(OfflineTradeDenialReason reason, OfflineTradeConfiguration cfg, int fee, bool isVi) =>
        reason switch
        {
            OfflineTradeDenialReason.NoCharacterSelected => isVi ? "Chưa chọn nhân vật." : "No character selected.",
            OfflineTradeDenialReason.NotAlive => isVi ? "Nhân vật phải còn sống để dùng /offtrade." : "You must be alive to start offline trade.",
            OfflineTradeDenialReason.InMiniGame => isVi ? "Không thể dùng /offtrade trong phụ bản/sự kiện." : "Cannot start offline trade inside an event or mini-game.",
            OfflineTradeDenialReason.AlreadyOffline => isVi ? "Tài khoản này đang có phiên offline hoạt động." : "An offline session is already active for this account.",
            OfflineTradeDenialReason.StoreNotOpen => isVi ? "Hãy mở Cửa Hàng Cá Nhân và đặt giá vật phẩm trước khi gõ /offtrade." : "Open your Personal Store with priced items before typing /offtrade.",
            OfflineTradeDenialReason.StoreEmpty => isVi ? "Cửa hàng cá nhân của bạn chưa có vật phẩm nào." : "Your personal store has no items listed.",
            OfflineTradeDenialReason.NotInSafeZone => isVi ? "Bạn cần đứng trong vùng an toàn (trong thành) để bật /offtrade." : "You must stand inside a town safe zone to start /offtrade.",
            OfflineTradeDenialReason.MapNotAllowed => isVi ? "Bản đồ này không cho phép treo cửa hàng offline." : "Offline trade is not allowed on this map.",
            OfflineTradeDenialReason.LevelTooLow => isVi ? $"Cần tối thiểu cấp {Math.Max(1, cfg.MinimumLevel)} để dùng /offtrade." : $"You need at least level {Math.Max(1, cfg.MinimumLevel)} to use /offtrade.",
            OfflineTradeDenialReason.CoinCurrenciesNotAllowed => isVi ? "Máy chủ chỉ cho phép /offtrade bán bằng Zen." : "Server settings allow Zen-only shops in offline trade.",
            OfflineTradeDenialReason.NotEnoughZen => isVi ? $"Cần {fee:N0} Zen phí mở cửa hàng offline." : $"You need {fee:N0} Zen to start offline trade.",
            _ => isVi ? "Không thể bật /offtrade." : "Cannot start offline trade.",
        };
}

// <copyright file="AdminNavHints.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;

/// <summary>
/// The short line the sidebar shows under each page's name: a few words that fit the menu's width without being cut.
/// The page's full description stays in the tooltip, the quick search and the page itself.
/// </summary>
public static class AdminNavHints
{
    /// <summary>The longest hint, in characters, that fits the sidebar on one line.</summary>
    public const int MaximumLength = 30;

    private static readonly IReadOnlyDictionary<string, (string En, string Vi)> Hints = new Dictionary<string, (string En, string Vi)>(StringComparer.OrdinalIgnoreCase)
    {
        [""] = ("Players online, health", "Người online, tình trạng"),
        ["servers"] = ("Start, stop, EXP/drop rates", "Bật/tắt, tỉ lệ EXP/rớt đồ"),
        ["worlds"] = ("Worlds, seasons, opening", "Cụm máy chủ, mùa, ngày mở"),
        ["logged-in"] = ("Who is online, kick", "Ai đang online, ngắt"),
        ["leaderboards"] = ("Levels, resets, guilds", "Cấp, reset, bang hội"),
        ["economy-dashboard"] = ("Zen and coin trends", "Xu hướng Zen và coin"),
        ["accounts"] = ("Find, edit, passwords", "Tìm, sửa, mật khẩu"),
        ["guilds"] = ("Guilds and alliances", "Bang hội và liên minh"),
        ["chat-log"] = ("What players said", "Người chơi đã chat gì"),
        ["banned-words"] = ("Words blocked in chat", "Từ bị chặn khi chat"),
        ["account-safety"] = ("Vault PIN, item lock", "Mã kho, khóa đồ"),
        ["duplicate-items"] = ("Frozen duplicate items", "Đồ nhân bản bị đóng băng"),
        ["bug-reports"] = ("Players' bug reports", "Báo lỗi của người chơi"),
        ["audit"] = ("What admins changed", "Quản trị đã đổi gì"),
        ["activity-log"] = ("Trades, coins, drops", "Giao dịch, coin, rớt đồ"),
        ["bans"] = ("Ban accounts, mute chat", "Khóa tài khoản, cấm chat"),
        ["character-tools"] = ("Move, rename, fix stats", "Dịch chuyển, đổi tên, chỉ số"),
        ["gm"] = ("Game Master rights", "Quyền Game Master"),
        ["merchants"] = ("What NPC shops sell", "NPC bán gì"),
        ["cash-shop-catalog"] = ("X shop items and prices", "Gói Cash Shop và giá"),
        ["market"] = ("F6 market rules, listings", "Luật chợ F6, tin đăng"),
        ["escrow-contracts"] = ("Shift+F6 safe trades", "Giao dịch an toàn Shift+F6"),
        ["jewel-bank"] = ("F7 jewel storage, fees", "Kho ngọc F7, phí"),
        ["off-trade"] = ("Shops while offline", "Bán hàng khi offline"),
        ["vip"] = ("VIP tiers and boosts", "Gói VIP và ưu đãi"),
        ["merchants/legacy"] = ("NPC shops, table view", "Cửa hàng NPC dạng bảng"),
        ["castle-siege"] = ("Siege dates and prizes", "Lịch công thành, phần thưởng"),
        ["s6-events"] = ("Blood Castle, Devil Square", "Huyết Lâu, Quảng Trường Quỷ"),
        ["timetable"] = ("Event times (Sydney)", "Giờ sự kiện (Sydney)"),
        ["event-alerts"] = ("Push and Discord alerts", "Thông báo đẩy và Discord"),
        ["world-bosses-rankings"] = ("Boss spawns, weekly prizes", "Boss, phần thưởng tuần"),
        ["top-races"] = ("Daily/weekly contests", "Đua top ngày/tuần"),
        ["arena"] = ("Arena classes and battles", "Lớp & trận Đấu trường"),
        ["server-log"] = ("Live logs, stop HNMO", "Log trực tiếp, tắt HNMO"),
        ["hnmo-update"] = ("Version, update HNMO", "Phiên bản, cập nhật HNMO"),
        ["ultimate-autoplay"] = ("Autoplay: Free or VIP", "Autoplay: Miễn phí hay VIP"),
        ["missions-rewards"] = ("Daily missions, coins", "Nhiệm vụ ngày, coin"),
        ["gift-codes"] = ("Make gift codes", "Tạo mã quà tặng"),
        ["topup-milestones"] = ("Top-up reward tiers", "Mốc thưởng nạp"),
        ["bank-topups"] = ("VietQR transfers to confirm", "Xác nhận chuyển khoản VietQR"),
        ["resets-relife"] = ("Reset and relife rules", "Luật reset và relife"),
        ["onboarding-starter-kit"] = ("New-player guide, kit", "Hướng dẫn tân thủ, quà"),
        ["community"] = ("Community events, Discord", "Sự kiện cộng đồng, Discord"),
        ["guilds-levels-ladder"] = ("Guild levels, prizes", "Cấp bang, phần thưởng"),
        ["content-activation"] = ("Turn maps, classes on/off", "Bật/tắt bản đồ, lớp"),
        ["lucky-number"] = ("Daily lucky draw", "Quay số may mắn mỗi ngày"),
        ["marriage"] = ("Weddings, couple buffs", "Kết hôn, buff vợ chồng"),
        ["catchup"] = ("Daily caps, catch-up EXP", "Giới hạn ngày, EXP đuổi kịp"),
        ["season-plan"] = ("Unlock features over time", "Mở tính năng theo thời gian"),
        ["leftovers"] = ("Trade surplus items", "Đổi đồ dư"),
        ["mounts"] = ("Mount shards, upgrades", "Mảnh thú cưỡi, nâng cấp"),
        ["party-options"] = ("Party rejoin, hunt spots", "Vào lại nhóm, bãi săn"),
        ["gift-box"] = ("Send gifts to players", "Gửi quà cho người chơi"),
        ["news"] = ("News for players", "Tin tức cho người chơi"),
        ["rate-tuning"] = ("Per-map rates, bonuses", "Tỉ lệ theo bản đồ, thưởng"),
        ["gameplay-rules"] = ("PK, party EXP, combat", "PK, EXP nhóm, chiến đấu"),
        ["anti-cheat"] = ("Speed and packet checks", "Kiểm tra tốc độ, gói tin"),
        ["qol-settings"] = ("Auto-play, small comforts", "Tự động đánh, tiện ích"),
        ["buffer-npc"] = ("Free starter buffs", "Buff miễn phí tân thủ"),
        ["teleport-npc"] = ("Warp gates and costs", "Cổng dịch chuyển, phí"),
        ["rankings-settings"] = ("Rankings, quick stats", "Bảng xếp hạng, cộng điểm"),
        ["custom-ranks"] = ("Titles and bonuses", "Danh hiệu và chỉ số"),
        ["hnmo-skills"] = ("Skill behaviour", "Cách kỹ năng hoạt động"),
        ["map-editor"] = ("Monster and NPC spots", "Vị trí quái và NPC"),
        ["terrain-editor"] = ("Safe zones, walls", "Vùng an toàn, vật cản"),
        ["world-editor"] = ("3D map objects", "Vật thể bản đồ 3D"),
        ["item-definitions"] = ("Create and edit items", "Tạo và sửa vật phẩm"),
        ["edit-item-drops"] = ("What monsters drop", "Quái rớt đồ gì"),
        ["fixed-options"] = ("Options on drops", "Dòng chỉ số khi rớt"),
        ["vault-chaos-rewards"] = ("Chaos mix rates, chests", "Tỉ lệ quay đồ, rương"),
        ["talismans"] = ("Talismans, stat scrolls", "Bùa, cuộn chỉ số"),
        ["admin-guide"] = ("How-to for hosts", "Hướng dẫn chủ server"),
        ["setup"] = ("Install the database", "Cài đặt cơ sở dữ liệu"),
        ["config-updates"] = ("Apply data updates", "Áp dụng cập nhật dữ liệu"),
        ["config-captions"] = ("Names in EN and VI", "Tên tiếng Anh và Việt"),
        ["plugins"] = ("Turn features on/off", "Bật/tắt tính năng"),
        ["chat-commands"] = ("All slash commands", "Tất cả lệnh chat"),
        ["users"] = ("Who can use this panel", "Ai được vào trang quản trị"),
        ["api-keys"] = ("Keys for website, tools", "Khóa cho website, công cụ"),
        ["discord"] = ("Connect Discord", "Kết nối Discord"),
        ["server-control"] = ("Services, ports, backups", "Dịch vụ, cổng, sao lưu"),
        ["seasons"] = ("Turn Season 9 & Arena on/off", "Bật/tắt Season 9 & Arena"),
        ["host-settings"] = ("Ports, database, email", "Cổng, cơ sở dữ liệu, email"),
        ["licence"] = ("Licence, trial and renewal", "Giấy phép, dùng thử, gia hạn"),
        ["crash-watch"] = ("Crashes and restarts", "Sự cố và khởi động lại"),
        ["import-server"] = ("Bring in another server", "Nhập dữ liệu server khác"),
        ["maintenance-tools"] = ("Clean-up and fixes", "Dọn dẹp và sửa lỗi"),
        ["state-retention"] = ("Backups and restore", "Sao lưu và khôi phục"),
        ["db-log-retention"] = ("How long logs are kept", "Giữ log bao lâu"),
        ["logfiles"] = ("Read server logs", "Đọc log máy chủ"),
        ["logging-configuration"] = ("How much is logged", "Mức ghi log"),
        ["network-analyzer"] = ("Watch game packets", "Xem gói tin game"),
        ["account/security"] = ("Your two-step login", "Đăng nhập 2 bước của bạn"),
        ["create-game-server"] = ("Add a game server", "Thêm máy chủ game"),
        ["create-connect-server"] = ("Add a connect server", "Thêm máy chủ kết nối"),
        ["email-settings"] = ("Sign-up email (SMTP)", "Email đăng ký (SMTP)"),
    };

    /// <summary>Gets all hints, for tests.</summary>
    public static IReadOnlyDictionary<string, (string En, string Vi)> All => Hints;

    /// <summary>Gets the short sidebar line of <paramref name="entry"/> in the current language, or an empty string.</summary>
    /// <param name="entry">The page.</param>
    /// <returns>The hint.</returns>
    public static string For(AdminNavEntry entry)
    {
        if (!Hints.TryGetValue(entry.Route, out var hint))
        {
            return string.Empty;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? hint.Vi : hint.En;
    }
}

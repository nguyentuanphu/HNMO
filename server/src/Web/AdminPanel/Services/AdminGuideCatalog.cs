// <copyright file="AdminGuideCatalog.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// Built-in bilingual EN/VI Admin Guide and operational recipes migrated out of the player-facing guide.
/// </summary>
public static class AdminGuideCatalog
{
    private static readonly IReadOnlyList<AdminGuideArticle> AllArticles =
    [
        new(
            "recipe-weekend-event",
            "Recipes",
            true,
            "Recipe: Run a Weekend 2x EXP / Bonus Event",
            "Công thức: Chạy sự kiện x2 EXP / Drop cuối tuần",
            "Boost server EXP/Drop/Zen rates temporarily or schedule recurring Sydney-time happy hours.",
            "Tăng hệ số EXP/Drop/Zen theo giờ vàng Sydney hoặc cho cả cuối tuần.",
            "1. Open /rate-tuning to create a Sydney-time Happy Hour rule (e.g. Friday 18:00 to Sunday 23:59 Australia/Sydney) multiplying EXP x2.0 and Zen x1.5.\n2. Or open /servers to raise a single sub-server's EXP, Drop, or Zen multiplier cleanly.\n3. Open /timetable to verify players see the upcoming event in (H) and on the website homepage.",
            "1. Mở /rate-tuning để tạo sự kiện Giờ Vàng theo múi giờ Australia/Sydney (ví dụ Thứ 6 18:00 đến Chủ nhật 23:59) với EXP x2.0 và Zen x1.5.\n2. Hoặc mở /servers để chỉnh riêng tỉ lệ EXP, Drop, Zen cho từng máy chủ con.\n3. Mở /timetable để kiểm tra lịch sự kiện hiển thị trong phím (H) và trang chủ.",
            "rate-tuning",
            "servers",
            "timetable"),
        new(
            "recipe-compensate-players",
            "Recipes",
            true,
            "Recipe: Compensate Players with Coins, Items, or Gift Codes",
            "Công thức: Đền bù người chơi bằng Coin, Vật phẩm hoặc Gift Code",
            "Issue targeted or server-wide compensation batches with optional expiry and bag-safe owed delivery.",
            "Gửi quà đền bù bằng Gift Code hoặc cộng coin trực tiếp cho tài khoản mà không mất đồ khi túi đầy.",
            "1. Open /gift-codes -> New Code (or New Batch for up to 10,000 random single-use codes) and configure W Coins, Zen, or exact items (group, number, level, Skill/Luck/Exc).\n2. Set start and end timestamps in Australia/Sydney time.\n3. To grant cash shop coins directly to a specific account, open /accounts -> Cash shop coins tab.",
            "1. Mở /gift-codes -> Tạo mã mới (hoặc Tạo lô tối đa 10.000 mã dùng 1 lần), cấu hình Coin, Zen hoặc vật phẩm.\n2. Đặt thời gian bắt đầu/kết thúc theo giờ Australia/Sydney.\n3. Để cộng coin trực tiếp cho một tài khoản, mở /accounts -> mục Cash shop coin.",
            "gift-codes",
            "accounts",
            "topup-milestones"),
        new(
            "recipe-items-shops",
            "Recipes",
            true,
            "Recipe: Add Custom Items and Stock Merchant Stores",
            "Công thức: Thêm vật phẩm mới và đưa vào Cửa hàng NPC",
            "Create bilingual items with custom tooltips and glow colors, then place them inside NPC merchant tabs.",
            "Tạo vật phẩm song ngữ có màu phát sáng riêng rồi thêm vào cửa hàng NPC hoặc tỷ lệ rớt quái.",
            "1. Open /item-definitions to add or edit items (EN/VI name, slot size, equip requirements, glow hex color, and custom tooltip notes).\n2. Open /merchants to pick a town NPC (e.g. Hanzo, Lumen, Alex) and place items in its grid.\n3. Open /edit-item-drops or /fixed-options to add the item to monster drop tables.",
            "1. Mở /item-definitions để thêm hoặc sửa vật phẩm (tên EN/VI, kích thước ô, điều kiện mặc, màu glow, ghi chú).\n2. Mở /merchants để chọn NPC bán hàng và xếp vật phẩm vào các ô.\n3. Mở /edit-item-drops hoặc /fixed-options để gắn tỉ lệ rơi từ quái.",
            "item-definitions",
            "merchants",
            "edit-item-drops"),
        new(
            "recipe-moderation",
            "Recipes",
            true,
            "Recipe: Investigate Cheaters, Chat Logs, and Account Safety",
            "Công thức: Kiểm duyệt gian lận, Chat và Bảo mật tài khoản",
            "Review anti-cheat alerts, live chat, profanity filters, and per-player trade/coin histories.",
            "Kiểm tra cảnh báo anti-cheat, lọc từ cấm chat, khóa đồ và lịch sử giao dịch.",
            "1. Open /anti-cheat to check attack speed, movement speed, and packet guard flags.\n2. Open /chat-log and /banned-words to review chat lines and add restricted EN/VI words.\n3. Open /account-safety to inspect IP/trade history, clear lockouts, or reset vault PINs; inspect /activity-log for detailed economic trails.",
            "1. Mở /anti-cheat để kiểm tra ngưỡng tốc độ đánh, di chuyển và gói tin.\n2. Mở /chat-log và /banned-words để theo dõi chat và thêm từ khóa cấm.\n3. Mở /account-safety để xem lịch sử IP/giao dịch hoặc mở khóa PIN rương; xem /activity-log để truy vết giao dịch và coin.",
            "anti-cheat",
            "banned-words",
            "account-safety"),
        new(
            "recipe-backups-import",
            "Recipes",
            true,
            "Recipe: Schedule Backups, Retention & Import Another Server",
            "Công thức: Sao lưu định kỳ, dọn log và Nhập dữ liệu server cũ",
            "Keep safe rolling backups in State Retention and dry-run import external server configs/accounts.",
            "Bật sao lưu tự động GFS và xem trước (dry-run) khi nhập dữ liệu từ gói máy chủ cũ.",
            "1. Open /state-retention and /db-log-retention to confirm GFS backups and PostgreSQL vacuum/archival intervals.\n2. Before major bulk changes, take a backup and check /maintenance-tools dry-run previews.\n3. Open /import-server to point at a local server folder or MSSQL database, preview what maps/shops/accounts convert, and keep a rollback snapshot.",
            "1. Mở /state-retention và /db-log-retention để bật lịch sao lưu GFS và dọn dẹp log CSDL.\n2. Trước khi chỉnh hàng loạt, xem bản xem trước trong /maintenance-tools.\n3. Mở /import-server để chạy thử (dry-run) nhập tỷ lệ, shop, rớt đồ hoặc tài khoản từ thư mục local kèm bản sao lưu hoàn tác.",
            "state-retention",
            "import-server",
            "maintenance-tools"),
        new(
            "host-world-bosses",
            "Host Reference",
            false,
            "Host Reference: World Bosses, Guild Ladder, Top Races & VIP Servers",
            "Tham khảo Host: Boss thế giới, Bang hội, Đua top & Phòng VIP",
            "Operational notes for seasonal rankings, VIP server locks, and weekly reward payouts.",
            "Chi tiết vận hành các bảng xếp hạng tuần, phòng VIP và phần thưởng bang hội.",
            "- /world-bosses-rankings: spawn rules (every N hours or fixed Sydney times), rank rewards (1, 2, 3, 4-10, last hit), weekly coins, and Spawn Now test button.\n- /guilds-levels-ladder: guild XP caps, level buffs, and Monday 00:00 Sydney weekly rewards.\n- /top-races: daily, weekly, monthly, or custom date-range competitions (keep race Id stable while running).\n- /servers: toggle VIP only (Stats.IsVip single source of truth shared with Account VIP) and set per-server EXP, Drop, and Zen multipliers.",
            "- /world-bosses-rankings: hẹn giờ xuất hiện (theo chu kỳ hoặc giờ Sydney), thưởng hạng và nút Spawn Now.\n- /guilds-levels-ladder: giới hạn XP bang hội, buff cấp bang và thưởng thứ Hai hằng tuần.\n- /top-races: đua top theo ngày/tuần/tháng/ngày đặc biệt.\n- /servers: bật/tắt phòng VIP (Stats.IsVip đồng bộ với trang Tài khoản) và chỉnh hệ số EXP/Drop/Zen từng máy chủ.",
            "world-bosses-rankings",
            "guilds-levels-ladder",
            "servers"),
        new(
            "host-community-topup",
            "Host Reference",
            false,
            "Host Reference: Community, Missions, Top-Up VietQR & Resets",
            "Tham khảo Host: Cộng đồng, Nhiệm vụ, Nạp VietQR & Reset/Relife",
            "Configure Zalo/Discord coin approvals, daily earning caps, VietQR banks/webhooks, and reset tiers.",
            "Cấu hình duyệt coin Zalo/Discord, giới hạn kiếm xu mỗi ngày, VietQR và bảng Reset/Relife.",
            "- /resets-relife: tier level/zen/coin rules, VIP discounts, Relife thresholds, and offline /uythac auto-reset limits.\n- /community: Discord bot link coin bonus, manual Zalo claim approve/reject queue, and homepage highlight posts.\n- /missions-rewards: daily/weekly tasks, login calendar, invite rewards, and daily per-account earned coin caps.\n- /bank-topups: VietQR accounts at any bank, players' waiting top-ups (Received/Reject), Casso/SePay keys; /topup-milestones, and cumulative VND rewards.\n- /setup & /item-definitions: edit game name, community links (in Plugins), and bilingual web item catalog feeds.",
            "- /resets-relife: điều kiện cấp/zen/coin từng mốc reset, ưu đãi VIP và /uythac.\n- /community: duyệt yêu cầu thưởng Zalo, liên kết Discord và sự kiện cộng đồng.\n- /missions-rewards: nhiệm vụ ngày/tuần, lịch đăng nhập và trần coin miễn phí mỗi ngày.\n- /host-settings & /topup-milestones: tài khoản nhận VietQR, khóa webhook Casso/SePay và thưởng mốc nạp.",
            "resets-relife",
            "community",
            "topup-milestones"),
    ];

    /// <summary>Gets all built-in guide articles and recipes.</summary>
    public static IReadOnlyList<AdminGuideArticle> Articles => AllArticles;

    /// <summary>Searches guide articles by keyword across EN &amp; VI title, summary, body, and category.</summary>
    /// <param name="query">Search text.</param>
    /// <returns>Matching articles (all when query is blank).</returns>
    public static IReadOnlyList<AdminGuideArticle> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return AllArticles;
        }

        var term = query.Trim();
        return AllArticles
            .Where(a =>
                a.TitleEn.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.TitleVi.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.SummaryEn.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.SummaryVi.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.BodyEn.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.BodyVi.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.Category.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

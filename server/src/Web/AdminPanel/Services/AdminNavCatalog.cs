// <copyright file="AdminNavCatalog.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// Single source of truth for EVERY AdminPanel page route organized by operator task group,
/// ensuring zero duplicates, EN/VI titles, one-line descriptions, icons, Basic/Advanced flags, and related pages.
/// </summary>
public static class AdminNavCatalog
{
    private static readonly IReadOnlyList<AdminNavGroup> GroupList =
    [
        new(
            "overview",
            "oi-dashboard",
            "Overview",
            "Tổng quan",
            [
                new("", "overview", "oi-dashboard", "Dashboard", "Bảng điều khiển", "Live players, setup checklist, and realm health.", "Thống kê trực tiếp, danh sách kiểm tra thiết lập và trạng thái máy chủ.", true, "servers", "worlds", "admin-guide"),
                new("servers", "overview", "oi-project", "Servers & Rates", "Máy chủ & Tỉ lệ", "Start/stop realms, VIP-only locks, and EXP/Drop/Zen rates.", "Bật/tắt máy chủ, khóa phòng VIP và tỉ lệ EXP/Drop/Zen.", true, "", "worlds", "rate-tuning"),
                new("worlds", "overview", "oi-globe", "Worlds", "Thế giới (Multi-World)", "Configure worlds, opening dates, and isolated saves.", "Cấu hình nhiều cụm máy chủ và ngày khai mở.", true, "servers", "setup", "import-server"),
                new("server-log", "overview", "oi-terminal", "Server Log & Stop", "Nhật ký máy chủ & Tắt", "The launcher's and the game server's logs live; stop HNMO.", "Log trực tiếp của chương trình khởi động và máy chủ; tắt HNMO.", true, "", "servers", "logged-in"),
                new("hnmo-update", "overview", "oi-data-transfer-download", "HNMO update", "Cập nhật HNMO", "The running and the newest version; update HNMO, only the changed files.", "Phiên bản đang chạy và bản mới nhất; cập nhật HNMO, chỉ tải file đã đổi.", true, "", "servers", "logged-in"),
                new("logged-in", "overview", "oi-pulse", "Online Accounts", "Tài khoản online", "See who is logged in right now and kick sessions.", "Xem tài khoản đang trực tuyến và ngắt kết nối.", true, "accounts", "chat-log", "activity-log"),
                new("leaderboards", "overview", "oi-badge", "Leaderboards", "Bảng xếp hạng", "View live character levels, resets, and guilds.", "Xem bảng xếp hạng cấp, reset và bang hội.", true, "rankings-settings", "resets-relife", "top-races"),
                new("economy-dashboard", "overview", "oi-graph", "Economy & Alerts", "Kinh tế & Cảnh báo", "Daily Zen, coins, jewels trend charts and anomaly alerts.", "Biểu đồ xu hướng Zen, coin, ngọc và cảnh báo bất thường.", true, "market", "jewel-bank", "activity-log"),
            ]),
        new(
            "players",
            "oi-people",
            "Players & Moderation",
            "Người chơi & Kiểm duyệt",
            [
                new("accounts", "players", "oi-person", "Accounts", "Tài khoản", "Search accounts, edit passwords, states, and characters.", "Tìm tài khoản, đổi mật khẩu, trạng thái và nhân vật.", true, "logged-in", "account-safety", "activity-log"),
                new("guilds", "players", "oi-flag", "Guilds", "Bang hội", "Inspect guilds, alliances, and membership.", "Xem bang hội, liên minh và thành viên.", true, "guilds-levels-ladder", "castle-siege", "leaderboards"),
                new("chat-log", "players", "oi-chat", "Chat Log", "Nhật ký chat", "Live realm chat history with search and filters.", "Lịch sử chat trong game theo thời gian thực.", true, "banned-words", "logged-in", "accounts"),
                new("banned-words", "players", "oi-ban", "Banned Words", "Lọc từ cấm", "EN/VI profanity filter for chat, names, and guilds.", "Lọc từ nhạy cảm EN/VI khi chat và đặt tên.", true, "chat-log", "anti-cheat", "accounts"),
                new("account-safety", "players", "oi-lock-locked", "Account Safety", "Bảo mật tài khoản", "Second password (vault PIN), item lock, and login/trade audit.", "Mật khẩu cấp 2, khóa đồ và lịch sử đăng nhập/giao dịch.", true, "accounts", "market", "activity-log"),
                new("duplicate-items", "players", "oi-warning", "Duplicate Items", "Đồ nhân bản", "Items sharing one serial: frozen until you decide.", "Vật phẩm trùng serial: đóng băng tới khi bạn quyết định.", true, "account-safety", "activity-log", "market"),
                new("bug-reports", "players", "oi-bug", "Bug Reports", "Báo lỗi", "Players' bug reports: answer, reward, send to the developer.", "Báo lỗi của người chơi: trả lời, thưởng, gửi nhà phát triển.", true, "activity-log", "account-safety"),
                new("audit", "players", "oi-clipboard", "Admin Audit", "Nhật ký quản trị", "Track every operator and API action recorded by the panel.", "Theo dõi mọi thao tác quản trị đã thực hiện.", false, "activity-log", "api-keys", "users"),
                new("activity-log", "players", "oi-list", "Activity Log", "Nhật ký hoạt động", "Per-player trades, coins, drops, mixes, and resets timeline.", "Nhật ký giao dịch, coin, rớt đồ, quay đồ và reset.", false, "economy-dashboard", "account-safety", "audit"),
                new("bans", "players", "oi-ban", "Bans & Mutes", "Khóa & Cấm chat", "Ban accounts/characters/IPs, mute chat, and inspect expiry.", "Khóa tài khoản, nhân vật, IP và cấm chat.", true, "accounts", "anti-cheat", "gm"),
                new("character-tools", "players", "oi-person", "Character Tools", "Công cụ nhân vật", "Teleport, restore, rename, and adjust character stats safely.", "Dịch chuyển, khôi phục, đổi tên và chỉnh chỉ số nhân vật.", false, "accounts", "gm", "bans"),
                new("gm", "players", "oi-badge", "GM Manager", "Quản lý GM", "Assign Game Master flags, command scopes, and GM activity.", "Phân quyền Game Master và theo dõi lệnh GM.", false, "accounts", "bans", "chat-commands"),
            ]),
        new(
            "economy",
            "oi-cart",
            "Economy & Shops",
            "Kinh tế & Cửa hàng",
            [
                new("merchants", "economy", "oi-cart", "Merchant Stores", "Cửa hàng NPC", "Edit NPC shop tabs and item stocks cleanly.", "Chỉnh sửa gian hàng và vật phẩm NPC.", true, "item-definitions", "cash-shop-catalog", "edit-item-drops"),
                new("bank-topups", "economy", "oi-dollar", "Bank Top-ups", "Nạp qua ngân hàng", "Your bank accounts for VietQR top-ups; confirm players' transfers.", "Tài khoản ngân hàng nhận nạp VietQR; xác nhận chuyển khoản của người chơi.", true, "topup-milestones", "cash-shop-catalog", "accounts"),
                new("cash-shop-catalog", "economy", "oi-credit-card", "Cash Shop Catalog", "Danh mục Cash Shop", "Manage in-game X shop items, coin prices, and limits.", "Quản lý gói Cash Shop X, giá coin và giới hạn.", true, "topup-milestones", "accounts", "merchants"),
                new("market", "economy", "oi-basket", "Item Market", "Chợ vật phẩm", "Monitor F6 web & NPC item marketplace listings and fees.", "Quản lý chợ vật phẩm F6, thuế và danh sách bán.", true, "escrow-contracts", "economy-dashboard", "account-safety"),
                new("escrow-contracts", "economy", "oi-briefcase", "Escrow Contracts", "Giao dịch trung gian", "Manage Shift+F6 escrow contracts, holds, and dispute resolution.", "Quản lý hợp đồng giao dịch trung gian Shift+F6 và tranh chấp.", true, "market", "account-safety", "activity-log"),
                new("jewel-bank", "economy", "oi-box", "Jewel Bank", "Ngân hàng ngọc", "Configure F7 shared jewel storage, fees, and master skill clears.", "Cấu hình Ngân hàng ngọc F7, phí và tẩy điểm master.", true, "resets-relife", "leftovers", "economy-dashboard"),
                new("off-trade", "economy", "oi-dollar", "Offline Trade", "Bán hàng offline", "Configure /offtrade Zen or coin personal stores while offline.", "Cấu hình cửa hàng cá nhân /offtrade treo máy offline.", false, "market", "qol-settings", "account-safety"),
                new("vip", "economy", "oi-star", "VIP Tiers & Coins", "Gói VIP & Xu", "Configure VIP Bronze/Silver/Gold tiers, EXP/drop boosts, and coins.", "Cấu hình gói VIP Đồng/Bạc/Vàng, hệ số EXP/rớt đồ và xu.", true, "servers", "cash-shop-catalog", "topup-milestones"),
                new("merchants/legacy", "economy", "oi-list", "Merchant Stores (Classic)", "Cửa hàng NPC (Cũ)", "Legacy raw view of NPC merchant definitions and inventories.", "Giao diện quản lý cửa hàng NPC dạng bảng truyền thống.", false, "merchants", "item-definitions", "cash-shop-catalog"),
            ]),
        new(
            "events",
            "oi-star",
            "Events & Rewards",
            "Sự kiện & Phần thưởng",
            [
                new("castle-siege", "events", "oi-shield", "Castle Siege", "Công Thành Chiến", "Schedule Castle Siege stages, guilds, and siege rewards.", "Lịch trình Công Thành Chiến, bang đăng ký và thưởng.", true, "guilds-levels-ladder", "s6-events", "timetable"),
                new("s6-events", "events", "oi-timer", "S6 Events & Chaos Castle", "Sự kiện S6 & Boss", "Tune Blood Castle, Devil Square, Chaos Castle, and Illusions.", "Tùy chỉnh BC, DV, CC, Huyết Lâu và phần thưởng.", true, "timetable", "world-bosses-rankings", "rate-tuning"),
                new("timetable", "events", "oi-calendar", "Timetable", "Lịch sự kiện", "Configure H event calendar, invasions, and alerts in Sydney time.", "Cấu hình lịch sự kiện phím H và xâm lăng theo giờ Sydney.", true, "event-alerts", "s6-events", "world-bosses-rankings"),
                new("event-alerts", "events", "oi-bell", "Event Alerts", "Thông báo sự kiện", "Manage Web Push VAPID notifications and Discord event posts.", "Quản lý thông báo đẩy Web Push và Discord cho sự kiện.", true, "timetable", "discord", "world-bosses-rankings"),
                new("world-bosses-rankings", "events", "oi-bolt", "World Bosses & Weekly Coins", "Boss thế giới & Đua tuần", "Configure custom boss spawns, damages, and weekly rewards.", "Cấu hình Boss thế giới, sát thương và thưởng tuần.", true, "timetable", "top-races", "mounts"),
                new("arena", "events", "oi-shield", "Arena Characters & Battles", "Nhân vật & trận Đấu trường", "Arena characters per class (level, stats, gear, balance), 5v5 and battle royale settings.", "Nhân vật Đấu trường theo lớp (cấp, chỉ số, đồ, cân bằng), cài đặt 5v5 và đấu sinh tồn.", true, "top-races", "servers", "leaderboards"),
                new("top-races", "events", "oi-trophy", "Top Races", "Đua top", "Create daily, weekly, monthly, or custom date-range competitions.", "Tạo sự kiện đua top ngày, tuần, tháng hoặc ngày đặc biệt.", true, "resets-relife", "leaderboards", "gift-codes"),
                new("missions-rewards", "events", "oi-task", "Missions & Coins", "Nhiệm vụ & Xu", "Configure J daily/weekly missions, streaks, and online coin caps.", "Thiết lập nhiệm vụ ngày/tuần, chuỗi và giới hạn coin mỗi ngày.", true, "onboarding-starter-kit", "gift-codes", "community"),
                new("gift-codes", "events", "oi-tag", "Gift Codes", "Gift Code", "Generate single/batch promo codes with EN/VI rules and items.", "Tạo mã quà tặng đơn hoặc hàng loạt kèm điều kiện & vật phẩm.", true, "topup-milestones", "missions-rewards", "community"),
                new("topup-milestones", "events", "oi-dollar", "Top-up Milestones", "Thưởng mốc nạp", "Configure cumulative VND top-up reward tiers and VietQR.", "Thiết lập mốc nạp tích lũy, thưởng và VietQR.", true, "cash-shop-catalog", "gift-codes", "host-settings")
                {
                    TitleDe = "Aufladestufen",
                    TitleZh = "充值里程碑",
                    DescriptionDe = "Belohnungsstufen für kumulierte VND-Aufladungen und VietQR konfigurieren.",
                    DescriptionZh = "配置累计 VND 充值奖励等级和 VietQR。",
                },
                new("resets-relife", "events", "oi-loop-circular", "Resets & Relife", "Reset & Relife", "Configure reset tiers, relife rules, and offline /uythac auto-reset.", "Cấu hình mức Reset, Relife và ủy thác tự reset.", true, "rankings-settings", "top-races", "jewel-bank"),
                new("onboarding-starter-kit", "events", "oi-flag", "Onboarding & Starter Kit", "Tân thủ & Quà khởi đầu", "Configure Y new-player tutorial steps and starter gear.", "Thiết lập hướng dẫn tân thủ phím Y và trang bị khởi đầu.", true, "missions-rewards", "buffer-npc", "gift-codes"),
                new("community", "events", "oi-comment-square", "Community & Zalo/Discord", "Cộng đồng Discord/Zalo", "Manage K community events, Discord/Zalo coin approvals, and feed.", "Duyệt thưởng Zalo/Discord, sự kiện K và tin nổi bật.", true, "discord", "gift-codes", "event-alerts"),
                new("guilds-levels-ladder", "events", "oi-people", "Guild Levels & Ladder", "Cấp bang hội & Xếp hạng", "Configure guild buffs, XP sources, and weekly Monday prizes.", "Thiết lập cấp bang, buff thành viên và thưởng tuần thứ Hai.", false, "guilds", "castle-siege", "world-bosses-rankings"),
                new("content-activation", "events", "oi-circle-check", "Content Activation", "Kích hoạt tính năng", "Quickly enable/disable major Season 6 maps, classes, and systems.", "Bật/tắt nhanh các bản đồ, lớp nhân vật và hệ thống Season 6.", false, "setup", "season-plan", "plugins"),
                new("lucky-number", "events", "oi-random", "Daily Lucky Number", "Con số may mắn", "Configure daily lottery tickets, pot shares, and Sydney draws.", "Thiết lập xổ số may mắn hằng ngày theo giờ Sydney.", false, "community", "gift-codes", "topup-milestones"),
                new("marriage", "events", "oi-heart", "Marriage", "Kết hôn", "Configure /marry Devias ceremony, couples buffs, and web list.", "Cấu hình kết hôn tại Devias, buff cặp đôi và bảng công khai.", false, "custom-ranks", "party-options", "community"),
                new("catchup", "events", "oi-arrow-top", "Catch-up Cap", "Giới hạn đuổi kịp", "Set daily top level/reset caps and underdog EXP boosts.", "Giới hạn cấp/reset hằng ngày và hệ số EXP hỗ trợ người mới.", false, "season-plan", "resets-relife", "rate-tuning"),
                new("season-plan", "events", "oi-spreadsheet", "Season Plan", "Lộ trình mùa giải", "Stage progressive feature unlocks (+9 -> Master -> C3 Wings).", "Mở khóa tính năng theo từng giai đoạn mùa giải.", false, "catchup", "content-activation", "worlds"),
                new("leftovers", "events", "oi-reload", "Combining Leftovers", "Đổi đồ dư & Nâng hộp", "Set box upgrade recipes and surplus item exchange rewards.", "Công thức nâng cấp hộp F10 và gói đổi nguyên liệu dư.", false, "mounts", "jewel-bank", "vault-chaos-rewards"),
                new("mounts", "events", "oi-badge", "Mount Chain", "Chuỗi thú cưỡi", "Configure Golden Boss shards and Shift+U mount upgrades.", "Rơi mảnh từ Boss Hoàng Kim và nâng cấp thú cưỡi Shift+U.", false, "world-bosses-rankings", "leftovers", "item-definitions"),
                new("party-options", "events", "oi-link-intact", "Party Options & Spots", "Tổ đội & Bãi săn", "Manage /partypass auto-rejoin and preset P hunting spots.", "Quản lý mật khẩu tổ đội /partypass và danh sách bãi săn.", false, "teleport-npc", "buffer-npc", "qol-settings"),
                new("gift-box", "events", "oi-inbox", "Gift Box & Mail", "Hòm quà & Thư", "Send in-game item/Zen/coin packages to players' Gift Box.", "Gửi thư kèm vật phẩm, Zen và coin vào Hòm quà người chơi.", true, "gift-codes", "missions-rewards", "accounts"),
                new("news", "events", "oi-document", "News & Announcements", "Tin tức & Thông báo", "Publish bilingual EN/VI announcements and login panel news.", "Đăng tin tức song ngữ Anh/Việt cho website và màn hình đăng nhập.", true, "community", "event-alerts", "discord"),
            ]),
        new(
            "world",
            "oi-map",
            "World, Combat & QoL",
            "Thế giới, Chiến đấu & QoL",
            [
                new("rate-tuning", "world", "oi-dial", "Rate & Drop Tuning", "Điều chỉnh tỉ lệ EXP/Drop", "Customize per-map rules, Sydney bonus events, and damage caps.", "Quy tắc bản đồ, sự kiện giờ vàng Sydney và giới hạn sát thương.", true, "servers", "s6-events", "gameplay-rules"),
                new("gameplay-rules", "world", "oi-shield", "Gameplay Rules", "Luật chơi chung", "Adjust PK limits, helper rules, party EXP, and core combat rules.", "Quy tắc PK, tổ đội, hỗ trợ và chiến đấu cốt lõi.", true, "rate-tuning", "anti-cheat", "qol-settings"),
                new("anti-cheat", "world", "oi-warning", "Anti-Cheat", "Chống gian lận", "Configure attack speed, movement speed, and packet guard thresholds.", "Thiết lập ngưỡng kiểm tra tốc độ đánh, di chuyển và gói tin.", true, "banned-words", "crash-watch", "accounts"),
                new("qol-settings", "world", "oi-wrench", "QoL & Auto-Play", "Tiện ích & Tự đánh", "Adjust the auto-play helper's extras, Zen fees, and client convenience.", "Điều chỉnh Auto Helper, phí Zen và tiện ích người chơi.", true, "off-trade", "party-options", "rate-tuning"),
                new("ultimate-autoplay", "world", "oi-bolt", "Ultimate Autoplay", "Autoplay Toàn Diện", "Switch Ultimate Autoplay features Off, Free, or VIP Only (Alt+Z).", "Bật/tắt và phân quyền Miễn phí / VIP cho từng tính năng Autoplay Toàn Diện.", true, "qol-settings", "vip", "jewel-bank"),
                new("buffer-npc", "world", "oi-plus", "Buffer NPC", "NPC Hỗ trợ (Elf)", "Configure starter buff level caps, durations, and NPC numbers.", "Cấu hình giới hạn cấp nhận buff Elf và thời gian hiệu lực.", true, "onboarding-starter-kit", "teleport-npc", "party-options"),
                new("teleport-npc", "world", "oi-map-marker", "Teleport NPC", "NPC Dịch chuyển", "Edit custom warp gates, Zen costs, VIP, and PK checks.", "Thiết lập điểm dịch chuyển, phí Zen, điều kiện VIP và PK.", false, "party-options", "world-editor", "servers"),
                new("rankings-settings", "world", "oi-list-rich", "In-Game Rankings & Add Stats", "BXH & Cộng điểm nhanh", "Toggle Shift+R live rankings window and Shift+A bulk stat add.", "Bật/tắt bảng xếp hạng Shift+R và cộng điểm nhanh Shift+A.", false, "leaderboards", "resets-relife", "custom-ranks"),
                new("custom-ranks", "world", "oi-star", "Custom Ranks & Titles", "Danh hiệu tùy chỉnh", "Define unlockable EN/VI nameplate titles and attribute bonuses.", "Tạo danh hiệu hiển thị cạnh tên nhân vật và chỉ số cộng thêm.", false, "rankings-settings", "marriage", "accounts"),
                new("hnmo-skills", "world", "oi-bolt", "Master Skills", "Kỹ năng Master", "Inspect and adjust custom skill behaviors and scaling.", "Kiểm tra và tinh chỉnh kỹ năng Master.", false, "gameplay-rules", "qol-settings", "accounts"),
                new("map-editor", "world", "oi-grid-four-up", "Map Spawn Editor", "Chỉnh sửa quái bản đồ", "Place monster spawns and NPC positions on 2D map grids.", "Chỉnh sửa vị trí quái và NPC trên bản đồ 2D.", false, "world-editor", "terrain-editor", "teleport-npc"),
                new("terrain-editor", "world", "oi-brush", "Terrain Safezone & Walk Map", "Vùng an toàn & Địa hình", "Paint safe zones, obstacles, and walkable terrain flags.", "Chỉnh vùng an toàn, vật cản và ô di chuyển được.", false, "map-editor", "world-editor", "worlds"),
                new("world-editor", "world", "oi-layers", "3D World Editor", "Trình sửa thế giới 3D", "Edit 3D map objects, foliage, and props interactively.", "Chỉnh sửa mô hình vật thể 3D trên từng bản đồ.", false, "map-editor", "terrain-editor", "worlds"),
            ]),
        new(
            "items",
            "oi-box",
            "Items & Crafting",
            "Vật phẩm & Chế tạo",
            [
                new("item-definitions", "items", "oi-tag", "Item Definitions", "Định nghĩa vật phẩm", "Create/edit items, EN/VI tooltips, glow colors, and stats.", "Thêm/sửa vật phẩm, mô tả EN/VI, màu phát sáng và chỉ số.", true, "edit-item-drops", "merchants", "fixed-options"),
                new("edit-item-drops", "items", "oi-data-transfer-download", "Item Drops", "Tỉ lệ rớt vật phẩm", "Manage monster and map item drop pools and percentages.", "Quản lý nhóm rơi đồ theo quái và bản đồ.", true, "item-definitions", "fixed-options", "rate-tuning"),
                new("fixed-options", "items", "oi-pin", "Fixed Item Options", "Tùy chọn đồ cố định", "Pin Skill, Luck, Excellent, or level options on specific drops.", "Cố định dòng Tuyệt Hảo, May Mắn, Tuyệt Chiêu cho đồ rơi.", false, "edit-item-drops", "item-definitions", "vault-chaos-rewards"),
                new("vault-chaos-rewards", "items", "oi-beaker", "Chaos Machine & Rewards", "Máy Chaos & Thưởng sự kiện", "Adjust Chaos mix success rates, multi-vaults, and event chests.", "Chỉnh tỉ lệ xoay Chaos, rương mở rộng và thưởng sự kiện.", false, "s6-events", "leftovers", "fixed-options"),
                new("talismans", "items", "oi-aperture", "Talismans & Figurines", "Bùa & Tượng cộng chỉ số", "Define passive inventory talismans and stat-grant scrolls.", "Tạo bùa tăng chỉ số khi mang trong túi và sách cộng điểm.", false, "item-definitions", "custom-ranks", "cash-shop-catalog"),
            ]),
        new(
            "host",
            "oi-cog",
            "Host, Setup & Diagnostics",
            "Hệ thống, Cài đặt & Chẩn đoán",
            [
                new("admin-guide", "host", "oi-book", "Admin Guide & Recipes", "Cẩm nang quản trị", "Searchable host manual with step-by-step configuration recipes.", "Tài liệu quản trị có tìm kiếm và các công thức thao tác nhanh.", true, "", "setup", "import-server"),
                new("setup", "host", "oi-cog", "Setup & Initialization", "Khởi tạo dữ liệu", "Install database versions (0.75 / Season 6) and seed data.", "Cài đặt dữ liệu ban đầu và phiên bản Season 6.", true, "config-updates", "worlds", "admin-guide"),
                new("config-updates", "host", "oi-medical-cross", "Config Updates", "Cập nhật cấu hình", "Apply non-destructive built-in configuration patches.", "Áp dụng các bản vá cấu hình tích hợp sẵn.", true, "config-captions", "setup", "plugins"),
                new("config-captions", "host", "oi-globe", "Translations & Captions", "Dịch thuật cấu hình", "Link or refresh bilingual captions across game items/maps.", "Cập nhật và đồng bộ bản dịch vật phẩm, bản đồ.", false, "config-updates", "item-definitions", "admin-guide"),
                new("plugins", "host", "oi-puzzle-piece", "Plug-ins", "Quản lý Plug-in", "Enable/disable server extensions and edit raw plug-in JSON.", "Bật/tắt các plug-in mở rộng và sửa cấu hình chi tiết.", true, "chat-commands", "content-activation", "host-settings"),
                new("chat-commands", "host", "oi-terminal", "Chat Commands", "Lệnh chat", "Inspect available player and GM slash commands.", "Xem danh sách lệnh chat của người chơi và GM.", false, "plugins", "admin-guide", "banned-words"),
                new("users", "host", "oi-key", "Admin Users", "Tài khoản Admin", "Manage panel operator logins, roles, and 2FA enforcement.", "Quản lý tài khoản đăng nhập bảng điều khiển và phân quyền.", false, "api-keys", "account/security", "audit"),
                new("api-keys", "host", "oi-share-boxed", "API Keys & MCP", "Khóa API & MCP", "Create scoped API tokens for website bridge and external tools.", "Tạo khóa API cho bridge website và công cụ tự động.", false, "users", "audit", "host-settings"),
                new("discord", "host", "oi-chat", "Discord Bridge", "Kết nối Discord", "Connect Discord bot webhook and channel announcements.", "Kết nối bot Discord để đồng bộ chat và thông báo.", false, "community", "event-alerts", "plugins"),
                new("server-control", "host", "oi-power-standby", "Server control", "Điều khiển máy chủ", "Manage local services, logs, updates, ports and backups.", "Quản lý dịch vụ, nhật ký, cập nhật, cổng và sao lưu trên máy chủ.", true, "host-settings", "logfiles", "state-retention")
                {
                    TitleDe = "Serversteuerung",
                    TitleZh = "服务器控制",
                    DescriptionDe = "Lokale Dienste, Protokolle, Updates, Ports und Sicherungen verwalten.",
                    DescriptionZh = "管理本地服务、日志、更新、端口和备份。",
                },
                new("seasons", "host", "oi-globe", "Seasons & Worlds", "Các Season & Thế giới", "Enable Season 9 and Arena alongside Season 6.3 and inspect ports.", "Bật/tắt Season 9 và Arena cùng Season 6.3 và xem cổng.", true, "host-settings", "season-plan", "setup"),
                new("licence", "host", "oi-key", "Licence", "Giấy phép", "Your HNMO licence: players, seasons, expiry; buy, renew or start the trial.", "Giấy phép HNMO: số người chơi, season, hạn dùng; mua, gia hạn hoặc dùng thử.", true, "host-settings", "seasons", "server-control"),
                new("host-settings", "host", "oi-wrench", "Host & VietQR Settings", "Cài đặt Host & VietQR", "Edit centralized ports, DB connection, SMTP email, and VietQR.", "Cấu hình cổng, PostgreSQL, SMTP gửi email và ngân hàng VietQR.", true, "crash-watch", "topup-milestones", "setup"),
                new("crash-watch", "host", "oi-warning", "Crash Watch", "Giám sát sự cố", "Inspect unexpected process exits, restart history, and logs.", "Theo dõi sự cố dừng server bất thường và tự khởi động lại.", true, "host-settings", "logfiles", "maintenance-tools"),
                new("import-server", "host", "oi-cloud-upload", "Import Another Server", "Nhập dữ liệu máy chủ khác", "Dry-run and import accounts, characters, shops, and drops safely.", "Xem thử và nhập tài khoản, nhân vật, shop từ máy chủ cũ.", false, "worlds", "setup", "maintenance-tools"),
                new("maintenance-tools", "host", "oi-wrench", "Maintenance Tools", "Công cụ bảo trì", "Preview cleanup tasks, clear locks, and run admin fixes.", "Xem trước và chạy công cụ bảo trì dữ liệu có lưu nhật ký.", false, "state-retention", "db-log-retention", "crash-watch"),
                new("state-retention", "host", "oi-layers", "State Retention & Backups", "Sao lưu & Lưu trữ", "Manage rolling GFS backups, state retention, and recovery.", "Quản lý lịch sao lưu tự động GFS và lưu trữ trạng thái.", false, "db-log-retention", "maintenance-tools", "host-settings"),
                new("db-log-retention", "host", "oi-hard-drive", "DB Log Retention", "Dọn nhật ký DB", "Configure log archival, vacuum schedules, and disk thresholds.", "Cấu hình dọn dẹp nhật ký CSDL và kiểm tra dung lượng đĩa.", false, "state-retention", "logfiles", "audit"),
                new("logfiles", "host", "oi-list", "Log Files", "Tệp nhật ký", "Read server log files live in the admin browser.", "Đọc trực tiếp tệp nhật ký hoạt động của máy chủ.", false, "logging-configuration", "crash-watch", "db-log-retention"),
                new("logging-configuration", "host", "oi-sliders", "Logging Levels", "Cấu hình mức log", "Adjust Serilog verbosity per subsystem.", "Tùy chỉnh mức độ ghi nhận log theo từng phân hệ.", false, "logfiles", "network-analyzer", "host-settings"),
                new("network-analyzer", "host", "oi-pulse", "Packet Analyzer", "Phân tích gói tin", "Inspect live/archived packets sent between client and server.", "Theo dõi gói tin trực tiếp giữa web client và game server.", false, "chat-commands", "logfiles", "servers"),
                new("account/security", "host", "oi-lock-locked", "2FA & Security", "Bảo mật 2 lớp (2FA)", "Set up authenticator two-factor security for your admin login.", "Thiết lập xác thực hai lớp cho tài khoản quản trị.", false, "users", "api-keys", "audit"),
                new("create-game-server", "host", "oi-plus", "Create Game Server", "Tạo Game Server mới", "Create a new game server instance definition.", "Tạo cấu hình máy chủ trò chơi mới.", false, "servers", "worlds", "setup"),
                new("create-connect-server", "host", "oi-plus", "Create Connect Server", "Tạo Connect Server mới", "Create a new connect server gateway definition.", "Tạo cấu hình cổng kết nối Connect Server mới.", false, "servers", "create-game-server", "setup"),
                new("email-settings", "host", "oi-envelope-closed", "Website Email (SMTP)", "Email Website (SMTP)", "Configure encrypted SMTP mail credentials for website verification.", "Cấu hình máy chủ gửi email SMTP mã hóa cho website.", false, "host-settings", "account/security", "users"),
            ]),
    ];

    private static readonly IReadOnlyList<AdminNavEntry> AllEntries = GroupList.SelectMany(g => g.Entries).ToList();

    private static readonly Dictionary<string, AdminNavEntry> EntriesByRoute =
        AllEntries.ToDictionary(e => e.Route, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets all task-based sidebar navigation groups.</summary>
    public static IReadOnlyList<AdminNavGroup> Groups => GroupList;

    /// <summary>Gets a flat list of every catalogued navigation entry (unique per route).</summary>
    public static IReadOnlyList<AdminNavEntry> Entries => AllEntries;

    /// <summary>Looks up a catalog entry by normalized route path.</summary>
    /// <param name="route">Route path (e.g. <c>/servers</c> or <c>servers</c>).</param>
    /// <returns>The entry or <c>null</c>.</returns>
    public static AdminNavEntry? FindByRoute(string? route)
    {
        var key = AdminNavigationPreferences.NormalizeRoute(route);
        return EntriesByRoute.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <summary>Searches EN &amp; VI titles, descriptions, and routes case-insensitively.</summary>
    /// <param name="query">Search text.</param>
    /// <param name="maxResults">Maximum entries returned.</param>
    /// <returns>Matching navigation entries.</returns>
    public static IReadOnlyList<AdminNavEntry> Search(string? query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var term = query.Trim();
        return AllEntries
            .Where(e =>
                e.TitleEn.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.TitleVi.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.DescriptionEn.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.DescriptionVi.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.Route.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e =>
                e.TitleEn.StartsWith(term, StringComparison.OrdinalIgnoreCase)
                || e.TitleVi.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .ThenBy(e => e.LocalizedTitle, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }
}

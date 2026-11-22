# Account portal redesign / Thiết kế lại cổng tài khoản

Owner request: 22 November 2026. Cover the web game's login, website sign-in, and a private comprehensive account dashboard.

Yêu cầu ngày 22/11/2026: thiết kế lại màn hình đăng nhập game, đăng nhập website và trang quản lý tài khoản riêng tư, đầy đủ thông tin.

## Reviewable design / Bản thiết kế có thể xem thử

Build the web client and open `/account-profile-preview.html`. The screen selector previews both login layouts and the account hub. All account names, balances, characters, events and item references are fictional. The preview performs no sign-in, account request, collection or payment. Inputs and financial controls cannot submit credentials or charges. Game branding still comes from `gameName()`.

Biên dịch web client rồi mở `/account-profile-preview.html`. Chọn màn hình để xem hai bố cục đăng nhập và trung tâm tài khoản. Tên tài khoản, số dư, nhân vật, sự kiện và mã vật phẩm đều là dữ liệu giả lập. Bản xem thử không đăng nhập, gọi API tài khoản, nhận quà hay thanh toán. Tên game vẫn lấy từ `gameName()`.

This is a design prototype, not a replacement for production `index.html` or `account.html`. It must not be described as a completed live profile.

Đây là bản mẫu thiết kế, chưa thay thế `index.html` hoặc `account.html` đang dùng. Không coi bản mẫu này là trang tài khoản hoàn chỉnh đã kết nối dữ liệu thật.

## Current source and ownership / Nguồn mã và phạm vi công việc

Reviewed main `c9d67fdc`, `docs/HANDOVER-2026-11-19.md`, the workboard, recent PRs, and audited integration PR85 at `685968a9` (`claude/hnmo-main-handover-54co2m`). Main currently lacks the integrated feature code. This design branch starts from that audited integration, not the obsolete account29 or gift64 worktrees. Keep other sessions' gameplay and release work untouched. Do not restart the earlier tools queue or resume automation.

Đã đọc main, tài liệu bàn giao, bảng công việc, các PR mới và nhánh tích hợp đã kiểm tra của PR85. Main hiện chưa có mã tính năng đã tích hợp. Nhánh thiết kế dựa trên PR85; không tiếp tục từ các bản tài khoản/quà cũ. Không thay đổi công việc gameplay/phát hành của các phiên khác, không tự khởi động lại hàng đợi công cụ hoặc lịch tự động cũ.

## Information architecture / Cấu trúc thông tin

| Section / Mục | Production behavior to build / Chức năng cần xây dựng |
|---|---|
| Overview / Tổng quan | Account state, selected world/season, character summary, balances, VIP expiry, pending gifts and explicit security checks. / Trạng thái tài khoản, thế giới/mùa đã chọn, tóm tắt nhân vật, số dư, hạn VIP, quà chờ nhận và từng mục bảo mật. |
| Characters / Nhân vật | Class, level/master level, resets/relife, stats, equipment, saved location, guild and supported activity. Label saved versus live data and timestamps. / Lớp nhân vật, cấp/cấp master, reset/relife, chỉ số, trang bị, vị trí lưu, bang hội và hoạt động có dữ liệu. Phân biệt dữ liệu lưu với trực tiếp và ghi thời điểm. |
| Items & vaults / Vật phẩm & rương | Equipment, bags/extensions, every vault page, personal shop and owned escrow/owed items. Search by name/ID/options/storage, exact options and locks, mobile detail sheet. / Trang bị, túi/phần mở rộng, tất cả trang rương, cửa hàng cá nhân và vật phẩm đang ký gửi/chờ nhận. Tìm theo tên/mã/tùy chọn/vị trí; xem đủ tùy chọn, khóa và chi tiết trên điện thoại. |
| Activity & trades / Hoạt động & giao dịch | Account-owned timeline for retained logins, trades, market/store sales, mixes, qualifying drops/pickups, rewards and receipts. Filters and per-transaction detail. / Dòng thời gian riêng của tài khoản: đăng nhập, giao dịch, chợ/cửa hàng, ép đồ, nhặt/rơi được ghi nhận, thưởng và biên nhận. Có bộ lọc và chi tiết từng giao dịch. |
| Wallet & VIP / Ví & VIP | Existing coin top-up and VIP flows, clear offers/expiry/benefits, receipts and pending/failed states. Trade credit, its held funds and withdrawals remain separate from earned coins. / Dùng luồng nạp xu/mua VIP hiện có; ghi rõ giá, hạn, quyền lợi, biên nhận và trạng thái chờ/lỗi. Tín dụng giao dịch, khoản giữ và rút tiền tách riêng xu kiếm trong game. |
| Security / Bảo mật | Supported email status, secondary PIN, item locks, masked login history and restrictions; session/device actions only after real support exists. / Trạng thái email có dữ liệu, mật khẩu cấp hai, khóa vật phẩm, lịch sử đăng nhập đã che và hạn chế tài khoản; chỉ thêm quản lý phiên/thiết bị khi có hỗ trợ thật. |
| Rewards & mail / Phần thưởng & thư | Daily missions/calendar, referral progress, gift box, codes, milestone rewards and expiry reminders in one place. / Nhiệm vụ/lịch điểm danh, tiến độ giới thiệu, hộp quà, mã quà, thưởng mốc và nhắc hết hạn trong một mục. |
| Appearance / Giao diện | Account-owned theme preference, compact/comfortable density, language and accessibility preferences; follow account across devices once stored server-side. / Lưu giao diện, độ gọn, ngôn ngữ và tùy chọn hỗ trợ theo tài khoản để đồng bộ nhiều thiết bị. |

## Login screens / Hai màn hình đăng nhập

- Game login: immediate play action, configured world/season choices, real availability, published news/events and account/guide links. Connection-host/relay fields stay under advanced settings. No private account information before authentication.
- Website login: one sign-in entry point, clear recovery/registration links and a short explanation of account features. After authentication, reuse the existing ticket play action and section registry; remove repeated feature-specific password forms from the normal signed-in flow.

- Đăng nhập game: vào chơi nhanh, chọn thế giới/mùa theo cấu hình, tình trạng thật, tin/sự kiện đã đăng và liên kết tài khoản/hướng dẫn. Thông số máy chủ/relay nằm trong nâng cao. Không hiện thông tin tài khoản trước khi xác thực.
- Đăng nhập website: một nơi đăng nhập, liên kết khôi phục/đăng ký rõ ràng và giới thiệu ngắn các mục tài khoản. Sau xác thực, dùng vé vào game và cơ chế đăng ký mục hiện có; không bắt nhập lại mật khẩu cho từng mục trong luồng thông thường.

## Data audit and gaps / Dữ liệu hiện có và phần còn thiếu

- `WebsiteAccountController`: saved character level/reset/map, presence and coin history. It does not currently expose the full character detail requested. Its default configuration lookup is not a unified cross-world identity service. Never combine unrelated accounts merely because their login names match.
- `WebsiteAccountSafetyController`: login/trade history, item IDs and locks. Its current item enumeration includes character inventory and `account.Vault`, not every secondary vault. It is gated by the safety plug-in; ordinary owned inventory visibility must not depend on an unrelated protection feature being enabled.
- `MultiVaultPlugIn`: current vault storage IDs preserve real persistent item identity. Read all authorized storage pages without switching/mutating the active vault. Legacy snapshots may lack identity/history; mark that explicitly.
- `ActivityLogEntry`: event time, both parties, item GUIDs where available, amounts and extra context. Current logging defaults to valuable drops/pickups and30-day active retention. An item GUID does not prove lifetime logging. Mix lineage also needs explicit input/output links before claiming complete item transformations.
- `ActivityLogStore.Query`: account search currently uses substring matching for admin search. Do not reuse that as player authorization. Player projections need exact account/world ownership and pagination; never return raw admin notes, private partner logins or arbitrary Details JSON.
- VIP, gift box, account missions, escrow, top-up milestones and the section registry already exist. Mount their tested flows; do not rebuild financial rules or add a second wallet.
- Existing website themes are host-selected. A player's account preference needs its own validated persistence and a defined fallback to the host theme. The preview's theme selection is deliberately temporary.
- A nonempty email is not evidence of verified ownership. A four-digit secondary PIN is not MFA. Unknown/off/unsupported states must remain distinct from verified/enabled states.

- API tài khoản có dữ liệu nhân vật cơ bản, trạng thái trực tuyến và lịch sử xu; chưa đủ chi tiết nhân vật và chưa phải dịch vụ định danh thống nhất nhiều thế giới. Không ghép hai tài khoản khác nhau chỉ vì trùng tên đăng nhập.
- API bảo mật đã có lịch sử và mã/khóa vật phẩm, nhưng chưa liệt kê mọi rương phụ và phụ thuộc plug-in bảo mật. Xem tài sản thuộc sở hữu phải độc lập với việc bật tính năng bảo mật bổ sung.
- Rương mới có mã kho và mã vật phẩm bền vững; đọc dữ liệu không được tự chuyển rương đang dùng. Bản chụp cũ thiếu mã/lịch sử phải được ghi rõ.
- Nhật ký hiện mặc định chỉ ghi nhặt/rơi vật phẩm giá trị và giữ bản ghi hoạt động30ngày. Không thể suy ra lịch sử trọn đời từ một mã GUID; ép/chuyển hóa cần liên kết đầu vào/đầu ra.
- Tìm kiếm quản trị đang khớp một phần tên tài khoản. API người chơi phải kiểm tra sở hữu chính xác theo tài khoản/thế giới và phân trang, không trả ghi chú riêng hoặc JSON nội bộ.
- Dùng lại VIP, thư quà, nhiệm vụ, ký quỹ và các luồng thanh toán đã có; không tạo ví/quy tắc tài chính thứ hai.
- Giao diện hiện do chủ máy chủ chọn; lựa chọn cá nhân cần lưu riêng trên máy chủ. Bản xem thử chưa lưu lựa chọn theo tài khoản.
- Có email không đồng nghĩa đã xác minh; mã PIN không phải MFA. Phân biệt rõ chưa biết, tắt và chưa hỗ trợ.

## Item passport / Hồ sơ vật phẩm

Display actual persistent ID, definition/options, location, lock/expiry and authorized retained events. Allow copying the ID into a support report. For crafted/upgraded replacements, add a separate lineage event linking input/output IDs. Never manufacture old events or expose a later owner's private activity. Default storage views are read-only; moving/selling/mixing requires separate existing gameplay safeguards.

Hiển thị mã bền vững thật, định nghĩa/tùy chọn, vị trí, khóa/hạn và sự kiện còn lưu mà tài khoản có quyền xem. Cho sao chép mã để gửi hỗ trợ. Với vật phẩm thay thế sau ép/nâng cấp, cần sự kiện liên kết mã đầu vào/đầu ra. Không dựng lịch sử cũ hoặc tiết lộ hoạt động riêng của chủ sau. Mặc định chỉ xem; chuyển/bán/ép phải qua cơ chế bảo vệ gameplay riêng.

## Five distinct themes / Năm phong cách khác biệt

- Obsidian: dark command centre, left navigation, dense account overview.
- Chronicle: light parchment, serif journal, horizontal navigation and open sections.
- Jade: light spacious workspace, rounded surfaces and quiet hierarchy.
- Terminal: monospace, compact rectangular panels and narrow navigation.
- Royal: violet gallery, centered headings and horizontal navigation.

Năm bản khác nhau cả bố cục, chữ, hình khối và màu sắc, không chỉ đổi màu nhấn. Khi làm bản chính thức cần kiểm tra tương phản, bàn phím, màn hình nhỏ và tùy chọn giảm chuyển động.

## Suggested additions / Đề xuất bổ sung

- Search across owned characters/items/receipts; a return-player summary based only on retained events.
- Pending/owed rewards and transaction recovery, VIP expiry and item expiry reminders.
- Optional loadout comparison and progression goals when authoritative stats are available.
- Support export with receipts/item references and no passwords/PINs.
- Account-safe session controls and optional MFA only after implementing the backend.
- Optional public character showcase separately consented; never expose the private account dashboard publicly.

Tìm xuyên suốt nhân vật/vật phẩm/biên nhận; tóm tắt khi quay lại chỉ dựa vào sự kiện có thật; nhắc thưởng chờ nhận và hạn VIP/vật phẩm; so sánh trang bị/mục tiêu khi có chỉ số chuẩn; xuất thông tin hỗ trợ không kèm bí mật; bổ sung quản lý phiên/MFA sau khi có backend; trang trưng bày nhân vật công khai phải tách riêng và do người chơi chọn bật.

## Delivery gates / Điều kiện hoàn thành

This preview is only the first reviewable design increment. Production completion requires authenticated world/account-scoped read models, ownership/privacy tests, a real persisted appearance setting, mounting existing payment/reward flows, retention/lineage instrumentation where missing, bilingual accessibility/mobile checks, and live testing against the packaged game. Keep release work and schedules untouched.

Bản xem thử chỉ là bước thiết kế đầu tiên. Muốn hoàn thành cần API đọc xác thực đúng tài khoản/thế giới, kiểm thử riêng tư/sở hữu, lưu giao diện thật, kết nối luồng thanh toán/thưởng hiện có, ghi lịch sử còn thiếu, kiểm tra song ngữ/truy cập/điện thoại và chạy thử với game đóng gói. Không thay đổi công việc phát hành hoặc lịch tự động.

## Interactive preview expansion / Mở rộng bản xem thử tương tác

The owner approved the additional ideas and requested a hosted private preview. The browser-local implementation now includes11sections: both login views; global owned-fixture search; item passports and watched items; event kind/character/Sydney-date filters and pagination; filtered CSV exports; before/after upgrades, consumed crafting inputs and exact storage slots; a return-player summary and expiry reminders; an adventure journal; equipment comparison with calculated deltas; safe support JSON export; locally simulated session ending; and saved preview theme/language/density preferences. All records are explicitly fictional. Payment and credential submission remain disabled. No game database is connected. Watch state is tab-local; browser preferences are not account-synced.

The supplied tracker page was inaccessible behind a browser challenge. Publicly indexed guide descriptions supported serial-based lifecycle tracking and detailed crafting outcomes. Only those general capabilities informed this implementation; no third-party code, branding, assets, item rates or rules were copied. The exact blocked tracker interface was not inspected.

Người dùng đã đồng ý các đề xuất và yêu cầu bản xem thử riêng tư có liên kết. Bản tương tác hiện có11mục, tìm kiếm dữ liệu mẫu, hồ sơ/theo dõi vật phẩm, lọc sự kiện theo loại/nhân vật/ngày Sydney, phân trang, xuất CSV theo bộ lọc, lịch sử nâng cấp/nguyên liệu/ô kho, tóm tắt khi quay lại, nhắc hết hạn, nhật ký hành trình, so sánh chỉ số, xuất báo cáo JSON an toàn, mô phỏng kết thúc phiên và lưu giao diện/ngôn ngữ/độ gọn trong trình duyệt. Mọi bản ghi là giả lập; chưa kết nối cơ sở dữ liệu hoặc thanh toán thật. Danh sách theo dõi chỉ tồn tại trong thẻ hiện tại; tùy chọn chưa đồng bộ theo tài khoản.

Trang tham khảo bị chặn bởi cơ chế kiểm tra trình duyệt. Chỉ đọc được mô tả đã lập chỉ mục từ hướng dẫn công khai; không sao chép mã, thương hiệu, tài nguyên, tỷ lệ vật phẩm hoặc quy tắc của bên khác.

Hosted private preview: https://hnmo-account-hub-preview.beautifulclock2.chatgpt.site

The preview is owner-private and uses no production account data. Its source/hosting manifest is maintained separately from the HNMO game repository. The game branch contains the editable TypeScript, styles, fixtures and browser tests. Do not change the Site audience or enable automated schedules as part of an update.

Bản xem thử chỉ chủ sở hữu được truy cập, không dùng dữ liệu tài khoản thật. Không tự thay đổi quyền truy cập hoặc bật lịch tự động khi cập nhật.

# Account portal redesign / Thiết kế lại cổng tài khoản

Owner request: 22 November 2026. Cover the web game's login, website sign-in, and a private comprehensive account dashboard.

Yêu cầu ngày 22/11/2026: thiết kế lại màn hình đăng nhập game, đăng nhập website và trang quản lý tài khoản riêng tư, đầy đủ thông tin.

## Reviewable design / Bản thiết kế có thể xem thử

Build the web client and open `/account-profile-preview.html`. The default owner review gallery offers five complete fantasy portal/profile kits. `?view=portal&theme=ember` previews the shared game/website entrance; `?view=profile&theme=ember` opens fictional account data. Repeat with `azure`, `jade`, `violet`, or `parchment`. `?view=admin` is a clearly labelled host-setting simulation, not a production admin route. Default language is Vietnamese; `lang=en` switches to English. Branding comes from `gameName()`.

Mở `/account-profile-preview.html` để xem năm bộ giao diện fantasy. Mỗi bộ có trang vào game và hồ sơ riêng. Chỉ chủ server chọn theme; hồ sơ người chơi không có mục chọn giao diện. `view=admin` chỉ mô phỏng thao tác quản trị trong bản mẫu, không ghi cấu hình máy chủ thật. Mặc định tiếng Việt, có tiếng Anh. Dữ liệu tài khoản đều là giả lập.

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
| Host theme / Giao diện do host chọn | One host-selected kit styles both portal and profile. No player theme picker. Density remains an individual browser display preference. / Host chọn một bộ cho trang game và hồ sơ; người chơi chỉ có tùy chọn độ gọn khi xem. |

## One sign-in journey / Một lần đăng nhập

There is one entry form, followed by game and account actions in the same session. The prototype uses only a tab-local example-session flag and disabled credential fields. It demonstrates navigation, not real authentication. Production must reuse the existing authenticated session and login-ticket exchange; never send a password in a URL or add separate per-feature sign-ins.

Chỉ có một nơi đăng nhập, sau đó chuyển giữa game và hồ sơ trong cùng phiên. Bản mẫu chỉ lưu cờ phiên giả lập trong thẻ trình duyệt, không nhận mật khẩu thật. Bản chính thức phải dùng lại phiên xác thực và vé vào game hiện có; chưa coi mô phỏng này là đã hoàn thành đăng nhập dùng chung thật.

Body text uses 16px, regular control labels use at least 14px, and metadata uses 12px. Mobile account navigation has five equal bottom targets, each at least 44px tall: Home, Characters, Items, Wallet and More. Secondary sections appear in a two-column modal menu with Escape, close button and focus return. Header actions use an equal two-column grid.

Chữ nội dung 16px, nhãn điều khiển ít nhất 14px, thông tin phụ 12px. Điện thoại có năm nút đều nhau: Trang chủ, Nhân vật, Tài sản, Ví, Thêm. Các mục phụ nằm trong menu hai cột, có nút đóng, hỗ trợ bàn phím và đưa tiêu điểm trở lại nút mở.

## Data audit and gaps / Dữ liệu hiện có và phần còn thiếu

- `WebsiteAccountController`: saved character level/reset/map, presence and coin history. It does not currently expose the full character detail requested. Its default configuration lookup is not a unified cross-world identity service. Never combine unrelated accounts merely because their login names match.
- `WebsiteAccountSafetyController`: login/trade history, item IDs and locks. Its current item enumeration includes character inventory and `account.Vault`, not every secondary vault. It is gated by the safety plug-in; ordinary owned inventory visibility must not depend on an unrelated protection feature being enabled.
- `MultiVaultPlugIn`: current vault storage IDs preserve real persistent item identity. Read all authorized storage pages without switching/mutating the active vault. Legacy snapshots may lack identity/history; mark that explicitly.
- `ActivityLogEntry`: event time, both parties, item GUIDs where available, amounts and extra context. Current logging defaults to valuable drops/pickups and30-day active retention. An item GUID does not prove lifetime logging. Mix lineage also needs explicit input/output links before claiming complete item transformations.
- `ActivityLogStore.Query`: account search currently uses substring matching for admin search. Do not reuse that as player authorization. Player projections need exact account/world ownership and pagination; never return raw admin notes, private partner logins or arbitrary Details JSON.
- VIP, gift box, account missions, escrow, top-up milestones and the section registry already exist. Mount their tested flows; do not rebuild financial rules or add a second wallet.
- Existing WebsiteConfiguration.Theme already provides host-selected themes with persisted enum IDs. The preview uses Ember/Azure/Jade/Violet/Parchment IDs without renumbering. Its admin simulation stores hnmo.preview.hostTheme locally; production configuration is not modified.
- A nonempty email is not evidence of verified ownership. A four-digit secondary PIN is not MFA. Unknown/off/unsupported states must remain distinct from verified/enabled states.

- API tài khoản có dữ liệu nhân vật cơ bản, trạng thái trực tuyến và lịch sử xu; chưa đủ chi tiết nhân vật và chưa phải dịch vụ định danh thống nhất nhiều thế giới. Không ghép hai tài khoản khác nhau chỉ vì trùng tên đăng nhập.
- API bảo mật đã có lịch sử và mã/khóa vật phẩm, nhưng chưa liệt kê mọi rương phụ và phụ thuộc plug-in bảo mật. Xem tài sản thuộc sở hữu phải độc lập với việc bật tính năng bảo mật bổ sung.
- Rương mới có mã kho và mã vật phẩm bền vững; đọc dữ liệu không được tự chuyển rương đang dùng. Bản chụp cũ thiếu mã/lịch sử phải được ghi rõ.
- Nhật ký hiện mặc định chỉ ghi nhặt/rơi vật phẩm giá trị và giữ bản ghi hoạt động30ngày. Không thể suy ra lịch sử trọn đời từ một mã GUID; ép/chuyển hóa cần liên kết đầu vào/đầu ra.
- Tìm kiếm quản trị đang khớp một phần tên tài khoản. API người chơi phải kiểm tra sở hữu chính xác theo tài khoản/thế giới và phân trang, không trả ghi chú riêng hoặc JSON nội bộ.
- Dùng lại VIP, thư quà, nhiệm vụ, ký quỹ và các luồng thanh toán đã có; không tạo ví/quy tắc tài chính thứ hai.
- Giao diện do chủ server chọn trong cấu hình hiện có. Bản mẫu chỉ lưu lựa chọn mô phỏng của host trong trình duyệt; không có bộ chọn theme của người chơi.
- Có email không đồng nghĩa đã xác minh; mã PIN không phải MFA. Phân biệt rõ chưa biết, tắt và chưa hỗ trợ.

## Item passport / Hồ sơ vật phẩm

Display actual persistent ID, definition/options, location, lock/expiry and authorized retained events. Allow copying the ID into a support report. For crafted/upgraded replacements, add a separate lineage event linking input/output IDs. Never manufacture old events or expose a later owner's private activity. Default storage views are read-only; moving/selling/mixing requires separate existing gameplay safeguards.

Hiển thị mã bền vững thật, định nghĩa/tùy chọn, vị trí, khóa/hạn và sự kiện còn lưu mà tài khoản có quyền xem. Cho sao chép mã để gửi hỗ trợ. Với vật phẩm thay thế sau ép/nâng cấp, cần sự kiện liên kết mã đầu vào/đầu ra. Không dựng lịch sử cũ hoặc tiết lộ hoạt động riêng của chủ sau. Mặc định chỉ xem; chuyển/bán/ép phải qua cơ chế bảo vệ gameplay riêng.

## Five fantasy kits / Năm bộ fantasy

- Ember / Lorencia: armored fortress, gold/black, full-width battle entrance and left account rail. / Thành trì giáp trụ, vàng/đen, đại cảnh chiến trận, menu hồ sơ trái.
- Azure / Devias: ice mage/citadel, split entrance, silver/crystal panels and horizontal account navigation. / Pháp sư và thành băng, lối vào chia đôi, bảng pha lê, menu hồ sơ ngang.
- Jade / Noria: elven forest, centered sanctuary gate, portrait banner and leaf-shaped panels. / Rừng tinh linh, cổng cân giữa, khung chân dung và bảng hình lá.
- Violet / Icarus: winged warrior/sky fortress, reversed asymmetric entrance and narrow account rail. / Chiến binh có cánh, pháo đài thiên không, bố cục đảo bất đối xứng, menu hẹp.
- Parchment / Ancient continent: fantasy atlas, parchment sign-in scroll and double-bordered account ledger. / Bản đồ lục địa, khung giấy da, hồ sơ kiểu sổ hành trình viền kép.

Five original generated fantasy illustrations are optimized to WebP under `public/images/portal-themes`. No external game's logos, screenshots or assets are included. These are portal and account-design previews, not a running gameplay server. Theme differences include composition, navigation, shapes and artwork, not just accent colors.

Năm tranh fantasy được tạo mới và tối ưu WebP. Không sử dụng logo, ảnh chụp hoặc tài nguyên từ trò chơi khác. Bản xem là thiết kế cổng game và hồ sơ; không phải máy chủ gameplay đang chạy.

## Suggested additions / Đề xuất bổ sung

- Search across owned characters/items/receipts; a return-player summary based only on retained events.
- Pending/owed rewards and transaction recovery, VIP expiry and item expiry reminders.
- Optional loadout comparison and progression goals when authoritative stats are available.
- Support export with receipts/item references and no passwords/PINs.
- Account-safe session controls and optional MFA only after implementing the backend.
- Optional public character showcase separately consented; never expose the private account dashboard publicly.

Tìm xuyên suốt nhân vật/vật phẩm/biên nhận; tóm tắt khi quay lại chỉ dựa vào sự kiện có thật; nhắc thưởng chờ nhận và hạn VIP/vật phẩm; so sánh trang bị/mục tiêu khi có chỉ số chuẩn; xuất thông tin hỗ trợ không kèm bí mật; bổ sung quản lý phiên/MFA sau khi có backend; trang trưng bày nhân vật công khai phải tách riêng và do người chơi chọn bật.

## Delivery gates / Điều kiện hoàn thành

This preview is only the first reviewable design increment. Production completion requires authenticated world/account-scoped read models, ownership/privacy tests, production host-theme styling, mounting existing payment/reward flows, retention/lineage instrumentation where missing, bilingual accessibility/mobile checks, and live testing against the packaged game. Keep release work and schedules untouched.

Bản xem thử chỉ là bước thiết kế đầu tiên. Muốn hoàn thành cần API đọc xác thực đúng tài khoản/thế giới, kiểm thử riêng tư/sở hữu, áp dụng giao diện theo cấu hình host, kết nối luồng thanh toán/thưởng hiện có, ghi lịch sử còn thiếu, kiểm tra song ngữ/truy cập/điện thoại và chạy thử với game đóng gói. Không thay đổi công việc phát hành hoặc lịch tự động.

## Interactive preview expansion / Mở rộng bản xem thử tương tác

The owner approved additional tools. The preview includes 10 account sections: overview, characters, items/vaults, wallet/VIP, item event log, adventure journal, equipment comparison, security, rewards/mail and support. Interactive fixture search, watched-item passports, Sydney-date/event/character filters, pagination, filtered CSV, crafting input lineage, calculated stat comparisons and safe JSON support exports remain available. Watch state is tab-local; density is browser-local. Payment and credential submission are disabled; there is no game database connection.

Bản mẫu có 10 mục tài khoản và các công cụ tương tác với dữ liệu mẫu: tìm kiếm, hồ sơ vật phẩm, theo dõi, lọc nhật ký, xuất CSV, nguyên liệu chế tạo, so sánh chỉ số và báo cáo hỗ trợ. Không có thanh toán hoặc dữ liệu tài khoản thật. Không có mục Appearance của người chơi.

The supplied tracker page was inaccessible behind a browser challenge. Publicly indexed guide descriptions supported serial-based lifecycle tracking and detailed crafting outcomes. Only those general capabilities informed this implementation; no third-party code, branding, assets, item rates or rules were copied. The exact blocked tracker interface was not inspected.

Trang tham khảo bị chặn bởi cơ chế kiểm tra trình duyệt. Chỉ đọc được mô tả đã lập chỉ mục từ hướng dẫn công khai; không sao chép mã, thương hiệu, tài nguyên, tỷ lệ vật phẩm hoặc quy tắc của bên khác.

Hosted private preview: https://hnmo-account-hub-preview.beautifulclock2.chatgpt.site

The preview is owner-private and uses no production account data. Its source/hosting manifest is maintained separately from the HNMO game repository. The game branch contains the editable TypeScript, styles, fixtures and browser tests. Do not change the Site audience or enable automated schedules as part of an update.

Bản xem thử chỉ chủ sở hữu được truy cập, không dùng dữ liệu tài khoản thật. Không tự thay đổi quyền truy cập hoặc bật lịch tự động khi cập nhật.

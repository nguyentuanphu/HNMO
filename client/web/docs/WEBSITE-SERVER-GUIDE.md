# Live server overview / Thông tin máy chủ

This additive home section reads progression, helper, storage, party EXP, active VIP offers and currently sold catalog products from running server configuration. It keeps all original home sections. Changing the selected game server changes the displayed facts; it never selects a website theme. Values are not copied from an example server.

Mục mới trên trang chủ đọc cấu hình máy chủ đang chạy: lớp nhân vật, helper, túi/kho, EXP tổ đội, gói VIP đang bật và danh mục đang bán. Giữ nguyên các mục cũ. Chọn máy chủ chỉ đổi dữ liệu, không đổi theme. Khi mất kết nối, xóa dữ liệu/giá cũ thay vì dựng số liệu thay thế.

Host-authored lines are in Plugins → Website: Client rules (English/Vietnamese), Not for sale (English/Vietnamese). Each accepts4000 characters and one line per rule. Blank means no policy has been published, not a claim that there are no restrictions. Metadata/validation is available in English, Vietnamese, German and Simplified Chinese. Existing JSON keeps its settings and gains empty policy fields. Only these public lines cross the API; the admin path is excluded.

Host sửa các dòng công khai trong Plugins → Website: Quy định dùng client và Không bán, mỗi mục có bản Anh/Việt. Mỗi dòng là một quy định, tối đa4000 ký tự cho mỗi ô. Để trống nghĩa là chưa công bố. Không nhập mật khẩu hoặc dữ liệu tài khoản riêng vào nội dung công khai.

The public projection never receives player inventories, account records or VIP payment state. VIP offers are read-only; payment remains in the existing account flow. Bundle prices describe the entire package, not a price charged once per displayed item. The catalog is bounded to100 active products and marks truncation. Shield is described as configured class data, not certification that every combat/map rule enables it. Master tier includes the class's promotion family. Party percentages describe the whole EXP pool shared by level.

The fixed response key is cached for 15 seconds and capped at 32 running servers, 32 quests, 32 recipe results and 100 shop products per server. The bridge applies its existing visitor rate budget to this read-only GET route. No visitor cookie or Authorization header is forwarded. Offline responses clear old offers rather than leaving obsolete prices visible.

NPC quests and supported recipes come only from positive-quantity spawns on active maps. Recipes are included only when built-in handler metadata confirms the settings-based ingredient and price paths; specialized handlers are omitted instead of guessing their chance. A price-derived base chance is labelled as ingredient-dependent. An excellent roll is shown only when that handler inherits the settings-based roll; the item must support excellent options. These are configuration possibilities, not an ownership or attainability certification.

The existing upcoming-event view now formats the same timestamp in Sydney and the visitor's zone, with dates and daylight-saving transitions. Invalid or timezone-ambiguous timestamps are not replaced by the current time. The hero retains its original marketing content and adds the host brand plus the selected world's reported season.

## Host settings

Open Configuration → plug-in configuration → Website. Four new text fields publish client rules and the not-for-sale list separately in English and Vietnamese. Labels, descriptions and length validation are available in English, Vietnamese, German and Simplified Chinese. These fields are multiline, limited to 4000 characters, and rendered as text; they cannot inject markup. Existing settings and private admin paths are not returned by the public endpoint. Rates, classes, helper, storage, VIP offers, catalog, quests and recipes continue to use their original configuration pages.

## Verified increment and limits

Final frontend build/typecheck and all 40 unit cases passed, with one existing optional data-pack skip. All 9 combined browser cases passed, including English/Vietnamese at 390/1440px, server selection, host-brand/season, horizontally contained tables, injected-text safety, outage clearing and Sydney/visitor date rollover. All 14 launcher tests passed, including the real Kestrel GET-only/privacy/rate-limit check.

The final Web test run passed 1443 cases, including 12 projection/cache/privacy/locale cases and 5 multiline-field bUnit cases. The runner printed the existing conditional PostgreSQL case as skipped while its aggregate reports 0 skips. The final fresh-copy core run passed 3726 cases with 2 existing aggregate skips. Inherited repository warnings remain; no live PostgreSQL, gameplay, payment or SMTP certification is claimed.

This is a reviewable home-page increment, not completion of the broader home or 14-part website request. Still missing: comprehensive highest-obtainable gear/drop analysis and specialized recipe quotes; complete enabled-system coverage and an authoritative auto-reconnect capability source. Existing core rates, reset rules, map/drop details and feature widgets remain available. Detailed tracker, item, class/event guides and other requested pages remain separate unfinished slices. The approved profile is retained and still uses labelled fictional item-history examples until its authoritative APIs are implemented.

Đã bổ sung thông tin máy chủ từ cấu hình thực: lớp nhân vật, helper, túi/kho, EXP tổ đội, VIP, hàng đang bán, nhiệm vụ và công thức được xác minh; giờ sự kiện hiển thị cả Sydney lẫn múi giờ người xem. Quy định host nhập riêng tiếng Anh/Việt trong cấu hình Website; tên trường, hướng dẫn và kiểm tra độ dài hỗ trợ thêm tiếng Đức và Trung giản thể. Đã qua 1443 ca Web, 40 ca unit (1 bỏ qua có sẵn), 14 ca launcher và 9 ca trình duyệt. Phần trang bị có thể đạt cao nhất, công thức đặc biệt, truy vết và các trang mở rộng vẫn chưa hoàn thành; không coi đây là toàn bộ 14 phần.

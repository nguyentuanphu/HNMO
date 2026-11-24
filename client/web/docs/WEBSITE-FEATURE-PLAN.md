# Website additions / Bổ sung website

Owner revision: 22 November 2026 (Sydney). Existing features and the approved account profile design are retained. The new pages add capabilities; a theme never selects or removes features. The authoritative source is the host's saved configuration and existing game records, not example numbers from a reference site.

Yêu cầu mới: giữ tính năng hiện có và thiết kế hồ sơ đã duyệt. Các mục mới là bổ sung. Host chọn một giao diện cho toàn website; thế giới, nhân vật và tài khoản không chọn bộ tính năng hoặc giao diện riêng. Các số liệu phải đọc từ máy chủ.

Target every draft PR at the latest `claude/hnmo-main-handover-54co2m`. Never push to the base branch, remotely merge, stash or force-push. Preserve the profile design in draft #277. The independent theme foundation starts at base `477b9466`; earlier profile illustrations are reused as original presentation assets. No gameplay data is changed.

## Delivery order / Thứ tự giao

| PR slice | Scope / Phạm vi | Status / Trạng thái |
|---|---|---|
| 1 | One host theme, shared navigation/credit/language, Brand logo/colors; retain all current features. / Theme chung, điều hướng, credit, ngôn ngữ, logo/màu Brand; giữ tính năng hiện có. | Global skins/Brand foundation, five distinct structures and shared account-entry dependency validated; feature-route navigation remains with its pages. Preview ticket handoff and the host-name regression check are in review. / Nền tảng theme/Brand đã kiểm thử để review; liên kết chức năng mới đi cùng trang tương ứng. Bản sửa chuyển tiếp vé xem trước và kiểm thử tên host đang chờ duyệt. |
| 2 | Home cards from live configuration, enabled systems, VIP and catalog. / Thẻ trang chủ từ cấu hình thật, hệ thống bật, VIP và danh mục cửa hàng. | Merged into the working branch on 24 Nov 2026 (#332); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#332); chưa phát hành. |
| 3 | Missing activity events and bounded batching/retention/performance proof. / Sự kiện nhật ký còn thiếu, ghi theo lô, lưu giữ và kiểm thử hiệu năng. | Merged into the working branch on 24 Nov 2026 (#333); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#333); chưa phát hành. |
| 4 | Tracker API with host visibility/retention and player name hiding. Reuse ActivityLog and serials. / API truy vết, quyền công khai, thời hạn và ẩn tên; dùng lại nhật ký/mã vật phẩm. | Merged into the working branch on 24 Nov 2026 (#334); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#334); chưa phát hành. |
| 5 | Item history/search and integration with the approved profile. No fabricated older history. / Lịch sử/tìm vật phẩm, nối hồ sơ đã duyệt; không dựng lịch sử cũ. | Merged into the working branch on 24 Nov 2026 (#349); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#349); chưa phát hành. |
| 6 | Jewel totals including bundles, banned-account filters and hunters. / Tổng ngọc tính cả bó, lọc tài khoản bị khóa, xếp hạng săn vật phẩm. | Merged into the working branch on 24 Nov 2026 (#351); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#351); chưa phát hành. |
| 7 | PvP log with field/duel/Arena/siege distinctions. / Nhật ký PvP phân biệt ngoài map/đấu tay đôi/Arena/công thành. | Merged into the working branch on 24 Nov 2026 (#352); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#352); chưa phát hành. |
| 8 | Class guides from actual attributes, formulas and skills. / Hướng dẫn lớp nhân vật từ chỉ số, công thức và kỹ năng thật. | Merged into the working branch on 24 Nov 2026 (#335); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#335); chưa phát hành. |
| 9 | Event, invasion and quest guides from current schedules/rewards. / Hướng dẫn sự kiện, xâm lăng, nhiệm vụ từ lịch/phần thưởng thật. | Merged into the working branch on 24 Nov 2026 (#336); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#336); chưa phát hành. |
| 10 | Drop, item, requirement, box and mix guides from live data. / Hướng dẫn rơi đồ, vật phẩm, yêu cầu, hộp và ép đồ từ dữ liệu thật. | Merged into the working branch on 24 Nov 2026 (#337); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#337); chưa phát hành. |
| 11 | PvP formulas and MVP/loot examples using authoritative rules. / Công thức PvP, ví dụ chia EXP/vật phẩm theo quy tắc máy chủ. | Merged into the working branch on 24 Nov 2026 (#338); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#338); chưa phát hành. |
| 12 | Existing rankings plus opt-in public banned-character records. / Dùng lại xếp hạng; danh sách nhân vật bị khóa chỉ công khai khi host bật. | Merged into the working branch on 24 Nov 2026 (#353); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#353); chưa phát hành. |
| 13 | Editable roadmap/content/legal pages and existing shop/payment flows. / Lộ trình, nội dung, trang pháp lý do host sửa; dùng lại luồng cửa hàng/thanh toán. | Merged into the working branch on 24 Nov 2026 (#339); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#339); chưa phát hành. |
| 14 | Audit/admin integration of every new setting, in EN/VI/DE/zh-CN. Settings ship with their feature, not only at the end. / Kiểm tra tích hợp admin và bốn ngôn ngữ; tính năng có cấu hình ngay khi giao. | Merged into the working branch on 24 Nov 2026 (#354); not released yet. / Đã gộp vào nhánh chung ngày 24/11/2026 (#354); chưa phát hành. |

## Mandatory boundaries / Giới hạn bắt buộc

- Reuse ActivityLog, Economy, PlayerRanking, serial tracking, existing accounts, VIP, missions, gifts and payment rules. No second log or wallet.
- No public login names, email, IP or device identifiers. Character names require host permission and honor player opt-out; admin access remains separate.
- Never claim an ID proves lifetime history. Indicate retention and missing events.
- Every new API is read-only where specified, bounded, cached briefly and rate limited. No combat-loop synchronous logging.
- Every PR includes NUnit, web unit and desktop/phone browser results, screenshots, settings locations and genuine limitations. No fixed sleeps or weakened tests.
- Public UI is EN/VI; admin settings are EN/VI/DE/zh-CN. Brand comes from host configuration, with the small engine credit retained. Times are Sydney with visitor time when requested.

Dùng lại hệ thống sẵn có, không tạo nhật ký hoặc ví thứ hai. Không lộ thông tin định danh riêng tư. Ghi rõ dữ liệu không có hoặc đã hết hạn lưu. Mỗi phần có kiểm thử, ảnh chụp, vị trí cấu hình và giới hạn thực tế; không gọi mô phỏng là tính năng đã kết nối máy chủ.

The external tracker could not be inspected directly (403). No implemented feature is attributed to its private interface. This owner-provided specification, the repository and its real configuration are the design inputs.

Trang tham khảo không đọc trực tiếp được (403). Không coi các mô phỏng trước đó là tính năng đã lấy từ trang ấy. Nội dung yêu cầu của chủ dự án và dữ liệu thật của HNMO là cơ sở thực hiện.

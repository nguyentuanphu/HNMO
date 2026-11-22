# Live server overview / Thông tin máy chủ

This additive home section reads progression, helper, storage, party EXP, active VIP offers and currently sold catalog products from running server configuration. It keeps all original home sections. Changing the selected game server changes the displayed facts; it never selects a website theme. Values are not copied from an example server.

Mục mới trên trang chủ đọc cấu hình máy chủ đang chạy: lớp nhân vật, helper, túi/kho, EXP tổ đội, gói VIP đang bật và danh mục đang bán. Giữ nguyên các mục cũ. Chọn máy chủ chỉ đổi dữ liệu, không đổi theme. Khi mất kết nối, xóa dữ liệu/giá cũ thay vì dựng số liệu thay thế.

Host-authored lines are in Plugins → Website: Client rules (English/Vietnamese), Not for sale (English/Vietnamese). Each accepts4000 characters and one line per rule. Blank means no policy has been published, not a claim that there are no restrictions. Metadata/validation is available in English, Vietnamese, German and Simplified Chinese. Existing JSON keeps its settings and gains empty policy fields. Only these public lines cross the API; the admin path is excluded.

Host sửa các dòng công khai trong Plugins → Website: Quy định dùng client và Không bán, mỗi mục có bản Anh/Việt. Mỗi dòng là một quy định, tối đa4000 ký tự cho mỗi ô. Để trống nghĩa là chưa công bố. Không nhập mật khẩu hoặc dữ liệu tài khoản riêng vào nội dung công khai.

The public projection never receives player inventories, account records or VIP payment state. VIP offers are read-only; payment remains in the existing account flow. Bundle prices describe the entire package, not a price charged once per displayed item. The catalog is bounded to100 active products and marks truncation. Shield is described as configured class data, not certification that every combat/map rule enables it. Master tier includes the class's promotion family. Party percentages describe the whole EXP pool shared by level.

The server snapshot is cached briefly; offline responses do not retain old prices. Current work is not yet release validated. Required next checks: finish current C#/frontend build and new NUnit/unit/phone+desktop browser cases; fix diagnostics; run the full touched test projects and bridge checks. Also finish the broader home slice: enabled-system coverage, highest obtainable gear/mix rates, quest progression and the combined Sydney/visitor event-time view, reusing their authoritative guide data rather than inventing constants. The complete14-part website specification remains unfinished.

Chưa coi phần này là bản phát hành: còn kiểm thử toàn bộ dự án liên quan và phần dữ liệu trang chủ mở rộng (hệ thống bật, trang bị có thể đạt, tỉ lệ ép, tiến trình nhiệm vụ, giờ sự kiện Sydney/địa phương). Các phần truy vết vật phẩm và hướng dẫn chi tiết vẫn đang chờ triển khai.

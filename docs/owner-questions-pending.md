# Câu hỏi chờ chủ dự án (cập nhật 26 Nov 2026, 18:10 giờ Sydney)

Đã trả lời hôm nay (ghi trong `docs/owner-decisions.md`): thứ tự lô merge, PR cũ, hub #2 (chỉ review), gộp `main`,
Q1-Q13, Arena Home (#381), app Mac không công chứng.

Còn chờ anh:
1. Khi nào merge nhánh làm việc vào `main` và phát hành 1.0.1 (folder đầy đủ + phần update; chưa có host).
2. Hub PR #2 (bảng đơn licence): đã review, `tsc` và 21/21 test qua. Merge không? (Netlify có thể tự publish.)
3. Thanh toán thật (Stripe, ngân hàng): vẫn khóa?
4. PK warp: hiện PK trả gấp 50 lần phí warp (nặng hơn 2x của bộ tham chiếu). Giữ 50x hay giảm còn 2x?
5. #377 đổi luật EXP master khi ở max level: chỉ nhận EXP master khi tính năng Master Level đã mở theo Season Plan.
   Đồng ý không?
6. Gens (#386): server chưa lưu cài đặt Gens sẽ áp mặc định mới ngay (không có Gens thì không tạo được guild).
   Đồng ý áp cho server đang chạy, hay chỉ cho server mới?

Việc của riêng anh: chạy thật trên Mac Intel, Mac Apple Silicon, Ubuntu/Debian x64 và ARM64 (CI đã xanh cả 5 nền tảng
với bản #391); xóa nhánh `audit-full-2026-11-23`; sau khi merge vào `main` thì xóa `archives/MUVIETNAM2` khỏi lịch sử git.

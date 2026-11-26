# Câu hỏi chờ chủ dự án (cập nhật 27 Nov 2026, 04:20 giờ Sydney)

Đã trả lời (ghi trong `docs/owner-decisions.md`): thứ tự lô merge, PR cũ, hub #2 (chỉ review), gộp `main`, Q1-Q13,
Arena Home (#381), app Mac không công chứng; tối 26/11: giữ khóa M cho người PK, phí warp người PK x2 trong battle
zone và x50 ngoài đó, một túi zen cho mỗi trang rương, mặc định Gens cho mọi server, bỏ điều kiện Season Plan cho EXP
master.

Còn chờ anh:
1. Khi nào merge nhánh làm việc vào `main` và phát hành 1.0.1 (folder đầy đủ + phần update; chưa có host).
2. Hub PR #2 (bảng đơn licence): đã review, `tsc` và 21/21 test qua. Merge không? (Netlify có thể tự publish.)
3. Thanh toán thật (Stripe, ngân hàng): vẫn khóa?
4. Được push hai nhánh local lên và mở draft PR không: `local/econ-crash-consistency` (Jewel Bank và Leftovers lưu
   trong DB, nhập JSON có dry-run, test SIGKILL trên PostgreSQL đều qua) và `local/s9c-gameplay` (cap master, Shield-Burn
   621, đối chiếu master tree)? Anh đã dặn hai phần này chỉ làm local.
5. Skill 618-621: người chơi học bằng cách nào (dữ liệu HNMO chưa có nguồn học)? Và hiệu ứng của 618-620 là gì?
6. Có làm Muun và Pentagram/Errtel không? Cần packet mới, ô trang bị, giao diện desktop và web.
7. Weapon Mastery 571: server cho 20 cấp, master tree trên web 10 cấp. Theo bên nào?
8. 10 cài đặt S9 trong Admin chưa có tác dụng khi chơi: ẩn đi hay giữ?
9. 30 buff chỉ có tiếng Việt và 15 mô tả item thiếu "vi": em tự viết bản dịch, hay anh có văn bản?

Việc của riêng anh: chạy thật trên Mac Intel, Mac Apple Silicon, Ubuntu/Debian x64 và ARM64; xóa nhánh
`audit-full-2026-11-23`; sau khi merge vào `main` thì xóa `archives/MUVIETNAM2` khỏi lịch sử git; nếu tiện, tắt phần
mềm lọc mạng/VPN một lần rồi chạy self-check pipe để xác nhận nguyên nhân (#406: máy Windows sạch của GitHub đã xanh).

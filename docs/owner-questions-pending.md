# Câu hỏi chờ chủ dự án (cập nhật 27 Nov 2026, 19:10 giờ Sydney)

Đã trả lời (ghi trong `docs/owner-decisions.md`): thứ tự lô merge, PR cũ, hub #2, Q1-Q13, Arena Home (#381), app Mac
không công chứng; tối 26/11: khóa M cho người PK, phí warp PK, túi zen mỗi trang rương, Gens mặc định, bỏ Season Plan
cho EXP master; popup sáng 27/11: push hai nhánh local, 618-621 học qua master tree, Muun và Pentagram làm đủ, Weapon
Mastery 571 theo desktop, nối 10 cài đặt S9, em viết bản dịch, hub PR #2 merge, thanh toán "Chuẩn bị mở", luật e2e,
GPU FPS không cần, chỉ Mac, rút ngắn thời gian chờ kết nối, chi phí và bonus Muun, Pentagram theo desktop S9 + Admin,
dữ liệu #403 rà lại theo desktop, từ ngữ tiếng Việt như game Việt phổ biến.

Còn chờ anh:
1. Gộp nhánh làm việc vào `main` và phát hành 1.0.1: anh đã nói "Chờ anh báo".
2. Luật PK: anh đã chọn (27/11 17:45) "chơi S6 thì luật S6, chơi S9 thì luật S9". Antigravity làm
   (`docs/antigravity-prompt-2026-11-27-1750.md`, mục 7); em review và merge.
3. Cửa sổ chat/messenger: anh chọn (27/11 19:00) cho kéo đổi kích thước, ghi là tính năng thêm (Antigravity mục 12).

Việc của riêng anh: chạy thật trên Mac Intel, Mac Apple Silicon, Ubuntu/Debian x64 và ARM64; xóa nhánh
`audit-full-2026-11-23`; sau khi merge vào `main` thì xóa `archives/MUVIETNAM2` khỏi lịch sử git; nếu tiện, tắt phần
mềm lọc mạng/VPN một lần rồi chạy self-check pipe để xác nhận nguyên nhân (#406: máy Windows sạch của GitHub đã xanh).

Đã trả lời tối 27/11 (popup): luật PK theo từng season; số kỹ năng theo bộ server khi khác client desktop; 263/559/336
đối chiếu bộ server trước; PK Arena: trong event không phạt, ngoài event phạt theo S6 (đã đúng); #431 merge cả tài
liệu, CSV và script; PR desktop merge hết (#420, #432 đã merge; phần desktop của #441 giữ); đóng PR cũ sau khi kiểm
tra (#401, #402, #403, #409, #13, #17 đã đóng); chat đổi kích thước được.

#399: anh chọn merge (đã merge 504fa615). 19:55: 618-621 là phép event/siege như S9 gốc (không vào cây Master);
gói /download vẫn công khai (lớp 1 chỉ khóa tự cập nhật; hub#6 đã merge 22b6f50f). Antigravity hết usage: việc
còn lại giao local Claude (docs/local-claude-prompt-2026-11-27-2000.md).

27/11 22:40: anh yêu cầu màn login/chọn server/chọn nhân vật giống client desktop (cảnh 3D). Prompt cho local Claude:
`docs/local-claude-prompt-2026-11-27-login-character-scenes.md` (làm trước cửa sổ chat đổi kích thước).

28/11 04:10: anh trả lời popup về main: CHƯA gộp main, anh tự kiểm tra thêm. Bản 1.0.1 CÓ Season 9: đợi local Claude xong
các việc (cảnh đăng nhập/chọn nhân vật 3D, báo lỗi tự động, cửa sổ chat đổi kích thước, #444, #439) rồi gộp hết một lần
(#419 cùng các PR của local), chạy lại CI đầy đủ, rồi hỏi anh lại trước khi đụng main.
Nhánh làm việc 5de17640 (có #450, #408, #451, #442...): CI sạch; lỗi nền duy nhất: walk-resume.spec (shard 7) đỏ ở mọi
nhánh từ 26/11 - cần một việc sửa riêng.

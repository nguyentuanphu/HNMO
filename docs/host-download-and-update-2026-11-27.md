# Host tải HNMO và cập nhật thế nào (27 Nov 2026, 20:50 Sydney)

## Host mới
1. Vào hub https://hnmobug.netlify.app, bấm "Download HNMO" (`/download`). Hub chuyển sang file
   `releases/<phiên bản>/HNMO.zip` trên bucket R2 của bản anh đã phát trong console (Security). Hiện là 1.0.0 (21/11).
2. Giải nén, chạy `Start HNMO.bat` (Windows) hoặc `start-hnmo.sh` (Mac/Linux). Mặc định cho 10 người cùng lúc.
3. Muốn nhiều hơn: trong Admin của họ gửi yêu cầu license; anh cấp trong console hub; server tự nhận `license.hnmo`.

## Host đang chạy, khi có bản mới
- Phía anh: gộp nhánh làm việc vào `main` (có `VERSION` mới, #449) → workflow "HNMO package" build, tải các file đổi lên
  R2 (`files/<sha256>`, `releases/<version>/manifest.json`, `HNMO.zip`) → anh vào console hub, Security, phát phiên bản
  mới.
- Phía host: Admin → "HNMO update" → "Update to <version>". Server hỏi hub (`/api/update-check`, kèm license nếu có,
  không có thì như host miễn phí), chỉ tải file đổi qua `/api/update-file`, sao lưu save, dừng, thay file, khởi động lại;
  không lên thì tự quay về bản cũ và save cũ. Log: `save/logs/update.log`.
- Bị chặn: license bị thu hồi / của máy khác / sai chữ ký. Host miễn phí vẫn cập nhật (anh chọn 27/11).

## Lưu ý một lần
Bản 1.0.0 dùng cách cập nhật cũ (`manifest_url`/`files_url`), hub đã bỏ cách đó (hub#6). Thư mục 1.0.0 không tự lên
1.0.1 được: tải lại gói 1.0.1 một lần (giữ thư mục `save/` của bản cũ). Từ 1.0.1 trở đi nút Update chạy theo cách mới.

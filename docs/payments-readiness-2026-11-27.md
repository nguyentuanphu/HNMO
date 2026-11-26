# Chuẩn bị mở thanh toán thật (27/11/2026, giờ Sydney)

Anh chọn "Chuẩn bị mở": đây là danh sách việc, **chưa bật gì**. Hiện mọi luồng đều ở chế độ thử.

## Đang có gì

| Luồng | Ở đâu | Trạng thái |
|---|---|---|
| Bán giấy phép HNMO (Stripe) | hub `hnmobug`: `netlify/lib/licence-purchase.mts`, `licence-stripe`, bảng đơn trong console | **Khóa cứng chế độ thử**: chỉ nhận khóa `sk_test_`/`rk_test_`, phiên `cs_test_`, sự kiện `livemode=false`. Đơn đã trả tiền thử phải xem tay trước khi cấp giấy phép. |
| Nạp coin trong game qua ngân hàng (Casso, SePay) | server: `API/TopUpController.cs`, `Services/BankTopUpService.cs`, plug-in `BankTopUpPlugIn` | Webhook thật của Casso/SePay đã có: mỗi giao dịch chỉ cộng một lần (mã giao dịch ngân hàng là duy nhất). Bật/tắt và tỉ giá trong plug-in. |
| Nạp coin qua Stripe trên website | server: `API/WebsiteTestTopUpController.cs` | **Chỉ chế độ thử** (chỉ nhận sự kiện Stripe test có chữ ký). |

## Việc phải làm trước khi bật

**A. Giấy tờ và tài khoản (anh làm)**
1. Tài khoản Stripe ở chế độ thật: xác minh doanh nghiệp/cá nhân, tài khoản nhận tiền, ABN nếu bán ở Úc.
2. Thuế: GST (Úc) nếu doanh thu đạt ngưỡng; với khách Việt Nam, hỏi kế toán về hóa đơn.
3. Trang điều khoản, chính sách bảo mật, chính sách hoàn tiền trên website (Stripe yêu cầu).
4. Với Casso/SePay: hợp đồng tài khoản ngân hàng thật, khóa webhook mới (không dùng lại khóa thử).

**B. Code (em và các agent làm, mỗi việc có test)**
1. Hub: thêm công tắc "chế độ thật" trong console, mặc định tắt; chỉ khi bật mới nhận `sk_live_`/`cs_live_` và sự kiện `livemode=true`. Giữ kiểm tra chữ ký webhook và chống xử lý trùng.
2. Hub: khi đơn thật đã trả tiền thì tự cấp giấy phép (ký bằng khóa trên máy anh, như hiện nay), có ghi lịch sử; hoàn tiền hoặc tranh chấp thì tự thu hồi và báo anh.
3. Server: nạp coin qua Stripe thật trên website giống bên hub (công tắc mặc định tắt, chữ ký, mã đơn duy nhất); nối với #423 để mỗi lần nạp là một biên nhận trong DB.
4. Đối soát: trang Admin so tổng tiền Stripe/ngân hàng với tổng coin đã cộng mỗi ngày (giờ Sydney), báo chỗ lệch.
5. Giới hạn và chống gian lận: số tiền tối thiểu/tối đa mỗi lần, mỗi ngày, mỗi tài khoản (chỉnh trong Admin).

**C. Thử trước khi mở cho người chơi**
1. Chạy toàn bộ luồng ở chế độ thử: trả tiền, nhận coin hoặc giấy phép, hoàn tiền, tranh chấp, webhook gửi lặp, webhook mất.
2. Một giao dịch thật nhỏ (vd. 1 AUD hoặc 10.000 đ) với tài khoản của anh, rồi hoàn tiền.
3. Cách quay lại: tắt công tắc là về chế độ thử ngay, không mất đơn đang xử lý.

## Cần anh quyết khi muốn bật
- Bật cho luồng nào trước: giấy phép (hub), nạp coin qua ngân hàng hay nạp coin qua Stripe.
- Tỉ giá coin và các mức giới hạn.
- Có cần hóa đơn cho khách không.

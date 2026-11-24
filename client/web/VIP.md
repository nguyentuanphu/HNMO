# VIP host instructions / Hướng dẫn quản trị VIP

## English

1. Sign in as an administrator and open **Accounts → VIP**, or `/vip`. Open **How to use this page** for the short explanation. New purchases start off. Save settings once to register/activate the plug-in before granting time; purchases may stay off.
2. Set English and Vietnamese level names, XP and Zen bonus percentages, the daily coin currency/amount, and each duration/price. Save before granting: host grants use saved benefits, not unsaved edits. The chat badge has its own switch. Turning new purchases off preserves already purchased benefits and badges until expiry.
3. Players review offers in the account page or **Alt+U** / **More → VIP** in the web game. The website requires them to leave the game before buying or claiming. Renewing the same level adds time. A different active level requires host removal first.
4. To grant time without charging coins, enter the exact account login, saved level, number of days and reason. The account must be offline. Review, then confirm. To remove VIP, use the removal action and enter a reason; only the three proved grants belonging to this feature are expired. Foreign or unproved grants require separate host review.
5. If an operation is pending, retry the same operation. Do not create a replacement transaction. Recovery reuses its durable proof. Staff identity/reason appear in the administration activity log; player coin history receives only a translated purchase/daily/removal label.
6. The list uses a paged subscription index; exact account search also checks the actual database proof. After restoring an older state file, an account may be absent from the index until checked. Daily coins are once per account/Sydney calendar day, using the purchased benefits. A full balance refuses a new claim instead of losing the excess.

The timed grants are `IsVip`, `BonusExperienceRate` and `MoneyAmountRate`, with one shared expiry. Existing host-configured VIP extra-vault/reset policies read that same VIP flag; their rules remain in the existing gameplay/reset settings. This feature does not add per-tier item-drop rates, a new free-reset allowance or per-tier vault counts. VIP-only servers and per-server drop/Zen rates are separate ROADMAP 15.6 work.

State uses the existing `vip` section in `save/hnmo-state.json`; account balances, receipts and grants remain in the account database. Never delete or manually edit payment proofs to resolve a pending operation. The read-only administrative MCP tool is `vip_list`; mutations require the interactive administrator confirmation flow.

Tests cover uncertain saves, replay, ownership, grant expiry, daily limits, role/confirmation/offline checks, audit failure, bilingual UI and packet handling. Real PostgreSQL transaction/crash behavior, multiple host-process coherence, real SMTP delivery and live payment/game deployment have not been certified. Cold receipt recovery has a 20,000-row bound; a host must investigate accounts exceeding it. Required dependencies are integration #51 and account #29. The original per-character last-seen account-page gap remains separate.

## Tiếng Việt

1. Đăng nhập bằng tài khoản quản trị, mở **Tài khoản → VIP** hoặc `/vip`. Nhấn **Cách sử dụng trang này** để xem hướng dẫn ngắn. Mua mới tắt mặc định. Lưu cấu hình một lần để bật plug-in trước khi cấp thời gian; vẫn có thể tắt mua mới.
2. Nhập tên hạng bằng tiếng Anh và tiếng Việt, phần trăm thưởng kinh nghiệm/Zen, loại và số coin hằng ngày, cùng thời hạn/giá bán. Lưu trước khi cấp: quyền lợi được lấy từ cấu hình đã lưu. Huy hiệu chat có công tắc riêng. Tắt mua mới không làm mất quyền lợi đã mua hoặc huy hiệu trước khi hết hạn.
3. Người chơi xem lại gói trên trang tài khoản hoặc **Alt+U** / **Thêm → VIP** trong game web. Khi thao tác trên website, cần thoát game trước khi mua hoặc nhận quà. Gia hạn cùng hạng cộng thêm thời gian; muốn đổi hạng đang còn hạn, quản trị viên cần gỡ hạng cũ trước.
4. Để cấp thời gian miễn phí, nhập đúng tên đăng nhập, hạng đã lưu, số ngày và lý do. Tài khoản phải ngoại tuyến. Xem lại rồi xác nhận. Khi gỡ VIP, cũng phải nhập lý do; chỉ ba quyền lợi có bằng chứng thuộc tính năng này bị kết thúc. Quyền lợi từ nguồn khác hoặc thiếu bằng chứng cần được quản trị viên kiểm tra riêng.
5. Giao dịch đang chờ phải được thử lại bằng chính giao dịch đó, không tạo giao dịch thay thế. Khôi phục dùng lại bằng chứng đã lưu. Tên quản trị viên/lý do nằm trong nhật ký quản trị; lịch sử coin của người chơi chỉ hiển thị nhãn mua/nhận quà/gỡ VIP đã dịch.
6. Danh sách dùng chỉ mục phân trang; tìm đúng tên tài khoản sẽ kiểm tra bằng chứng trong cơ sở dữ liệu. Sau khi phục hồi tệp trạng thái cũ, tài khoản có thể chưa xuất hiện cho đến khi được kiểm tra. Quà coin nhận một lần cho mỗi tài khoản/ngày Sydney theo quyền lợi đã mua. Nếu số dư đã đầy, yêu cầu mới bị từ chối để không mất phần vượt mức.

Ba quyền lợi `IsVip`, `BonusExperienceRate` và `MoneyAmountRate` dùng chung hạn kết thúc. Chính sách kho thêm/reset dành cho VIP đã có đọc cùng cờ VIP; tiếp tục cấu hình tại các trang luật chơi/reset hiện có. Tính năng này không thêm tỷ lệ rơi đồ theo hạng, lượt reset miễn phí mới hoặc số kho riêng theo hạng. Máy chủ VIP và hệ số rơi đồ/Zen từng máy chủ thuộc mục 15.6.

Trạng thái nằm trong mục `vip` của `save/hnmo-state.json` dùng chung; số dư, biên nhận và quyền lợi vẫn nằm trong cơ sở dữ liệu tài khoản. Không xóa hoặc sửa tay bằng chứng thanh toán để xử lý giao dịch chờ. Công cụ MCP `vip_list` chỉ đọc; thay đổi cần quy trình xác nhận trực tiếp của quản trị viên.

Đã kiểm thử lưu chưa rõ kết quả, thử lại, quyền sở hữu, hết hạn, giới hạn ngày, quyền quản trị, xác nhận, ngoại tuyến, lỗi nhật ký, giao diện hai ngôn ngữ và gói tin. Chưa chứng nhận PostgreSQL khi gặp sự cố, nhiều tiến trình máy chủ dùng chung trạng thái, gửi SMTP thực tế hoặc triển khai thanh toán/game trực tiếp. Khôi phục lạnh giới hạn 20.000 biên nhận; tài khoản vượt mức cần được kiểm tra. Cần nền tích hợp #51 và trang tài khoản #29. Thời điểm hoạt động gần nhất theo từng nhân vật vẫn là hạn chế riêng của trang tài khoản.

## Test-only payments

Use a demo server. In **Top-up milestones**, save the coin packages and Sydney-day order limit. Package prices are whole VND.
Enable the simulated bank transfer, Stripe test cards, or both. The simulated QR uses fictional bank information. Compare its
reference and amount with the displayed order before simulating confirmation. This uses the existing cash-shop balance.
Players can then review the existing VIP offers from the account page.

For Stripe, set the website's exact HTTPS origin, a Stripe test API key, and the test signing secret. In Stripe test
mode, point the webhook at `/test-topup/stripe/webhook` on that origin. Select the Checkout completion, async payment success,
charge refund, and charge dispute creation events. Test using Stripe test cards. A browser returning from Checkout does not
confirm payment. A verified event must match the order session and total before crediting. Review flagged refunds and disputes
from the test order list. Live checkout remains locked.

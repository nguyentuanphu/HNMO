# VIP rules foundation

This directory is not a registered gameplay plug-in yet. It exposes no purchase endpoint,
packet handler, admin page or client window. A quote does not debit coins, and returned
benefit values do not apply character attributes. Do not present this foundation as a
working VIP shop.

The host configuration defaults to disabled and supplies bilingual Bronze/Đồng,
Silver/Bạc and Gold/Vàng levels with configurable 7/30/90-day offers. Quotes reject an
unknown level/duration, invalid configuration, insufficient balance or an implicit change
to another active level. Extending the same active level preserves its start and adds days
to its expiry; a new subscription after expiry starts from the new quote's time. Start is
inclusive, expiry exclusive, and rule timestamps must be UTC.

`VipStateStore` uses the `vip` section of `HnmoStateStore`. Production callers must obtain
the backend through `HnmoStateStore.Open` for `save/hnmo-state.json`, and reuse
`VipStateStore.Open(backend)`. There is no separate VIP file. An invalid existing section
fails closed instead of silently discarding subscriptions. Other sections remain intact.

`RecordConfirmedPayment` is bookkeeping for a trusted server adapter, not payment
verification. The adapter MUST authenticate the player, serialize operations per account,
validate the exact quote, persist a recovery journal before a debit, use a durable
idempotent wallet receipt, and recover interrupted fulfillment before exposing purchases.
A stale quote is rejected and an unchanged receipt cannot extend twice or resurrect a
removed membership. Never call it merely because a browser supplied a receipt id.
The foundation does not yet implement this wallet/journal adapter.

The eventual account-attribute projection must use the same subscription start/expiry,
including `Stats.IsVip` for server join requirements, and apply/remove only grants owned
by this feature. Experience and zen bonus values have existing server attributes; gameplay
application, expiry notices, chat marks and any other benefit still need implementing.
Do not invent item-drop, extra-vault or free-reset support without checking the server.

Daily bonus bookkeeping is once per Sydney calendar day. The wallet grant must be
idempotent by account/day, with its original timestamp retained for recovery. Removing
and regranting VIP does not reset the daily claim history. These methods do not grant coins.
Administrative rule changes require an actor/reason and keep an operational audit; they
are not a substitute for the admin authorization/audit operations layer.

## Trạng thái triển khai

Đây mới là phần quy tắc và lưu trạng thái VIP, chưa phải plug-in đang hoạt động. Chưa có
mua VIP, trừ coin, áp dụng chỉ số, trang quản trị, cửa sổ game hoặc mục website. Cấu hình
mặc định tắt. Dữ liệu nằm trong mục `vip` của tệp trạng thái HNMO dùng chung, không tạo
tệp VIP riêng. Gia hạn cộng vào thời hạn còn lại; hết hạn thì quyền lợi không còn được
trả về. Mốc nhận quà hằng ngày tính theo ngày Sydney. Cần hoàn thiện xác thực, giao dịch
coin có thể khôi phục, áp dụng chỉ số tài khoản và giao diện trước khi mở cho người chơi.

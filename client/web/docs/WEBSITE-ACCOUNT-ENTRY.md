# One website account entry / Một cửa vào tài khoản

The Account menu opens `/account.html`, not a form midway down the home page. A visitor sees sign-in first, with a registration action and Google creation when the host configured Google. The duplicate home registration/Google forms and standalone header registration action are removed; those capabilities remain on the account screen. Rewards, gift box, reset, safety, VIP and history panels are hidden until the server verifies the account.

Menu Tài khoản mở trang đăng nhập riêng, không kéo xuống giữa trang chủ. Nếu chưa có tài khoản, chọn Tạo tài khoản hoặc dùng Google khi host đã cấu hình. Các mục quà, hộp thư, reset, bảo mật, VIP và lịch sử chỉ hiện sau đăng nhập. Giữ chức năng đăng ký và khôi phục mật khẩu qua Google, không tạo nhà cung cấp đăng nhập giả.

## How the session works / Cách giữ phiên đăng nhập

The bridge verifies the initial password with the existing account-overview API before issuing an opaque random HttpOnly, SameSite=Strict cookie. The bounded, process-memory store holds the server-side credential adapter for existing handlers; it is not a new account database, wallet, plug-in state file or persistent password store. Sessions expire after one idle hour or eight hours absolute, and bridge restart clears them. No password or login ticket goes into localStorage/sessionStorage. The old feature-form identity fields are stripped from browser requests; the cookie binds them to the verified account on the bridge. Every operation still goes through the existing server password/ban/ownership/offline checks.

Bridge kiểm tra mật khẩu với API tài khoản đang có rồi cấp cookie HttpOnly chỉ chứa mã ngẫu nhiên. Phiên được giữ có giới hạn trong bộ nhớ bridge, tối đa tám giờ và hết hạn sau một giờ không dùng; khởi động lại bridge sẽ cần đăng nhập lại. Không lưu mật khẩu hoặc vé game trong localStorage/sessionStorage. Máy chủ vẫn kiểm tra tài khoản và quyền sở hữu ở mỗi thao tác; cookie không bỏ qua kiểm tra đó.

Cookie-authenticated mutations require an explicit request marker and matching origin/fetch-site checks. Client-supplied account/password fields cannot select another account. HTTPS uses Secure cookies. HTTPS termination may forward protocol/host only from a loopback proxy; headers from remote addresses are ignored. No change to global forwarded-IP or host-only administration rules. Responses are no-store; logout invalidates the bridge token, and server401 revokes it. Broadcast messages update other account tabs; bfcache restores recheck the server before showing private panels.

Thao tác dùng cookie cần đúng nguồn website và dấu hiệu yêu cầu rõ ràng. Không thể đổi sang tài khoản khác bằng cách sửa dữ liệu gửi lên. Cookie dùng Secure trên HTTPS; chỉ proxy cục bộ được cung cấp thông tin giao thức/host chuyển tiếp. Đăng xuất và lỗi xác thực từ máy chủ thu hồi phiên; các thẻ tài khoản khác cập nhật theo, trang khôi phục từ lịch sử kiểm tra lại phiên trước khi hiện dữ liệu.

## Enter game / Vào game

Enter game checks the existing website session, requests the existing single-use game ticket (with device ID), and opens the original game entry with the ticket fragment. Signed-out players go through the same account sign-in screen once and then continue. A temporary session-check failure asks to retry and does not force another password prompt. Existing legacy `#gift`, `#safety`, `#play` links resolve to the correct private sections. The game consumes/removes the ticket fragment through its unchanged bootstrap. No new packet or gameplay handler is introduced.

Nút Vào game dùng phiên website và vé game một lần đang có. Nếu chưa đăng nhập, chỉ hỏi ở màn hình tài khoản rồi tiếp tục vào game. Nếu lỗi mạng khi kiểm tra phiên, trang yêu cầu thử lại thay vì bắt nhập mật khẩu lại. Các liên kết cũ vẫn dẫn đúng mục riêng tư. Không đổi gói tin hoặc luật game.

Website logout does not kick an already connected game character; the game's existing logout remains separate. The host's existing `RequireWebsiteLogin` setting still governs manual legacy game-login access. The new normal Enter game flow uses the website session/ticket in both cases.

Đăng xuất website không tự đá nhân vật đã kết nối vào game; dùng chức năng thoát game hiện có. Cài đặt RequireWebsiteLogin đang có vẫn kiểm soát đường đăng nhập game cũ; nút Vào game thông thường luôn dùng phiên website và vé.

## Settings and review / Cấu hình và review

Google still uses the existing admin Plug-ins → Website → Google client ID setting. No new operator setting, payment provider or admin resource key. Public new copy is English/Vietnamese. A floating Home link is added to every existing public website page and is hidden during active gameplay to avoid covering its controls. The approved profile design and the structural theme changes remain in their separate work; this slice changes authentication/navigation and preserves every account capability.

Google dùng cấu hình Google client ID hiện có trong admin. Không thêm nhà cung cấp thanh toán hay cài đặt host mới. Văn bản mới có EN/VI; các trang công khai có nút nổi về trang chủ. Giữ bản profile đã duyệt và các chức năng tài khoản; thay đổi theme/bố cục được review riêng.

Screenshots in `screenshots/account-flow` show the sign-in, registration and authenticated profile states in EN/VI at390px and1440px. They use fictional fixture accounts and are not live financial/gameplay proof.

## Verified checks and limits

22 November 2026: bridge Release build passed (one inherited VisitorLimits warning); all26 launcher tests passed, including real cookie round trips, account substitution, CSRF, proxy trust, expiry and stale-response/sign-in races. Client build/typecheck and35 unit tests passed, with one existing optional data-pack skip. The affected browser suite passed55 cases; three existing game-server-dependent cases were skipped because no live game server was running (account safety, gift codes and reset/relife). Two ticket cases exercised the actual game bootstrap with a fixture ticket response. Twelve screenshots cover EN/VI sign-in, registration and profile at390/1440px.

No live account database, payment, SMTP delivery or real game-server ticket consumption was certified. Fixtures contain fictional accounts; no real credentials are recorded. The bridge intentionally retains the existing server credential adapter in bounded process memory; a process restart expires these sessions. Cross-tab logout revokes website sessions without disconnecting an already connected game character.

Đã kiểm tra build, kiểu dữ liệu,26 kiểm thử bridge,35 kiểm thử đơn vị và55 tình huống trình duyệt. Ba tình huống cũ cần máy chủ game thật không chạy trong môi trường này. Chưa chứng nhận thanh toán, gửi email, dữ liệu tài khoản thật hoặc vé vào máy chủ game thật.

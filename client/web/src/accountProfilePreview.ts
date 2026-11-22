import { gameName, loadGameName } from './gameName.js';
import { itemExplorer, eventLog, equipmentComparison, adventureJournal, supportReport, returnSummary, expiryReminders, globalSearch, sessionPreview, densityControl } from './accountHubTools.js';

// An isolated design preview. All account examples below are fictional; no account API or payment is called.
const text = {
  preview: ['Design preview · fictional account data', 'Bản xem thử thiết kế · dữ liệu tài khoản giả lập'],
  game: ['Game login', 'Đăng nhập game'], website: ['Website sign-in', 'Đăng nhập website'], profile: ['Account hub', 'Trung tâm tài khoản'],
  home: ['Overview', 'Tổng quan'], characters: ['Characters', 'Nhân vật'], items: ['Items & vaults', 'Vật phẩm & rương'],
  activity: ['Activity & trades', 'Hoạt động & giao dịch'], wallet: ['Wallet & VIP', 'Ví & VIP'], security: ['Account security', 'Bảo mật tài khoản'],
  journal: ['Adventure journal', 'Nhật ký hành trình'], compare: ['Compare equipment', 'So sánh trang bị'], support: ['Support', 'Hỗ trợ'],
  rewards: ['Rewards & mail', 'Phần thưởng & thư'], appearance: ['Appearance', 'Giao diện'],
  welcome: ['Your next adventure starts here.', 'Hành trình tiếp theo bắt đầu tại đây.'],
  welcomeSub: ['Choose your world. Your characters, rewards and account stay within reach.', 'Chọn thế giới. Theo dõi nhân vật, phần thưởng và tài khoản ngay tại đây.'],
  account: ['Account name', 'Tên tài khoản'], password: ['Password', 'Mật khẩu'], signIn: ['Sign in', 'Đăng nhập'],
  enter: ['Explore the account preview', 'Khám phá trang tài khoản mẫu'],
  previewHint: ['Open this design with fictional data; no sign-in or payment takes place.', 'Mở thiết kế với dữ liệu giả lập; không đăng nhập hay thanh toán.'],
  sampleOnly: ['Sample account · no live connection', 'Tài khoản mẫu · chưa kết nối máy chủ'],
  yourWorlds: ['Choose a world', 'Chọn thế giới'], s6: ['Season 6.3', 'Season 6.3'], s9: ['Season 9', 'Season 9'],
  available: ['Availability comes from the server', 'Tình trạng được lấy từ máy chủ'],
  announcements: ['News & upcoming events', 'Tin tức & sự kiện sắp tới'], newsExample: ['Example: weekend experience event', 'Ví dụ: sự kiện tăng kinh nghiệm cuối tuần'],
  guide: ['New player guide', 'Hướng dẫn người chơi mới'], recovery: ['Account recovery', 'Khôi phục tài khoản'],
  private: ['One account. A clearer picture.', 'Một tài khoản. Mọi thông tin rõ ràng.'],
  privateSub: ['Your characters, belongings, transactions and account protection in one private place.', 'Nhân vật, tài sản, giao dịch và bảo mật trong một trang riêng tư.'],
  overviewTitle: ['Welcome back, Explorer.', 'Chào mừng Explorer trở lại.'],
  overviewSub: ['See what needs attention before your next session.', 'Xem những việc cần chú ý trước khi vào game.'],
  attention: ['Needs your attention', 'Cần chú ý'], vipExpiring: ['Example: VIP expires in 3 days', 'Ví dụ: VIP còn 3 ngày'],
  mailWaiting: ['Example: 2 gifts are waiting to be collected', 'Ví dụ: 2 món quà đang chờ nhận'],
  protection: ['Protection checklist', 'Kiểm tra bảo mật'], pin: ['Secondary PIN enabled', 'Đã bật mật khẩu cấp hai'],
  email: ['Email verification: not available in current data', 'Xác minh email: dữ liệu hiện tại chưa có'],
  lastLogin: ['Recent login history', 'Lịch sử đăng nhập gần đây'], securityNote: ['Security status reflects the information available to this server.', 'Trạng thái bảo mật dựa trên thông tin máy chủ hiện có.'],
  balances: ['Account balances', 'Số dư tài khoản'], vipPlan: ['VIP membership', 'Gói VIP'],
  sampleVip: ['Example: Silver · 3 days remaining', 'Ví dụ: Bạc · còn 3 ngày'],
  topup: ['Top up coins', 'Nạp xu'], renew: ['Review VIP plans', 'Xem các gói VIP'], noPayments: ['Preview only. No money or coins will move.', 'Chỉ xem thử. Không chuyển tiền hay xu.'],
  yourCharacters: ['Your characters', 'Nhân vật của bạn'], characterNote: ['Group characters by world and season, with saved/live timestamps.', 'Nhóm nhân vật theo thế giới và mùa, ghi rõ thời điểm dữ liệu lưu hoặc trực tiếp.'],
  name: ['Character', 'Nhân vật'], world: ['World', 'Thế giới'], level: ['Level / resets', 'Cấp / reset'], location: ['Location', 'Vị trí'],
  knight: ['Knight', 'Chiến binh'], mage: ['Mage', 'Pháp sư'], offline: ['Offline · sample', 'Ngoại tuyến · mẫu'],
  gear: ['Equipment, bags and every vault', 'Trang bị, túi đồ và tất cả rương'],
  itemNote: ['Search owned items by name or trace ID. Opening an item shows only authorized recorded events.', 'Tìm vật phẩm theo tên hoặc mã truy vết. Chỉ xem các sự kiện đã ghi nhận mà tài khoản được phép xem.'],
  search: ['Search sample item name or trace ID', 'Tìm tên vật phẩm hoặc mã truy vết mẫu'],
  sword: ['Example sword +9', 'Kiếm mẫu +9'], jewel: ['Example jewel', 'Ngọc mẫu'], ring: ['Example ring', 'Nhẫn mẫu'],
  bag: ['Explorer · bag', 'Explorer · túi đồ'], vault: ['Vault 2', 'Rương 2'], equipped: ['Explorer · equipped', 'Explorer · đang mặc'],
  trace: ['Trace ID', 'Mã truy vết'], where: ['Current location', 'Vị trí hiện tại'], item: ['Item', 'Vật phẩm'],
  traceTitle: ['Recorded item history', 'Lịch sử vật phẩm đã ghi nhận'],
  acquired: ['Example: acquired by this account', 'Ví dụ: tài khoản nhận vật phẩm'], stored: ['Example: moved to current storage', 'Ví dụ: chuyển đến kho hiện tại'],
  missing: ['Earlier history unavailable. An ID is not proof of a complete lifetime history.', 'Chưa có lịch sử trước đó. Có mã vật phẩm không có nghĩa là có đủ lịch sử từ lúc tạo.'],
  events: ['Your activity timeline', 'Dòng thời gian hoạt động'], eventNote: ['Filter by character, date, item and event type. Display only retained records involving this account.', 'Lọc theo nhân vật, ngày, vật phẩm và loại sự kiện. Chỉ hiện bản ghi còn lưu có liên quan đến tài khoản này.'],
  tradeExample: ['Example: completed player trade', 'Ví dụ: hoàn tất giao dịch với người chơi'], rewardExample: ['Example: claimed a daily reward', 'Ví dụ: nhận thưởng hằng ngày'],
  ledger: ['Receipts and balance history', 'Biên nhận và lịch sử số dư'], separateCredit: ['Trade credit stays separate from ordinary game coins.', 'Tín dụng giao dịch tách riêng với xu trong game.'],
  ledgerExample: ['Example: VIP purchase · −100 WCoinC', 'Ví dụ: mua VIP · −100 WCoinC'],
  accountSafety: ['Protect your account', 'Bảo vệ tài khoản'], history: ['Logins & devices', 'Đăng nhập & thiết bị'],
  safetyDetail: ['Masked login addresses, failed attempts, secondary PIN, item locks and recovery settings. Device controls appear only when the server supports them.', 'Địa chỉ đăng nhập đã che, lần đăng nhập thất bại, mật khẩu cấp hai, khóa vật phẩm và khôi phục. Chỉ hiện quản lý thiết bị khi máy chủ hỗ trợ.'],
  rewardDetail: ['Daily missions, login calendar, referrals, gifts and top-up milestone rewards.', 'Nhiệm vụ hằng ngày, lịch điểm danh, giới thiệu bạn bè, thư quà và mốc nạp.'],
  empty: ['No matching sample items', 'Không tìm thấy vật phẩm mẫu'],
  themeTitle: ['Make this space yours', 'Chọn phong cách của bạn'],
  themeNote: ['Five distinct layouts, typography and surfaces. Production preferences should follow your account across devices.', 'Năm phong cách khác nhau về bố cục, chữ và bề mặt. Bản chính thức cần lưu lựa chọn theo tài khoản để dùng trên nhiều thiết bị.'],
  obsidian: ['Obsidian · command centre', 'Obsidian · trung tâm điều hành'], parchment: ['Chronicle · illuminated journal', 'Chronicle · nhật ký cổ'],
  jade: ['Jade · calm, open workspace', 'Jade · không gian thoáng'], cyber: ['Terminal · precise and compact', 'Terminal · gọn và rõ'], royal: ['Royal · gallery and glass', 'Royal · trưng bày và kính'],
  supportHelp: ['Need help with a transaction?', 'Cần hỗ trợ giao dịch?'], supportNote: ['A support reference can include the receipt or trace ID, without your password or PIN.', 'Mã hỗ trợ có thể kèm biên nhận hoặc mã truy vết, không kèm mật khẩu hay mã PIN.'],
} as const;
type Key = keyof typeof text;
type Language = 'en' | 'vi';
type Mode = 'game' | 'website' | 'profile';
type Theme = 'obsidian' | 'parchment' | 'jade' | 'cyber' | 'royal';
const sections: readonly Key[] = ['home', 'characters', 'items', 'activity', 'journal', 'compare', 'wallet', 'security', 'rewards', 'appearance', 'support'];
const themes: readonly Theme[] = ['obsidian', 'parchment', 'jade', 'cyber', 'royal'];
let language: Language = 'en';
let mode: Mode = 'profile';
let theme: Theme = 'obsidian';
let section: Key = 'home';
try { const saved = localStorage.getItem('hnmo.preview.theme'); if (themes.some(value => value === saved)) theme = saved as Theme; const lang = localStorage.getItem('hnmo.preview.language'); if (lang === 'en' || lang === 'vi') language = lang; } catch { /* Optional preview preferences, never credentials. */ }
const root = document.getElementById('profilePreview');
if (!(root instanceof HTMLElement)) throw new Error('Missing profile preview root');
const host: HTMLElement = root;
const t = (key: Key): string => text[key][language === 'vi' ? 1 : 0];
function node(tag: string, label = '', className = ''): HTMLElement {
  const result = document.createElement(tag); result.textContent = label; result.className = className; return result;
}
function button(label: string, action: () => void, hint = label): HTMLButtonElement {
  const result = document.createElement('button'); result.type = 'button'; result.textContent = label; result.title = hint; result.addEventListener('click', action); return result;
}
function panel(title: Key, ...children: HTMLElement[]): HTMLElement {
  const result = node('section', '', 'preview-card'); result.append(node('h3', t(title)), ...children); return result;
}
function go(next: Key): void { section = next; mode = 'profile'; render(); }
function selectTheme(next: Theme): void { theme = next; try { localStorage.setItem('hnmo.preview.theme', next); } catch { /* Optional browser preferences. */ } render(); }
function controls(): HTMLElement {
  const bar = node('div', '', 'preview-controls'); bar.append(node('p', t('preview')));
  const modes = node('div', '', 'preview-mode');
  for (const next of ['game', 'website', 'profile'] as const) {
    const control = button(t(next), () => { mode = next; render(); }); control.setAttribute('aria-pressed', String(mode === next)); modes.append(control);
  }
  bar.append(modes);
  const langs = node('div', '', 'preview-mode');
  for (const next of ['en', 'vi'] as const) { const control = button(next === 'en' ? 'English' : 'Tiếng Việt', () => { language = next; try { localStorage.setItem('hnmo.preview.language', next); } catch { /* Optional browser preferences. */ } render(); }); control.setAttribute('aria-pressed', String(language === next)); langs.append(control); }
  bar.append(langs); return bar;
}
function heading(title: Key, subtitle: Key): HTMLElement { const header = node('header', '', 'preview-page-heading'); header.append(node('p', t('sampleOnly'), 'eyebrow'), node('h2', t(title)), node('p', t(subtitle), 'muted')); return header; }
function overview(): HTMLElement {
  const result = node('div', '', 'preview-content'); result.append(heading('overviewTitle', 'overviewSub'));
  const cards = node('div', '', 'preview-grid');
  cards.append(panel('attention', button(t('vipExpiring'), () => go('wallet')), button(t('mailWaiting'), () => go('rewards'))),
    panel('protection', node('p', '✓ ' + t('pin')), node('p', '○ ' + t('email')), node('p', t('securityNote'), 'muted')));
  const money = panel('balances', node('div', '2,400 WCoinC', 'preview-big'), node('p', '120 WCoinP · 650 GoblinPoints'), button(t('topup'), () => go('wallet')));
  cards.append(money); result.append(cards, returnSummary(language), expiryReminders(language, go), characterTable());
  result.append(panel('events', node('p', '21/11/2026 20:30 · ' + t('tradeExample')), node('p', '21/11/2026 19:10 · ' + t('rewardExample')), button(t('activity'), () => go('activity')))); return result;
}
function table(headings: readonly string[], rows: readonly (readonly (string | HTMLElement)[])[]): HTMLElement {
  const wrap = node('div', '', 'preview-scroll'); const result = document.createElement('table'); const head = document.createElement('thead'); const line = document.createElement('tr');
  for (const label of headings) line.append(node('th', label)); head.append(line); const body = document.createElement('tbody');
  for (const row of rows) { const tr = document.createElement('tr'); for (const cell of row) { const td = document.createElement('td'); if (typeof cell === 'string') td.textContent = cell; else td.append(cell); tr.append(td); } body.append(tr); }
  result.append(head, body); wrap.append(result); return wrap;
}
function characterTable(): HTMLElement { return panel('yourCharacters', node('p', t('characterNote'), 'muted'), table([t('name'), t('world'), t('level'), t('location')], [['Explorer · ' + t('knight'), 'S6.3 · Atlas', '300 / 12', t('offline')], ['Starlight · ' + t('mage'), 'S9 · Aurora', '210 / 3', t('offline')]])); }
function wallet(): HTMLElement {
  const result = node('div', '', 'preview-content'); result.append(heading('wallet', 'noPayments'));
  const cards = node('div', '', 'preview-grid'); const pay = button(t('topup'), () => {} , t('noPayments')); pay.disabled = true;
  const renew = button(t('renew'), () => {}, t('noPayments')); renew.disabled = true;
  cards.append(panel('balances', node('div', '2,400 WCoinC', 'preview-big'), node('p', '120 WCoinP · 650 GoblinPoints'), pay), panel('vipPlan', node('p', t('sampleVip')), renew));
  result.append(cards, panel('ledger', node('p', t('ledgerExample')), node('p', t('separateCredit'), 'muted')), panel('supportHelp', node('p', t('supportNote')))); return result;
}
function appearance(): HTMLElement { const result = node('div', '', 'preview-content'); result.append(heading('themeTitle', 'themeNote')); const choices = node('div', '', 'preview-grid'); for (const next of themes) { const control = button(t(next), () => selectTheme(next), t('themeNote')); control.className = 'preview-theme-choice'; control.setAttribute('aria-pressed', String(next === theme)); choices.append(control); } result.append(choices, densityControl(language)); return result; }
function content(): HTMLElement {
  if (section === 'home') return overview();
  if (section === 'characters') return characterTable();
  if (section === 'items') return itemExplorer(language);
  if (section === 'activity') return eventLog(language);
  if (section === 'journal') return adventureJournal(language);
  if (section === 'compare') return equipmentComparison(language);
  if (section === 'support') return supportReport(language);
  if (section === 'wallet') return wallet();
  if (section === 'appearance') return appearance();
  if (section === 'security') return panel('accountSafety', sessionPreview(language), node('p', t('safetyDetail')), node('h3', t('lastLogin')), node('p', '21/11/2026 20:30 · ***.***.12.34 · Sydney'), node('p', t('securityNote'), 'muted'));
  if (section === 'rewards') return panel('rewards', node('p', t('rewardDetail')), node('p', t('mailWaiting')));
  return panel('events', node('p', t('eventNote')), node('p', '21/11/2026 20:30 · ' + t('tradeExample')), node('p', '21/11/2026 19:10 · ' + t('rewardExample')), node('p', t('missing'), 'muted'));
}
function profile(): HTMLElement {
  const shell = node('div', '', 'preview-shell'); const nav = node('nav', '', 'preview-sidebar'); nav.setAttribute('aria-label', t('profile'));
  nav.append(node('div', gameName(), 'preview-brand'), node('p', 'Explorer', 'preview-identity'), node('p', 'S6.3 / S9', 'muted'));
  for (const key of sections) { const control = button(t(key), () => go(key)); if (section === key) control.setAttribute('aria-current', 'page'); nav.append(control); }
  const main = node('div', '', 'preview-main'); main.append(globalSearch(language, go), content()); shell.append(nav, main); return shell;
}
function login(): HTMLElement {
  const result = node('div', '', 'preview-login'); const hero = node('section', '', 'preview-hero'); hero.append(node('p', gameName(), 'preview-brand'), node('h1', t(mode === 'game' ? 'welcome' : 'private')), node('p', t(mode === 'game' ? 'welcomeSub' : 'privateSub')));
  const shortcuts = node('div', '', 'preview-login-links'); for (const key of ['characters', 'items', 'wallet', 'security'] as const) shortcuts.append(button(t(key), () => go(key), t('previewHint'))); hero.append(shortcuts);
  const form = panel('signIn', node('p', t('sampleOnly'), 'muted'));
  for (const key of ['account', 'password'] as const) { const label = document.createElement('label'); label.append(node('span', t(key))); const input = document.createElement('input'); input.type = key === 'password' ? 'password' : 'text'; input.disabled = true; input.placeholder = key === 'account' ? 'Explorer' : '••••••••'; label.append(input); form.append(label); }
  const enter = button(t('enter'), () => go('home'), t('previewHint')); enter.className = 'preview-primary'; form.append(enter);
  result.append(hero, form);
  const worlds = panel('yourWorlds', node('p', t('available'), 'muted')); worlds.append(node('div', 'S6.3 · Atlas', 'preview-world'), node('div', 'S9 · Aurora', 'preview-world'));
  result.append(worlds, panel('announcements', node('p', t('newsExample')), button(t('guide'), () => go('rewards'), t('previewHint')))); return result;
}
function render(): void {
  document.documentElement.lang = language; document.body.dataset.previewTheme = theme;
  try { document.body.dataset.density = localStorage.getItem('hnmo.preview.density') === 'compact' ? 'compact' : 'comfortable'; } catch { document.body.dataset.density = 'comfortable'; }
  const themeLabel = document.createElement('label'); themeLabel.className = 'preview-theme-control'; themeLabel.append(node('span', t('appearance')));
  const select = document.createElement('select'); select.title = t('themeNote'); select.setAttribute('aria-label', t('appearance')); for (const next of themes) { const option = document.createElement('option'); option.value = next; option.textContent = t(next); option.selected = theme === next; select.append(option); }
  select.addEventListener('change', () => { const next = themes.find(value => value === select.value); if (next) selectTheme(next); }); themeLabel.append(select);
  const bar = controls(); bar.append(themeLabel); host.replaceChildren(bar, mode === 'profile' ? profile() : login());
}
render(); void loadGameName().then(() => render());

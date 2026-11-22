import { hubItems, hubEvents, localized, hubTime, type HubItem, type HubLanguage, type HubEvent } from './accountHubDemoData.js';

const words = {
  events: ['Item event log', 'Nhật ký vật phẩm'], passport: ['Item passport', 'Hồ sơ vật phẩm'],
  search: ['Search items, trace IDs, characters or receipts', 'Tìm vật phẩm, mã truy vết, nhân vật hoặc biên nhận'],
  filter: ['Filter name, trace ID, character or location', 'Lọc tên, mã truy vết, nhân vật hoặc vị trí'],
  all: ['All events', 'Tất cả sự kiện'], allCharacters: ['All characters', 'Tất cả nhân vật'],
  drop: ['Dropped', 'Rơi xuống'], pickup: ['Picked up', 'Nhặt lên'], trade: ['Traded', 'Giao dịch'], upgrade: ['Upgraded', 'Nâng cấp'], storage: ['Storage move', 'Chuyển kho'], craft: ['Crafted', 'Chế tạo'], reward: ['Reward', 'Phần thưởng'], boss: ['Boss defeated', 'Hạ trùm'],
  date: ['Sydney time', 'Giờ Sydney'], event: ['Event', 'Sự kiện'], character: ['Character', 'Nhân vật'], location: ['Location / slot', 'Vị trí / ô'], item: ['Item', 'Vật phẩm'],
  trace: ['Trace ID', 'Mã truy vết'], copy: ['Copy trace ID', 'Sao chép mã truy vết'], copied: ['Copied', 'Đã sao chép'], copyFailed: ['Select and copy the ID above.', 'Chọn rồi sao chép mã ở trên.'],
  watch: ['Watch this item', 'Theo dõi vật phẩm'], watched: ['Watching this item', 'Đang theo dõi vật phẩm'],
  history: ['Recorded history', 'Lịch sử đã ghi nhận'], partial: ['Earlier history unavailable. An ID does not prove a complete lifetime history.', 'Chưa có lịch sử trước đó. Mã vật phẩm không chứng minh có đủ lịch sử từ lúc tạo.'],
  input: ['Crafting inputs (consumed)', 'Nguyên liệu chế tạo (đã tiêu hao)'], before: ['Before', 'Trước'], after: ['After', 'Sau'],
  exportCsv: ['Export filtered CSV', 'Xuất CSV theo bộ lọc'], exportHint: ['Download the visible sample events. No passwords or PINs are included.', 'Tải sự kiện mẫu theo bộ lọc. Không kèm mật khẩu hay mã PIN.'],
  noResults: ['No matching records', 'Không có bản ghi phù hợp'], previous: ['Previous page', 'Trang trước'], next: ['Next page', 'Trang sau'],
  dateFrom: ['From date (Sydney)', 'Từ ngày (Sydney)'], dateTo: ['To date (Sydney)', 'Đến ngày (Sydney)'],
  compare: ['Compare equipment', 'So sánh trang bị'], left: ['Current choice', 'Lựa chọn hiện tại'], right: ['Alternative', 'Phương án khác'],
  attack: ['Attack', 'Tấn công'], defense: ['Defense', 'Phòng thủ'], speed: ['Speed', 'Tốc độ'], delta: ['Difference', 'Chênh lệch'],
  sampleStats: ['Sample item values only. Real totals depend on character and server rules.', 'Chỉ số vật phẩm mẫu. Tổng chỉ số thật phụ thuộc nhân vật và quy tắc máy chủ.'],
  journal: ['Adventure journal', 'Nhật ký hành trình'], journalNote: ['Sample progression and party activity; only events actually recorded can appear in the live account.', 'Tiến trình và hoạt động tổ đội mẫu; bản chính thức chỉ hiện sự kiện đã được ghi nhận.'],
  support: ['Support report', 'Báo cáo hỗ trợ'], report: ['Download support report', 'Tải báo cáo hỗ trợ'], issue: ['What happened?', 'Mô tả sự việc'],
  safeExport: ['Includes the selected item reference and public event details. Never enter a password or PIN.', 'Kèm mã vật phẩm đã chọn và thông tin sự kiện công khai. Không nhập mật khẩu hoặc mã PIN.'],
  supportNote: ['Sample report only; this does not submit a support ticket.', 'Chỉ tạo báo cáo mẫu; chưa gửi yêu cầu hỗ trợ.'],
  return: ['Since your last visit', 'Kể từ lần truy cập trước'], returnNote: ['Sample last visit: 21 November, 19:00 Sydney.', 'Lần truy cập mẫu: 19:00 ngày 21/11, giờ Sydney.'],
  reminders: ['Expiring soon', 'Sắp hết hạn'], vip: ['VIP membership · 3 days', 'Gói VIP · còn 3 ngày'], mail: ['Unclaimed gift · 2 days', 'Quà chưa nhận · còn 2 ngày'],
  locked: ['Item locked', 'Đã khóa vật phẩm'], unlocked: ['Item not locked', 'Chưa khóa vật phẩm'],
  anyDate: ['Any date', 'Mọi ngày'], clear: ['Clear filters', 'Xóa bộ lọc'],
  result: ['matching records', 'bản ghi phù hợp'], details: ['View item history', 'Xem lịch sử vật phẩm'],
  sessions: ['Example active sessions', 'Phiên hoạt động mẫu'], endSession: ['End example session', 'Kết thúc phiên mẫu'], ended: ['Example session ended locally', 'Đã kết thúc phiên mẫu trên trình duyệt'],
  sessionsNote: ['Local simulation only. Real session revocation and MFA need server support.', 'Chỉ mô phỏng cục bộ. Thu hồi phiên thật và MFA cần máy chủ hỗ trợ.'],
  preference: ['Preview preferences are saved in this browser only.', 'Tùy chọn xem thử chỉ được lưu trong trình duyệt này.'],
  density: ['Display density', 'Độ gọn giao diện'], comfortable: ['Comfortable', 'Thoáng'], compact: ['Compact', 'Gọn'],
} as const;
type Word = keyof typeof words;
export type ToolSection = 'items' | 'activity' | 'journal' | 'compare' | 'support' | 'wallet' | 'rewards' | 'characters';
const pageSize = 5;
let selected = hubItems[0].id;
const watched = new Set<string>();
let exampleSessionEnded = false;
const tr = (key: Word, language: HubLanguage): string => localized(words[key], language);
const el = (tag: string, value = '', cls = ''): HTMLElement => { const n = document.createElement(tag); n.textContent = value; n.className = cls; return n; };
function action(label: string, run: () => void, hint = label): HTMLButtonElement { const n = document.createElement('button'); n.type = 'button'; n.textContent = label; n.title = hint; n.addEventListener('click', run); return n; }
function card(title: string, ...children: HTMLElement[]): HTMLElement { const n = el('section', '', 'preview-card'); n.append(el('h3', title), ...children); return n; }
function select(label: string, values: readonly (readonly [string, string])[], chosen: string, change: (value: string) => void): HTMLElement {
  const wrap = document.createElement('label'); wrap.append(el('span', label)); const n = document.createElement('select'); n.title = label; n.setAttribute('aria-label', label);
  for (const [value, text] of values) { const option = document.createElement('option'); option.value = value; option.textContent = text; option.selected = value === chosen; n.append(option); }
  n.addEventListener('change', () => change(n.value)); wrap.append(n); return wrap;
}
function grid(headings: readonly string[], rows: readonly (readonly (string | HTMLElement)[])[]): HTMLElement {
  const wrap = el('div', '', 'preview-scroll'); const table = document.createElement('table'); const thead = document.createElement('thead'); const hr = document.createElement('tr');
  for (const text of headings) hr.append(el('th', text)); thead.append(hr); const tbody = document.createElement('tbody');
  for (const row of rows) { const tr = document.createElement('tr'); for (const value of row) { const td = document.createElement('td'); if (typeof value === 'string') td.textContent = value; else td.append(value); tr.append(td); } tbody.append(tr); }
  table.append(thead, tbody); wrap.append(table); return wrap;
}
function download(name: string, body: string, type: string): void { const url = URL.createObjectURL(new Blob([body], { type })); const link = document.createElement('a'); link.href = url; link.download = name; link.click(); requestAnimationFrame(() => URL.revokeObjectURL(url)); }
function csvCell(value: string): string { const safe = /^[=+\-@\t\r]/.test(value) ? `'${value}` : value; return `"${safe.replaceAll('"', '""')}"`; }
function itemById(id: string): HubItem { return hubItems.find(item => item.id === id) ?? hubItems[0]; }
function eventPlace(event: HubEvent, language: HubLanguage): string {
  if (language === 'en') return `${event.location} · ${event.slot}`;
  const names: Readonly<Record<string, string>> = { Town: 'Thành phố', Dungeon: 'Hang ngục', ground: 'mặt đất', bag: 'túi', vault: 'rương', equipped: 'trang bị', craft: 'chế tạo', mail: 'hộp thư' };
  return `${event.location} · ${event.slot}`.replace(/Town|Dungeon|ground|bag|vault|equipped|craft|mail/g, key => names[key]);
}
function eventRows(events: readonly HubEvent[], language: HubLanguage, open: (id: string) => void): HTMLElement {
  return grid(['date', 'event', 'character', 'location', 'trace'].map(key => tr(key as Word, language)), events.map(event => [hubTime(event.at, language), tr(event.kind, language), event.character, eventPlace(event, language), event.traceId ? action(event.traceId, () => open(event.traceId), tr('details', language)) : '—']));
}
export function itemPassport(language: HubLanguage, id = selected): HTMLElement {
  selected = id; const item = itemById(id); const root = card(tr('passport', language), el('h2', localized(item.name, language)), el('code', item.id), el('p', `${item.world} · ${localized(item.storage, language)}`), el('p', tr(item.locked ? 'locked' : 'unlocked', language)));
  const controls = el('div', '', 'account-hub-controls'); const message = el('p'); message.setAttribute('aria-live', 'polite');
  controls.append(action(tr('copy', language), () => { if (!navigator.clipboard) { message.textContent = tr('copyFailed', language); return; } void navigator.clipboard.writeText(item.id).then(() => { message.textContent = tr('copied', language); }).catch(() => { message.textContent = tr('copyFailed', language); }); }));
  const watch = action(tr(watched.has(id) ? 'watched' : 'watch', language), () => { if (watched.has(id)) watched.delete(id); else watched.add(id); watch.textContent = tr(watched.has(id) ? 'watched' : 'watch', language); watch.setAttribute('aria-pressed', String(watched.has(id))); }); watch.setAttribute('aria-pressed', String(watched.has(id))); controls.append(watch); root.append(controls, message, el('h3', tr('history', language)));
  for (const event of hubEvents.filter(event => event.traceId === id)) {
    const entry = el('article', '', 'account-hub-event'); entry.append(el('small', `${hubTime(event.at, language)} · ${tr(event.kind, language)}`), el('p', localized(event.description, language)), el('p', eventPlace(event, language), 'muted'));
    if (event.before && event.after) entry.append(el('p', `${tr('before', language)}: ${event.before} / ${tr('after', language)}: ${event.after}`));
    if (event.parents) entry.append(el('p', `${tr('input', language)}: ${event.parents.join(', ')}`)); root.append(entry);
  }
  root.append(el('p', tr('partial', language), 'muted')); return root;
}
export function itemExplorer(language: HubLanguage): HTMLElement {
  const root = el('div', '', 'preview-content'); const input = document.createElement('input'); input.type = 'search'; input.placeholder = tr('filter', language); input.setAttribute('aria-label', tr('filter', language)); input.title = tr('filter', language);
  const listing = el('div'); const details = el('div'); const open = (id: string): void => { details.replaceChildren(itemPassport(language, id)); };
  const update = (): void => { const term = input.value.trim().toLocaleLowerCase(); const found = hubItems.filter(item => `${item.id} ${localized(item.name, language)} ${item.character} ${localized(item.storage, language)}`.toLocaleLowerCase().includes(term)); listing.replaceChildren(found.length ? grid([tr('item', language), tr('location', language), tr('trace', language)], found.map(item => [localized(item.name, language), localized(item.storage, language), action(item.id, () => open(item.id))])) : el('p', tr('noResults', language))); };
  input.addEventListener('input', update); update(); open(selected); root.append(el('h2', tr('passport', language)), input, listing, details); return root;
}
export function eventLog(language: HubLanguage): HTMLElement {
  const root = el('div', '', 'preview-content'); root.append(el('h2', tr('events', language)));
  const controls = el('div', '', 'account-hub-controls'); const query = document.createElement('input'); query.type = 'search'; query.placeholder = tr('filter', language); query.setAttribute('aria-label', tr('filter', language)); query.title = tr('filter', language);
  let kind = ''; let character = ''; let offset = 0; let filtered: readonly HubEvent[] = hubEvents;
  const from = document.createElement('input'); from.type = 'date'; from.setAttribute('aria-label', tr('dateFrom', language)); from.title = tr('dateFrom', language); const to = document.createElement('input'); to.type = 'date'; to.setAttribute('aria-label', tr('dateTo', language)); to.title = tr('dateTo', language);
  const listing = el('div'); const details = el('div'); const count = el('p'); count.setAttribute('aria-live', 'polite');
  const previous = action(tr('previous', language), () => { offset = Math.max(0, offset - pageSize); update(); }); const next = action(tr('next', language), () => { offset += pageSize; update(); });
  const open = (id: string): void => { details.replaceChildren(itemPassport(language, id)); };
  const dateKey = (value: string): string => new Intl.DateTimeFormat('en-CA', { timeZone: 'Australia/Sydney', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(value));
  function update(): void {
    const term = query.value.toLocaleLowerCase().trim(); filtered = hubEvents.filter(event => (!kind || event.kind === kind) && (!character || event.character === character) && (!from.value || dateKey(event.at) >= from.value) && (!to.value || dateKey(event.at) <= to.value) && `${event.traceId} ${event.character} ${event.location} ${localized(event.description, language)}`.toLocaleLowerCase().includes(term)).slice().reverse();
    if (offset >= filtered.length) offset = 0; count.textContent = `${filtered.length} ${tr('result', language)}`; listing.replaceChildren(filtered.length ? eventRows(filtered.slice(offset, offset + pageSize), language, open) : el('p', tr('noResults', language))); previous.disabled = offset === 0; next.disabled = offset + pageSize >= filtered.length;
  }
  const kinds = select(tr('event', language), [['', tr('all', language)], ...(['drop', 'pickup', 'trade', 'upgrade', 'storage', 'craft', 'reward', 'boss'] as const).map(value => [value, tr(value, language)] as const)], '', value => { kind = value; offset = 0; update(); });
  const people = select(tr('character', language), [['', tr('allCharacters', language)], ['Explorer', 'Explorer'], ['Starlight', 'Starlight']], '', value => { character = value; offset = 0; update(); });
  for (const control of [query, from, to]) control.addEventListener('input', () => { offset = 0; update(); });
  const dates = el('div', '', 'account-hub-controls'); for (const [label, control] of [[tr('dateFrom', language), from], [tr('dateTo', language), to]] as const) { const wrap = document.createElement('label'); wrap.append(el('span', label), control); dates.append(wrap); }
  const exportButton = action(tr('exportCsv', language), () => { const rows = [['time_sydney', 'event', 'character', 'location', 'slot', 'trace_id', 'description'], ...filtered.map(event => [hubTime(event.at, language), tr(event.kind, language), event.character, event.location, event.slot, event.traceId, localized(event.description, language)])]; download('hnmo-sample-events.csv', '\uFEFF' + rows.map(row => row.map(csvCell).join(',')).join('\r\n'), 'text/csv;charset=utf-8'); }, tr('exportHint', language));
  controls.append(query, kinds, people); const pager = el('div', '', 'account-hub-controls'); pager.append(previous, next, exportButton); root.append(controls, dates, count, listing, pager, details); update(); return root;
}
export function equipmentComparison(language: HubLanguage): HTMLElement {
  const root = el('div', '', 'preview-content'); root.append(el('h2', tr('compare', language)), el('p', tr('sampleStats', language), 'muted'));
  let left = hubItems[0].id; let right = hubItems[3].id; const result = el('div'); const values = hubItems.filter(item => item.attack > 0).map(item => [item.id, localized(item.name, language)] as const);
  function update(): void { const a = itemById(left); const b = itemById(right); result.replaceChildren(grid([tr('compare', language), localized(a.name, language), localized(b.name, language), tr('delta', language)], (['attack', 'defense', 'speed'] as const).map(key => [tr(key, language), String(a[key]), String(b[key]), `${b[key] - a[key] > 0 ? '+' : ''}${b[key] - a[key]}`]))); }
  const controls = el('div', '', 'account-hub-controls'); controls.append(select(tr('left', language), values, left, value => { left = value; update(); }), select(tr('right', language), values, right, value => { right = value; update(); })); root.append(controls, result); update(); return root;
}
export function adventureJournal(language: HubLanguage): HTMLElement { return card(tr('journal', language), el('p', tr('journalNote', language), 'muted'), ...hubEvents.filter(event => event.kind === 'boss' || event.kind === 'reward' || event.kind === 'upgrade').slice().reverse().map(event => card(tr(event.kind, language), el('p', localized(event.description, language)), el('small', `${hubTime(event.at, language)} · ${event.character} · ${event.location}`)))); }
export function supportReport(language: HubLanguage): HTMLElement {
  const root = card(tr('support', language), el('p', tr('safeExport', language)), el('p', tr('supportNote', language), 'muted')); const reference = el('div');
  const choices = select(tr('item', language), hubItems.map(item => [item.id, `${localized(item.name, language)} · ${item.id}`] as const), selected, value => { selected = value; reference.textContent = value; }); reference.textContent = selected;
  // Deliberately no free-text field: an example support export should not collect secrets.
  root.append(choices, reference, action(tr('report', language), () => { const item = itemById(selected); download('hnmo-sample-support.json', JSON.stringify({ sample: true, account: 'Explorer', item: { id: item.id, name: localized(item.name, language), world: item.world, storage: localized(item.storage, language) }, events: hubEvents.filter(event => event.traceId === item.id).map(event => ({ id: event.id, at: event.at, kind: event.kind, description: localized(event.description, language) })) }, null, 2), 'application/json'); }, tr('safeExport', language))); return root;
}
export function returnSummary(language: HubLanguage): HTMLElement { const since = '2026-11-21T08:00:00Z'; const recent = hubEvents.filter(event => event.at > since); return card(tr('return', language), el('p', tr('returnNote', language), 'muted'), el('p', `${recent.length} ${tr('result', language)}`), ...recent.slice(-3).reverse().map(event => el('p', `${hubTime(event.at, language)} · ${localized(event.description, language)}`))); }
export function expiryReminders(language: HubLanguage, go: (section: ToolSection) => void): HTMLElement { return card(tr('reminders', language), action(tr('vip', language), () => go('wallet')), action(tr('mail', language), () => go('rewards')), ...hubItems.filter(item => item.expires).map(item => action(`${localized(item.name, language)} · ${hubTime(item.expires!, language)}`, () => { selected = item.id; go('items'); }))); }
export function globalSearch(language: HubLanguage, go: (section: ToolSection) => void): HTMLElement {
  const root = el('div', '', 'account-hub-search'); const input = document.createElement('input'); input.type = 'search'; input.placeholder = tr('search', language); input.setAttribute('aria-label', tr('search', language)); input.title = tr('search', language); const results = el('div', '', 'account-hub-search-results'); results.setAttribute('aria-live', 'polite');
  input.addEventListener('input', () => { results.replaceChildren(); const term = input.value.trim().toLocaleLowerCase(); if (!term) return; const matches = hubItems.filter(item => `${item.id} ${localized(item.name, language)} ${item.character}`.toLocaleLowerCase().includes(term)); for (const item of matches) results.append(action(`${localized(item.name, language)} · ${item.id}`, () => { selected = item.id; go('items'); })); for (const name of ['Explorer', 'Starlight'].filter(name => name.toLocaleLowerCase().includes(term))) results.append(action(`${tr('character', language)} · ${name}`, () => go('characters'))); if ('vip receipt demo-receipt-001'.includes(term)) results.append(action('VIP · DEMO-RECEIPT-001', () => go('wallet'))); if (matches.length === 0 && !'vip receipt demo-receipt-001'.includes(term) && !['explorer', 'starlight'].some(name => name.includes(term))) results.append(el('p', tr('noResults', language))); }); root.append(input, results); return root;
}
export function sessionPreview(language: HubLanguage): HTMLElement { const state = el('p', exampleSessionEnded ? tr('ended', language) : (language === 'vi' ? 'Trình duyệt · ***.***.12.34 · Sydney' : 'Browser · ***.***.12.34 · Sydney')); const end = action(tr('endSession', language), () => { exampleSessionEnded = true; state.textContent = tr('ended', language); end.disabled = true; }, tr('sessionsNote', language)); end.disabled = exampleSessionEnded; return card(tr('sessions', language), state, end, el('p', tr('sessionsNote', language), 'muted')); }
export function densityControl(language: HubLanguage): HTMLElement { let current = 'comfortable'; try { current = localStorage.getItem('hnmo.preview.density') === 'compact' ? 'compact' : 'comfortable'; } catch { /* Optional browser storage. */ } document.body.dataset.density = current; return select(tr('density', language), [['comfortable', tr('comfortable', language)], ['compact', tr('compact', language)]], current, value => { document.body.dataset.density = value; try { localStorage.setItem('hnmo.preview.density', value); } catch { /* Optional browser storage. */ } }); }

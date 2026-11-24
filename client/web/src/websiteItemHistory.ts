import { currentLanguage, onLanguageChange } from './i18n.js';

type Snapshot = { group: number; number: number; level: number; quantity: number; durability: number; sockets: number; hasSkill: boolean };
type HistoryEvent = { kind: string; time: string; character: string | null; partner: string | null; before: Snapshot | null; after: Snapshot | null };
type History = { enabled: true; retentionDays: number; events: HistoryEvent[] } | { enabled: false };

const form = document.querySelector<HTMLFormElement>('#item-history-search')!;
const input = document.querySelector<HTMLInputElement>('#item-serial')!;
const results = document.querySelector<HTMLOListElement>('#item-history-results')!;
const status = document.querySelector<HTMLElement>('#item-history-status')!;
const serialPattern = /^(?:[0-9a-f]{32}|[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})$/i;
const kinds: Record<string, [string, string]> = {
  loot: ['Loot generated', 'Tạo đồ rơi'], pickup: ['Picked up', 'Nhặt lên'], drop: ['Dropped', 'Thả xuống'],
  trade: ['Trade', 'Giao dịch'], mix: ['Crafting', 'Chế tạo'], upgrade: ['Upgrade', 'Nâng cấp'],
  consume: ['Consumed', 'Đã dùng'], grant: ['Granted', 'Đã cấp'], 'npc-buy': ['NPC purchase', 'Mua từ NPC'],
  'npc-sell': ['NPC sale', 'Bán cho NPC'], 'personal-store': ['Personal store', 'Cửa hàng cá nhân'], 'item-market': ['Market', 'Chợ vật phẩm'],
};
let history: History | undefined;
let phase: 'idle' | 'loading' | 'invalid' | 'unavailable' = 'idle';
let request: AbortController | undefined;

function render(): void {
  const vi = currentLanguage() === 'vi';
  const copy = (en: string, vietnamese: string) => vi ? vietnamese : en;
  const number = new Intl.NumberFormat(vi ? 'vi-VN' : 'en-AU', { maximumFractionDigits: 2 });
  const date = new Intl.DateTimeFormat(vi ? 'vi-VN' : 'en-AU', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Australia/Sydney' });
  document.querySelector('#item-history-heading')!.textContent = copy('Item history', 'Lịch sử vật phẩm');
  form.querySelector('label')!.textContent = copy('Item serial', 'Số sê-ri vật phẩm');
  document.querySelector('#item-search-button')!.textContent = copy('Search', 'Tìm kiếm');
  document.querySelector('#item-history-limits')!.textContent = history?.enabled
    ? copy(`Up to ${number.format(history.retentionDays)} days of retained activity. Some actions may be missing or private.`, `Tối đa ${number.format(history.retentionDays)} ngày hoạt động còn lưu. Một số hoạt động có thể thiếu hoặc được giữ riêng tư.`)
    : copy('Search for an exact serial to see recorded activity.', 'Tìm theo số sê-ri chính xác để xem hoạt động đã ghi nhận.');

  status.textContent = {
    idle: history?.enabled
      ? (history.events.length ? '' : copy('No public records are available for this serial.', 'Chưa có bản ghi công khai cho số sê-ri này.'))
      : history ? copy('The host has disabled item history.', 'Máy chủ đã tắt lịch sử vật phẩm.') : '',
    loading: copy('Searching…', 'Đang tìm…'),
    invalid: copy('Enter a valid item serial.', 'Nhập số sê-ri vật phẩm hợp lệ.'),
    unavailable: copy('Item history is temporarily unavailable.', 'Lịch sử vật phẩm tạm thời không có.'),
  }[phase];

  results.replaceChildren();
  if (!history?.enabled) return;
  for (const entry of history.events) {
    const row = document.createElement('li');
    const heading = document.createElement('h2');
    heading.textContent = kinds[entry.kind]?.[vi ? 1 : 0] ?? copy('Item activity', 'Hoạt động vật phẩm');
    const time = document.createElement('time');
    time.dateTime = entry.time;
    time.textContent = `${date.format(new Date(entry.time))} (Sydney)`;
    row.append(heading, time);
    const actors = [entry.character, entry.partner].filter(Boolean);
    if (actors.length) {
      const participants = document.createElement('p');
      participants.textContent = actors.join(' · ');
      row.append(participants);
    }
    for (const [label, snapshot] of [[copy('Before', 'Trước'), entry.before], [copy('After', 'Sau'), entry.after]] as const) {
      if (!snapshot) continue;
      const state = document.createElement('p');
      state.textContent = copy(
        `${label} · Item ${snapshot.group}/${snapshot.number} · Level +${snapshot.level} · Quantity ${number.format(snapshot.quantity)} · Sockets ${number.format(snapshot.sockets)}`,
        `${label} · Vật phẩm ${snapshot.group}/${snapshot.number} · Cấp +${snapshot.level} · Số lượng ${number.format(snapshot.quantity)} · Ô socket ${number.format(snapshot.sockets)}`,
      );
      row.append(state);
    }
    results.append(row);
  }
}

async function search(raw: string): Promise<void> {
  request?.abort();
  history = undefined;
  const serial = raw.trim();
  if (!serialPattern.test(serial) || !/[1-9a-f]/i.test(serial)) {
    phase = 'invalid';
    render();
    return;
  }

  const url = new URL(location.href);
  url.searchParams.set('serial', serial);
  window.history.replaceState(null, '', url);
  request = new AbortController();
  const { signal } = request;
  phase = 'loading';
  render();
  try {
    const response = await fetch(`/website/tracker/item?${new URLSearchParams({ serial })}`, { cache: 'no-store', signal });
    if (!response.ok) throw new Error('Item history unavailable');
    const reply = await response.json() as History;
    if (signal.aborted) return;
    history = reply;
    phase = 'idle';
  } catch {
    if (signal.aborted) return;
    phase = 'unavailable';
  }
  render();
}

form.addEventListener('submit', event => {
  event.preventDefault();
  void search(input.value);
});
onLanguageChange(render);
render();
const initial = new URLSearchParams(location.search).get('serial');
if (initial) {
  input.value = initial;
  void search(initial);
}

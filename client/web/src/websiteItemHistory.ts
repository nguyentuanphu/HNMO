import { currentLanguage, onLanguageChange } from './i18n.js';
import { renderItemHistoryEvents, type History } from './itemHistoryTimeline.js';

const form = document.querySelector<HTMLFormElement>('#item-history-search')!;
const input = document.querySelector<HTMLInputElement>('#item-serial')!;
const results = document.querySelector<HTMLOListElement>('#item-history-results')!;
const status = document.querySelector<HTMLElement>('#item-history-status')!;
const serialPattern = /^(?:[0-9a-f]{32}|[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})$/i;
let history: History | undefined;
let phase: 'idle' | 'loading' | 'invalid' | 'unavailable' = 'idle';
let request: AbortController | undefined;

function render(): void {
  const vi = currentLanguage() === 'vi';
  const copy = (en: string, vietnamese: string) => vi ? vietnamese : en;
  const number = new Intl.NumberFormat(vi ? 'vi-VN' : 'en-AU', { maximumFractionDigits: 2 });
  document.querySelector('#item-history-heading')!.textContent = copy('Item history', 'Lịch sử vật phẩm');
  document.querySelector('#item-jewel-link')!.textContent = copy('Jewel totals and hunters', 'Tổng ngọc và thợ săn');
  document.querySelector('#item-pvp-link')!.textContent = copy('PvP log', 'Nhật ký PvP');
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

  renderItemHistoryEvents(results, history?.enabled ? history.events : [], vi ? 'vi' : 'en', 'h2');
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

import { currentLanguage, onLanguageChange } from './i18n.js';

type JewelStats = { enabled: false } | { enabled: true; available: false } | {
  enabled: true;
  available: true;
  dateSydney: string;
  capturedAt: string;
  inventoryAndVaultPieces: number;
  bankPieces?: number | null;
  logWindowCapped: boolean;
  showHunters: boolean;
  hunterWindowAvailable: boolean;
  hunters: { character: string; pieces: number }[];
};

const status = document.querySelector('#jewel-status')!;
const summary = document.querySelector<HTMLElement>('#jewel-summary')!;
const results = document.querySelector('#item-history-results')!;
let stats: JewelStats | undefined;
let failed = false;

function render(): void {
  const vi = currentLanguage() === 'vi';
  const copy = (en: string, vietnamese: string) => vi ? vietnamese : en;
  const locale = vi ? 'vi-VN' : 'en-AU';
  const date = new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Australia/Sydney' });
  const count = new Intl.NumberFormat(locale);
  document.querySelector('#jewel-history-heading')!.textContent = copy('Jewel totals and hunters', 'Tổng ngọc và thợ săn');
  document.querySelector('#jewel-item-link')!.textContent = copy('Item history', 'Lịch sử vật phẩm');
  summary.hidden = !stats?.enabled || !stats.available;
  results.replaceChildren();
  if (!stats?.enabled || !stats.available) {
    status.textContent = failed
      ? copy('Jewel data is temporarily unavailable.', 'Dữ liệu ngọc tạm thời không có.')
      : stats?.enabled
        ? copy('The host has no verified jewel snapshot yet.', 'Máy chủ chưa có bản chụp dữ liệu ngọc đã xác minh.')
        : stats ? copy('The host has disabled this tracker.', 'Máy chủ đã tắt mục theo dõi này.') : copy('Loading jewel data…', 'Đang tải dữ liệu ngọc…');
    return;
  }

  status.textContent = '';
  document.querySelector('#jewel-total-heading')!.textContent = copy('Jewels in inventories and vaults', 'Ngọc trong túi và kho');
  document.querySelector('#jewel-total')!.textContent = copy(
    `${count.format(stats.inventoryAndVaultPieces)} pieces · Bundles counted as individual jewels`,
    `${count.format(stats.inventoryAndVaultPieces)} viên · Các bó được quy đổi thành viên`,
  );
  const bankTotal = document.querySelector<HTMLElement>('#jewel-bank-total')!;
  const bankPieces = stats.bankPieces;
  bankTotal.hidden = typeof bankPieces !== 'number';
  bankTotal.textContent = typeof bankPieces === 'number'
    ? copy(`Jewel Bank balance · ${count.format(bankPieces)} pieces`, `Số dư ngân hàng ngọc · ${count.format(bankPieces)} viên`)
    : '';
  document.querySelector('#jewel-date')!.textContent = copy(
    `Captured ${date.format(new Date(stats.capturedAt))} (Sydney)`, `Ghi nhận lúc ${date.format(new Date(stats.capturedAt))} (Sydney)`,
  );
  document.querySelector('#jewel-hunters-heading')!.textContent = copy('Recorded jewel hunters', 'Thợ săn ngọc đã ghi nhận');
  document.querySelector('#jewel-hunter-note')!.textContent = !stats.showHunters
    ? copy('The host has hidden character names.', 'Máy chủ đã ẩn tên nhân vật.')
    : !stats.hunterWindowAvailable
      ? copy('The hunter records are outside the current retention window.', 'Bản ghi thợ săn đã ngoài thời hạn lưu hiện tại.')
      : stats.logWindowCapped
      ? copy('The capture reached its activity limit. The standings use the recorded window.', 'Bản chụp đã đạt giới hạn nhật ký. Xếp hạng dựa trên phần đã ghi nhận.')
      : stats.hunters.length
        ? copy('Ranked by recorded monster loot picked up during the capture day.', 'Xếp hạng theo ngọc rơi từ quái đã nhặt trong ngày chụp.')
        : copy('No eligible hunters appeared in the capture.', 'Bản chụp chưa có thợ săn đủ điều kiện.');

  for (const hunter of (stats.showHunters && stats.hunterWindowAvailable ? stats.hunters : [])) {
    const row = document.createElement('li');
    row.textContent = copy(`${hunter.character} · ${count.format(hunter.pieces)} jewels`, `${hunter.character} · ${count.format(hunter.pieces)} viên ngọc`);
    results.append(row);
  }
}

onLanguageChange(render);
render();
void fetch('/website/tracker/jewels', { cache: 'no-store' }).then(async response => {
  if (!response.ok) throw new Error('Jewel snapshot unavailable');
  stats = await response.json() as JewelStats;
}).catch(() => { failed = true; }).finally(render);

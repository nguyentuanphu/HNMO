import { currentLanguage, onLanguageChange } from './i18n.js';

const titles = { field: ['Field', 'Bản đồ thường'], duel: ['Duel', 'Đấu tay đôi'], arena: ['Arena', 'Đấu trường'], siege: ['Siege', 'Công thành'] } as const;
type Mode = keyof typeof titles;
type PvpEvent = { time: string; winner: string | null; defeated: string | null; serverId: number | null; mapNumber: number | null };
type PvpData = { enabled: false } | { enabled: true; retentionDays: number; groups: { mode: Mode; events: PvpEvent[] }[] };

const modes = document.querySelector<HTMLElement>('#pvp-modes')!;
const results = document.querySelector<HTMLOListElement>('#pvp-results')!;
let selected: Mode = 'field';
let data: PvpData | undefined;
let failed = false;

function render(): void {
  const vi = currentLanguage() === 'vi';
  const copy = (en: string, vietnamese: string) => vi ? vietnamese : en;
  const date = new Intl.DateTimeFormat(vi ? 'vi-VN' : 'en-AU', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Australia/Sydney' });
  const count = new Intl.NumberFormat(vi ? 'vi-VN' : 'en-AU');
  document.querySelector('#pvp-heading')!.textContent = copy('PvP log', 'Nhật ký PvP');
  document.querySelector('#pvp-item-link')!.textContent = copy('Item history', 'Lịch sử vật phẩm');
  const retention = document.querySelector('#pvp-retention')!;
  const status = document.querySelector('#pvp-status')!;
  modes.hidden = !data?.enabled;
  modes.setAttribute('aria-label', copy('Combat mode', 'Chế độ chiến đấu'));
  modes.replaceChildren();
  results.replaceChildren();
  retention.textContent = '';

  if (!data?.enabled) {
    status.textContent = failed
      ? copy('The PvP log is temporarily unavailable.', 'Nhật ký PvP tạm thời không có.')
      : data ? copy('The host has disabled the PvP log.', 'Máy chủ đã tắt nhật ký PvP.') : copy('Loading PvP records…', 'Đang tải nhật ký PvP…');
    return;
  }

  retention.textContent = copy(
    `Up to ${count.format(data.retentionDays)} days retained. Some events may be missing or private.`,
    `Tối đa ${count.format(data.retentionDays)} ngày còn lưu. Một số hoạt động có thể thiếu hoặc được giữ riêng tư.`,
  );
  for (const [value, names] of Object.entries(titles)) {
    const button = document.createElement('button');
    button.type = 'button';
    button.id = `pvp-mode-${value}`;
    button.textContent = names[vi ? 1 : 0];
    button.setAttribute('aria-pressed', String(value === selected));
    button.addEventListener('click', () => {
      selected = value as Mode;
      render();
      document.getElementById(button.id)?.focus();
    });
    modes.append(button);
  }

  const snapshots = data.groups.find(group => group.mode === selected)?.events ?? [];
  status.textContent = snapshots.length ? '' : copy('No public records for this mode.', 'Chưa có bản ghi công khai cho chế độ này.');
  for (const entry of snapshots) {
    const row = document.createElement('li');
    const time = document.createElement('time');
    time.dateTime = entry.time;
    time.textContent = `${date.format(new Date(entry.time))} (Sydney)`;
    row.append(time);
    if (entry.winner || entry.defeated) {
      const actors = document.createElement('p');
      const unknown = copy('Hidden player', 'Người chơi ẩn danh');
      actors.textContent = copy(`${entry.winner ?? unknown} defeated ${entry.defeated ?? unknown}`, `${entry.winner ?? unknown} hạ ${entry.defeated ?? unknown}`);
      row.append(actors);
    }
    const location = [entry.serverId === null ? '' : copy(`Server ${entry.serverId}`, `Máy chủ ${entry.serverId}`),
      entry.mapNumber === null ? '' : copy(`Map ${entry.mapNumber}`, `Bản đồ ${entry.mapNumber}`)].filter(Boolean);
    if (location.length) {
      const place = document.createElement('p');
      place.textContent = location.join(' · ');
      row.append(place);
    }
    results.append(row);
  }
}

onLanguageChange(render);
render();
void fetch('/website/tracker/pvp', { cache: 'no-store' }).then(async response => {
  if (!response.ok) throw new Error('PvP tracker unavailable');
  data = await response.json() as PvpData;
}).catch(() => { failed = true; }).finally(render);

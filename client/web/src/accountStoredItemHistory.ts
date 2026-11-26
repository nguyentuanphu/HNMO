import { itemHistoryKind, renderItemHistoryEvents, type History } from './itemHistoryTimeline.js';

export function appendStoredItemHistory(dialog: HTMLDialogElement, serial: string, language: 'en' | 'vi'): void {
  const copy = (en: string, vi: string) => language === 'vi' ? vi : en;
  const load = document.createElement('button');
  load.type = 'button';
  load.textContent = copy('Show recorded activity', 'Xem hoạt động đã ghi nhận');
  const status = document.createElement('p');
  status.setAttribute('role', 'status');
  const timeline = document.createElement('ol');
  const filter = document.createElement('select');
  const label = document.createElement('label');
  label.append(copy('Event type', 'Loại sự kiện'), filter);
  label.hidden = true;
  dialog.append(load, status, label, timeline);

  let request: AbortController | undefined;
  dialog.addEventListener('close', () => { request?.abort(); }, { once: true });
  load.addEventListener('click', () => {
    request = new AbortController();
    const { signal } = request;
    status.textContent = copy('Loading recorded activity…', 'Đang tải hoạt động đã ghi nhận…');
    load.disabled = true;
    void fetch(`/website/tracker/item?${new URLSearchParams({ serial })}`, { cache: 'no-store', signal }).then(async response => {
      if (!response.ok) throw new Error('Item history unavailable');
      const data = await response.json() as History;
      if (signal.aborted || !dialog.open || !dialog.isConnected) return;
      load.hidden = true;
      if (!data.enabled) {
        status.textContent = copy('The host has disabled item history.', 'Máy chủ đã tắt lịch sử vật phẩm.');
        return;
      }

      const days = new Intl.NumberFormat(language === 'vi' ? 'vi-VN' : 'en-AU').format(data.retentionDays);
      status.textContent = data.events.length
        ? copy(`Up to ${days} days of retained activity. Some events may be missing or private.`, `Tối đa ${days} ngày hoạt động còn lưu. Một số sự kiện có thể thiếu hoặc được giữ riêng tư.`)
        : copy('No public records are available for this serial.', 'Chưa có bản ghi công khai cho số sê-ri này.');
      if (!data.events.length) return;

      filter.append(new Option(copy('All recorded events', 'Mọi sự kiện đã ghi'), ''));
      for (const kind of new Set(data.events.map(event => event.kind))) filter.append(new Option(itemHistoryKind(kind, language), kind));
      label.hidden = false;
      const show = () => {
        const kind = filter.value;
        renderItemHistoryEvents(timeline, kind ? data.events.filter(event => event.kind === kind) : data.events, language, 'h4');
      };
      filter.addEventListener('change', show);
      show();
    }).catch(() => {
      if (signal.aborted || !dialog.open || !dialog.isConnected) return;
      load.disabled = false;
      status.textContent = copy('Item history is temporarily unavailable.', 'Lịch sử vật phẩm tạm thời không có.');
    });
  });
}

// Browser Web Push opt-in widget for HNMO scheduled events and world bosses.
// Works inside the in-game modal (via Timetable window or window event 'hnmo-event-alerts-open')
// and as a self-contained mountable widget for the website.

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface EventAlertsPublicConfig {
  off?: boolean;
  leadTimeMinutes: number;
  remindAtStart?: boolean;
  categories: string[];
  vapidPublicKey: string;
  subscriberCount?: number;
}

const CATEGORY_LABELS: Record<string, { en: string; vi: string }> = {
  blood_castle: { en: 'Blood Castle', vi: 'Huyết Lâu' },
  devil_square: { en: 'Devil Square', vi: 'Quảng Trường Quỷ' },
  chaos_castle: { en: 'Chaos Castle', vi: 'Hỗn Nguyên Lâu' },
  world_boss: { en: 'World Bosses', vi: 'Boss Thế Giới' },
  invasion: { en: 'Map Invasions', vi: 'Sự Kiện Xâm Lăng' },
};

function tx(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) outputArray[i] = rawData.charCodeAt(i);
  return outputArray;
}

function arrayBufferToBase64Url(buf: ArrayBuffer | null): string {
  if (!buf) return '';
  const bytes = new Uint8Array(buf);
  let binary = '';
  for (let i = 0; i < bytes.byteLength; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Populates a DOM container with an interactive EN/VI Web Push event alert subscription manager. */
export async function mountEventAlertsWidget(container: HTMLElement, getLanguage = currentLanguage): Promise<{ refreshLanguage: () => void }> {
  const tx = (en: string, vi: string): string => getLanguage() === 'vi' ? vi : en;
  let config: EventAlertsPublicConfig | null = null;
  let statusText = '';
  let statusError = false;
  let selected = new Set<string>(['blood_castle', 'devil_square', 'chaos_castle', 'world_boss', 'invasion']);

  const refreshConfig = async () => {
    try {
      const resp = await fetch('/website/event-alerts', { cache: 'no-store' });
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      config = (await resp.json()) as EventAlertsPublicConfig;
      if (config.categories?.length) {
        selected = new Set(config.categories);
      }
    } catch {
      config = { off: true, leadTimeMinutes: 5, categories: [], vapidPublicKey: '' };
    }
    render();
  };

  const render = () => {
    container.replaceChildren();
    const wrap = document.createElement('div');
    wrap.style.display = 'flex';
    wrap.style.flexDirection = 'column';
    wrap.style.gap = '10px';

    if (!config || config.off || !config.vapidPublicKey) {
      const p = document.createElement('p');
      p.textContent = tx(
        'Browser push event alerts are currently turned off by the host.',
        'Thông báo trình duyệt cho sự kiện hiện đang tắt trên máy chủ.',
      );
      wrap.append(p);
      container.append(wrap);
      return;
    }

    const intro = document.createElement('div');
    intro.style.fontSize = '13px';
    intro.textContent = tx(
      `Receive browser notifications ${config.leadTimeMinutes} min before chosen events or boss spawns (Sydney schedule).`,
      `Nhận thông báo trình duyệt trước ${config.leadTimeMinutes} phút khi sự kiện hoặc Boss sắp mở (theo giờ Sydney).`,
    );
    wrap.append(intro);

    const grid = document.createElement('div');
    grid.style.display = 'flex';
    grid.style.flexWrap = 'wrap';
    grid.style.gap = '10px';
    const cats = config.categories?.length ? config.categories : Object.keys(CATEGORY_LABELS);
    for (const key of cats) {
      const lbl = document.createElement('label');
      lbl.style.display = 'flex';
      lbl.style.alignItems = 'center';
      lbl.style.gap = '5px';
      lbl.style.fontSize = '13px';
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.title = tx('Choose whether to receive alerts for this event.', 'Chọn nhận thông báo cho sự kiện này.');
      cb.checked = selected.has(key);
      cb.addEventListener('change', () => {
        if (cb.checked) selected.add(key);
        else selected.delete(key);
      });
      const textInfo = CATEGORY_LABELS[key];
      const name = textInfo ? (currentLanguage() === 'vi' ? textInfo.vi : textInfo.en) : key;
      lbl.append(cb, document.createTextNode(name));
      grid.append(lbl);
    }
    wrap.append(grid);

    const actions = document.createElement('div');
    actions.style.display = 'flex';
    actions.style.gap = '8px';
    const enableBtn = document.createElement('button');
    enableBtn.type = 'button';
    enableBtn.title = tx('Allow browser notifications for the events you selected.', 'Cho phép trình duyệt thông báo về các sự kiện đã chọn.');
    enableBtn.className = 'hnmo-btn hnmo-btn-primary';
    enableBtn.textContent = tx('Enable Browser Alerts', 'Bật Thông Báo Trình Duyệt');
    enableBtn.addEventListener('click', async () => {
      try {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
          throw new Error(tx('This browser does not support Web Push notifications.', 'Trình duyệt này chưa hỗ trợ Web Push.'));
        }
        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
          throw new Error(tx('Notification permission was declined.', 'Bạn chưa cấp quyền hiển thị thông báo.'));
        }
        const reg = await navigator.serviceWorker.register('/sw-alerts.js');
        await navigator.serviceWorker.ready;
        const appKey = urlBase64ToUint8Array(config!.vapidPublicKey);
        const keyBuffer = appKey.buffer.slice(appKey.byteOffset, appKey.byteOffset + appKey.byteLength) as ArrayBuffer;
        const sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBuffer });
        const resp = await fetch('/website/event-alerts/subscribe', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            endpoint: sub.endpoint,
            p256dh: arrayBufferToBase64Url(sub.getKey('p256dh')),
            auth: arrayBufferToBase64Url(sub.getKey('auth')),
            language: getLanguage() === 'vi' ? 'vi' : 'en',
            events: [...selected],
          }),
        });
        if (!resp.ok) throw new Error('HTTP ' + resp.status);
        statusError = false;
        statusText = tx('Subscribed to selected event alerts!', 'Đã đăng ký nhận thông báo sự kiện thành công!');
      } catch (err) {
        statusError = true;
        statusText = err instanceof Error ? err.message : String(err);
      }
      render();
    });

    const disableBtn = document.createElement('button');
    disableBtn.type = 'button';
    disableBtn.title = tx('Stop event notifications on this device.', 'Ngừng nhận thông báo sự kiện trên thiết bị này.');
    disableBtn.className = 'hnmo-btn';
    disableBtn.textContent = tx('Unsubscribe', 'Hủy Đăng Ký');
    disableBtn.addEventListener('click', async () => {
      try {
        const reg = 'serviceWorker' in navigator ? await navigator.serviceWorker.getRegistration('/sw-alerts.js') : null;
        const sub = reg ? await reg.pushManager.getSubscription() : null;
        if (sub) {
          await fetch('/website/event-alerts/unsubscribe', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ endpoint: sub.endpoint }),
          });
          await sub.unsubscribe();
        }
        statusError = false;
        statusText = tx('Browser alerts disabled for this device.', 'Đã tắt thông báo trình duyệt trên thiết bị này.');
      } catch (err) {
        statusError = true;
        statusText = err instanceof Error ? err.message : String(err);
      }
      render();
    });

    actions.append(enableBtn, disableBtn);
    wrap.append(actions);

    if (statusText) {
      const msg = document.createElement('div');
      msg.className = statusError ? 'hnmo-notice-err' : 'hnmo-notice-ok';
      msg.textContent = statusText;
      wrap.append(msg);
    }

    container.append(wrap);
  };

  onLanguageChange(() => render());
  await refreshConfig();
  return { refreshLanguage: render };
}

/** Modal panel wrapper for in-game Event Alerts subscription management. */
export class EventAlertsWindow {
  private readonly panel: HnmoPanel;

  constructor() {
    this.panel = createHnmoPanel('hnmo-event-alerts-panel', tx('Browser Event Alerts', 'Thông Báo Sự Kiện'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Browser Event Alerts', 'Thông Báo Sự Kiện'));
    });
  }

  /** Opens the opt-in modal and loads the live VAPID key + categories. */
  open(): void {
    this.panel.open();
    void mountEventAlertsWidget(this.panel.body);
  }
}

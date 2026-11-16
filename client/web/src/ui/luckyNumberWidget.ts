// Self-contained Daily Lucky Number widget for the website account page.
// Can be mounted by the account page without editing account.html/account script directly.

import type { LuckyNumberStatePayload } from './luckyNumberWindow.js';

export interface LuckyNumberWidgetCredentials {
  loginName: string;
  password: string;
  language?: 'en' | 'vi';
}

export function mountLuckyNumberWidget(
  container: HTMLElement,
  getCredentials: () => LuckyNumberWidgetCredentials | null,
): () => Promise<void> {
  const refresh = async (): Promise<void> => {
    const creds = getCredentials();
    if (!creds) {
      container.replaceChildren();
      return;
    }

    const vi = creds.language === 'vi';
    const res = await fetch('/portal/lucky-number/account', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ loginName: creds.loginName, password: creds.password }),
    });
    if (!res.ok) {
      container.textContent = vi ? 'Con số may mắn hiện chưa sẵn sàng.' : 'Daily Lucky Number is unavailable.';
      return;
    }

    const data = (await res.json()) as LuckyNumberStatePayload;
    container.replaceChildren();
    if (!data.enabled || !data.allowWebsite) {
      container.textContent = vi ? 'Chọn số trên website hiện đang tắt.' : 'Website lucky number picks are turned off.';
      return;
    }

    const heading = document.createElement('h3');
    heading.textContent = vi ? `Con số may mắn (${data.roundKey})` : `Daily Lucky Number (${data.roundKey})`;
    const info = document.createElement('p');
    info.textContent = vi
      ? `Khoảng ${data.minNumber}–${data.maxNumber} · Đã chọn: ${data.myPicks.map((p) => `#${p.number}`).join(', ') || 'Chưa chọn'}`
      : `Range ${data.minNumber}–${data.maxNumber} · Your picks: ${data.myPicks.map((p) => `#${p.number}`).join(', ') || 'None yet'}`;
    container.append(heading, info);

    if (data.myPicks.length < data.maxPicksPerDay) {
      const form = document.createElement('form');
      const input = document.createElement('input');
      input.type = 'number';
      input.min = String(data.minNumber);
      input.max = String(data.maxNumber);
      const btn = document.createElement('button');
      btn.type = 'submit';
      btn.textContent = vi ? 'Chọn số' : 'Pick number';
      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const num = Number.parseInt(input.value, 10);
        if (Number.isNaN(num)) return;
        await fetch('/portal/lucky-number/pick', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ loginName: creds.loginName, password: creds.password, number: num }),
        });
        await refresh();
      });
      form.append(input, btn);
      container.append(form);
    }
  };

  void refresh();
  return refresh;
}

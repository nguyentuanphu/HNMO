// Self-contained widget for ChatGPT's account page: shows cumulative VND progress and lets
// signed-in players claim reached milestone steps via /portal/topup-milestones/*.

export interface AccountCredentialsProvider {
  getCredentials: () => { loginName: string; password: string } | null;
  getLanguage?: () => 'en' | 'vi';
}

interface WidgetStep {
  id: string;
  thresholdVnd: number;
  titleEn: string;
  titleVi: string;
  coinType: string;
  coins: number;
  zen: number;
  items: { name?: string; level: number; quantity: number }[];
  reached: boolean;
  claimed: boolean;
  claimable: boolean;
}

interface WidgetAccountState {
  enabled: boolean;
  totalVnd: number;
  nextThresholdVnd?: number | null;
  steps: WidgetStep[];
  messageEn?: string;
  messageVi?: string;
}

/**
 * Mounts the self-contained Top-up milestones progress & claim panel inside `container`.
 * Uses POST /portal/topup-milestones/account and POST /portal/topup-milestones/claim.
 */
export function mountTopUpMilestonesWidget(
  container: HTMLElement,
  provider: AccountCredentialsProvider,
): { refresh: () => Promise<void> } {
  const lang = (): 'en' | 'vi' => provider.getLanguage?.() ?? 'en';
  const L = (en: string, vi: string): string => (lang() === 'vi' ? vi : en);
  const fmt = (n: number): string => n.toLocaleString(lang() === 'vi' ? 'vi-VN' : 'en-AU');

  const render = (state: WidgetAccountState | null, notice = ''): void => {
    container.replaceChildren();
    const wrap = document.createElement('div');
    wrap.className = 'hnmo-topup-milestones-widget';
    if (!state || !state.enabled) {
      wrap.textContent = L('Top-up milestones are currently disabled.', 'Thưởng mốc nạp hiện đang tắt.');
      container.append(wrap);
      return;
    }

    const head = document.createElement('p');
    head.innerHTML = `<strong>${L('Confirmed top-up total', 'Tổng nạp xác nhận')}:</strong> ${fmt(state.totalVnd)} VND`
      + (state.nextThresholdVnd ? ` · ${L('Next milestone', 'Mốc tiếp theo')}: ${fmt(state.nextThresholdVnd)} VND` : '');
    wrap.append(head);

    if (notice) {
      const msg = document.createElement('p');
      msg.textContent = notice;
      wrap.append(msg);
    }

    for (const step of state.steps) {
      const row = document.createElement('div');
      row.style.cssText = 'display:flex;justify-content:space-between;align-items:center;gap:8px;padding:8px 0;border-bottom:1px solid rgba(255,255,255,0.1);';
      const title = document.createElement('span');
      const label = lang() === 'vi' && step.titleVi ? step.titleVi : step.titleEn;
      title.textContent = `${fmt(step.thresholdVnd)} VND — ${label} (${step.coins} ${step.coinType})`;
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.textContent = step.claimed ? L('Claimed', 'Đã nhận') : step.claimable ? L('Claim', 'Nhận thưởng') : L('Locked', 'Chưa đạt');
      btn.disabled = !step.claimable;
      btn.addEventListener('click', () => {
        const creds = provider.getCredentials();
        if (!creds) return;
        void fetch('/portal/topup-milestones/claim', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ loginName: creds.loginName, password: creds.password, stepId: step.id }),
        }).then((r) => (r.ok ? r.json() as Promise<WidgetAccountState> : null)).then((updated) => {
          if (updated) render(updated, (lang() === 'vi' ? updated.messageVi : updated.messageEn) ?? '');
        });
      });
      row.append(title, btn);
      wrap.append(row);
    }
    container.append(wrap);
  };

  const refresh = async (): Promise<void> => {
    const creds = provider.getCredentials();
    if (!creds) return;
    const response = await fetch('/portal/topup-milestones/account', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(creds),
    });
    if (!response.ok) return;
    const data = (await response.json()) as WidgetAccountState;
    render(data);
  };

  return { refresh };
}

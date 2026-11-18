// Standalone mountable Account-Page Escrow Contracts section (ROADMAP 16.1 + 16.3).
// Does not modify the core account HTML shell; callers mount via mountEscrowContractsSection(el, options).

import { currentLanguage, onLanguageChange } from './i18n.js';

export interface EscrowAccountMountOptions {
  loginName: string;
  password: string;
  baseUrl?: string;
}

interface AccountEscrowResponse {
  v: number;
  enabled: boolean;
  reputation?: { score: number; completed: number; cancelled: number };
  contracts?: Array<{
    contractId: string;
    isSeller: boolean;
    status: string;
    priceCurrency: string;
    priceAmount: number;
    feeAmount: number;
    createdAtSydney: string;
    expiresAtSydney: string;
    frozenByAdmin: boolean;
  }>;
  owedDeliveries?: Array<{ contractId: string; reasonEn: string; reasonVi: string; zen: number; itemCount: number; jewelCount: number }>;
  error?: string;
}

export function mountEscrowContractsSection(container: HTMLElement, options: EscrowAccountMountOptions): () => void {
  const base = options.baseUrl ?? '';
  let destroyed = false;
  let lastData: AccountEscrowResponse | null = null;
  let statusNotice = '';
  let showHelp = false;

  const t = (en: string, vi: string) => (currentLanguage() === 'vi' ? vi : en);

  async function refresh(): Promise<void> {
    try {
      const res = await fetch(`${base}/portal/escrow/account`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ loginName: options.loginName, password: options.password }),
      });
      lastData = (await res.json()) as AccountEscrowResponse;
    } catch {
      lastData = { v: 1, enabled: false, error: 'network' };
    }
    if (!destroyed) render();
  }

  async function cancelContract(contractId: string): Promise<void> {
    try {
      const res = await fetch(`${base}/portal/escrow/cancel`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ loginName: options.loginName, password: options.password, contractId }),
      });
      const data = (await res.json()) as { messageEn?: string; messageVi?: string; error?: string };
      statusNotice = t(data.messageEn ?? data.error ?? '', data.messageVi ?? data.error ?? '');
    } catch {
      statusNotice = t('Network error while cancelling.', 'Lỗi mạng khi hủy hợp đồng.');
    }
    await refresh();
  }

  function render(): void {
    container.replaceChildren();
    const wrap = document.createElement('section');
    wrap.className = 'hnmo-escrow-account-section';
    wrap.style.cssText = 'border:1px solid #3c3a32;border-radius:8px;padding:12px;background:#12151f;color:#e6e8ef;';

    const head = document.createElement('div');
    head.style.cssText = 'display:flex;justify-content:space-between;align-items:center;margin-bottom:8px;';
    const title = document.createElement('h3');
    title.style.cssText = 'margin:0;color:#f1cf7a;font-size:15px;';
    title.textContent = t('Escrow Contracts', 'Hợp đồng Ký gửi');

    const helpBtn = document.createElement('button');
    helpBtn.type = 'button';
    helpBtn.textContent = '?';
    helpBtn.title = t('Help for account Escrow Contracts', 'Hướng dẫn mục Hợp đồng Ký gửi');
    helpBtn.addEventListener('click', () => {
      showHelp = !showHelp;
      render();
    });
    head.append(title, helpBtn);
    wrap.append(head);

    if (showHelp) {
      const help = document.createElement('p');
      help.style.cssText = 'font-size:12px;background:#172236;padding:6px 8px;border-radius:4px;';
      help.textContent = t(
        'View your active escrow contracts, reputation score, owed deliveries, and cancel open contracts offline.',
        'Xem hợp đồng đang mở, điểm uy tín, quà chờ nhận và hủy hợp đồng ngay cả khi ngoại tuyến.',
      );
      wrap.append(help);
    }

    if (statusNotice) {
      const n = document.createElement('div');
      n.style.cssText = 'font-size:12px;margin-bottom:6px;color:#b7f5d0;';
      n.textContent = statusNotice;
      wrap.append(n);
    }

    if (!lastData || lastData.enabled === false) {
      const p = document.createElement('p');
      p.textContent = t('Escrow contracts are currently unavailable.', 'Hợp đồng ký gửi hiện chưa bật.');
      wrap.append(p);
      container.append(wrap);
      return;
    }

    if (lastData.reputation) {
      const rep = document.createElement('div');
      rep.style.cssText = 'font-size:12px;margin-bottom:8px;color:#f1cf7a;';
      rep.textContent = `${t('Reputation', 'Uy tín')}: ${lastData.reputation.score} (${lastData.reputation.completed} ${t('completed', 'hoàn tất')}, ${lastData.reputation.cancelled} ${t('cancelled', 'đã hủy')})`;
      wrap.append(rep);
    }

    for (const c of lastData.contracts ?? []) {
      const row = document.createElement('div');
      row.style.cssText = 'display:flex;justify-content:space-between;align-items:center;padding:5px 8px;border:1px solid #2b3040;border-radius:4px;margin-bottom:4px;font-size:12px;';
      const info = document.createElement('span');
      info.textContent = `${c.contractId} [${c.status}] — ${c.priceAmount.toLocaleString()} ${c.priceCurrency} (${c.expiresAtSydney})`;
      row.append(info);
      if (c.isSeller && c.status === 'Open' && !c.frozenByAdmin) {
        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.textContent = t('Cancel', 'Hủy');
        cancelBtn.addEventListener('click', () => { void cancelContract(c.contractId); });
        row.append(cancelBtn);
      }
      wrap.append(row);
    }

    container.append(wrap);
  }

  onLanguageChange(() => { if (!destroyed) render(); });
  void refresh();
  return () => { destroyed = true; };
}

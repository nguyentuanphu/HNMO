// Self-contained Account-page widget for Jewel Bank & Master Skill Clearing (/portal/jewel-bank/*).
// Does not touch account.html or website.ts account logic; mount via mountJewelBankWidget(container, creds).

export interface JewelBankCredentials {
  loginName: string;
  password: string;
  language?: 'en' | 'vi';
}

interface JewelAccountResponse {
  v: number;
  busy: boolean;
  state: {
    enabled: boolean;
    rules: { allowClearMasterSkills: boolean; clearMasterZen: number; clearMasterCoins: number; clearMasterCoinType: string };
    jewels: { key: string; nameEn: string; nameVi: string; enabled: boolean; balance: number; maxCapacity: number }[];
    pending: { id: string; key: string; quantity: number }[];
    event?: { result: string; textEn?: string; textVi?: string } | null;
  };
  characters: { name: string; masterFreePoints: number; masterSpentPoints: number; zen: number }[];
}

export async function mountJewelBankWidget(container: HTMLElement, creds: JewelBankCredentials): Promise<void> {
  const vi = creds.language === 'vi';
  const tx = (en: string, v: string) => (vi ? v : en);

  container.innerHTML = `<p class="muted">${tx('Loading Jewel Bank…', 'Đang tải Ngân hàng ngọc…')}</p>`;

  const post = async (endpoint: string, body: Record<string, unknown>): Promise<JewelAccountResponse | { error?: string }> => {
    const res = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ loginName: creds.loginName, password: creds.password, ...body }),
    });
    return (await res.json()) as JewelAccountResponse | { error?: string };
  };

  const render = (data: JewelAccountResponse, notice?: string) => {
    container.replaceChildren();
    if (notice) {
      const p = document.createElement('p');
      p.textContent = notice;
      container.append(p);
    }

    for (const j of data.state.jewels.filter((x) => x.enabled)) {
      const row = document.createElement('div');
      row.className = 'panel';
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.marginBottom = '6px';

      const lbl = document.createElement('span');
      lbl.textContent = `${vi ? j.nameVi : j.nameEn}: ${j.balance.toLocaleString()} / ${j.maxCapacity.toLocaleString()}`;

      const btn = document.createElement('button');
      btn.type = 'button';
      btn.disabled = j.balance <= 0;
      btn.textContent = tx('Withdraw 1 to game', 'Rút 1 vào game');
      btn.addEventListener('click', async () => {
        const next = await post('/portal/jewel-bank/withdraw', { jewelKey: j.key, quantity: 1 });
        if ('state' in next) {
          const msg = vi ? next.state.event?.textVi : next.state.event?.textEn;
          render(next, msg ?? undefined);
        }
      });
      row.append(lbl, btn);
      container.append(row);
    }

    if (data.state.rules.allowClearMasterSkills && data.characters.length > 0) {
      for (const ch of data.characters) {
        const cRow = document.createElement('div');
        cRow.className = 'panel';
        cRow.style.display = 'flex';
        cRow.style.justifyContent = 'space-between';
        cRow.style.alignItems = 'center';
        cRow.style.marginBottom = '6px';

        const cLbl = document.createElement('span');
        cLbl.textContent = `${ch.name}: ${tx('Master spent', 'Master đã cộng')} ${ch.masterSpentPoints}, ${tx('free', 'chưa cộng')} ${ch.masterFreePoints}`;

        const cBtn = document.createElement('button');
        cBtn.type = 'button';
        cBtn.disabled = data.busy || ch.masterSpentPoints <= 0;
        cBtn.textContent = tx('Clear master points', 'Tẩy điểm master');
        cBtn.addEventListener('click', async () => {
          const next = await post('/portal/jewel-bank/clear-master', { character: ch.name });
          if ('state' in next) {
            const msg = vi ? next.state.event?.textVi : next.state.event?.textEn;
            render(next, msg ?? undefined);
          }
        });
        cRow.append(cLbl, cBtn);
        container.append(cRow);
      }
    }
  };

  try {
    const initial = await post('/portal/jewel-bank/account', {});
    if ('state' in initial) {
      render(initial);
    } else {
      container.innerHTML = `<p class="muted">${tx('Jewel Bank is unavailable.', 'Ngân hàng ngọc hiện tắt.')}</p>`;
    }
  } catch {
    container.innerHTML = `<p class="muted">${tx('Could not reach Jewel Bank.', 'Không thể kết nối Ngân hàng ngọc.')}</p>`;
  }
}

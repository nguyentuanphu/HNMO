// Self-contained website Account-page widget for Item Market: view coin balances, buy coin listings into
// escrow, or cancel your own listings via /portal/market/*.
// Note: Does not modify account.html or website.ts account logic; mount via mountMarketWidget(container, credentials).

export interface MarketCredentials {
  loginName: string;
  password: string;
  language?: 'en' | 'vi';
}

interface WidgetListing {
  id: string;
  sellerName: string;
  isMine: boolean;
  currency: string;
  price: number;
  expiresAt: number;
  item: { NameEn: string; NameVi: string; Level: number };
}

interface WidgetState {
  enabled: boolean;
  listings: WidgetListing[];
  deliveries: { Id?: string; id?: string; NameEn?: string; nameEn?: string; NameVi?: string; nameVi?: string }[];
  pendingZen: number;
}

interface AccountMarketResponse {
  v: number;
  result?: string;
  state: WidgetState;
  coins: { wCoinC: number; wCoinP: number; goblinPoints: number };
}

export async function mountMarketWidget(container: HTMLElement, creds: MarketCredentials): Promise<void> {
  const vi = creds.language === 'vi';
  const tx = (en: string, v: string) => (vi ? v : en);

  container.innerHTML = `<p class="muted">${tx('Loading Item Market…', 'Đang tải Chợ vật phẩm…')}</p>`;

  const post = async (endpoint: string, listingId?: string): Promise<AccountMarketResponse | { error?: string }> => {
    const res = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ loginName: creds.loginName, password: creds.password, listingId }),
    });
    return (await res.json()) as AccountMarketResponse | { error?: string };
  };

  const render = (data: AccountMarketResponse, notice?: string) => {
    container.replaceChildren();
    const coins = document.createElement('p');
    coins.className = 'muted';
    coins.textContent = `${tx('Balances', 'Số dư')}: ${data.coins.wCoinC.toLocaleString()} WCoinC • ${data.coins.wCoinP.toLocaleString()} WCoinP • ${data.coins.goblinPoints.toLocaleString()} GoblinPoints`;
    container.append(coins);

    if (notice) {
      const note = document.createElement('p');
      note.textContent = notice;
      container.append(note);
    }

    if (data.state.deliveries.length > 0 || data.state.pendingZen > 0) {
      const escrow = document.createElement('p');
      escrow.textContent = tx(
        `Pending in-game claim (F6): ${data.state.deliveries.length} item(s), ${data.state.pendingZen.toLocaleString()} Zen.`,
        `Đồ chờ nhận trong game (F6): ${data.state.deliveries.length} vật phẩm, ${data.state.pendingZen.toLocaleString()} Zen.`,
      );
      container.append(escrow);
    }

    for (const l of data.state.listings) {
      const row = document.createElement('div');
      row.className = 'panel';
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.marginBottom = '6px';

      const name = (vi ? l.item.NameVi : l.item.NameEn) || l.item.NameEn;
      const info = document.createElement('span');
      info.textContent = `${name}${l.item.Level > 0 ? ` +${l.item.Level}` : ''} — ${l.price.toLocaleString()} ${l.currency} (${l.sellerName})`;

      const btn = document.createElement('button');
      btn.type = 'button';
      if (l.isMine) {
        btn.textContent = tx('Cancel', 'Hủy');
        btn.addEventListener('click', async () => {
          const next = await post('/portal/market/cancel', l.id);
          if ('state' in next) render(next, tx('Canceled. Claim item in-game (F6).', 'Đã hủy. Vào game bấm F6 để nhận lại đồ.'));
        });
      } else {
        btn.textContent = tx('Buy', 'Mua');
        btn.disabled = l.currency === 'Zen';
        btn.title = l.currency === 'Zen' ? tx('Buy Zen listings in-game (F6).', 'Vật phẩm Zen mua trong game (F6).') : '';
        btn.addEventListener('click', async () => {
          const next = await post('/portal/market/buy', l.id);
          if ('state' in next) {
            const msg = next.state ? `${next.result ?? 'OK'}` : '';
            render(next, msg);
          }
        });
      }

      row.append(info, btn);
      container.append(row);
    }
  };

  try {
    const initial = await post('/portal/market/account');
    if ('state' in initial) {
      render(initial);
    } else {
      container.innerHTML = `<p class="muted">${tx('Item Market is unavailable.', 'Chợ vật phẩm hiện không khả dụng.')}</p>`;
    }
  } catch {
    container.innerHTML = `<p class="muted">${tx('Could not reach Item Market.', 'Không thể kết nối Chợ vật phẩm.')}</p>`;
  }
}

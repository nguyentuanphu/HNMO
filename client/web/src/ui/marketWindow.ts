// HNMO Item Market in-game window (F6 or NPC 547 Julia): browse/search/filter listings, sell bag items into
// escrow, cancel own listings, and claim bought items or earned Zen.
// Uses F5 38 (HnmoMarketCommand) and F5 39 (HnmoMarketState).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export type MarketCurrencyName = 'Zen' | 'WCoinC' | 'WCoinP' | 'GoblinPoints';

export interface MarketListingItem {
  Group: number;
  Number: number;
  Level: number;
  Durability: number;
  Skill: boolean;
  Luck: boolean;
  OptionLevel: number;
  ExcellentOptions: number;
  NameEn: string;
  NameVi: string;
}

export interface MarketListingDto {
  id: string;
  sellerName: string;
  isMine: boolean;
  currency: MarketCurrencyName;
  currencyCode: number;
  price: number;
  listedAt: number;
  expiresAt: number;
  item: MarketListingItem;
}

export interface MarketBagItemDto {
  slot: number;
  group: number;
  number: number;
  level: number;
  nameEn: string;
  nameVi: string;
  banned: boolean;
}

export interface MarketDeliveryDto {
  id?: string;
  Id?: string;
  reason?: string;
  Reason?: string;
  nameEn?: string;
  NameEn?: string;
  nameVi?: string;
  NameVi?: string;
  level?: number;
  Level?: number;
}

export interface MarketStatePayload {
  v: number;
  enabled: boolean;
  allowWebsite: boolean;
  now: number;
  rules: {
    allowZen: boolean;
    allowWCoinC: boolean;
    allowWCoinP: boolean;
    allowGoblinPoints: boolean;
    feePercent: number;
    listingZenFee: number;
    maxListings: number;
    /** The one-off access fee of an account (0: none) and its coin. */
    accessFee?: number;
    buyerAccessFee?: number;
    accessCoin?: string;
  };
  /** Whether this account may sell / buy on this season's market (null on public states). */
  accessPaid?: boolean | null;
  buyerAccessPaid?: boolean | null;
  listings: MarketListingDto[];
  deliveries: MarketDeliveryDto[];
  pendingZen: number;
  bagItems: MarketBagItemDto[];
  event?: { result: string; textEn?: string; textVi?: string } | null;
}

export interface MarketWindowOptions {
  send: (name: string, fields: PacketValues) => void;
  message: (text: string) => void;
}

const SUBCODE_STATE = 0x39;
const CMD = { Request: 1, List: 2, Buy: 3, Cancel: 4, Claim: 5, PayAccess: 6, PayBuyerAccess: 7 } as const;
const CURRENCY_CODE: Record<MarketCurrencyName, number> = {
  Zen: 0,
  WCoinC: 1,
  WCoinP: 2,
  GoblinPoints: 3,
};

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function tx(en: string, vi: string): string {
  return isVi() ? vi : en;
}

function locale(): string {
  return isVi() ? 'vi-VN' : 'en-AU';
}

function sydneyAndLocal(ms: number): string {
  const sydney = new Intl.DateTimeFormat(locale(), {
    month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'Australia/Sydney',
  }).format(ms);
  const local = new Intl.DateTimeFormat(locale(), {
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(ms);
  return `${sydney} AEDT (${local} local)`;
}

function itemLabel(item: { NameEn: string; NameVi: string; Level: number; Skill?: boolean; Luck?: boolean; OptionLevel?: number; ExcellentOptions?: number }): string {
  const base = (isVi() ? item.NameVi : item.NameEn) || item.NameEn;
  const parts: string[] = [base];
  if (item.Level > 0) parts.push(`+${item.Level}`);
  if (item.Skill) parts.push('[Skill]');
  if (item.Luck) parts.push('[Luck]');
  if ((item.OptionLevel ?? 0) > 0) parts.push(`+${(item.OptionLevel ?? 0) * 4}`);
  if ((item.ExcellentOptions ?? 0) > 0) parts.push('[Exc]');
  return parts.join(' ');
}

export class MarketWindow {
  private readonly panel: HnmoPanel;
  private state: MarketStatePayload | null = null;
  private tab: 'browse' | 'sell' | 'mine' = 'browse';
  private searchText = '';
  private currencyFilter = 'ALL';
  private pendingOpen = false;
  private selectedSlot: number | null = null;
  private selectedCurrency: MarketCurrencyName = 'WCoinC';
  private askingPrice = '100';

  constructor(private readonly options: MarketWindowOptions) {
    this.panel = createHnmoPanel('hnmo-market-panel', tx('Item Market (F6)', 'Chợ Vật Phẩm (F6)'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Item Market (F6)', 'Chợ Vật Phẩm (F6)'));
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public open(): void {
    this.pendingOpen = true;
    this.panel.setTitle(tx('Item Market (F6)', 'Chợ Vật Phẩm (F6)'));
    this.panel.open();
    this.requestState();
    this.render();
  }

  public close(): void {
    this.pendingOpen = false;
    this.panel.close();
  }

  public toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  public requestState(): void {
    this.options.send('HnmoMarketCommand', { Action: CMD.Request, Slot: 0, Currency: 0, Price: 0, ListingId: '' });
  }

  public handle(bytes: Uint8Array): boolean {
    if (bytes.length < 4 || bytes[0] !== 0xC2 || bytes[3] !== 0xF5 || bytes[4] !== SUBCODE_STATE) {
      return false;
    }

    const parsed = read('ServerToClient', 'HnmoMarketState', bytes);
    const json = typeof parsed.Json === 'string' ? parsed.Json : '';
    if (!json) return true;

    try {
      const next = JSON.parse(json) as MarketStatePayload;
      this.state = next;
      if (next.event) {
        const msg = isVi() ? (next.event.textVi || next.event.textEn) : (next.event.textEn || next.event.textVi);
        if (msg) this.options.message(msg);
      }

      if (this.pendingOpen || next.event?.result === 'Open' || next.event?.result === 'Claimed') {
        if (!this.panel.isOpen() && (this.pendingOpen || next.event?.result === 'Open')) {
          this.panel.open();
        }
        this.pendingOpen = false;
      }

      if (this.panel.isOpen()) this.render();
    } catch {
      // Ignore malformed JSON packets.
    }
    return true;
  }

  private render(): void {
    const body = this.panel.body;
    body.replaceChildren();

    const s = this.state;
    if (!s) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = tx('Loading marketplace…', 'Đang tải Chợ Vật Phẩm…');
      body.append(p);
      return;
    }

    if (!s.enabled) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = tx('Item Market is currently disabled by the server administrator.', 'Chợ Vật Phẩm hiện đang tắt.');
      body.append(p);
      return;
    }

    // Header rules summary
    const info = document.createElement('div');
    info.className = 'hnmo-muted';
    info.textContent = tx(
      `Sale tax: ${s.rules.feePercent}% • Listing Zen fee: ${s.rules.listingZenFee.toLocaleString(locale())} • Max listings: ${s.rules.maxListings}`,
      `Thuế chợ: ${s.rules.feePercent}% • Phí đăng: ${s.rules.listingZenFee.toLocaleString(locale())} Zen • Tối đa: ${s.rules.maxListings} tin`,
    );
    body.append(info);

    // The one-off access fees of this season's market: selling (and buying), or buying only (cheaper).
    const sellFee = s.rules.accessFee ?? 0;
    const buyFee = s.rules.buyerAccessFee ?? 0;
    const canSell = s.accessPaid !== false || sellFee <= 0;
    const canBuy = s.buyerAccessPaid !== false || buyFee <= 0 || canSell;
    if (!canSell || !canBuy) {
      const accessBar = document.createElement('div');
      accessBar.className = 'hnmo-card';
      accessBar.id = 'market-access';
      const coin = s.rules.accessCoin ?? 'WCoinC';
      const amount = (n: number) => `${n.toLocaleString(locale())} ${coin}`;
      const label = document.createElement('span');
      label.textContent = canBuy
        ? tx(`Selling needs a one-off ${amount(sellFee)} for your account on this season's market.`, `Bán đồ cần trả phí một lần ${amount(sellFee)} cho tài khoản ở chợ season này.`)
        : tx(`One-off fees for your account on this season's market: buying ${amount(buyFee)}, selling (and buying) ${amount(sellFee)}.`, `Phí một lần cho tài khoản ở chợ season này: mua ${amount(buyFee)}, bán (và mua) ${amount(sellFee)}.`);
      accessBar.append(label);
      const button = (id: string, en: string, vi: string, action: number) => {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'hnmo-btn';
        btn.id = id;
        btn.textContent = tx(en, vi);
        btn.addEventListener('click', () => {
          this.options.send('HnmoMarketCommand', { Action: action, Slot: 0, Currency: 0, Price: 0, ListingId: '' });
        });
        accessBar.append(btn);
      };
      if (!canBuy) button('market-buyer-access-btn', `Unlock buying (${amount(buyFee)})`, `Mở quyền mua (${amount(buyFee)})`, CMD.PayBuyerAccess);
      button('market-access-btn', `Unlock selling (${amount(sellFee)})`, `Mở quyền bán (${amount(sellFee)})`, CMD.PayAccess);
      body.append(accessBar);
    }

    // Escrow Claim banner if items or Zen are waiting
    const claimCount = s.deliveries.length;
    if (claimCount > 0 || s.pendingZen > 0) {
      const claimBar = document.createElement('div');
      claimBar.className = 'hnmo-card';
      const label = document.createElement('span');
      label.textContent = tx(
        `Escrow mailbox: ${claimCount} item(s), ${s.pendingZen.toLocaleString(locale())} Zen waiting`,
        `Kho nhận đồ: ${claimCount} vật phẩm, ${s.pendingZen.toLocaleString(locale())} Zen đang chờ`,
      );
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'hnmo-btn';
      btn.id = 'market-claim-btn';
      btn.textContent = tx('Claim all to bag', 'Nhận tất cả');
      btn.addEventListener('click', () => {
        this.options.send('HnmoMarketCommand', { Action: CMD.Claim, Slot: 0, Currency: 0, Price: 0, ListingId: '' });
      });
      claimBar.append(label, btn);
      body.append(claimBar);
    }

    // Tabs
    const tabs = document.createElement('div');
    tabs.className = 'hnmo-tabs';
    const mkTab = (key: 'browse' | 'sell' | 'mine', en: string, vi: string, count?: number) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = `hnmo-tab${this.tab === key ? ' active' : ''}`;
      b.textContent = count !== undefined ? `${tx(en, vi)} (${count})` : tx(en, vi);
      b.addEventListener('click', () => {
        this.tab = key;
        this.render();
      });
      return b;
    };
    const mineCount = s.listings.filter((l) => l.isMine).length;
    tabs.append(
      mkTab('browse', 'Browse Market', 'Mua Đồ', s.listings.length),
      mkTab('sell', 'Sell from Bag', 'Đăng Bán'),
      mkTab('mine', 'My Listings', 'Tin Của Tôi', mineCount),
    );
    body.append(tabs);

    if (this.tab === 'browse') {
      this.renderBrowse(body, s);
    } else if (this.tab === 'sell') {
      this.renderSell(body, s);
    } else {
      this.renderMine(body, s);
    }
  }

  private renderBrowse(body: HTMLDivElement, s: MarketStatePayload): void {
    const filterRow = document.createElement('div');
    filterRow.className = 'hnmo-row';

    const search = document.createElement('input');
    search.className = 'hnmo-input';
    search.type = 'text';
    search.style.flex = '1';
    search.placeholder = tx('Search item or seller…', 'Tìm tên vật phẩm hoặc người bán…');
    search.value = this.searchText;
    search.addEventListener('input', () => {
      this.searchText = search.value;
      this.renderFilteredListings(listWrap, s);
    });

    const curSelect = document.createElement('select');
    curSelect.className = 'hnmo-select';
    for (const [val, lbl] of [['ALL', tx('All currencies', 'Mọi loại tiền')], ['Zen', 'Zen'], ['WCoinC', 'WCoinC'], ['WCoinP', 'WCoinP'], ['GoblinPoints', 'GoblinPoints']]) {
      const o = document.createElement('option');
      o.value = val;
      o.textContent = lbl;
      o.selected = this.currencyFilter === val;
      curSelect.append(o);
    }
    curSelect.addEventListener('change', () => {
      this.currencyFilter = curSelect.value;
      this.renderFilteredListings(listWrap, s);
    });

    const refresh = document.createElement('button');
    refresh.type = 'button';
    refresh.className = 'hnmo-btn';
    refresh.textContent = tx('Refresh', 'Làm mới');
    refresh.addEventListener('click', () => this.requestState());

    filterRow.append(search, curSelect, refresh);
    const listWrap = document.createElement('div');
    listWrap.style.display = 'flex';
    listWrap.style.flexDirection = 'column';
    listWrap.style.gap = '6px';

    body.append(filterRow, listWrap);
    this.renderFilteredListings(listWrap, s);
  }

  private renderFilteredListings(wrap: HTMLDivElement, s: MarketStatePayload): void {
    wrap.replaceChildren();
    const q = this.searchText.trim().toLowerCase();
    const items = s.listings.filter((l) => {
      if (this.currencyFilter !== 'ALL' && l.currency !== this.currencyFilter) return false;
      if (!q) return true;
      return l.item.NameEn.toLowerCase().includes(q)
        || l.item.NameVi.toLowerCase().includes(q)
        || l.sellerName.toLowerCase().includes(q);
    });

    if (items.length === 0) {
      const empty = document.createElement('p');
      empty.className = 'hnmo-muted';
      empty.textContent = tx('No matching listings on the market.', 'Không có vật phẩm nào phù hợp.');
      wrap.append(empty);
      return;
    }

    for (const l of items) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';

      const left = document.createElement('div');
      const title = document.createElement('div');
      title.innerHTML = '';
      const strong = document.createElement('strong');
      strong.textContent = itemLabel(l.item);
      const badge = document.createElement('span');
      badge.className = 'hnmo-badge';
      badge.style.marginLeft = '6px';
      badge.textContent = `${l.price.toLocaleString(locale())} ${l.currency}`;
      title.append(strong, badge);

      const meta = document.createElement('div');
      meta.className = 'hnmo-muted';
      meta.textContent = `${tx('Seller', 'Người bán')}: ${l.sellerName} • ${tx('Expires', 'Hết hạn')}: ${sydneyAndLocal(l.expiresAt)}`;
      left.append(title, meta);

      const right = document.createElement('div');
      if (l.isMine) {
        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.className = 'hnmo-btn danger';
        cancelBtn.textContent = tx('Cancel', 'Hủy');
        cancelBtn.addEventListener('click', () => {
          this.options.send('HnmoMarketCommand', { Action: CMD.Cancel, Slot: 0, Currency: 0, Price: 0, ListingId: l.id });
        });
        right.append(cancelBtn);
      } else {
        const buyBtn = document.createElement('button');
        buyBtn.type = 'button';
        buyBtn.className = 'hnmo-btn';
        buyBtn.textContent = tx('Buy', 'Mua');
        buyBtn.addEventListener('click', () => {
          this.options.send('HnmoMarketCommand', { Action: CMD.Buy, Slot: 0, Currency: 0, Price: 0, ListingId: l.id });
        });
        right.append(buyBtn);
      }

      card.append(left, right);
      wrap.append(card);
    }
  }

  private renderSell(body: HTMLDivElement, s: MarketStatePayload): void {
    const allowedCurrencies: MarketCurrencyName[] = [];
    if (s.rules.allowWCoinC) allowedCurrencies.push('WCoinC');
    if (s.rules.allowWCoinP) allowedCurrencies.push('WCoinP');
    if (s.rules.allowGoblinPoints) allowedCurrencies.push('GoblinPoints');
    if (s.rules.allowZen) allowedCurrencies.push('Zen');
    if (allowedCurrencies.length > 0 && !allowedCurrencies.includes(this.selectedCurrency)) {
      this.selectedCurrency = allowedCurrencies[0];
    }

    const listable = s.bagItems.filter((b) => !b.banned);
    if (listable.length === 0) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = tx('No listable items inside your inventory bag right now.', 'Không có vật phẩm nào trong túi có thể đăng bán.');
      body.append(p);
      return;
    }

    if (this.selectedSlot === null || !listable.some((b) => b.slot === this.selectedSlot)) {
      this.selectedSlot = listable[0].slot;
    }

    const form = document.createElement('div');
    form.className = 'hnmo-row';

    const itemSel = document.createElement('select');
    itemSel.className = 'hnmo-select';
    itemSel.style.flex = '1';
    for (const b of listable) {
      const o = document.createElement('option');
      o.value = String(b.slot);
      const lvl = b.level > 0 ? ` +${b.level}` : '';
      o.textContent = `[Slot ${b.slot}] ${(isVi() ? b.nameVi : b.nameEn) || b.nameEn}${lvl}`;
      o.selected = b.slot === this.selectedSlot;
      itemSel.append(o);
    }
    itemSel.addEventListener('change', () => {
      this.selectedSlot = Number(itemSel.value);
    });

    const curSel = document.createElement('select');
    curSel.className = 'hnmo-select';
    for (const c of allowedCurrencies) {
      const o = document.createElement('option');
      o.value = c;
      o.textContent = c;
      o.selected = c === this.selectedCurrency;
      curSel.append(o);
    }
    curSel.addEventListener('change', () => {
      this.selectedCurrency = curSel.value as MarketCurrencyName;
    });

    const priceIn = document.createElement('input');
    priceIn.className = 'hnmo-input';
    priceIn.type = 'number';
    priceIn.min = '1';
    priceIn.style.width = '110px';
    priceIn.value = this.askingPrice;
    priceIn.addEventListener('input', () => {
      this.askingPrice = priceIn.value;
      updatePreview();
    });

    const listBtn = document.createElement('button');
    listBtn.type = 'button';
    listBtn.className = 'hnmo-btn';
    listBtn.id = 'market-list-submit';
    listBtn.textContent = tx('List item', 'Đăng bán');
    listBtn.addEventListener('click', () => {
      const price = Math.max(0, Math.floor(Number(this.askingPrice) || 0));
      if (this.selectedSlot === null || price <= 0) return;
      this.options.send('HnmoMarketCommand', {
        Action: CMD.List,
        Slot: this.selectedSlot,
        Currency: CURRENCY_CODE[this.selectedCurrency],
        Price: price,
        ListingId: '',
      });
    });

    form.append(itemSel, curSel, priceIn, listBtn);
    const preview = document.createElement('div');
    preview.className = 'hnmo-muted';
    const updatePreview = () => {
      const p = Math.max(0, Math.floor(Number(this.askingPrice) || 0));
      const fee = Math.floor(p * (s.rules.feePercent / 100));
      const net = Math.max(0, p - fee);
      preview.textContent = tx(
        `Net payout if sold: ${net.toLocaleString(locale())} ${this.selectedCurrency} (${s.rules.feePercent}% tax = ${fee.toLocaleString(locale())}). Safe zone required.`,
        `Thực nhận khi bán được: ${net.toLocaleString(locale())} ${this.selectedCurrency} (thuế ${s.rules.feePercent}% = ${fee.toLocaleString(locale())}). Cần đứng trong thành.`,
      );
    };
    updatePreview();

    body.append(form, preview);
  }

  private renderMine(body: HTMLDivElement, s: MarketStatePayload): void {
    const mine = s.listings.filter((l) => l.isMine);
    if (mine.length === 0) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = tx('You have no active listings.', 'Bạn chưa đăng bán vật phẩm nào.');
      body.append(p);
      return;
    }

    for (const l of mine) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';
      const info = document.createElement('div');
      info.textContent = `${itemLabel(l.item)} — ${l.price.toLocaleString(locale())} ${l.currency} (${tx('expires', 'hết hạn')} ${sydneyAndLocal(l.expiresAt)})`;
      const cancelBtn = document.createElement('button');
      cancelBtn.type = 'button';
      cancelBtn.className = 'hnmo-btn danger';
      cancelBtn.textContent = tx('Cancel listing', 'Hủy tin đăng');
      cancelBtn.addEventListener('click', () => {
        this.options.send('HnmoMarketCommand', { Action: CMD.Cancel, Slot: 0, Currency: 0, Price: 0, ListingId: l.id });
      });
      card.append(info, cancelBtn);
      body.append(card);
    }
  }
}

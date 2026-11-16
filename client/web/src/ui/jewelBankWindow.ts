// HNMO Jewel Bank & Master Skill Reset window (F7): deposit/withdraw Bless, Soul, Life, Creation,
// Chaos, Harmony, Guardian, and custom jewels, or clear & refund Master Skill points.
// Uses F5 3A (HnmoJewelBankCommand) and F5 3B (HnmoJewelBankState).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface JewelEntryState {
  key: string;
  nameEn: string;
  nameVi: string;
  group: number;
  number: number;
  enabled: boolean;
  maxCapacity: number;
  balance: number;
}

export interface BagJewelState {
  slot: number;
  key: string;
  nameEn: string;
  nameVi: string;
}

export interface JewelBankStatePayload {
  v: number;
  enabled: boolean;
  allowWebsite: boolean;
  rules: {
    withdrawZenFee: number;
    depositZenFee: number;
    safeZoneOnly: boolean;
    allowClearMasterSkills: boolean;
    clearMasterZen: number;
    clearMasterCoins: number;
    clearMasterCoinType: string;
  };
  jewels: JewelEntryState[];
  pending: { id: string; key: string; quantity: number }[];
  bagJewels: BagJewelState[];
  master: { freePoints: number; spentPoints: number };
  event?: { result: string; textEn?: string; textVi?: string } | null;
}

export interface JewelBankWindowOptions {
  send: (name: string, fields: PacketValues) => void;
  message: (text: string) => void;
}

const SUBCODE_STATE = 0x3B;
const CMD = { Request: 1, DepositSlot: 2, DepositAll: 3, Withdraw: 4, ClearMaster: 5 } as const;

function isVi(): boolean {
  return currentLanguage() === 'vi';
}

function tx(en: string, vi: string): string {
  return isVi() ? vi : en;
}

function locale(): string {
  return isVi() ? 'vi-VN' : 'en-AU';
}

export class JewelBankWindow {
  private readonly panel: HnmoPanel;
  private state: JewelBankStatePayload | null = null;
  private qtyInputs: Map<string, number> = new Map();

  constructor(private readonly options: JewelBankWindowOptions) {
    this.panel = createHnmoPanel('hnmo-jewel-bank-panel', tx('Jewel Bank (F7)', 'Ngân Hàng Ngọc (F7)'));
    onLanguageChange(() => {
      this.panel.setTitle(tx('Jewel Bank (F7)', 'Ngân Hàng Ngọc (F7)'));
      if (this.panel.isOpen()) this.render();
    });
  }

  public isOpen(): boolean {
    return this.panel.isOpen();
  }

  public open(): void {
    this.panel.setTitle(tx('Jewel Bank (F7)', 'Ngân Hàng Ngọc (F7)'));
    this.panel.open();
    this.requestState();
    this.render();
  }

  public close(): void {
    this.panel.close();
  }

  public toggle(): void {
    if (this.panel.isOpen()) this.close();
    else this.open();
  }

  public requestState(): void {
    this.options.send('HnmoJewelBankCommand', { Action: CMD.Request, Slot: 0, Amount: 0, Key: '' });
  }

  public handle(bytes: Uint8Array): boolean {
    if (bytes.length < 4 || bytes[0] !== 0xC2 || bytes[3] !== 0xF5 || bytes[4] !== SUBCODE_STATE) {
      return false;
    }

    const parsed = read('ServerToClient', 'HnmoJewelBankState', bytes);
    const json = typeof parsed.Json === 'string' ? parsed.Json : '';
    if (!json) return true;

    try {
      const next = JSON.parse(json) as JewelBankStatePayload;
      this.state = next;
      if (next.event) {
        const msg = isVi() ? (next.event.textVi || next.event.textEn) : (next.event.textEn || next.event.textVi);
        if (msg) this.options.message(msg);
      }

      if (this.panel.isOpen()) this.render();
    } catch {
      // Ignore malformed JSON.
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
      p.textContent = tx('Loading Jewel Bank…', 'Đang tải Ngân Hàng Ngọc…');
      body.append(p);
      return;
    }

    if (!s.enabled) {
      const p = document.createElement('p');
      p.className = 'hnmo-muted';
      p.textContent = tx('Jewel Bank is currently disabled by the server administrator.', 'Ngân Hàng Ngọc hiện đang tắt.');
      body.append(p);
      return;
    }

    // Header actions
    const topBar = document.createElement('div');
    topBar.className = 'hnmo-row';
    const depAllBtn = document.createElement('button');
    depAllBtn.type = 'button';
    depAllBtn.className = 'hnmo-btn';
    depAllBtn.id = 'jb-deposit-all';
    depAllBtn.disabled = s.bagJewels.length === 0;
    depAllBtn.textContent = tx(`Deposit all jewels from bag (${s.bagJewels.length})`, `Gửi toàn bộ ngọc trong túi (${s.bagJewels.length})`);
    depAllBtn.addEventListener('click', () => {
      this.options.send('HnmoJewelBankCommand', { Action: CMD.DepositAll, Slot: 0, Amount: 0, Key: '' });
    });

    const feeInfo = document.createElement('span');
    feeInfo.className = 'hnmo-muted';
    feeInfo.textContent = tx(
      `Deposit fee: ${s.rules.depositZenFee.toLocaleString(locale())} Zen • Withdraw fee: ${s.rules.withdrawZenFee.toLocaleString(locale())} Zen`,
      `Phí gửi: ${s.rules.depositZenFee.toLocaleString(locale())} Zen • Phí rút: ${s.rules.withdrawZenFee.toLocaleString(locale())} Zen`,
    );
    topBar.append(depAllBtn, feeInfo);
    body.append(topBar);

    // Balances table
    for (const j of s.jewels.filter((x) => x.enabled)) {
      const card = document.createElement('div');
      card.className = 'hnmo-card';

      const left = document.createElement('div');
      const title = document.createElement('strong');
      title.textContent = isVi() ? j.nameVi : j.nameEn;
      const cap = document.createElement('div');
      cap.className = 'hnmo-muted';
      cap.textContent = `${tx('Banked', 'Trong kho')}: ${j.balance.toLocaleString(locale())} / ${j.maxCapacity.toLocaleString(locale())}`;
      left.append(title, cap);

      const right = document.createElement('div');
      right.className = 'hnmo-row';

      const qtyIn = document.createElement('input');
      qtyIn.className = 'hnmo-input';
      qtyIn.type = 'number';
      qtyIn.min = '1';
      qtyIn.max = '50';
      qtyIn.style.width = '64px';
      qtyIn.value = String(this.qtyInputs.get(j.key) ?? 1);
      qtyIn.addEventListener('input', () => {
        const val = Math.max(1, Math.min(50, Math.floor(Number(qtyIn.value) || 1)));
        this.qtyInputs.set(j.key, val);
      });

      const wdBtn = document.createElement('button');
      wdBtn.type = 'button';
      wdBtn.className = 'hnmo-btn';
      wdBtn.setAttribute('data-jb-withdraw', j.key);
      wdBtn.disabled = j.balance <= 0;
      wdBtn.textContent = tx('Withdraw', 'Rút');
      wdBtn.addEventListener('click', () => {
        const amount = Math.max(1, Math.min(50, Math.floor(Number(qtyIn.value) || 1)));
        this.options.send('HnmoJewelBankCommand', { Action: CMD.Withdraw, Slot: 0, Amount: amount, Key: j.key });
      });

      const bagMatch = s.bagJewels.find((b) => b.key === j.key);
      const depOneBtn = document.createElement('button');
      depOneBtn.type = 'button';
      depOneBtn.className = 'hnmo-btn';
      depOneBtn.disabled = !bagMatch;
      depOneBtn.textContent = tx('Deposit 1', 'Gửi 1');
      depOneBtn.addEventListener('click', () => {
        if (!bagMatch) return;
        this.options.send('HnmoJewelBankCommand', { Action: CMD.DepositSlot, Slot: bagMatch.slot, Amount: 1, Key: j.key });
      });

      right.append(depOneBtn, qtyIn, wdBtn);
      card.append(left, right);
      body.append(card);
    }

    // Master Skill Clearing section
    if (s.rules.allowClearMasterSkills) {
      const mCard = document.createElement('div');
      mCard.className = 'hnmo-card';
      const mLeft = document.createElement('div');
      const mTitle = document.createElement('strong');
      mTitle.textContent = tx('Master Skill Points Clearing', 'Tẩy Điểm Kỹ Năng Master');
      const mMeta = document.createElement('div');
      mMeta.className = 'hnmo-muted';
      const coinPart = s.rules.clearMasterCoins > 0 ? ` + ${s.rules.clearMasterCoins.toLocaleString(locale())} ${s.rules.clearMasterCoinType}` : '';
      mMeta.textContent = tx(
        `Spent: ${s.master.spentPoints} • Free: ${s.master.freePoints} • Cost: ${s.rules.clearMasterZen.toLocaleString(locale())} Zen${coinPart}`,
        `Đã cộng: ${s.master.spentPoints} • Chưa cộng: ${s.master.freePoints} • Phí: ${s.rules.clearMasterZen.toLocaleString(locale())} Zen${coinPart}`,
      );
      mLeft.append(mTitle, mMeta);

      const clearBtn = document.createElement('button');
      clearBtn.type = 'button';
      clearBtn.className = 'hnmo-btn danger';
      clearBtn.id = 'jb-clear-master';
      clearBtn.disabled = s.master.spentPoints <= 0;
      clearBtn.textContent = tx('Clear master skills', 'Tẩy điểm master');
      clearBtn.addEventListener('click', () => {
        this.options.send('HnmoJewelBankCommand', { Action: CMD.ClearMaster, Slot: 0, Amount: 0, Key: '' });
      });
      mCard.append(mLeft, clearBtn);
      body.append(mCard);
    }
  }
}

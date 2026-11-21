// HNMO Marriage window (Shift+M, touch "more" menu, or /marry).
// Supports proposals, Devias Church ceremonies, couple titles, party hunting bonus display, spouse warp, divorce, and the couples registry.

import { currentLanguage } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { HnmoModalPanel } from './hnmoPanel.js';

export interface MarriageStatePayload {
  v: number;
  enabled: boolean;
  now: number;
  rules: {
    minLevel: number;
    requireCeremonyLocation: boolean;
    ceremonyMapNumber: number;
    ceremonyX: number;
    ceremonyY: number;
    ceremonyZenCost: number;
    allowCustomTitle: boolean;
    partyExpBonusPercent: number;
    partyDefenseBonus: number;
    divorceZenFee: number;
    remarryCooldownHours: number;
    allowSpouseWarp: boolean;
  };
  me?: {
    character: string;
    married: boolean;
    spouse: string | null;
    titleEn: string;
    titleVi: string;
    customTitle: string;
    marriedAt: number;
    bonusActiveNow: boolean;
    incomingProposalFrom: string | null;
    outgoingProposalTo: string | null;
  } | null;
  couples: { id: string; partnerA: string; partnerB: string; title: string; marriedAt: number; days: number }[];
  event?: { result: string; textEn?: string; textVi?: string } | null;
}

export interface MarriageWindowOptions {
  send: (name: string, fields: PacketValues) => void;
  message: (text: string) => void;
}

const ACTION = {
  Request: 1,
  Propose: 2,
  Accept: 3,
  Decline: 4,
  Divorce: 5,
  SetTitle: 6,
  TeleportToSpouse: 7,
} as const;

function L(en: string, vi: string): string {
  return currentLanguage() === 'vi' ? vi : en;
}

function el<K extends keyof HTMLElementTagNameMap>(tag: K, text?: string, className?: string): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

export class MarriageWindow {
  private readonly panel: HnmoModalPanel;
  private state: MarriageStatePayload | null = null;

  constructor(private readonly options: MarriageWindowOptions) {
    this.panel = new HnmoModalPanel('hnmoMarriage', '', () => this.render());
  }

  get isOpen(): boolean {
    return this.panel.isOpen;
  }

  start(): void {
    this.request();
  }

  request(): void {
    this.options.send('HnmoMarriageCommand', { Action: ACTION.Request, Argument: '' });
  }

  open(): void {
    this.panel.open();
    this.request();
    this.render();
  }

  close(): void {
    this.panel.close();
  }

  toggle(): void {
    if (this.panel.isOpen) this.close();
    else this.open();
  }

  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x37) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoMarriageState', bytes);
    if (!Json) return true;
    try {
      const parsed = JSON.parse(Json) as MarriageStatePayload;
      if (typeof parsed !== 'object' || parsed === null) return true;
      this.state = parsed;
      if (parsed.event?.textEn || parsed.event?.textVi) {
        this.options.message(L(parsed.event.textEn ?? '', parsed.event.textVi ?? parsed.event.textEn ?? ''));
      }
      if (this.panel.isOpen) this.render();
    } catch {
      // Ignore malformed JSON.
    }
    return true;
  }

  render(): void {
    this.panel.setTitle(L('Marriage (Shift+M)', 'Kết hôn (Shift+M)'));
    const body = this.panel.body;
    body.replaceChildren();

    const s = this.state;
    if (!s) {
      body.append(el('p', L('Loading marriage information...', 'Đang tải thông tin Kết hôn...'), 'hnmo-muted'));
      return;
    }

    if (!s.enabled) {
      body.append(el('p', L('Marriage is currently turned off on this server.', 'Tính năng Kết hôn hiện đang tắt trên máy chủ.'), 'hnmo-muted'));
      return;
    }

    const r = s.rules;
    const rulesCard = el('div', undefined, 'hnmo-card');
    rulesCard.append(
      el('div', L(`Requirements: Lv ${r.minLevel}+ · Ceremony fee: ${r.ceremonyZenCost.toLocaleString()} Zen` + (r.requireCeremonyLocation ? ` · Devias Church (${r.ceremonyX}, ${r.ceremonyY})` : ''), `Điều kiện: Cấp ${r.minLevel}+ · Phí lễ: ${r.ceremonyZenCost.toLocaleString()} Zen` + (r.requireCeremonyLocation ? ` · Nhà thờ Devias (${r.ceremonyX}, ${r.ceremonyY})` : '')), 'hnmo-muted'),
      el('div', L(`Hunting together in party: +${r.partyExpBonusPercent}% EXP & +${r.partyDefenseBonus} Defense · Divorce fee: ${r.divorceZenFee.toLocaleString()} Zen (${r.remarryCooldownHours}h cooldown)`, `Tổ đội cùng săn quái: +${r.partyExpBonusPercent}% EXP & +${r.partyDefenseBonus} Phòng thủ · Phí ly hôn: ${r.divorceZenFee.toLocaleString()} Zen (chờ ${r.remarryCooldownHours}g)`), 'hnmo-muted'),
    );
    body.append(rulesCard);

    const me = s.me;
    if (me?.incomingProposalFrom) {
      const propCard = el('div', undefined, 'hnmo-card');
      propCard.append(el('strong', L(`Proposal from ${me.incomingProposalFrom}!`, `${me.incomingProposalFrom} đã gửi lời cầu hôn tới bạn!`), 'hnmo-warn'));
      const row = el('div');
      row.style.display = 'flex';
      row.style.gap = '8px';
      const acceptBtn = el('button', L('Accept at Ceremony', 'Đồng ý kết hôn'), 'hnmo-btn hnmo-btn-primary');
      acceptBtn.type = 'button';
      acceptBtn.addEventListener('click', () => this.options.send('HnmoMarriageCommand', { Action: ACTION.Accept, Argument: '' }));
      const declineBtn = el('button', L('Decline', 'Từ chối'), 'hnmo-btn');
      declineBtn.type = 'button';
      declineBtn.addEventListener('click', () => this.options.send('HnmoMarriageCommand', { Action: ACTION.Decline, Argument: '' }));
      row.append(acceptBtn, declineBtn);
      propCard.append(row);
      body.append(propCard);
    }

    if (me?.married && me.spouse) {
      const myCard = el('div', undefined, 'hnmo-card');
      myCard.append(
        el('strong', L(`Married to ${me.spouse}`, `Bạn đời: ${me.spouse}`), 'hnmo-ok'),
        el('div', L(`Couple title: ${me.titleEn}`, `Danh hiệu: ${me.titleVi}`)),
        el('div', me.bonusActiveNow ? L('Party couple buff ACTIVE!', 'Buff phu thê ĐANG KÍCH HOẠT!') : L('Join the same party & map as your spouse to activate the bonus.', 'Hãy vào cùng tổ đội và bản đồ với bạn đời để kích hoạt buff.'), me.bonusActiveNow ? 'hnmo-ok' : 'hnmo-muted'),
      );

      if (r.allowCustomTitle) {
        const form = el('form');
        form.style.display = 'flex';
        form.style.gap = '6px';
        const input = document.createElement('input');
        input.type = 'text';
        input.maxLength = 32;
        input.value = me.customTitle;
        input.placeholder = L('Custom couple title...', 'Nhập danh hiệu phu thê...');
        input.style.cssText = 'flex:1;padding:4px 8px;border-radius:5px;border:1px solid #8a6a30;background:#111520;color:#fff;';
        const saveBtn = el('button', L('Set title', 'Đặt danh hiệu'), 'hnmo-btn');
        saveBtn.type = 'submit';
        form.addEventListener('submit', (e) => {
          e.preventDefault();
          this.options.send('HnmoMarriageCommand', { Action: ACTION.SetTitle, Argument: input.value.trim() });
        });
        form.append(input, saveBtn);
        myCard.append(form);
      }

      const actionsRow = el('div');
      actionsRow.style.display = 'flex';
      actionsRow.style.gap = '8px';
      if (r.allowSpouseWarp) {
        const warpBtn = el('button', L('Warp to spouse', 'Tới bên bạn đời'), 'hnmo-btn hnmo-btn-primary');
        warpBtn.type = 'button';
        warpBtn.addEventListener('click', () => this.options.send('HnmoMarriageCommand', { Action: ACTION.TeleportToSpouse, Argument: '' }));
        actionsRow.append(warpBtn);
      }
      const divBtn = el('button', L('Divorce', 'Ly hôn'), 'hnmo-btn');
      divBtn.type = 'button';
      divBtn.addEventListener('click', () => this.options.send('HnmoMarriageCommand', { Action: ACTION.Divorce, Argument: '' }));
      actionsRow.append(divBtn);
      myCard.append(actionsRow);
      body.append(myCard);
    } else if (me) {
      const proposeCard = el('div', undefined, 'hnmo-card');
      proposeCard.append(el('strong', L('Propose Marriage', 'Gửi lời cầu hôn')));
      if (me.outgoingProposalTo) {
        proposeCard.append(el('div', L(`Waiting for ${me.outgoingProposalTo} to accept...`, `Đang chờ ${me.outgoingProposalTo} đồng ý...`), 'hnmo-warn'));
      }
      const form = el('form');
      form.style.display = 'flex';
      form.style.gap = '8px';
      const input = document.createElement('input');
      input.type = 'text';
      input.maxLength = 10;
      input.placeholder = L('Partner character name', 'Tên nhân vật bạn đời');
      input.style.cssText = 'flex:1;padding:4px 8px;border-radius:5px;border:1px solid #8a6a30;background:#111520;color:#fff;';
      const sendBtn = el('button', L('Propose', 'Cầu hôn'), 'hnmo-btn hnmo-btn-primary');
      sendBtn.type = 'submit';
      form.addEventListener('submit', (e) => {
        e.preventDefault();
        if (!input.value.trim()) return;
        this.options.send('HnmoMarriageCommand', { Action: ACTION.Propose, Argument: input.value.trim() });
      });
      form.append(input, sendBtn);
      proposeCard.append(form);
      body.append(proposeCard);
    }

    if (s.couples.length > 0) {
      const listCard = el('div', undefined, 'hnmo-card');
      listCard.append(el('strong', L(`Married Couples (${s.couples.length})`, `Danh sách Cặp đôi (${s.couples.length})`)));
      for (const c of s.couples.slice(0, 10)) {
        listCard.append(el('div', `${c.partnerA} ♥ ${c.partnerB}` + (c.title ? ` — ${c.title}` : '') + L(` (${c.days}d)`, ` (${c.days} ngày)`), 'hnmo-muted'));
      }
      body.append(listCard);
    }
  }
}

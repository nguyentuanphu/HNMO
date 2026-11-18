// HNMO In-Game Escrow Contracts window (F9, /contract, or CommandWindow 'Contract' -> right-click player).
// Uses F5 4C (HnmoEscrowCommand) and F5 4D (HnmoEscrowState).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { buildVietQrPayload, renderVietQrSvgDataUrl } from '../vietqr.js';
import { createHnmoPanel, type HnmoPanel } from './hnmoPanel.js';

export interface EscrowDirectVndDto {
  bankBin: string;
  bankName: string;
  bankAccountNumber: string;
  bankAccountName: string;
  paymentReference: string;
  webhookSecret?: string;
  webhookVerified?: boolean;
  sellerBondCurrency?: number | string;
  sellerBondCoins: number;
  serviceFeeCoins: number;
  paymentDeadlineSydney?: string;
  extendedOnce?: boolean;
  buyerNote?: string;
  receiptMime?: string;
  receiptBase64?: string;
  disputeReason?: string;
}

export interface EscrowItemDto {
  group: number;
  number: number;
  level: number;
  durability: number;
  skill: boolean;
  luck: boolean;
  optionLevel: number;
  excellentOptions: number;
  nameEn: string;
  nameVi: string;
  originSummary: string;
}

export interface EscrowJewelLineDto {
  jewelKey: string;
  quantity: number;
}

export interface EscrowContractDto {
  contractId: string;
  mode: string;
  status: number | string;
  createdAtSydney: string;
  expiresAtSydney: string;
  sellerLogin: string;
  sellerCharacter: string;
  targetBuyer: string;
  acceptedByLogin?: string;
  acceptedByCharacter?: string;
  items: EscrowItemDto[];
  escrowedZen: number;
  escrowedJewels: EscrowJewelLineDto[];
  priceCurrency: number | string;
  priceAmount: number;
  feePayer: number | string;
  feeAmount: number;
  buyerTotalDebit: number;
  netSellerProceeds: number;
  frozenByAdmin: boolean;
  adminReason: string;
  directVnd?: EscrowDirectVndDto | null;
}

export interface EscrowBagItemDto {
  slot: number;
  nameEn: string;
  nameVi: string;
  level: number;
  durability: number;
}

export interface EscrowOwedDto {
  deliveryId: string;
  contractId: string;
  reasonEn: string;
  reasonVi: string;
  zen: number;
  items: EscrowItemDto[];
  jewels: EscrowJewelLineDto[];
}

export interface EscrowReputationDto {
  loginName: string;
  completed: number;
  cancelled: number;
  disputesLost: number;
  score: number;
}

export interface EscrowStatePayload {
  enabled: boolean;
  accountLogin: string;
  characterName: string;
  prefillTarget: string;
  requiresPin: boolean;
  feePercent: number;
  minFee: number;
  maxFee: number;
  feePayer: string;
  allowedCurrencies: string[];
  highValueCoins: number;
  highValueZen: number;
  highValueJewels: number;
  cooldownHours: number;
  directVndEnabled?: boolean;
  directVndPaymentWindowMinutes?: number;
  directVndExtendMinutes?: number;
  directVndServiceFeeCurrency?: string;
  directVndServiceFeeCoins?: number;
  directVndServiceFeePercent?: number;
  directVndFeePayer?: string;
  directVndMaxSellerBondCoins?: number;
  directVndMaxReceiptBytes?: number;
  reputation: EscrowReputationDto;
  bagItems: EscrowBagItemDto[];
  jewelBalances: Record<string, number>;
  openContracts: EscrowContractDto[];
  historyContracts: EscrowContractDto[];
  owedDeliveries: EscrowOwedDto[];
  noticeEn?: string | null;
  noticeVi?: string | null;
  isError?: boolean;
}

export interface EscrowFeePreview {
  totalFee: number;
  sellerFee: number;
  buyerFee: number;
  buyerTotalDebit: number;
  netSellerProceeds: number;
}

const CURRENCY_BY_INDEX = [
  'Zen', 'WCoinC', 'WCoinP', 'GoblinPoints',
  'JewelBless', 'JewelSoul', 'JewelChaos', 'JewelCreation', 'JewelLife',
] as const;

const STATUS_BY_INDEX = [
  'Open', 'Completed', 'Cancelled', 'Expired', 'Frozen',
  'ReservedForPayment', 'BuyerMarkedPaid', 'Disputed',
] as const;

const BANK_PRESETS: ReadonlyArray<{ bin: string; name: string }> = [
  { bin: '970436', name: 'Vietcombank' },
  { bin: '970407', name: 'Techcombank' },
  { bin: '970422', name: 'MB Bank' },
  { bin: '970416', name: 'ACB' },
  { bin: '970418', name: 'BIDV' },
  { bin: '970415', name: 'VietinBank' },
  { bin: '970432', name: 'VPBank' },
  { bin: '970423', name: 'TPBank' },
];

export function currencyLabel(raw: number | string): string {
  if (typeof raw === 'number') return CURRENCY_BY_INDEX[raw] ?? String(raw);
  return raw;
}

export function statusLabel(raw: number | string): string {
  if (typeof raw === 'number') return STATUS_BY_INDEX[raw] ?? String(raw);
  return raw;
}

export function computeEscrowFee(
  priceAmount: number,
  feePercent: number,
  minFee: number,
  maxFee: number,
  feePayer: string,
): EscrowFeePreview {
  if (priceAmount <= 0 || feePercent <= 0) {
    return { totalFee: 0, sellerFee: 0, buyerFee: 0, buyerTotalDebit: priceAmount, netSellerProceeds: priceAmount };
  }
  const pct = Math.min(25, Math.max(0, feePercent));
  let raw = Math.round(priceAmount * (pct / 100));
  if (raw < minFee) raw = minFee;
  if (maxFee > 0 && raw > maxFee) raw = maxFee;
  const totalFee = Math.max(0, Math.min(raw, priceAmount));
  if (feePayer === 'Buyer') {
    return { totalFee, sellerFee: 0, buyerFee: totalFee, buyerTotalDebit: priceAmount + totalFee, netSellerProceeds: priceAmount };
  }
  if (feePayer === 'Split') {
    const buyerFee = Math.floor(totalFee / 2);
    const sellerFee = totalFee - buyerFee;
    return {
      totalFee,
      sellerFee,
      buyerFee,
      buyerTotalDebit: priceAmount + buyerFee,
      netSellerProceeds: Math.max(0, priceAmount - sellerFee),
    };
  }
  return {
    totalFee,
    sellerFee: totalFee,
    buyerFee: 0,
    buyerTotalDebit: priceAmount,
    netSellerProceeds: Math.max(0, priceAmount - totalFee),
  };
}

export interface EscrowWindowOptions {
  send: (packetName: string, fields: PacketValues) => void;
  message: (text: string) => void;
}

type EscrowTab = 'open' | 'create' | 'history';

export class EscrowWindow {
  private readonly panel: HnmoPanel;
  private state: EscrowStatePayload | null = null;
  private activeTab: EscrowTab = 'open';
  private showHelp = false;
  private targetPrefill = '';
  private selectedSlots = new Set<number>();
  private escrowedZenInput = '0';
  private escrowedJewelKey = 'JewelBless';
  private escrowedJewelQtyInput = '0';
  private priceCurrency = 'WCoinC';
  private priceAmountInput = '100';
  private deadlineHoursInput = '24';
  private pinInput = '';
  private confirmHighValue = false;
  private filterText = '';
  private createMode: 'InGame' | 'DirectVnd' = 'InGame';
  private bankBin = '970436';
  private bankName = 'Vietcombank';
  private bankAccountNumber = '';
  private bankAccountName = '';
  private sellerBondInput = '0';
  private webhookSecretInput = '';
  private disputeReasonInput = '';
  private pendingReceiptMime = '';
  private pendingReceiptBase64 = '';

  constructor(private readonly options: EscrowWindowOptions) {
    this.panel = createHnmoPanel('hnmo-escrow-panel', this.t('Escrow Contracts (F9)', 'Hợp đồng Ký gửi (F9)'));
    this.panel.panel.style.maxWidth = '760px';
    onLanguageChange(() => {
      this.panel.setTitle(this.t('Escrow Contracts (F9)', 'Hợp đồng Ký gửi (F9)'));
      if (this.panel.isOpen()) this.render();
    });
  }

  get isOpen(): boolean {
    return this.panel.isOpen();
  }

  open(tab: EscrowTab = 'open'): void {
    this.activeTab = tab;
    this.panel.open();
    this.requestState();
    this.render();
  }

  openForTarget(characterName: string): void {
    this.targetPrefill = characterName.trim();
    this.open('create');
  }

  close(): void {
    this.panel.close();
  }

  toggle(): void {
    if (this.isOpen) this.close();
    else this.open('open');
  }

  requestState(): void {
    this.options.send('HnmoEscrowCommand', { Action: 0, Payload: '{}' });
  }

  handlePacket(bytes: Uint8Array): boolean {
    if (bytes.length < 5 || bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x4d) return false;
    const parsed = read('ServerToClient', 'HnmoEscrowState', bytes);
    const raw = typeof parsed.Json === 'string' ? parsed.Json : '';
    if (!raw) return false;
    try {
      const next = JSON.parse(raw) as EscrowStatePayload;
      this.state = next;
      if (next.prefillTarget && next.prefillTarget.trim().length > 0) {
        this.targetPrefill = next.prefillTarget.trim();
        this.activeTab = 'create';
      }
      if (next.allowedCurrencies?.length && !next.allowedCurrencies.includes(this.priceCurrency)) {
        this.priceCurrency = next.allowedCurrencies[0];
      }
      const msg = this.t(next.noticeEn ?? '', next.noticeVi ?? '');
      if (msg) {
        this.options.message(msg);
      }
      if (!this.panel.isOpen() && msg) {
        this.panel.open();
      }
      if (this.panel.isOpen()) {
        this.render();
      }
    } catch {
      // Ignore malformed JSON.
    }
    return true;
  }

  private isVi(): boolean {
    return currentLanguage() === 'vi';
  }

  private t(en: string, vi: string): string {
    return this.isVi() ? vi : en;
  }

  private sendAction(action: number, payload: Record<string, unknown>): void {
    this.options.send('HnmoEscrowCommand', { Action: action, Payload: JSON.stringify(payload) });
  }

  private render(): void {
    const root = this.panel.body;
    root.replaceChildren();

    const s = this.state;
    const topBar = document.createElement('div');
    topBar.style.cssText = 'display:flex;flex-wrap:wrap;gap:6px;align-items:center;justify-content:space-between;margin-bottom:8px;';

    const tabs = document.createElement('div');
    tabs.style.cssText = 'display:flex;gap:6px;';
    const mkTab = (id: EscrowTab, en: string, vi: string) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.textContent = this.t(en, vi);
      b.style.cssText = `padding:4px 10px;border-radius:5px;border:1px solid #8a6a30;cursor:pointer;font:inherit;background:${this.activeTab === id ? '#8a6a30' : '#1b2030'};color:${this.activeTab === id ? '#111' : '#e6e8ef'};font-weight:${this.activeTab === id ? '700' : '400'};`;
      b.addEventListener('click', () => {
        this.activeTab = id;
        this.render();
      });
      return b;
    };
    tabs.append(
      mkTab('open', `Open Offers (${s?.openContracts?.length ?? 0})`, `Hợp đồng mở (${s?.openContracts?.length ?? 0})`),
      mkTab('create', 'Create Contract', 'Tạo hợp đồng'),
      mkTab('history', `History & Owed (${s?.owedDeliveries?.length ?? 0})`, `Lịch sử & Chờ nhận (${s?.owedDeliveries?.length ?? 0})`),
    );

    const actions = document.createElement('div');
    actions.style.cssText = 'display:flex;gap:6px;align-items:center;';
    if (s?.reputation) {
      const rep = document.createElement('span');
      rep.title = this.t('Your escrow reputation score (completed / cancelled / lost disputes)', 'Điểm uy tín hợp đồng của bạn');
      rep.style.cssText = 'font-size:12px;color:#f1cf7a;border:1px solid #5c4820;padding:2px 7px;border-radius:4px;';
      rep.textContent = `${this.t('Rep', 'Uy tín')}: ${s.reputation.score}`;
      actions.append(rep);
    }
    const refreshBtn = document.createElement('button');
    refreshBtn.type = 'button';
    refreshBtn.textContent = this.t('Refresh', 'Làm mới');
    refreshBtn.title = this.t('Reload live contract state', 'Tải lại danh sách hợp đồng');
    refreshBtn.style.cssText = 'padding:4px 9px;border-radius:5px;border:1px solid #555;background:#1e2433;color:#e6e8ef;cursor:pointer;font:inherit;';
    refreshBtn.addEventListener('click', () => this.requestState());

    const helpBtn = document.createElement('button');
    helpBtn.type = 'button';
    helpBtn.id = 'escrow-in-game-help-btn';
    helpBtn.textContent = '?';
    helpBtn.title = this.t('How Escrow Contracts work', 'Hướng dẫn Hợp đồng Ký gửi');
    helpBtn.style.cssText = 'padding:4px 8px;border-radius:5px;border:1px solid #4895ef;background:#172236;color:#9cc9ff;cursor:pointer;font:inherit;font-weight:700;';
    helpBtn.addEventListener('click', () => {
      this.showHelp = !this.showHelp;
      this.render();
    });

    actions.append(refreshBtn, helpBtn);
    topBar.append(tabs, actions);
    root.append(topBar);

    if (this.showHelp) {
      const help = document.createElement('div');
      help.id = 'escrow-in-game-help';
      help.style.cssText = 'background:#162338;border:1px solid #3c6ea8;border-radius:6px;padding:8px 10px;margin-bottom:8px;font-size:12px;color:#cde4ff;';
      help.textContent = this.t(
        'Open with F9, /contract, or Command Window (D -> Contract -> right-click player). Goods lock safely in escrow; buyer payment and item delivery swap atomically. Cancel or expired items arrive under History & Owed.',
        'Mở bằng F9, /contract, hoặc bảng Lệnh (D -> Contract -> chuột phải vào người chơi). Đồ được khóa an toàn; thanh toán và giao đồ hoàn tất nguyên tử. Đồ hủy/hết hạn nhận tại Lịch sử & Chờ nhận.',
      );
      root.append(help);
    }

    if (s?.noticeEn || s?.noticeVi) {
      const notice = document.createElement('div');
      notice.style.cssText = `padding:6px 10px;border-radius:5px;margin-bottom:8px;font-size:12px;border:1px solid ${s.isError ? '#a83838' : '#358856'};background:${s.isError ? '#2d1518' : '#142a1e'};color:${s.isError ? '#ffb4b4' : '#b7f5d0'};`;
      notice.textContent = this.t(s.noticeEn ?? '', s.noticeVi ?? '');
      root.append(notice);
    }

    if (!s) {
      const loading = document.createElement('p');
      loading.textContent = this.t('Loading contracts...', 'Đang tải hợp đồng...');
      root.append(loading);
      return;
    }

    if (!s.enabled) {
      const off = document.createElement('p');
      off.textContent = this.t('Escrow contracts are disabled on this server.', 'Hợp đồng ký gửi đang tạm tắt trên máy chủ.');
      root.append(off);
      return;
    }

    if (this.activeTab === 'open') {
      this.renderOpenTab(root, s);
    } else if (this.activeTab === 'create') {
      this.renderCreateTab(root, s);
    } else {
      this.renderHistoryTab(root, s);
    }
  }

  private renderOpenTab(root: HTMLElement, s: EscrowStatePayload): void {
    const controls = document.createElement('div');
    controls.style.cssText = 'display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:8px;';

    const filter = document.createElement('input');
    filter.type = 'text';
    filter.value = this.filterText;
    filter.placeholder = this.t('Search seller, item, ID...', 'Tìm người bán, món đồ, mã...');
    filter.title = this.t('Filter open contracts', 'Lọc danh sách hợp đồng');
    filter.style.cssText = 'flex:1;min-width:160px;padding:5px 8px;border-radius:4px;border:1px solid #444;background:#10131e;color:#fff;';
    filter.addEventListener('input', () => {
      this.filterText = filter.value;
      this.render();
    });

    const hvLabel = document.createElement('label');
    hvLabel.style.cssText = 'display:flex;align-items:center;gap:4px;font-size:12px;cursor:pointer;';
    hvLabel.title = this.t('Confirm high-value contracts before accepting', 'Xác nhận chấp nhận hợp đồng giá trị lớn');
    const hvCb = document.createElement('input');
    hvCb.type = 'checkbox';
    hvCb.checked = this.confirmHighValue;
    hvCb.addEventListener('change', () => { this.confirmHighValue = hvCb.checked; });
    hvLabel.append(hvCb, document.createTextNode(this.t('Confirm high value', 'Xác nhận giá lớn')));

    controls.append(filter, hvLabel);

    if (s.requiresPin) {
      const pin = document.createElement('input');
      pin.type = 'password';
      pin.maxLength = 4;
      pin.value = this.pinInput;
      pin.placeholder = 'PIN';
      pin.title = this.t('4-digit second password required for contracts', 'Mật khẩu cấp 2 (4 chữ số)');
      pin.style.cssText = 'width:76px;padding:5px 8px;border-radius:4px;border:1px solid #8a6a30;background:#10131e;color:#fff;';
      pin.addEventListener('input', () => { this.pinInput = pin.value; });
      controls.append(pin);
    }

    root.append(controls);

    const q = this.filterText.trim().toLowerCase();
    const list = (s.openContracts ?? []).filter((c) => {
      if (!q) return true;
      return c.contractId.toLowerCase().includes(q)
        || c.sellerCharacter.toLowerCase().includes(q)
        || c.items.some((i) => i.nameEn.toLowerCase().includes(q) || i.nameVi.toLowerCase().includes(q));
    });

    if (list.length === 0) {
      const empty = document.createElement('p');
      empty.style.color = '#9ca3af';
      empty.textContent = this.t('No matching open escrow contracts.', 'Không có hợp đồng ký gửi nào đang mở.');
      root.append(empty);
      return;
    }

    for (const c of list) {
      const card = document.createElement('div');
      card.style.cssText = 'border:1px solid #3f3828;border-radius:6px;padding:8px 10px;margin-bottom:8px;background:#121622;';
      const st = statusLabel(c.status);
      const isDirect = c.mode === 'DirectVnd' && !!c.directVnd;
      const cur = isDirect ? 'VND' : currencyLabel(c.priceCurrency);
      const goodsParts: string[] = [];
      for (const it of c.items) {
        const nm = `${this.t(it.nameEn, it.nameVi)} +${it.level}${it.luck ? ' Luck' : ''}${it.skill ? ' Skill' : ''}`;
        goodsParts.push(nm);
      }
      if (c.escrowedZen > 0) goodsParts.push(`${c.escrowedZen.toLocaleString()} Zen`);
      for (const j of c.escrowedJewels) goodsParts.push(`${j.quantity}x ${j.jewelKey}`);

      const header = document.createElement('div');
      header.style.cssText = 'display:flex;justify-content:space-between;align-items:center;gap:8px;margin-bottom:4px;';
      const title = document.createElement('strong');
      title.style.color = '#f1cf7a';
      title.textContent = `${c.contractId}${isDirect ? ' [P2P VND]' : ''} • ${c.sellerCharacter}${c.targetBuyer ? ` -> ${c.targetBuyer}` : ''} [${st}]`;
      const exp = document.createElement('span');
      exp.style.cssText = 'font-size:11px;color:#9ca3af;';
      exp.textContent = isDirect && c.directVnd?.paymentDeadlineSydney
        ? `${this.t('Pay by', 'Hạn CK')}: ${c.directVnd.paymentDeadlineSydney}`
        : c.expiresAtSydney;
      header.append(title, exp);

      const goods = document.createElement('div');
      goods.style.cssText = 'font-size:12px;margin-bottom:4px;';
      goods.textContent = `${this.t('Goods', 'Hàng')}: ${goodsParts.join(' | ')}`;
      if (c.items.length > 0) {
        goods.title = c.items.map((i) => `${this.t(i.nameEn, i.nameVi)}: ${i.originSummary}`).join('\n');
      }

      card.append(header, goods);

      if (isDirect && c.directVnd && st !== 'Open') {
        const dv = c.directVnd;
        const payBox = document.createElement('div');
        payBox.style.cssText = 'display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin:6px 0;padding:6px 8px;border:1px dashed #6b5528;border-radius:5px;background:#171820;font-size:12px;';
        if (dv.bankBin && dv.bankAccountNumber && st === 'ReservedForPayment') {
          const qrPayload = buildVietQrPayload({
            bin: dv.bankBin,
            account: dv.bankAccountNumber,
            accountName: dv.bankAccountName,
            amount: c.priceAmount,
            message: dv.paymentReference,
          });
          const img = document.createElement('img');
          img.src = renderVietQrSvgDataUrl(qrPayload);
          img.alt = 'VietQR';
          img.style.cssText = 'width:110px;height:110px;border-radius:4px;background:#fff;padding:4px;';
          payBox.append(img);
        }
        const infoDiv = document.createElement('div');
        infoDiv.style.cssText = 'display:grid;gap:2px;';
        if (dv.bankAccountNumber) {
          const bLine = document.createElement('div');
          bLine.textContent = `${dv.bankName} (${dv.bankBin}): ${dv.bankAccountNumber} — ${dv.bankAccountName}`;
          infoDiv.append(bLine);
        }
        const rLine = document.createElement('div');
        rLine.textContent = `${this.t('Transfer Ref', 'Nội dung CK')}: ${dv.paymentReference || 'Locked until accepted'}${dv.sellerBondCoins > 0 ? ` | ${this.t('Seller Bond', 'Cọc')}: ${dv.sellerBondCoins}` : ''}`;
        infoDiv.append(rLine);
        if (dv.receiptBase64 && dv.receiptMime) {
          const rImg = document.createElement('img');
          rImg.src = `data:${dv.receiptMime};base64,${dv.receiptBase64}`;
          rImg.alt = 'Receipt';
          rImg.style.cssText = 'max-height:90px;max-width:160px;border-radius:4px;border:1px solid #555;margin-top:4px;';
          infoDiv.append(rImg);
        }
        payBox.append(infoDiv);
        card.append(payBox);
      }

      const footer = document.createElement('div');
      footer.style.cssText = 'display:flex;justify-content:space-between;align-items:center;gap:8px;flex-wrap:wrap;';
      const price = document.createElement('span');
      price.style.cssText = 'font-size:12px;color:#b7f5d0;';
      price.textContent = isDirect
        ? `${this.t('Price', 'Giá')}: ${c.priceAmount.toLocaleString()} VND | ${this.t('Service Fee', 'Phí DV')}: ${c.buyerTotalDebit} ${currencyLabel(c.priceCurrency)}${(c.directVnd?.sellerBondCoins ?? 0) > 0 ? ` | ${this.t('Bond', 'Cọc')}: ${c.directVnd?.sellerBondCoins}` : ''}`
        : `${this.t('Price', 'Giá')}: ${c.priceAmount.toLocaleString()} ${cur} | ${this.t('Buyer pays', 'Người mua trả')}: ${c.buyerTotalDebit.toLocaleString()} ${cur} (${this.t('fee', 'phí')} ${c.feeAmount.toLocaleString()})`;

      const btns = document.createElement('div');
      btns.style.cssText = 'display:flex;gap:6px;flex-wrap:wrap;align-items:center;';
      const isMine = c.sellerLogin.toLowerCase() === s.accountLogin.toLowerCase();
      if (isDirect) {
        if (isMine && st === 'Open') {
          const cancelBtn = document.createElement('button');
          cancelBtn.type = 'button';
          cancelBtn.textContent = this.t('Cancel Contract', 'Hủy hợp đồng');
          cancelBtn.style.cssText = 'padding:4px 10px;border-radius:4px;border:1px solid #b44;background:#441818;color:#ffd0d0;cursor:pointer;font:inherit;';
          cancelBtn.addEventListener('click', () => this.sendAction(3, { contractId: c.contractId }));
          btns.append(cancelBtn);
        } else if (!isMine && st === 'Open') {
          const resBtn = document.createElement('button');
          resBtn.type = 'button';
          resBtn.textContent = this.t('Accept & Show Seller VietQR', 'Nhận & Xem VietQR người bán');
          resBtn.style.cssText = 'padding:4px 10px;border-radius:4px;border:1px solid #358856;background:#194d2f;color:#e6fff0;cursor:pointer;font:inherit;font-weight:600;';
          resBtn.addEventListener('click', () => this.sendAction(6, { contractId: c.contractId, pin: this.pinInput }));
          btns.append(resBtn);
        } else if (!isMine && st === 'ReservedForPayment') {
          const extBtn = document.createElement('button');
          extBtn.type = 'button';
          extBtn.disabled = !!c.directVnd?.extendedOnce;
          extBtn.textContent = this.t('Extend Once', 'Gia hạn 1 lần');
          extBtn.style.cssText = 'padding:4px 8px;border-radius:4px;border:1px solid #888;background:#1b2030;color:#fff;cursor:pointer;font:inherit;';
          extBtn.addEventListener('click', () => this.sendAction(7, { contractId: c.contractId }));

          const fileInp = document.createElement('input');
          fileInp.type = 'file';
          fileInp.accept = 'image/png,image/jpeg,image/webp';
          fileInp.title = this.t('Optional transfer receipt image (PNG/JPG/WEBP)', 'Ảnh biên lai chuyển khoản (tùy chọn)');
          fileInp.style.cssText = 'max-width:155px;font-size:11px;';
          fileInp.addEventListener('change', () => {
            const file = fileInp.files?.[0];
            if (!file) return;
            const maxBytes = s.directVndMaxReceiptBytes ?? 524288;
            if (file.size > maxBytes) {
              this.options.message(this.t('Receipt image exceeds size limit.', 'Ảnh biên lai vượt quá dung lượng cho phép.'));
              fileInp.value = '';
              return;
            }
            const reader = new FileReader();
            reader.onload = () => {
              const res = typeof reader.result === 'string' ? reader.result : '';
              this.pendingReceiptMime = file.type;
              this.pendingReceiptBase64 = res;
            };
            reader.readAsDataURL(file);
          });

          const paidBtn = document.createElement('button');
          paidBtn.type = 'button';
          paidBtn.textContent = this.t("I've Paid", 'Tôi đã chuyển khoản');
          paidBtn.style.cssText = 'padding:4px 10px;border-radius:4px;border:1px solid #358856;background:#194d2f;color:#e6fff0;cursor:pointer;font:inherit;font-weight:600;';
          paidBtn.addEventListener('click', () => {
            this.sendAction(8, {
              contractId: c.contractId,
              receiptMime: this.pendingReceiptMime,
              receiptBase64: this.pendingReceiptBase64,
            });
          });
          btns.append(extBtn, fileInp, paidBtn);
        } else if (isMine && (st === 'ReservedForPayment' || st === 'BuyerMarkedPaid')) {
          const confirmBtn = document.createElement('button');
          confirmBtn.type = 'button';
          confirmBtn.textContent = this.t('Money Received — Release', 'Đã nhận tiền — Giao đồ');
          confirmBtn.style.cssText = 'padding:4px 10px;border-radius:4px;border:1px solid #358856;background:#194d2f;color:#e6fff0;cursor:pointer;font:inherit;font-weight:600;';
          confirmBtn.addEventListener('click', () => this.sendAction(9, { contractId: c.contractId }));
          btns.append(confirmBtn);
        }

        if (st === 'ReservedForPayment' || st === 'BuyerMarkedPaid') {
          const dispBtn = document.createElement('button');
          dispBtn.type = 'button';
          dispBtn.textContent = this.t('Dispute', 'Tranh chấp');
          dispBtn.style.cssText = 'padding:4px 8px;border-radius:4px;border:1px solid #c97a2b;background:#3a2410;color:#ffe1b3;cursor:pointer;font:inherit;';
          dispBtn.addEventListener('click', () => this.sendAction(10, { contractId: c.contractId, reason: this.disputeReasonInput || 'Participant dispute' }));
          btns.append(dispBtn);
        }
      } else if (isMine) {
        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.textContent = this.t('Cancel Contract', 'Hủy hợp đồng');
        cancelBtn.title = this.t('Cancel this open contract and reclaim escrowed goods', 'Hủy hợp đồng để nhận lại đồ');
        cancelBtn.disabled = c.frozenByAdmin || st === 'Frozen';
        cancelBtn.style.cssText = 'padding:4px 10px;border-radius:4px;border:1px solid #b44;background:#441818;color:#ffd0d0;cursor:pointer;font:inherit;';
        cancelBtn.addEventListener('click', () => this.sendAction(3, { contractId: c.contractId }));
        btns.append(cancelBtn);
      } else {
        const buyBtn = document.createElement('button');
        buyBtn.type = 'button';
        buyBtn.textContent = this.t('Accept & Pay', 'Chấp nhận & Trả');
        buyBtn.title = this.t('Pay asking price + fee share and receive contract goods atomically', 'Thanh toán giá hợp đồng và nhận đồ ngay');
        buyBtn.disabled = c.frozenByAdmin || st === 'Frozen';
        buyBtn.style.cssText = 'padding:4px 10px;border-radius:4px;border:1px solid #358856;background:#194d2f;color:#e6fff0;cursor:pointer;font:inherit;font-weight:600;';
        buyBtn.addEventListener('click', () => {
          this.sendAction(2, {
            contractId: c.contractId,
            confirmHighValue: this.confirmHighValue,
            pin: this.pinInput,
          });
        });
        btns.append(buyBtn);
      }

      footer.append(price, btns);
      card.append(footer);
      root.append(card);
    }
  }

  private renderCreateTab(root: HTMLElement, s: EscrowStatePayload): void {
    const form = document.createElement('div');
    form.style.cssText = 'display:grid;gap:8px;';

    if (s.directVndEnabled) {
      const modeRow = document.createElement('div');
      modeRow.style.cssText = 'display:flex;gap:8px;align-items:center;';
      const mBtn = (mode: 'InGame' | 'DirectVnd', en: string, vi: string) => {
        const b = document.createElement('button');
        b.type = 'button';
        b.textContent = this.t(en, vi);
        b.style.cssText = `padding:3px 10px;border-radius:4px;border:1px solid #6b5528;cursor:pointer;font:inherit;background:${this.createMode === mode ? '#8a6a30' : '#181c2b'};color:${this.createMode === mode ? '#111' : '#fff'};font-weight:${this.createMode === mode ? '700' : '400'};`;
        b.addEventListener('click', () => { this.createMode = mode; this.render(); });
        return b;
      };
      modeRow.append(
        mBtn('InGame', 'In-Game Currencies', 'Tiền tệ trong game'),
        mBtn('DirectVnd', 'Direct P2P (VND VietQR)', 'Bán VND trực tiếp (VietQR)'),
      );
      form.append(modeRow);
    } else {
      this.createMode = 'InGame';
    }

    const bagHeader = document.createElement('div');
    bagHeader.style.cssText = 'font-size:12px;font-weight:600;color:#f1cf7a;';
    bagHeader.textContent = this.t('1. Select bag items to escrow (optional):', '1. Chọn vật phẩm trong túi để ký gửi (tùy chọn):');
    form.append(bagHeader);

    const bagGrid = document.createElement('div');
    bagGrid.style.cssText = 'display:flex;flex-wrap:wrap;gap:6px;max-height:110px;overflow-y:auto;padding:6px;border:1px solid #333;border-radius:5px;background:#0e111b;';
    if (!s.bagItems || s.bagItems.length === 0) {
      bagGrid.textContent = this.t('No bag items available.', 'Không có vật phẩm nào trong túi.');
    } else {
      for (const item of s.bagItems) {
        const lbl = document.createElement('label');
        lbl.style.cssText = 'display:flex;align-items:center;gap:4px;padding:3px 7px;border:1px solid #444;border-radius:4px;font-size:12px;cursor:pointer;background:#191e2e;';
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.checked = this.selectedSlots.has(item.slot);
        cb.addEventListener('change', () => {
          if (cb.checked) this.selectedSlots.add(item.slot);
          else this.selectedSlots.delete(item.slot);
        });
        lbl.append(cb, document.createTextNode(`${this.t(item.nameEn, item.nameVi)} +${item.level}`));
        bagGrid.append(lbl);
      }
    }
    form.append(bagGrid);

    const row2 = document.createElement('div');
    row2.style.cssText = 'display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:8px;';

    const zenWrap = this.labeledInput(
      this.t('Escrow Zen', 'Zen ký gửi'),
      this.escrowedZenInput,
      'number',
      (v) => { this.escrowedZenInput = v; },
      this.t('Optional Zen placed into escrow', 'Số Zen đưa vào ký gửi'),
    );

    const jewelWrap = document.createElement('div');
    const jLbl = document.createElement('div');
    jLbl.style.cssText = 'font-size:11px;color:#ccc;margin-bottom:2px;';
    jLbl.textContent = this.t('Escrow Jewel Bank', 'Ngọc từ Ngân hàng');
    const jRow = document.createElement('div');
    jRow.style.cssText = 'display:flex;gap:4px;';
    const jSel = document.createElement('select');
    jSel.style.cssText = 'flex:1;padding:4px;border-radius:4px;border:1px solid #444;background:#10131e;color:#fff;';
    for (const k of ['JewelBless', 'JewelSoul', 'JewelChaos', 'JewelCreation', 'JewelLife']) {
      const opt = document.createElement('option');
      opt.value = k;
      const bal = s.jewelBalances?.[k] ?? 0;
      opt.textContent = `${k} (${bal})`;
      opt.selected = this.escrowedJewelKey === k;
      jSel.append(opt);
    }
    jSel.addEventListener('change', () => { this.escrowedJewelKey = jSel.value; });
    const jQty = document.createElement('input');
    jQty.type = 'number';
    jQty.min = '0';
    jQty.value = this.escrowedJewelQtyInput;
    jQty.style.cssText = 'width:68px;padding:4px;border-radius:4px;border:1px solid #444;background:#10131e;color:#fff;';
    jQty.addEventListener('input', () => { this.escrowedJewelQtyInput = jQty.value; });
    jRow.append(jSel, jQty);
    jewelWrap.append(jLbl, jRow);

    const targetWrap = this.labeledInput(
      this.t('Target Buyer (blank = Open)', 'Người mua chỉ định (trống = Mở)'),
      this.targetPrefill,
      'text',
      (v) => { this.targetPrefill = v; },
      this.t('Optional account login or character name', 'Tên tài khoản hoặc nhân vật người mua'),
    );

    row2.append(zenWrap, jewelWrap, targetWrap);
    form.append(row2);

    const row3 = document.createElement('div');
    row3.style.cssText = 'display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:8px;align-items:end;';

    if (this.createMode === 'DirectVnd') {
      const vndWrap = this.labeledInput(
        this.t('Asking Price (VND)', 'Giá bán (VNĐ)'),
        this.priceAmountInput,
        'number',
        (v) => { this.priceAmountInput = v; },
        this.t('VND transferred directly by buyer to your bank account', 'Số tiền VNĐ người mua chuyển trực tiếp cho bạn'),
      );
      const bondWrap = this.labeledInput(
        this.t(`Seller Bond (${s.directVndServiceFeeCurrency ?? 'WCoinC'})`, `Cọc người bán (${s.directVndServiceFeeCurrency ?? 'WCoinC'})`),
        this.sellerBondInput,
        'number',
        (v) => { this.sellerBondInput = v; },
        this.t('Optional coin bond forfeited if buyer wins dispute', 'Tiền cọc tự chọn (mất nếu thua tranh chấp)'),
      );
      row3.append(vndWrap, bondWrap);
    } else {
      const curWrap = document.createElement('div');
      const cLbl = document.createElement('div');
      cLbl.style.cssText = 'font-size:11px;color:#ccc;margin-bottom:2px;';
      cLbl.textContent = this.t('Asking Currency', 'Loại tiền yêu cầu');
      const cSel = document.createElement('select');
      cSel.style.cssText = 'width:100%;padding:5px;border-radius:4px;border:1px solid #444;background:#10131e;color:#fff;';
      for (const c of s.allowedCurrencies ?? ['WCoinC', 'Zen']) {
        const opt = document.createElement('option');
        opt.value = c;
        opt.textContent = c;
        opt.selected = this.priceCurrency === c;
        cSel.append(opt);
      }
      cSel.addEventListener('change', () => { this.priceCurrency = cSel.value; this.render(); });
      curWrap.append(cLbl, cSel);

      const amtWrap = this.labeledInput(
        this.t('Asking Amount', 'Số tiền yêu cầu'),
        this.priceAmountInput,
        'number',
        (v) => { this.priceAmountInput = v; },
        this.t('Contract price paid by buyer', 'Giá người mua cần trả'),
      );
      row3.append(curWrap, amtWrap);
    }

    const dlWrap = this.labeledInput(
      this.t('Deadline (hours)', 'Hạn hợp đồng (giờ)'),
      this.deadlineHoursInput,
      'number',
      (v) => { this.deadlineHoursInput = v; },
      this.t('Hours before unaccepted contract auto-returns', 'Số giờ tự hoàn hàng nếu chưa có người nhận'),
    );

    row3.append(dlWrap);

    if (s.requiresPin) {
      const pinWrap = this.labeledInput(
        this.t('2nd Password (PIN)', 'Mật khẩu cấp 2'),
        this.pinInput,
        'password',
        (v) => { this.pinInput = v; },
        this.t('4-digit account safety PIN', 'Mật khẩu cấp 2 gồm 4 chữ số'),
      );
      row3.append(pinWrap);
    }
    form.append(row3);

    if (this.createMode === 'DirectVnd') {
      const bankRow = document.createElement('div');
      bankRow.style.cssText = 'display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:8px;align-items:end;padding:6px;border:1px solid #353024;border-radius:5px;';
      const bankSelWrap = document.createElement('div');
      const bLbl = document.createElement('div');
      bLbl.style.cssText = 'font-size:11px;color:#ccc;margin-bottom:2px;';
      bLbl.textContent = this.t('Seller Bank (hidden until accepted)', 'Ngân hàng (ẩn tới khi chốt)');
      const bSel = document.createElement('select');
      bSel.style.cssText = 'width:100%;padding:5px;border-radius:4px;border:1px solid #444;background:#10131e;color:#fff;';
      for (const b of BANK_PRESETS) {
        const opt = document.createElement('option');
        opt.value = b.bin;
        opt.textContent = `${b.name} (${b.bin})`;
        opt.selected = this.bankBin === b.bin;
        bSel.append(opt);
      }
      bSel.addEventListener('change', () => {
        this.bankBin = bSel.value;
        this.bankName = BANK_PRESETS.find((x) => x.bin === bSel.value)?.name ?? 'Bank';
      });
      bankSelWrap.append(bLbl, bSel);

      const acctWrap = this.labeledInput(
        this.t('Account Number', 'Số tài khoản'),
        this.bankAccountNumber,
        'text',
        (v) => { this.bankAccountNumber = v; },
        this.t('Hidden from buyer until they accept the contract', 'Chỉ hiện cho người mua sau khi họ nhấn nhận hợp đồng'),
      );
      const nameWrap = this.labeledInput(
        this.t('Account Holder', 'Tên chủ TK'),
        this.bankAccountName,
        'text',
        (v) => { this.bankAccountName = v; },
        this.t('Bank account holder name', 'Tên chủ tài khoản ngân hàng'),
      );
      const whWrap = this.labeledInput(
        this.t('Webhook Secret (optional)', 'Khóa Webhook HMAC (tùy chọn)'),
        this.webhookSecretInput,
        'password',
        (v) => { this.webhookSecretInput = v; },
        this.t('Optional HMAC-SHA256 secret for automatic confirmation webhook', 'Khóa bí mật HMAC để tự động nhả đồ qua webhook'),
      );
      bankRow.append(bankSelWrap, acctWrap, nameWrap, whWrap);
      form.append(bankRow);
    }

    const feePreview = computeEscrowFee(
      Math.max(0, Number.parseInt(this.priceAmountInput || '0', 10) || 0),
      s.feePercent,
      s.minFee,
      s.maxFee,
      s.feePayer,
    );
    const feeBox = document.createElement('div');
    feeBox.style.cssText = 'font-size:12px;color:#f1cf7a;padding:6px 8px;border:1px dashed #6b5528;border-radius:5px;background:#151412;';
    feeBox.textContent = this.t(
      `Fee (${s.feePercent}%, paid by ${s.feePayer}): ${feePreview.totalFee.toLocaleString()} ${this.priceCurrency} | Buyer pays: ${feePreview.buyerTotalDebit.toLocaleString()} | You receive: ${feePreview.netSellerProceeds.toLocaleString()}`,
      `Phí (${s.feePercent}%, bên trả: ${s.feePayer}): ${feePreview.totalFee.toLocaleString()} ${this.priceCurrency} | Người mua trả: ${feePreview.buyerTotalDebit.toLocaleString()} | Thực nhận: ${feePreview.netSellerProceeds.toLocaleString()}`,
    );
    form.append(feeBox);

    const bottomRow = document.createElement('div');
    bottomRow.style.cssText = 'display:flex;justify-content:space-between;align-items:center;gap:8px;flex-wrap:wrap;';
    const hvLabel = document.createElement('label');
    hvLabel.style.cssText = 'display:flex;align-items:center;gap:5px;font-size:12px;cursor:pointer;';
    const hvCb = document.createElement('input');
    hvCb.type = 'checkbox';
    hvCb.checked = this.confirmHighValue;
    hvCb.addEventListener('change', () => { this.confirmHighValue = hvCb.checked; });
    hvLabel.append(hvCb, document.createTextNode(this.t('Confirm high-value escrow contract', 'Xác nhận hợp đồng giá trị cao')));

    const submit = document.createElement('button');
    submit.type = 'button';
    submit.textContent = this.t('Lock Goods & Create Contract', 'Khóa đồ & Tạo hợp đồng');
    submit.title = this.t('Move selected goods atomically into escrow', 'Chuyển đồ vào trạng thái ký gửi nguyên tử');
    submit.style.cssText = 'padding:6px 14px;border-radius:5px;border:1px solid #c9a24a;background:#8a6a30;color:#111;font-weight:700;cursor:pointer;font:inherit;';
    submit.addEventListener('click', () => {
      if (this.createMode === 'DirectVnd') {
        this.sendAction(5, {
          slots: [...this.selectedSlots],
          escrowedZen: Math.max(0, Number.parseInt(this.escrowedZenInput || '0', 10) || 0),
          escrowedJewelKey: this.escrowedJewelKey,
          escrowedJewelQty: Math.max(0, Number.parseInt(this.escrowedJewelQtyInput || '0', 10) || 0),
          targetBuyer: this.targetPrefill.trim(),
          priceAmount: Math.max(0, Number.parseInt(this.priceAmountInput || '0', 10) || 0),
          deadlineHours: Math.max(1, Number.parseInt(this.deadlineHoursInput || '24', 10) || 24),
          confirmHighValue: this.confirmHighValue,
          pin: this.pinInput,
          bankBin: this.bankBin,
          bankName: this.bankName,
          bankAccountNumber: this.bankAccountNumber.trim(),
          bankAccountName: this.bankAccountName.trim(),
          sellerBondCoins: Math.max(0, Number.parseInt(this.sellerBondInput || '0', 10) || 0),
          webhookSecret: this.webhookSecretInput,
        });
        return;
      }
      this.sendAction(1, {
        slots: [...this.selectedSlots],
        escrowedZen: Math.max(0, Number.parseInt(this.escrowedZenInput || '0', 10) || 0),
        escrowedJewelKey: this.escrowedJewelKey,
        escrowedJewelQty: Math.max(0, Number.parseInt(this.escrowedJewelQtyInput || '0', 10) || 0),
        targetBuyer: this.targetPrefill.trim(),
        priceCurrency: this.priceCurrency,
        priceAmount: Math.max(0, Number.parseInt(this.priceAmountInput || '0', 10) || 0),
        deadlineHours: Math.max(1, Number.parseInt(this.deadlineHoursInput || '24', 10) || 24),
        confirmHighValue: this.confirmHighValue,
        pin: this.pinInput,
      });
    });

    bottomRow.append(hvLabel, submit);
    form.append(bottomRow);
    root.append(form);
  }

  private renderHistoryTab(root: HTMLElement, s: EscrowStatePayload): void {
    const owedHdr = document.createElement('div');
    owedHdr.style.cssText = 'display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;';
    const owedTitle = document.createElement('strong');
    owedTitle.style.color = '#f1cf7a';
    owedTitle.textContent = `${this.t('Owed Deliveries', 'Quà chờ nhận')} (${s.owedDeliveries?.length ?? 0})`;
    const claimBtn = document.createElement('button');
    claimBtn.type = 'button';
    claimBtn.textContent = this.t('Claim All Owed', 'Nhận tất cả');
    claimBtn.title = this.t('Deliver owed items, Zen, and jewels into your bag / Jewel Bank', 'Nhận lại đồ, Zen và Ngọc vào túi/ngân hàng');
    claimBtn.disabled = !s.owedDeliveries || s.owedDeliveries.length === 0;
    claimBtn.style.cssText = 'padding:4px 10px;border-radius:4px;border:1px solid #358856;background:#194d2f;color:#e6fff0;cursor:pointer;font:inherit;';
    claimBtn.addEventListener('click', () => this.sendAction(4, {}));
    owedHdr.append(owedTitle, claimBtn);
    root.append(owedHdr);

    if (s.owedDeliveries && s.owedDeliveries.length > 0) {
      for (const o of s.owedDeliveries) {
        const line = document.createElement('div');
        line.style.cssText = 'font-size:12px;padding:5px 8px;border:1px solid #2e4a38;border-radius:4px;margin-bottom:4px;background:#111d17;';
        line.textContent = `${this.t(o.reasonEn, o.reasonVi)} — ${o.items.length} item(s), ${o.zen.toLocaleString()} Zen, ${o.jewels.reduce((n, j) => n + j.quantity, 0)} jewel(s)`;
        root.append(line);
      }
    }

    const histTitle = document.createElement('strong');
    histTitle.style.cssText = 'display:block;color:#f1cf7a;margin:10px 0 6px;';
    histTitle.textContent = this.t('Recent Closed Contracts', 'Lịch sử hợp đồng gần đây');
    root.append(histTitle);

    if (!s.historyContracts || s.historyContracts.length === 0) {
      const empty = document.createElement('p');
      empty.style.color = '#9ca3af';
      empty.textContent = this.t('No completed or cancelled contracts yet.', 'Chưa có hợp đồng lịch sử.');
      root.append(empty);
      return;
    }

    for (const c of s.historyContracts) {
      const row = document.createElement('div');
      row.style.cssText = 'font-size:12px;padding:5px 8px;border:1px solid #333;border-radius:4px;margin-bottom:4px;background:#121622;';
      row.textContent = `${c.contractId} [${statusLabel(c.status)}] — ${c.sellerCharacter} -> ${c.acceptedByCharacter || c.targetBuyer || 'Open'} — ${c.priceAmount.toLocaleString()} ${currencyLabel(c.priceCurrency)}`;
      root.append(row);
    }
  }

  private labeledInput(
    label: string,
    value: string,
    type: string,
    onChange: (v: string) => void,
    tooltip: string,
  ): HTMLDivElement {
    const wrap = document.createElement('div');
    const lbl = document.createElement('div');
    lbl.style.cssText = 'font-size:11px;color:#ccc;margin-bottom:2px;';
    lbl.textContent = label;
    const inp = document.createElement('input');
    inp.type = type;
    inp.value = value;
    inp.title = tooltip;
    inp.style.cssText = 'width:100%;box-sizing:border-box;padding:5px 8px;border-radius:4px;border:1px solid #444;background:#10131e;color:#fff;';
    inp.addEventListener('input', () => onChange(inp.value));
    wrap.append(lbl, inp);
    return wrap;
  }
}

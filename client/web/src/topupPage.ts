import { buildVietQrPayload, renderVietQrSvgDataUrl } from './vietqr.js';

interface BankInfo {
  code: string;
  name: string;
  bin: string;
  account: string;
  holder?: string;
}

/** A top-up the server keeps for this browser (POST /topup/pending, GET /topup/status). */
interface PendingView {
  code: string;
  account: string;
  amount: number;
  coins: number;
  bin: string;
  bank: string;
  number: string;
  holder: string;
  status: 'pending' | 'credited' | 'rejected' | 'expired';
  created: string;
}

interface TopUpInfo {
  enabled: boolean;
  accountName: string;
  banks: BankInfo[];
  vndPerCoin: number;
  minimumVnd: number;
  coinType: string;
  prefix: string;
  maximumVnd?: number;
  codes?: boolean;
}

interface MilestoneStepPublic {
  id: string;
  thresholdVnd: number;
  titleEn: string;
  titleVi: string;
  coinType: string;
  coins: number;
  zen: number;
  items: { name?: string; level: number; quantity: number }[];
}

interface PublicMilestones {
  enabled: boolean;
  steps?: MilestoneStepPublic[];
}

const TEXT: Record<'vi' | 'en', Record<string, string>> = {
  vi: {
    title: 'Nạp coin',
    play: 'Vào game →',
    off: 'Chưa mở nạp coin bằng chuyển khoản. Hãy hỏi chủ server.',
    step1: '1. Tài khoản và số tiền',
    account: 'Tên tài khoản (tên đăng nhập game)',
    amount: 'Số tiền (VND)',
    youGet: 'Bạn nhận được',
    step2: '2. Ngân hàng',
    step3: '3. Quét mã bằng app ngân hàng',
    needAccount: 'Hãy nhập tên tài khoản trước.',
    bank: 'Ngân hàng',
    number: 'Số tài khoản',
    holder: 'Chủ tài khoản',
    amountShort: 'Số tiền',
    message: 'Nội dung chuyển khoản',
    copy: 'Sao chép',
    copied: 'Đã chép',
    keepMessage: 'Giữ nguyên nội dung chuyển khoản như trên: game dựa vào đó để biết tài khoản nào nhận coin.',
    arrive: 'Coin sẽ tự động vào tài khoản trong 1–2 phút sau khi chuyển hoặc sau khi admin xác nhận: mở cửa hàng (X) trong game để thấy.',
    minimum: 'Tối thiểu',
    milestonesHead: 'Thưởng mốc nạp tích lũy',
    milestonesNote: 'Mỗi mốc nhận một lần khi tổng tiền nạp đạt ngưỡng (mở U trong game hoặc trang tài khoản để nhận).',
    create: 'Lấy mã chuyển khoản',
    creating: 'Đang tạo…',
    errAccountNotFound: 'Không có tài khoản này. Kiểm tra tên đăng nhập.',
    errBadAmount: 'Số tiền không hợp lệ.',
    errTooMany: 'Bạn đang có nhiều lệnh nạp chờ. Hãy chuyển khoản hoặc chờ chủ server xác nhận.',
    errOther: 'Không tạo được mã. Thử lại sau.',
    statusLabel: 'Trạng thái',
    pending: 'Đang chờ chủ server xác nhận',
    credited: 'Đã cộng coin',
    rejected: 'Chủ server chưa thấy khoản chuyển',
    expired: 'Hết hạn (nếu đã chuyển, chủ server vẫn cộng được)',
    historyHead: 'Lệnh nạp của tôi trên thiết bị này',
  },
  en: {
    create: 'Get my transfer code',
    creating: 'Creating…',
    errAccountNotFound: 'There is no such account. Check your login name.',
    errBadAmount: 'This amount is not accepted.',
    errTooMany: 'You already have several waiting top-ups. Transfer, or wait for the host to confirm.',
    errOther: 'Could not create a code. Try again later.',
    pending: 'Waiting for the host to confirm',
    credited: 'Coins added',
    rejected: 'The host did not see the transfer',
    expired: 'Expired (if you transferred, the host can still credit it)',
    copied: 'Copied',
    minimum: 'Minimum',
    milestonesHead: 'Top-up milestone rewards',
    milestonesNote: 'Claimed once per step when cumulative confirmed top-ups pass each threshold (open U in the game or the account page).',
  },
};

const english = new Map<string, string>();
let lang: 'en' | 'vi' = 'vi';
try {
  lang = localStorage.getItem('hnmo.language') === 'en' ? 'en' : 'vi';
} catch {
  /* ignore */
}

const t = (key: string): string => TEXT[lang]?.[key] || english.get(key) || TEXT.en[key] || key;
const byId = <T extends HTMLElement>(id: string): T => document.getElementById(id) as T;
const vnd = (n: number): string => new Intl.NumberFormat('vi-VN').format(n) + ' đ';

let info: TopUpInfo | null = null;
let bank: BankInfo | null = null;
let current: PendingView | null = null;
let history: PendingView[] = [];
let pollTimer: number | undefined;
const TICKETS_KEY = 'hnmo.topup.tickets';

function tickets(): string[] {
  try {
    const raw: unknown = JSON.parse(localStorage.getItem(TICKETS_KEY) ?? '[]');
    return Array.isArray(raw) ? raw.filter((x): x is string => typeof x === 'string' && /^[A-Za-z0-9]{16,100}$/.test(x)).slice(-10) : [];
  } catch {
    return [];
  }
}

function newTicket(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  const ticket = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  try {
    localStorage.setItem(TICKETS_KEY, JSON.stringify([...tickets(), ticket].slice(-10)));
  } catch {
    /* the code still works for this page */
  }
  return ticket;
}

function currentAccount(): string {
  return byId<HTMLInputElement>('account').value.trim().replace(/[^A-Za-z0-9]/g, '');
}

function currentAmount(): number {
  return Math.max(0, Math.floor(Number(byId<HTMLInputElement>('amount').value) || 0));
}

/** Whether the shown code still belongs to what the player has chosen. */
function matchesChoice(view: PendingView | null): view is PendingView {
  return !!view && !!bank && view.account.toLowerCase() === currentAccount().toLowerCase()
    && view.amount === currentAmount() && view.bin === bank.bin && view.number === bank.account;
}

function isPendingView(value: unknown): value is PendingView {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  return typeof v.code === 'string' && typeof v.account === 'string' && typeof v.amount === 'number'
    && typeof v.bin === 'string' && typeof v.number === 'string' && typeof v.status === 'string';
}

async function createPending(): Promise<void> {
  if (!info || !bank) return;
  const button = byId<HTMLButtonElement>('create');
  const error = byId('createError');
  button.disabled = true;
  button.textContent = t('creating');
  error.hidden = true;
  try {
    const response = await fetch('/topup/pending', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ account: currentAccount(), amount: currentAmount(), bin: bank.bin, number: bank.account, ticket: newTicket() }),
    });
    const data: unknown = await response.json().catch(() => null);
    if (response.ok && isPendingView(data)) {
      current = data;
      void refreshHistory();
    } else {
      const code = data && typeof data === 'object' && typeof (data as { error?: unknown }).error === 'string' ? (data as { error: string }).error : '';
      error.textContent = t(code === 'AccountNotFound' ? 'errAccountNotFound' : code === 'BadAmount' ? 'errBadAmount' : code === 'TooMany' ? 'errTooMany' : 'errOther');
      error.hidden = false;
    }
  } catch {
    error.textContent = t('errOther');
    error.hidden = false;
  } finally {
    button.disabled = false;
    button.textContent = t('create');
    render();
  }
}

async function refreshHistory(): Promise<void> {
  const list = tickets();
  if (!list.length) return;
  try {
    const response = await fetch('/topup/status?t=' + encodeURIComponent(list.join(',')), { cache: 'no-store' });
    const data: unknown = response.ok ? await response.json() : null;
    const items = data && typeof data === 'object' ? (data as { topUps?: unknown }).topUps : null;
    history = Array.isArray(items) ? items.filter(isPendingView) : history;
    if (current) current = history.find((h) => h.code === current?.code) ?? current;
  } catch {
    /* try again on the next round */
  }
  renderHistory();
  render();
  schedulePoll();
}

/** Refreshes every 15 s while something waits and the page is visible: only this server is asked. */
function schedulePoll(): void {
  window.clearTimeout(pollTimer);
  if (document.visibilityState === 'visible' && history.some((h) => h.status === 'pending')) {
    pollTimer = window.setTimeout(() => { void refreshHistory(); }, 15_000);
  }
}

function renderHistory(): void {
  const section = byId('history');
  section.hidden = history.length === 0;
  byId('historyList').replaceChildren(...history.map((h) => {
    const row = document.createElement('div');
    row.className = 'history-row';
    const left = document.createElement('span');
    left.textContent = `${h.code} · ${h.account} · ${vnd(h.amount)}`;
    const right = document.createElement('span');
    right.className = `state ${h.status}`;
    right.textContent = t(h.status);
    row.append(left, right);
    return row;
  }));
}
let milestones: PublicMilestones | null = null;

function applyLanguage(): void {
  document.documentElement.lang = lang;
  for (const element of document.querySelectorAll<HTMLElement>('[data-t]')) {
    const key = element.dataset.t ?? '';
    if (!english.has(key)) english.set(key, element.textContent ?? '');
    element.textContent = t(key);
  }
  for (const button of document.querySelectorAll<HTMLElement>('[data-lang]')) {
    button.setAttribute('aria-pressed', String(button.dataset.lang === lang));
  }
  render();
  renderMilestones();
  renderHistory();
}

function render(): void {
  if (!info || !bank) return;
  const accountEl = byId<HTMLInputElement>('account');
  const amountEl = byId<HTMLInputElement>('amount');
  const account = accountEl.value.trim().replace(/[^A-Za-z0-9]/g, '');
  const amount = Math.max(0, Math.floor(Number(amountEl.value) || 0));

  byId('coins').textContent = new Intl.NumberFormat('vi-VN').format(Math.floor(amount / info.vndPerCoin));
  byId('coinName').textContent = info.coinType === 'WCoinP'
    ? 'W Coin (P)'
    : info.coinType === 'GoblinPoints'
      ? 'Goblin Points'
      : 'W Coin (C)';

  for (const b of Array.from(byId('banks').children)) {
    b.setAttribute('aria-pressed', String((b as HTMLElement).dataset.code === bank.code));
  }
  for (const b of Array.from(byId('presets').children)) {
    b.setAttribute('aria-pressed', String(Number((b as HTMLElement).dataset.amount) === amount));
  }

  const codes = info.codes === true;
  const shown = matchesChoice(current) ? current : null;
  const message = codes ? (shown?.code ?? '') : `${info.prefix} ${account}`.trim();
  byId('bankName').textContent = bank.name;
  byId('bankAccount').textContent = bank.account;
  byId('holder').textContent = shown?.holder ?? bank.holder ?? info.accountName;
  byId('amountText').textContent = amount < info.minimumVnd
    ? `${vnd(amount)} (${t('minimum')} ${vnd(info.minimumVnd)})`
    : vnd(amount);
  byId('amountRaw').textContent = String(amount);
  byId('message').textContent = message || '—';

  const ready = account.length >= 3 && amount >= info.minimumVnd && amount <= (info.maximumVnd ?? Number.MAX_SAFE_INTEGER);
  byId('needAccount').hidden = account.length >= 3;
  const create = byId<HTMLButtonElement>('create');
  create.hidden = !codes || !!shown;
  create.disabled = !ready;
  const status = byId('status');
  status.hidden = !shown;
  if (shown) {
    const state = byId('statusText');
    state.className = `state ${shown.status}`;
    state.textContent = t(shown.status);
  }

  const qr = byId<HTMLImageElement>('qr');
  const showQr = ready && message.length > 0 && (!codes || shown?.status === 'pending' || shown?.status === 'expired');
  qr.hidden = !showQr;
  if (showQr) {
    const payload = buildVietQrPayload({
      bin: bank.bin,
      account: bank.account,
      accountName: shown?.holder ?? bank.holder ?? info.accountName,
      amount,
      message,
    });
    qr.dataset.vietqr = payload;
    qr.src = renderVietQrSvgDataUrl(payload);
  } else {
    delete qr.dataset.vietqr;
  }
}

function renderMilestones(): void {
  const section = byId('milestones');
  if (!milestones?.enabled || !milestones.steps?.length) {
    section.hidden = true;
    return;
  }
  section.hidden = false;
  const list = byId('milestonesList');
  list.replaceChildren(...milestones.steps.map((step) => {
    const row = document.createElement('div');
    row.className = 'milestone-row';
    const title = lang === 'vi' && step.titleVi ? step.titleVi : (step.titleEn || step.id);
    const rewardParts: string[] = [];
    if (step.coins > 0) rewardParts.push(`${new Intl.NumberFormat('vi-VN').format(step.coins)} ${step.coinType}`);
    if (step.zen > 0) rewardParts.push(`${new Intl.NumberFormat('vi-VN').format(step.zen)} Zen`);
    for (const item of step.items ?? []) {
      rewardParts.push(`${item.name ?? 'Item'}${item.level > 0 ? ` +${item.level}` : ''} ×${item.quantity}`);
    }
    const left = document.createElement('strong');
    left.textContent = `${vnd(step.thresholdVnd)} — ${title}`;
    const right = document.createElement('span');
    right.textContent = rewardParts.join(' · ');
    row.append(left, right);
    return row;
  }));
}

for (const button of document.querySelectorAll<HTMLElement>('[data-lang]')) {
  button.addEventListener('click', () => {
    lang = button.dataset.lang === 'en' ? 'en' : 'vi';
    try {
      localStorage.setItem('hnmo.language', lang);
    } catch {
      /* ignore */
    }
    applyLanguage();
  });
}

const params = new URLSearchParams(location.search);
const accountParam = params.get('account');
if (accountParam) byId<HTMLInputElement>('account').value = accountParam.slice(0, 10);

for (const id of ['account', 'amount']) {
  byId(id).addEventListener('input', render);
}
byId('create').addEventListener('click', () => { void createPending(); });
document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') void refreshHistory(); });

for (const button of document.querySelectorAll<HTMLElement>('.copy')) {
  button.addEventListener('click', async () => {
    const targetId = button.dataset.copy;
    if (!targetId) return;
    const text = byId(targetId).textContent ?? '';
    try {
      await navigator.clipboard.writeText(text);
      button.textContent = t('copied');
      setTimeout(() => { button.textContent = t('copy'); }, 1200);
    } catch {
      /* ignore */
    }
  });
}

void fetch('/topup/info', { cache: 'no-cache' })
  .then((r) => (r.ok ? r.json() as Promise<TopUpInfo> : null))
  .then((data) => {
    if (!data || !data.enabled || !Array.isArray(data.banks) || !data.banks.length) {
      byId('off').hidden = false;
      return;
    }
    info = data;
    bank = data.banks[0];
    byId('banks').replaceChildren(...data.banks.map((b) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.code = b.code;
      button.textContent = b.name;
      button.addEventListener('click', () => { bank = b; render(); });
      return button;
    }));
    byId<HTMLInputElement>('amount').min = String(data.minimumVnd);
    const presets = [20000, 50000, 100000, 200000, 500000, 1000000].filter((n) => n >= data.minimumVnd);
    byId('presets').replaceChildren(...presets.map((n) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.amount = String(n);
      button.textContent = vnd(n);
      button.addEventListener('click', () => {
        byId<HTMLInputElement>('amount').value = String(n);
        render();
      });
      return button;
    }));
    byId('topup').hidden = false;
    render();
    void refreshHistory();
  })
  .catch(() => { byId('off').hidden = false; });

void fetch('/website/topup-milestones', { cache: 'no-store' })
  .then((r) => (r.ok ? r.json() as Promise<PublicMilestones> : null))
  .then((data) => {
    milestones = data;
    renderMilestones();
  })
  .catch(() => undefined);

void fetch('/game-name.json', { cache: 'no-cache' })
  .then((r) => (r.ok ? r.json() as Promise<{ name?: unknown }> : null))
  .then((d) => {
    const name = d && typeof d.name === 'string' && d.name.trim() ? d.name.trim().slice(0, 60) : null;
    if (!name) return;
    document.title = document.title.replace('HenryN Mu Online', name);
    for (const e of document.querySelectorAll('[data-game-name]')) e.textContent = name;
  })
  .catch(() => undefined);

applyLanguage();

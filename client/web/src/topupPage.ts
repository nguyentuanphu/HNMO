import { buildVietQrPayload, renderVietQrSvgDataUrl } from './vietqr.js';

interface BankInfo {
  code: string;
  name: string;
  bin: string;
  account: string;
}

interface TopUpInfo {
  enabled: boolean;
  accountName: string;
  banks: BankInfo[];
  vndPerCoin: number;
  minimumVnd: number;
  coinType: string;
  prefix: string;
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
    arrive: 'Coin sẽ tự động vào tài khoản trong 1–2 phút sau khi chuyển hoặc sau khi admin xác nhận: mở cửa hàng (X) trong game để thấy. Trên season không có cửa hàng (classic), coin dùng để mua VIP (cửa sổ VIP hoặc trang tài khoản).',
    minimum: 'Tối thiểu',
    milestonesHead: 'Thưởng mốc nạp tích lũy',
    milestonesNote: 'Mỗi mốc nhận một lần khi tổng tiền nạp đạt ngưỡng (mở U trong game hoặc trang tài khoản để nhận).',
  },
  en: {
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

  const message = `${info.prefix} ${account}`.trim();
  byId('bankName').textContent = bank.name;
  byId('bankAccount').textContent = bank.account;
  byId('holder').textContent = info.accountName;
  byId('amountText').textContent = amount < info.minimumVnd
    ? `${vnd(amount)} (${t('minimum')} ${vnd(info.minimumVnd)})`
    : vnd(amount);
  byId('amountRaw').textContent = String(amount);
  byId('message').textContent = account ? message : '—';

  const ready = account.length >= 3 && amount >= info.minimumVnd;
  byId('needAccount').hidden = account.length >= 3;
  const qr = byId<HTMLImageElement>('qr');
  qr.hidden = !ready;
  if (ready) {
    const payload = buildVietQrPayload({
      bin: bank.bin,
      account: bank.account,
      accountName: info.accountName,
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

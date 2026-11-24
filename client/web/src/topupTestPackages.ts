import { installSessionTransport } from './websiteSession.js';
import { buildVietQrPayload, renderVietQrSvgDataUrl } from './vietqr.js';

interface Package {
  id: string;
  englishName: string;
  vietnameseName: string;
  priceVnd: number;
  coins: number;
}
type PaymentProvider = "bank" | "stripe";

interface Order {
  amount: number;
  reference: string;
  credited: boolean;
  checkoutUrl?: string;
}

const panel = document.querySelector<HTMLElement>('#testPackages');
const buttons = panel?.querySelector<HTMLElement>('[data-packages]');
const status = panel?.querySelector<HTMLElement>('[role=status]');
const qr = panel?.querySelector<HTMLImageElement>('img');

const strings = {
  en: {
    title: 'Test coin packages',
    vip: 'Review VIP offers in your account',
    hint: 'Test mode. Pay only with Stripe test cards. The simulated bank QR uses a fictional destination. Do not send money.',
    signIn: 'Sign in on the account page to start a test order.',
    failure: 'The order could not be created. Check your daily limit or try again.',
    bank: 'Bank simulation',
    stripe: 'Stripe test card',
    returned: 'Returned from Stripe test checkout. A signed payment confirmation is required before credit.',
    cancelled: 'Stripe test checkout was cancelled.',
    pending: 'Give the test reference to the host for simulated confirmation.',
    credited: 'The host confirmed the test credit.',
  },
  vi: {
    title: 'Gói coin thử nghiệm',
    vip: 'Xem gói VIP trong tài khoản',
    hint: 'Chỉ để thử. Hãy dùng thẻ Stripe test. Mã QR dùng thông tin nhận hư cấu. Đừng chuyển tiền.',
    signIn: 'Đăng nhập ở trang tài khoản để tạo đơn thử.',
    failure: 'Không tạo được đơn. Hãy kiểm tra giới hạn mỗi ngày hoặc thử lại.',
    bank: 'Mô phỏng ngân hàng',
    stripe: 'Thẻ Stripe thử nghiệm',
    returned: 'Đã quay lại từ trang Stripe test. Cần xác nhận thanh toán có chữ ký trước khi cộng coin.',
    cancelled: 'Đã hủy thanh toán Stripe thử nghiệm.',
    pending: 'Gửi mã thử cho chủ máy chủ để mô phỏng xác nhận.',
    credited: 'Chủ máy chủ đã xác nhận coin thử.',
  },
};

let packages: Package[] = [];
let providers: PaymentProvider[] = [];
const checkoutResult = new URLSearchParams(location.search).get('stripe-test');
let order: Order | undefined;
let error: 'signIn' | 'failure' | undefined;
const operations = new Map<string, string>();
let running = false;

function render(): void {
  if (!panel || !buttons || !status || !qr) return;
  const lang = document.documentElement.lang === 'en' ? 'en' : 'vi';
  const text = strings[lang];
  panel.querySelector('h2')!.textContent = text.title;
  panel.querySelector('[data-hint]')!.textContent = text.hint;
  panel.querySelector('[data-test-vip]')!.textContent = text.vip;
  const format = new Intl.NumberFormat(lang === 'en' ? 'en-AU' : 'vi-VN');
  buttons.replaceChildren();
  for (const item of packages) {
    for (const provider of providers) {
      const button = document.createElement('button');
      button.type = 'button';
      button.disabled = running;
      const name = lang === 'vi' ? item.vietnameseName : item.englishName;
      button.textContent = `${name} · ${format.format(item.priceVnd)} VND · ${format.format(item.coins)} coin · ${text[provider]}`;
      button.addEventListener('click', () => void createOrder(item.id, provider));
      buttons.append(button);
    }
  }

  if (error) {
    status.textContent = text[error];
    if (error === 'signIn') {
      const link = document.createElement('a');
      link.href = '/account.html';
      link.textContent = lang === 'vi' ? 'Tài khoản' : 'Account';
      status.append(' ', link);
    }
  } else {
    status.textContent = order
      ? `${order.reference} · ${format.format(order.amount)} VND · ${order.credited ? text.credited : text.pending}`
      : checkoutResult === 'returned' ? text.returned : checkoutResult === 'cancelled' ? text.cancelled : '';
  }

  qr.hidden = !order || order.credited;
  if (order && !order.credited) {
    qr.alt = text.hint;
    qr.src = renderVietQrSvgDataUrl(buildVietQrPayload({
      bin: '000000', account: '0000000000', accountName: 'FICTIONAL TEST ONLY',
      amount: order.amount, message: order.reference,
    }));
  } else {
    qr.removeAttribute('src');
  }
}

async function createOrder(packageId: string, provider: PaymentProvider): Promise<void> {
  if (running) return;
  const operation = packageId + ":" + provider;
  const operationId = operations.get(operation) ?? crypto.randomUUID();
  operations.set(operation, operationId);
  running = true;
  order = undefined;
  error = undefined;
  render();
  try {
    const response = await fetch('/portal/account/test-topup/orders', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ packageId, operationId, provider }),
    });
    if (response.status === 401) {
      error = 'signIn';
    } else if (!response.ok) {
      error = 'failure';
    } else {
      const result = await response.json() as Order;
      if (provider === 'stripe' && result.checkoutUrl) {
        const target = new URL(result.checkoutUrl);
        if (target.origin !== 'https://checkout.stripe.com' || target.username || target.password) throw new Error('Checkout URL');
        location.assign(target.href);
        return;
      }

      order = result;
      if (order.credited) operations.delete(operation);
    }
  } catch {
    error = 'failure';
  } finally {
    running = false;
    render();
  }
}

if (panel) {
  installSessionTransport();
  new MutationObserver(render).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
  fetch('/website/test-topup', { cache: 'no-store' })
    .then(async response => {
      if (!response.ok) return;
      const result = await response.json() as { enabled: boolean; bank: boolean; stripe: boolean; packages: Package[] };
      if (result.enabled && result.packages.length > 0) {
        packages = result.packages;
        providers = [];
        if (result.bank) providers.push('bank');
        if (result.stripe) providers.push('stripe');
        render();
        panel.hidden = false;
      }
    }).catch(() => {});
}

import { loadWebsiteSettings } from './websiteTheme.js';
import { signInWebsite } from './websiteSession.js';

const copy = {
  register: ['Create an account', 'Tạo tài khoản'], backSignIn: ['Back to sign in', 'Về đăng nhập'], newHere: ['No account yet?', 'Bạn chưa có tài khoản?'],
  emailLater: ['Add a verified email in your profile after registration.', 'Thêm email đã xác minh trong hồ sơ sau khi đăng ký.'],
  error: ['Check the account name and password, then try again.', 'Kiểm tra tên tài khoản và mật khẩu rồi thử lại.'],
  unavailable: ['Accounts are not available right now. Try again later.', 'Hiện chưa dùng được dịch vụ tài khoản. Vui lòng thử lại sau.'],
  busy: ['Too many attempts. Wait before trying again.', 'Bạn đã thử quá nhiều lần. Hãy đợi rồi thử lại.'],
  taken: ['That account name is already in use. Choose another.', 'Tên tài khoản này đã có người dùng. Hãy chọn tên khác.'],
  created: ['Your account was created. Sign in to open your profile.', 'Đã tạo tài khoản. Đăng nhập để mở hồ sơ của bạn.'],
  googleNew: ['Choose an account name and password to finish creating your account with Google.', 'Chọn tên tài khoản và mật khẩu để hoàn tất tạo tài khoản bằng Google.'],
  googleKnown: ['This Google account is already linked. Sign in below, or set a new game password with Google.', 'Tài khoản Google này đã liên kết. Đăng nhập bên dưới, hoặc đặt mật khẩu game mới bằng Google.'],
  newPassword: ['New game password', 'Mật khẩu game mới'], setPassword: ['Set password and open profile', 'Đặt mật khẩu và mở hồ sơ'],
  startingCoins: ['New accounts receive {0} for the cash shop.', 'Tài khoản mới nhận {0} để dùng trong cửa hàng coin.'],
  setupSaved: ['Saved. Registration is ready.', 'Đã lưu. Có thể đăng ký tài khoản.'],
} as const;
let googleCredential = '';
function vi(): boolean { return document.documentElement.lang === 'vi'; }
function t(key: keyof typeof copy): string { return copy[key][vi() ? 1 : 0]; }
function el<T extends HTMLElement>(id: string): T { return document.getElementById(id) as T; }
function message(text: string): void { el('registerMessage').textContent = text; }
function view(register: boolean): void {
  el('accountRegistration').hidden = !register;
  el('accountLoginPanel').hidden = register;
  if (register) el<HTMLInputElement>('loginName').focus(); else el<HTMLInputElement>('accountUser').focus();
}
function translate(): void {
  for (const node of document.querySelectorAll<HTMLElement>('[data-auth-text]')) {
    const key = node.dataset.authText as keyof typeof copy;
    if (key in copy) node.textContent = t(key);
  }
}
translate(); new MutationObserver(translate).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
el('accountCreateButton').onclick = () => { googleCredential = ''; view(true); };
el('accountReturnSignIn').onclick = () => view(false);

async function post(path: string, body: unknown): Promise<Record<string, unknown>> {
  const response = await fetch(path, { method: 'POST', cache: 'no-store', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const result: unknown = await response.json().catch(() => null);
  if (!response.ok) throw new Error(t(response.status === 409 ? 'taken' : response.status === 429 ? 'busy' : response.status === 503 ? 'unavailable' : 'error'));
  if (!result || typeof result !== 'object' || Array.isArray(result)) throw new Error(t('unavailable'));
  if ('error' in result && result.error) throw new Error(t('error'));
  return result as Record<string, unknown>;
}
async function finish(loginName: string, password: string): Promise<void> {
  const response = await signInWebsite(loginName, password);
  el<HTMLFormElement>('registerForm').reset();
  if (!response.ok) { view(false); el('google-message').textContent = t('created'); return; }
  el('accountRegistration').hidden = true;
  window.dispatchEvent(new Event('hnmo-account-ready'));
}
el('registerForm').addEventListener('submit', event => {
  event.preventDefault();
  const form = el<HTMLFormElement>('registerForm'); const button = form.querySelector<HTMLButtonElement>('button[type=submit]')!;
  const loginName = el<HTMLInputElement>('loginName').value.trim(); const password = el<HTMLInputElement>('password').value;
  if (button.disabled) return;
  button.disabled = true;
  void post(googleCredential ? '/portal/google' : '/portal/register', { loginName, password, ...(googleCredential ? { credential: googleCredential } : {}) })
    .then(() => finish(loginName, password)).catch(error => message(error instanceof Error ? error.message : t('unavailable'))).finally(() => { button.disabled = false; });
});

interface GoogleIdentity { accounts: { id: { initialize(options: { client_id: string; callback: (response: { credential: string }) => void }): void; renderButton(parent: HTMLElement, options: Record<string, string | number>): void } } }
async function setupGoogle(): Promise<void> {
  const clientId = await loadWebsiteSettings(); if (!clientId) return;
  const script = document.createElement('script'); script.src = 'https://accounts.google.com/gsi/client'; script.async = true;
  script.onload = () => {
    const google = (window as unknown as { google?: GoogleIdentity }).google; if (!google) return;
    google.accounts.id.initialize({ client_id: clientId, callback: response => {
      googleCredential = response.credential;
      void post('/portal/google', { credential: googleCredential }).then(answer => {
        if (answer.needsAccount === true) { view(true); message(t('googleNew')); }
        else if (typeof answer.loginName === 'string') {
          el<HTMLInputElement>('accountUser').value = answer.loginName; el('google-message').textContent = t('googleKnown');
          el('google-linked').hidden = false;
        }
      }).catch(() => { el('google-message').textContent = t('error'); });
    } });
    const root = el('google-button'); root.hidden = false;
    google.accounts.id.renderButton(root, { theme: 'filled_black', size: 'large', text: 'signup_with', locale: vi() ? 'vi' : 'en' });
  };
  document.head.append(script);
}
const reset = document.createElement('form'); reset.id = 'google-linked'; reset.hidden = true;
const label = document.createElement('label'); const text = document.createElement('span'); text.dataset.authText = 'newPassword';
const password = document.createElement('input'); password.type = 'password'; password.minLength = 3; password.maxLength = 20; password.required = true; password.autocomplete = 'new-password';
const save = document.createElement('button'); save.type = 'submit'; save.dataset.authText = 'setPassword'; label.append(text, password); reset.append(label, save); el('accountGoogle').append(reset); translate();
reset.onsubmit = event => { event.preventDefault(); save.disabled = true; const next = password.value; void post('/portal/google', { credential: googleCredential, newPassword: next }).then(answer => {
  if (typeof answer.loginName !== 'string') throw new Error(t('error')); return finish(answer.loginName, next);
}).catch(() => { el('google-message').textContent = t('error'); }).finally(() => { password.value = ''; save.disabled = false; }); };
void setupGoogle();
void fetch('/portal/status').then(response => response.json()).then((status: { ready?: boolean; startingCoins?: string }) => {
  const onHost = ['localhost', '127.0.0.1', '[::1]'].includes(location.hostname);
  el('setupPanel').hidden = !!status.ready || !onHost;
  if (status.startingCoins) el('welcomeCoins').textContent = t('startingCoins').replace('{0}', status.startingCoins);
}).catch(() => {});
el('setupForm').addEventListener('submit', event => { event.preventDefault(); void post('/portal/setup', { apiKey: el<HTMLInputElement>('apiKey').value }).then(() => { el('setupMessage').textContent = t('setupSaved'); el<HTMLInputElement>('apiKey').value = ''; }).catch(() => { el('setupMessage').textContent = t('error'); }); });

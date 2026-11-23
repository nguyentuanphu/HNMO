import { websitePreviewReply, websitePreviewCharacters } from './websiteThemePreviewData.js';

// Loaded only by the owner-private Site, never by the production HTML pages.
// Static hosting may redirect page.html to /page; keep both forms equivalent.
const previewPage = location.pathname.replace(/\.html$/, '').replace(/\/$/, '') || '/index';
const hostSetting = 'hnmo.preview.owner.websiteTheme';
const skins = ['classic', 'ember', 'azure', 'jade', 'violet', 'parchment'] as const;
function selectedSkin(): string {
  try { const saved = localStorage.getItem(hostSetting); if (skins.some(skin => skin === saved)) return saved!; } catch { /* A preview can run without storage. */ }
  return 'classic';
}
const sampleSessionKey = 'hnmo.preview.sample.session';
function sampleSession(): boolean { try { return localStorage.getItem(sampleSessionKey) === 'sample'; } catch { return false; } }
function setSampleSession(active: boolean): boolean { try { if (active) localStorage.setItem(sampleSessionKey, 'sample'); else localStorage.removeItem(sampleSessionKey); return true; } catch { return false; } }
const originalFetch = window.fetch.bind(window);
window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
  const url = new URL(input instanceof Request ? input.url : String(input), location.href);
  if (url.origin !== location.origin) return originalFetch(input, init);
  if (url.pathname === '/portal/session') {
    if (init?.method === 'DELETE') { setSampleSession(false); return Response.json({ authenticated: false }); }
    if (init?.method === 'POST') {
      let body: unknown; try { body = typeof init.body === 'string' ? JSON.parse(init.body) : null; } catch { return Response.json({ error: 'request' }, { status: 400 }); }
      if (!body || typeof body !== 'object' || !('loginName' in body) || !('password' in body) || body.loginName !== 'Explorer' || body.password !== 'example-only') return Response.json({ error: 'login' }, { status: 401 });
      if (!setSampleSession(true)) return Response.json({ error: 'preview-storage' }, { status: 503 });
      return Response.json({ authenticated: true });
    }
    return Response.json({ authenticated: sampleSession() });
  }
  if (url.pathname.startsWith('/portal/') && !['/portal/status', '/portal/register', '/portal/setup', '/portal/google'].includes(url.pathname) && !sampleSession()) return Response.json({ error: 'login' }, { status: 401 });
  if (url.pathname === '/website/settings') return Response.json({ theme: selectedSkin(), googleClientId: '', requireWebsiteLogin: true });
  if (url.pathname === '/game-name.json') return Response.json({ name: 'HNMO', links: {} });
  if (url.pathname === '/index.html') return originalFetch('/game.html', init);
  if (url.pathname.startsWith('/website/') || url.pathname.startsWith('/portal/') || url.pathname === '/topup/info') {
    const reply = websitePreviewReply(url.pathname);
    return reply === undefined ? Response.json({ error: 'preview-only' }, { status: 503 }) : Response.json(reply);
  }
  return originalFetch(input, init);
};
// The approved profile design is retained; its review-only layout follows the same host choice.
if (previewPage === '/account-profile-preview') {
  const url = new URL(location.href);
  const skin = selectedSkin();
  const layout = skin === 'classic' ? 'ember' : skin;
  if (url.searchParams.get('view') !== 'profile' || url.searchParams.get('theme') !== layout) {
    url.searchParams.set('view', 'profile'); url.searchParams.set('theme', layout); location.replace(url);
  }
}
if (previewPage === '/index' && location.hash.startsWith('#ticket=')) location.replace('/game.html' + location.hash);

async function initializeGamePreview(): Promise<void> {
  const [{ loadGameName }, i18n] = await Promise.all([import('./gameName.js'), import('./i18n.js')]);
  await loadGameName(); await i18n.initLanguage();
  const form = document.getElementById('loginForm');
  const fieldset = form?.querySelector('fieldset'); if (fieldset) fieldset.hidden = true;
  const language = document.getElementById('loginLanguage'); if (language && form) form.prepend(language);
  const guide = document.getElementById('loginGuide'); if (guide instanceof HTMLAnchorElement) guide.href = '/guide.html';
  const website = document.getElementById('websiteLoginButton');
  const choices = document.getElementById('characters');
  const signedIn = sampleSession(); if (website) website.hidden = signedIn;
  // The private preview does not redeem tickets. Keep the review link intact.
  const panel = document.createElement('div'); panel.dataset.noI18n = '';
  const title = document.createElement('h2'); const detail = document.createElement('p'); const actions = document.createElement('div');
  actions.style.cssText = 'display:grid;gap:12px;grid-template-columns:repeat(auto-fit,minmax(180px,1fr))';
  const exit = document.createElement('button'); exit.type = 'button';
  exit.onclick = () => { void fetch('/portal/session', { method: 'DELETE' }).then(() => { try { const channel = new BroadcastChannel('hnmo.website.session'); channel.postMessage('changed'); channel.close(); } catch { /* Optional review synchronization. */ } location.href = '/account.html'; }); };
  let selected = '';
  const cards = websitePreviewCharacters().map(character => {
    const button = document.createElement('button'); button.type = 'button';
    button.style.cssText = 'padding:20px;min-height:120px;text-align:left;border:1px solid var(--gold);background:var(--panel);color:var(--text)';
    button.onclick = () => { selected = character.name; update(); };
    actions.append(button); return { button, character };
  });
  const update = (): void => {
    const vi = i18n.currentLanguage() === 'vi';
    for (const button of document.querySelectorAll<HTMLButtonElement>('[data-lang]')) button.setAttribute('aria-pressed', String(button.dataset.lang === i18n.currentLanguage()));
    title.textContent = vi ? 'Chọn nhân vật · dữ liệu mẫu' : 'Choose a character · sample records';
    detail.textContent = selected ? (vi ? `${selected}: bản xem chưa nối máy chủ game thật.` : `${selected}: no live gameplay server is connected to this preview.`) : (vi ? 'Phiên mẫu đã được giữ; không cần nhập mật khẩu lần nữa.' : 'Your sample session is retained; no second password is needed.');
    for (const { button, character } of cards) button.textContent = `${character.name} · ${vi ? character.classNameVi : character.className} · ${vi ? 'Cấp' : 'Level'} ${character.level}`;
    exit.textContent = vi ? 'Đăng xuất tài khoản mẫu' : 'Sign out of the sample account';
    exit.title = vi ? 'Kết thúc phiên xem tài khoản mẫu trên website.' : 'End the sample account session on this website.';
  };
  if (form instanceof HTMLElement) { form.style.boxSizing = 'border-box'; form.style.maxWidth = '100%'; }
  if (signedIn && choices) { panel.append(title, detail, actions, exit); choices.replaceChildren(panel); choices.hidden = false; }
  for (const button of document.querySelectorAll<HTMLButtonElement>('[data-lang]')) button.addEventListener('click', () => { void i18n.setLanguage(button.dataset.lang ?? 'en').then(update); });
  update();
}

function banner(): void {
  const note = document.createElement('aside');
  note.className = 'website-preview-notice';
  note.style.cssText = 'position:relative;z-index:100;padding:12px 18px;background:#171b21;color:#f0e5cc;font:14px/1.5 system-ui;border-bottom:1px solid #8d784e';
  const update = (): void => { note.textContent = document.documentElement.lang === 'en' ? 'Private design preview · original website features · fictional records · real payments and changes are blocked.' : 'Bản xem riêng · giữ tính năng website hiện có · dữ liệu mẫu · chặn thanh toán và thay đổi thật.'; };
  new MutationObserver(update).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
  update(); document.body.prepend(note);
  // The existing form and account modules do the rendering and navigation; no replacement dashboard.
  const user = document.getElementById('accountUser'); const secret = document.getElementById('accountSecret');
  if (user instanceof HTMLInputElement && secret instanceof HTMLInputElement) { user.value = 'Explorer'; secret.value = 'example-only'; user.readOnly = true; secret.readOnly = true; }
  if (previewPage === '/game') {
    const login = document.getElementById('login');
    if (login) { login.style.cssText = 'position:relative;inset:auto;min-height:calc(100svh - 100px);box-sizing:border-box;padding:20px 16px;display:flex;flex-direction:column;gap:16px'; const explanation = document.createElement('p'); explanation.textContent = 'Chỉ xem giao diện đăng nhập; không có máy chủ gameplay ở bản xem. / Login appearance only; no gameplay server is connected.'; explanation.style.cssText = 'padding:16px;background:#10151ef0;color:#eee;margin:0;font:14px/1.5 system-ui;max-width:600px;box-sizing:border-box'; login.prepend(explanation); }
    document.querySelector('form')?.addEventListener('submit', event => event.preventDefault());
    void initializeGamePreview();
  }
}
// Review controls go to the real page layouts, not the old reduced portal demo.
if (previewPage === '/account-profile-preview') {
  document.addEventListener('click', event => {
    const target = event.target instanceof Element ? event.target.closest('button, a') : null;
    if (!target) return;
    let path: string | undefined;
    if (target.matches('.portal-header .preview-brand')) path = '/home.html';
    if (target.matches('.portal-actions button:first-child')) path = '/game.html';
    if (target.matches('.portal-actions button:last-child')) path = '/account.html';
    if (target.matches('.portal-footer a')) path = '/home.html';
    if (path) { event.preventDefault(); event.stopImmediatePropagation(); location.href = path; }
  }, { capture: true });
}
window.addEventListener('storage', event => { if (event.key === hostSetting) location.reload(); });
if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', banner, { once: true }); else banner();

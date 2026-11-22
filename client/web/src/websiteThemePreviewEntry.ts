import { websitePreviewReply } from './websiteThemePreviewData.js';

// Loaded only by the owner-private Site, never by the production HTML pages.
const hostSetting = 'hnmo.preview.owner.websiteTheme';
const skins = ['classic', 'ember', 'azure', 'jade', 'violet', 'parchment'] as const;
function selectedSkin(): string {
  try { const saved = localStorage.getItem(hostSetting); if (skins.some(skin => skin === saved)) return saved!; } catch { /* A preview can run without storage. */ }
  return 'classic';
}
const originalFetch = window.fetch.bind(window);
window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
  const url = new URL(input instanceof Request ? input.url : String(input), location.href);
  if (url.origin !== location.origin) return originalFetch(input, init);
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
if (location.pathname === '/account-profile-preview.html') {
  const url = new URL(location.href);
  const skin = selectedSkin();
  const layout = skin === 'classic' ? 'ember' : skin;
  if (url.searchParams.get('view') !== 'profile' || url.searchParams.get('theme') !== layout) {
    url.searchParams.set('view', 'profile'); url.searchParams.set('theme', layout); location.replace(url);
  }
}
if (location.pathname === '/index.html' && location.hash.startsWith('#ticket=')) location.replace('/game.html' + location.hash);

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
  if (location.pathname === '/game.html') {
    const login = document.getElementById('login');
    if (login) { const explanation = document.createElement('p'); explanation.textContent = 'Chỉ xem giao diện đăng nhập; không có máy chủ gameplay ở bản xem. / Login appearance only; no gameplay server is connected.'; explanation.style.cssText = 'padding:16px;background:#10151ef0;color:#eee;position:absolute;top:0;left:0;right:0;margin:0;font:14px/1.5 system-ui'; login.prepend(explanation); }
    document.querySelector('form')?.addEventListener('submit', event => event.preventDefault());
  }
}
// Review controls go to the real page layouts, not the old reduced portal demo.
if (location.pathname === '/account-profile-preview.html') {
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

import { enterGame, installSessionTransport } from './websiteSession.js';
installSessionTransport();
const back = document.createElement('a');
back.className = 'website-back-home'; back.href = '/home.html';
const status = document.createElement('p'); status.className = 'website-entry-status'; status.setAttribute('role', 'status'); status.hidden = true;
function language(): void {
  const vi = document.documentElement.lang === 'vi';
  back.textContent = vi ? 'Trang chủ' : 'Home';
  back.title = vi ? 'Về đầu trang chủ website.' : 'Return to the top of the website home page.';
}
language();new MutationObserver(language).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
document.body.append(back, status);
document.addEventListener('click', event => {
  const target = event.target instanceof Element ? event.target.closest<HTMLAnchorElement>('a[data-enter-game]') : null;
  if (!target || event instanceof MouseEvent && (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0)) return;
  event.preventDefault();
  if (target.getAttribute('aria-busy') === 'true') return;
  target.setAttribute('aria-busy', 'true');
  void enterGame().catch(() => {
    status.hidden = false; status.textContent = document.documentElement.lang === 'vi' ? 'Chưa thể vào game. Hãy thử lại; phiên tài khoản của bạn vẫn được giữ.' : 'The game could not be opened. Try again; your account session is still available.';
  }).finally(() => target.removeAttribute('aria-busy'));
});

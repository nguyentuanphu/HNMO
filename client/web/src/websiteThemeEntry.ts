import { loadWebsiteSettings } from './websiteTheme.js';

/** Wraps the existing links without replacing their elements, destinations or handlers. */
function mountWebsiteNavigation(): void {
  if (document.body.dataset.websitePage !== 'home') return;
  const nav = document.querySelector<HTMLElement>('body > header > nav');
  if (!nav) return;
  const menu = document.createElement('details');
  menu.className = 'website-navigation';
  const toggle = document.createElement('summary');
  const translate = (): void => {
    const vi = document.documentElement.lang === 'vi';
    toggle.textContent = vi ? 'Menu website' : 'Website menu';
    toggle.title = vi ? 'Mở hoặc đóng menu; mọi mục trên website vẫn có ở đây.' : 'Open or close navigation; every website section remains here.';
  };
  nav.before(menu); menu.append(toggle, nav);
  const mobile = matchMedia('(max-width: 850px)');
  const resize = (): void => { menu.open = !mobile.matches; };
  mobile.addEventListener('change', resize);
  new MutationObserver(translate).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
  translate(); resize();
}

void loadWebsiteSettings();
mountWebsiteNavigation();

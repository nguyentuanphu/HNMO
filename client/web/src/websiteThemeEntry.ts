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

function mountWebsiteCredit(): void {
  let footer = document.body.dataset.websitePage === 'game' ? document.querySelector('#login .panel') : document.querySelector('body > footer');
  if (!footer) { footer = document.createElement('footer'); document.body.append(footer); }
  const credit = document.createElement('small');
  credit.className = 'website-powered-by';
  const translate = (): void => {
    credit.textContent = document.documentElement.lang === 'vi' ? 'Vận hành bởi HNMO' : 'Powered by HNMO';
  };
  new MutationObserver(translate).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
  translate(); footer.append(credit);
}

/** Scroll wide tables inside their card without replacing table nodes or event handlers. */
function mountScrollableTables(): void {
  if (document.body.dataset.websitePage === 'game') return;
  const wrap = (): void => {
    for (const table of document.querySelectorAll('table')) {
      if (table.closest('.website-table-scroll, .account-scroll, .safety-scroll, .preview-scroll')) continue;
      const scroll = document.createElement('div');
      scroll.className = 'website-table-scroll';
      table.before(scroll); scroll.append(table);
    }
  };
  wrap();
  new MutationObserver(wrap).observe(document.body, { childList: true, subtree: true });
}

void loadWebsiteSettings();
mountWebsiteNavigation();
mountWebsiteCredit();
mountScrollableTables();

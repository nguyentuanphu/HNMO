/** Recompose the original feature nodes. No feature is re-rendered, cloned or replaced by a theme. */
export function mountWebsiteThemeLayout(): void {
  if (document.body.dataset.websitePage !== 'home') return;
  const main = document.querySelector('body > main');
  const menu = document.querySelector('.website-navigation');
  if (!(main instanceof HTMLElement) || !(menu instanceof HTMLElement)) return;
  const page = main; const navigation = menu;
  const original = [...page.children]; const header = navigation.parentElement!; const after = navigation.nextSibling;
  const anchor = document.createComment('website content'); page.before(anchor);
  let structure: HTMLElement | null = null;
  const labels = {
    world: ['The realm', 'Lục địa'], adventure: ['Your next adventure', 'Hành trình tiếp theo'], community: ['Life in the realm', 'Nhịp sống lục địa'],
  } as const;
  function node(tag: 'div' | 'aside', className: string): HTMLElement { const result = document.createElement(tag); result.className = className; return result; }
  function heading(key: keyof typeof labels): HTMLElement { const title = document.createElement('h2'); title.dataset.themeLabel = key; return title; }
  function language(): void {
    for (const title of document.querySelectorAll<HTMLElement>('[data-theme-label]')) {
      const key = title.dataset.themeLabel as keyof typeof labels;
      title.textContent = labels[key][document.documentElement.lang === 'vi' ? 1 : 0];
    }
  }
  function sections(ids: readonly string[]): Element[] { return ids.map(id => original.find(child => child.id === id)).filter((item): item is Element => !!item); }
  function rememberNewFeatures(): Element[] {
    const added: Element[] = [];
    for (const feature of page.querySelectorAll('section[id]')) {
      if (original.some(root => root.contains(feature))) continue;
      original.push(feature); added.push(feature);
    }
    return added;
  }
  function update(): void {
    const theme = document.documentElement.dataset.websiteTheme ?? 'classic';
    rememberNewFeatures();
    if (document.body.dataset.websiteLayout === theme) return;
    if (after?.parentNode === header) header.insertBefore(navigation, after); else header.append(navigation);
    anchor.after(page); structure?.remove(); structure = null;
    page.replaceChildren(...original);
    document.body.dataset.websiteLayout = theme;
    const hero = original.find(child => child.classList.contains('hero'));
    if (!hero) return;
    if (theme === 'ember' || theme === 'violet') {
      structure = node('div', `theme-frame theme-frame-${theme}`);
      const rail = node('aside', 'theme-navigation-rail'); rail.append(navigation);
      anchor.after(structure);
      structure.append(...(theme === 'ember' ? [rail, page] : [page, rail]));
      if (theme === 'violet') {
        const deck = node('div', 'theme-command-deck'); const feed = node('aside', 'theme-live-feed');
        feed.append(...sections(['news', 'schedule', 'bosses'])); deck.append(feed, hero); page.prepend(deck);
      }
    } else if (theme === 'azure') {
      const intro = node('div', 'theme-bento-intro'); const board = node('div', 'theme-server-board');
      board.append(...sections(['realm', 'news'])); intro.append(hero, board); page.prepend(intro);
    } else if (theme === 'jade') {
      const ribbon = node('div', 'theme-sanctuary-ribbon'); ribbon.append(navigation);
      const columns = node('div', 'theme-sanctuary-columns');
      const groups = [
        ['world', ['realm', 'world', 'rankings', 'drops', 'resets', 'jewel-bank']],
        ['adventure', ['newplayers', 'missions', 'events', 'schedule', 'bosses', 'races', 'catchup', 'season-plan', 'mounts', 'start', 'phone']],
        ['community', ['news', 'guilds', 'social', 'gifts', 'market', 'marriage', 'party-spots', 'lucky-number', 'leftovers', 'topup-milestones-section']],
      ] as const;
      const assigned = new Set<Element>([hero]);
      for (const [key, ids] of groups) {
        const column = node('div', 'theme-sanctuary-column'); const content = sections(ids);
        for (const child of content) assigned.add(child);
        column.append(heading(key), ...content); columns.append(column);
      }
      for (const child of original) if (!assigned.has(child)) columns.lastElementChild!.append(child);
      page.replaceChildren(hero, ribbon, columns);
    } else if (theme === 'parchment') {
      const book = node('div', 'theme-open-book'); const cover = node('aside', 'theme-book-cover'); const pages = node('div', 'theme-book-pages');
      cover.append(hero, navigation); pages.append(...original.filter(child => child !== hero)); book.append(cover, pages); page.append(book);
    }
    language();
  }
  new MutationObserver(() => {
    const added = rememberNewFeatures();
    const theme = document.body.dataset.websiteLayout;
    const target = theme === 'jade' ? page.querySelector('.theme-sanctuary-column:last-child') : theme === 'parchment' ? page.querySelector('.theme-book-pages') : null;
    if (target) for (const feature of added) target.append(feature);
  }).observe(page, { childList: true, subtree: true });
  new MutationObserver(update).observe(document.documentElement, { attributes: true, attributeFilter: ['data-website-theme'] });
  new MutationObserver(language).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
  update();
}

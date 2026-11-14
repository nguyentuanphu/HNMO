// The game's name players see. The admin panel sets it (the "Game name" plug-in); the web server
// hands it out as /game-name.json. Until it's loaded, and when it can't be, the default is used.

export const DEFAULT_GAME_NAME = 'HenryN Mu Online';

let current = DEFAULT_GAME_NAME;
/** The community links set in the admin panel (zalo, discord, facebook), https only. */
let links: Record<string, string> = {};
const LINK_LABELS: Record<string, string> = { zalo: 'Zalo', discord: 'Discord', facebook: 'Facebook' };

/** The game's name, e.g. for messages ("… Item Shop"). */
export function gameName(): string {
  return current;
}

/** Writes the name into the page: the title and every element marked with data-game-name. */
export function applyGameName(root: ParentNode = document): void {
  if (root === document) document.title = document.title.includes('{game}') ? document.title.replace('{game}', current) : current;
  for (const element of root.querySelectorAll<HTMLElement>('[data-game-name]')) {
    const template = element.dataset.gameName || '{game}';
    const text = template.replaceAll('{game}', current);
    const attribute = element.dataset.gameNameAttr;
    if (attribute) element.setAttribute(attribute, text);
    else element.textContent = text;
  }
}

/** Fetches the configured name (once at start; the page shows the default until then). */
export async function loadGameName(): Promise<string> {
  try {
    const response = await fetch('/game-name.json', { cache: 'no-cache' });
    if (response.ok) {
      const info = (await response.json()) as { name?: unknown; links?: unknown };
      if (typeof info.name === 'string' && info.name.trim()) current = info.name.trim().slice(0, 60);
      if (info.links && typeof info.links === 'object') {
        links = Object.fromEntries(Object.entries(info.links as Record<string, unknown>)
          .filter((entry): entry is [string, string] => typeof entry[1] === 'string' && /^https:\/\//.test(entry[1]) && entry[0] in LINK_LABELS));
      }
    }
  } catch {
    // Offline or the page opened from the disk: keep the default.
  }
  applyGameName();
  showCommunityLinks();
  return current;
}

/** The community links (Zalo, Discord, Facebook) set in the admin panel. */
export function communityLinks(): Readonly<Record<string, string>> {
  return links;
}

/** Fills every [data-community-links] element with the links; it stays hidden when there are none. */
export function showCommunityLinks(root: ParentNode = document): void {
  for (const box of root.querySelectorAll<HTMLElement>('[data-community-links]')) {
    const entries = Object.entries(links);
    box.hidden = entries.length === 0;
    const label = document.createElement('span');
    label.textContent = box.dataset.communityLinks || 'Contact:';
    box.replaceChildren(label, ...entries.map(([kind, url]) => {
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.target = '_blank';
      anchor.rel = 'noopener noreferrer';
      anchor.className = `community-link community-${kind}`;
      anchor.textContent = LINK_LABELS[kind] ?? kind;
      return anchor;
    }));
  }
}

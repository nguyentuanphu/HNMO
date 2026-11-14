// The game's name players see. The admin panel sets it (the "Game name" plug-in); the web server
// hands it out as /game-name.json. Until it's loaded, and when it can't be, the default is used.

export const DEFAULT_GAME_NAME = 'HenryN Mu Online';

let current = DEFAULT_GAME_NAME;

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
      const { name } = (await response.json()) as { name?: unknown };
      if (typeof name === 'string' && name.trim()) current = name.trim().slice(0, 60);
    }
  } catch {
    // Offline or the page opened from the disk: keep the default.
  }
  applyGameName();
  return current;
}

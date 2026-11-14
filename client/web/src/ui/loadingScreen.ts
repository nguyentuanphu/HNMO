// The loading screen (CLoadingScene, loading.rml): the four-piece picture over the whole window from
// the moment a character is chosen until its world is ready, as the desktop shows it between the
// character screen and the game.
import { openDocument, type RmlDocument } from './rml.js';

let shown: Promise<RmlDocument | null> | null = null;

/** Shows the loading picture (once; a second call keeps the first). */
export function showLoadingScreen(parent: HTMLElement = document.body): void {
  if (shown) return;
  shown = openDocument('loading', {}, { parent }).then((doc) => {
    Object.assign(doc.host.style, { position: 'fixed', inset: '0', zIndex: '90' });
    Object.assign(doc.root.style, { width: '100vw', height: '100vh' });
    doc.host.classList.add('loading-screen');
    return doc;
  }).catch((error: unknown) => { console.warn('Interface', error); return null; });
}

/** Takes the loading picture away. */
export function hideLoadingScreen(): void {
  const doc = shown;
  shown = null;
  void doc?.then((d) => d?.close());
}

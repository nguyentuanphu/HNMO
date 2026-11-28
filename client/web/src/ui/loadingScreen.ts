// The loading screen (CLoadingScene, loading.rml): the four-piece picture over the whole window from
// the moment a character is chosen until its world is ready, as the desktop shows it between the
// character screen and the game. HNMO adds the loading bar over its lower band (ui/loadingProgress.ts).
import { openDocument, type RmlDocument } from './rml.js';
import { LoadingProgress, loadingWord } from './loadingProgress.js';

let shown: Promise<RmlDocument | null> | null = null;
let progress: LoadingProgress | null = null;

/** Shows the loading picture and its bar (once; a second call keeps the first). */
export function showLoadingScreen(parent: HTMLElement = document.body): void {
  if (shown) return;
  progress = new LoadingProgress(loadingWord());
  shown = openDocument('loading', {}, { parent }).then((doc) => {
    Object.assign(doc.host.style, { position: 'fixed', inset: '0', zIndex: '90' });
    Object.assign(doc.root.style, { width: '100vw', height: '100vh' });
    doc.host.classList.add('loading-screen');
    return doc;
  }).catch((error: unknown) => { console.warn('Interface', error); return null; });
}

/** The bar of the loading screen while it shows (null otherwise). */
export function loadingScreenProgress(): LoadingProgress | null {
  return progress;
}

/** Takes the loading picture and its bar away. */
export function hideLoadingScreen(): void {
  const doc = shown;
  shown = null;
  progress?.close();
  progress = null;
  void doc?.then((d) => d?.close());
}

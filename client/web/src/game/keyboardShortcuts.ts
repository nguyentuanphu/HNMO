// Shared keyboard rules for the game and its dialogs.
type Modifiers = 'none' | 'shift' | 'alt' | 'ctrl' | 'ctrl-shift';

const EDITOR = 'input, textarea, select, [contenteditable]:not([contenteditable="false"]), [role="textbox"]';
const CONTROL = `${EDITOR}, button, a[href], summary, [role="button"], [role="link"], [tabindex]`;
let composing = false;
let chatOpen: () => boolean = () => false;

export function installKeyboardShortcuts(isChatOpen: () => boolean): void {
  chatOpen = isChatOpen;
  window.addEventListener('compositionstart', () => { composing = true; }, true);
  window.addEventListener('compositionend', () => { composing = false; }, true);
  window.addEventListener('blur', () => { composing = false; });
}

function matchesFocus(event: KeyboardEvent, selector: string): boolean {
  if (event.composedPath().some((node) => node instanceof HTMLElement && (node.matches(selector)
    || (selector === EDITOR && node.isContentEditable)))) return true;
  // Synthetic toolbar events target window; the focused editor can be nested in multiple roots.
  for (let active = document.activeElement; active; active = active.shadowRoot?.activeElement ?? null) {
    if (active instanceof HTMLElement && (active.matches(selector) || (selector === EDITOR && active.isContentEditable))) return true;
  }
  return false;
}

export const hasTextInput = (event: KeyboardEvent): boolean => matchesFocus(event, EDITOR);
export const hasKeyboardControl = (event: KeyboardEvent): boolean => matchesFocus(event, CONTROL);

export function matchesModifiers(event: KeyboardEvent, modifiers: Modifiers = 'none', platform = navigator.platform): boolean {
  const alt = modifiers === 'alt';
  const ctrl = modifiers === 'ctrl' || modifiers === 'ctrl-shift';
  const shift = modifiers === 'shift' || modifiers === 'ctrl-shift';
  if (event.altKey !== alt || event.ctrlKey !== ctrl || event.shiftKey !== shift || event.metaKey) return false;
  return !event.getModifierState('AltGraph') || (alt && /^Mac/i.test(platform));
}

export function canHandleKey(event: KeyboardEvent): boolean {
  return !event.defaultPrevented && !event.repeat && !composing && !event.isComposing
    && event.keyCode !== 229 && event.key !== 'Process';
}

export function canUseGameShortcut(event: KeyboardEvent): boolean {
  return canHandleKey(event) && !chatOpen() && !hasTextInput(event);
}

export function gameKey(event: KeyboardEvent, key: string, modifiers: Modifiers = 'none'): boolean {
  return event.key.toLowerCase() === key.toLowerCase()
    && matchesModifiers(event, modifiers) && canUseGameShortcut(event);
}

export function optionKey(event: KeyboardEvent, code: string): boolean {
  return event.code === code && matchesModifiers(event, 'alt') && canUseGameShortcut(event);
}

export function panelKey(event: KeyboardEvent, key: 'Enter' | 'Escape'): boolean {
  return event.key === key && canHandleKey(event) && matchesModifiers(event) && !chatOpen()
    && (key !== 'Enter' || !hasKeyboardControl(event) || hasTextInput(event));
}

/** Tracks the ground label hold and the unmodified physical Alt tap. */
export function trackAltNames(): { readonly held: boolean; readonly toggled: boolean } {
  let held = false;
  let tapped = false;
  let toggled = false;
  const clear = () => { held = false; tapped = false; };
  window.addEventListener('keydown', (event) => {
    if (event.key !== 'Alt') { tapped = false; return; }
    if (event.repeat) return;
    // The touch toolbar uses a synthetic Alt press to hold the ground labels.
    if (!canUseGameShortcut(event) || !matchesModifiers(event, event.altKey ? 'alt' : 'none')) return;
    held = true;
    tapped = event.isTrusted;
    event.preventDefault();
  }, true);
  window.addEventListener('keyup', (event) => {
    if (event.key !== 'Alt') return;
    if (tapped && event.isTrusted && canUseGameShortcut(event) && matchesModifiers(event)) {
      toggled = !toggled;
      event.preventDefault();
    }
    clear();
  }, true);
  window.addEventListener('pointerdown', () => { tapped = false; }, true);
  window.addEventListener('focusin', clear, true);
  window.addEventListener('blur', clear);
  document.addEventListener('visibilitychange', clear);
  return { get held() { return held; }, get toggled() { return toggled; } };
}

// The desktop client's text sizes and its shrink-to-fit rule, so the windows' texts take the same room
// as there (UITransform.cpp: TypographyScale, NativeTextPixelSize, FitTextPixelSizeToWidth; RmlNativeText,
// RmlNativeTextFit) and the font the legacy theme names (tokens.ini font-body / font-title "DejaVu Sans",
// shipped with the game in fonts/ and served from /fonts).

export const REFERENCE_WIDTH = 640;
export const REFERENCE_HEIGHT = 480;
/** UI::Scaling::MaximumPanelScale. */
const MAXIMUM_PANEL_SCALE = 2;
/** kMaximumTypographyScale: the scale at which the text reaches its largest size. */
const MAXIMUM_TYPOGRAPHY_SCALE = 2.25;

export type FontRole = 'normal' | 'big' | 'fixed';
/** GetFontPointRange: the smallest and largest size of each font role. */
const POINT_RANGE: Record<FontRole, [number, number]> = { normal: [11, 16], big: [22, 32], fixed: [13, 18] };

/** The family the legacy theme's texts use (tokens.ini). */
export const FONT_FAMILY = '"DejaVu Sans"';

/** TypographyScale (= the panels' dp ratio): the 640x480 reference fitted to the window, 1..2. */
export function typographyScale(width = window.innerWidth, height = window.innerHeight): number {
  return Math.min(Math.max(Math.min(width / REFERENCE_WIDTH, height / REFERENCE_HEIGHT), 1), MAXIMUM_PANEL_SCALE);
}

/** NativeTextPixelSize: the size the native text renderer draws at, in screen pixels. It grows more slowly than the panels. */
export function nativeTextPx(role: FontRole = 'normal', width = window.innerWidth, height = window.innerHeight): number {
  const [minimum, maximum] = POINT_RANGE[role];
  const growth = Math.min(Math.max((typographyScale(width, height) - 1) / (MAXIMUM_TYPOGRAPHY_SCALE - 1), 0), 1);
  return Math.round(minimum + (maximum - minimum) * growth);
}

/** MinimumTextPixelSize: the smallest size a text shrunk into its box keeps. */
export function minimumTextPx(role: FontRole = 'normal'): number {
  return POINT_RANGE[role][0];
}

/** The native text size in a panel's reference pixels (a panel drawn at `panelScale`). */
export function nativeTextReferencePx(panelScale: number, role: FontRole = 'normal'): number {
  return nativeTextPx(role) / (panelScale || 1);
}

/** SceneWindowScale: how much the native text has grown against its size at 1024x768, never below 1. */
export function sceneWindowScale(): number {
  return Math.max(nativeTextPx() / nativeTextPx('normal', 1024, 768), 1);
}

/** SceneBarScale. */
export function sceneBarScale(): number {
  return Math.min(Math.max(Math.min(window.innerWidth / 800, window.innerHeight / 600), 1), 2);
}

/** FitTextPixelSizeToWidth: a text measured wider than its box drawn smaller, down to the minimum. */
export function fitTextPx(textPx: number, measuredWidth: number, boxWidth: number, minimumPx = minimumTextPx()): number {
  if (measuredWidth <= boxWidth || measuredWidth <= 0) return textPx;
  return Math.max(textPx * boxWidth / measuredWidth, Math.min(minimumPx, textPx));
}

let measureContext: CanvasRenderingContext2D | null = null;

/** The width of `text` in the theme's font at `px` pixels. */
export function measureText(text: string, px: number, bold = false): number {
  measureContext ??= document.createElement('canvas').getContext('2d');
  if (!measureContext) return text.length * px * 0.6;
  measureContext.font = `${bold ? 'bold ' : ''}${px}px ${FONT_FAMILY}, sans-serif`;
  return measureContext.measureText(text).width;
}

/** NativeTextPxInBox: the native size of a text drawn into a box of `boxPx` screen pixels. */
export function nativeTextPxInBox(text: string, boxPx: number, bold = false, role: FontRole = 'normal'): number {
  const px = nativeTextPx(role);
  if (!text || boxPx <= 0) return px;
  return fitTextPx(px, measureText(text, px, bold), boxPx, minimumTextPx(role));
}

let fontsLoaded = false;

/** Declares the theme's font once on the page (a shadow root's own @font-face is not used by the browser). */
export function loadThemeFonts(): void {
  if (fontsLoaded) return;
  fontsLoaded = true;
  const style = document.createElement('style');
  style.textContent = `
@font-face { font-family: "DejaVu Sans"; src: url("/fonts/DejaVuSans.ttf") format("truetype"); font-weight: normal; font-style: normal; font-display: swap; }
@font-face { font-family: "DejaVu Sans"; src: url("/fonts/DejaVuSans-Bold.ttf") format("truetype"); font-weight: bold; font-style: normal; font-display: swap; }
`;
  document.head.append(style);
}

/**
 * FitNativeTextToBoxes: elements with class "native-fit" or "text-fit" draw their one-line text smaller
 * until it fits their box ("native-fit" keeps the native minimum, "text-fit" may go down to 1 px). The
 * size is set in em of the inherited size, like the desktop's pass.
 */
export function fitTextToBoxes(root: ParentNode): void {
  const elements = root.querySelectorAll<HTMLElement>('.native-fit, .text-fit');
  if (elements.length === 0) return;
  const nativePx = nativeTextPx();
  for (const element of elements) {
    if (element.offsetParent === null) continue; // not shown
    element.style.removeProperty('font-size');
    const box = element.clientWidth;
    const text = element.scrollWidth;
    if (box <= 0 || text <= box + 0.5) continue;
    const parentPx = parseFloat(getComputedStyle(element).fontSize) || 12;
    // The native minimum, in the units the element's text is laid out in.
    const minimum = element.classList.contains('native-fit') ? Math.min(parentPx * minimumTextPx() / nativePx, parentPx) : 1;
    const px = Math.max(parentPx * box / text, minimum);
    element.style.fontSize = `${(px / parentPx).toFixed(4)}em`;
  }
}

// ---------- marquee (RmlMarquee) ----------

const MARQUEE_SPEED = 50;        // the element's own px per second
const MARQUEE_START_DELAY = 0.15;
const MARQUEE_END_PAUSE = 0.8;
const ELLIPSIS = '..';

const pointer = { x: -1, y: -1, tracked: false };
function trackPointer(): void {
  if (pointer.tracked) return;
  pointer.tracked = true;
  window.addEventListener('pointermove', (event) => { pointer.x = event.clientX; pointer.y = event.clientY; }, { capture: true, passive: true });
}

interface MarqueeState { full: string; shown: string; hoverStart: number }
const marqueeStates = new WeakMap<HTMLElement, MarqueeState>();

/** Where the text sits `elapsed` seconds into a hover: a wait, the run to the end, a pause, the run back, a pause. */
function marqueeOffset(elapsed: number, overflow: number): number {
  const run = overflow / MARQUEE_SPEED;
  if (elapsed < MARQUEE_START_DELAY) return 0;
  let t = (elapsed - MARQUEE_START_DELAY) % (2 * (run + MARQUEE_END_PAUSE));
  if (t < run) return t * MARQUEE_SPEED;
  t -= run;
  if (t < MARQUEE_END_PAUSE) return overflow;
  t -= MARQUEE_END_PAUSE;
  if (t < run) return Math.max(0, overflow - t * MARQUEE_SPEED);
  return 0;
}

/**
 * RmlMarquee: a ".marquee" text wider than its box shows its start cut with ".." and, while the pointer
 * is on it, its whole text scrolling to the end and back. Returns whether one is scrolling (call again
 * next frame then).
 */
export function runMarquees(root: ParentNode, now = performance.now() / 1000): boolean {
  const elements = root.querySelectorAll<HTMLElement>('.marquee');
  if (elements.length === 0) return false;
  trackPointer();
  let scrolling = false;
  for (const element of elements) {
    const text = element.firstChild;
    if (!(text instanceof Text) || element.childNodes.length !== 1) continue;
    let state = marqueeStates.get(element);
    const current = text.data;
    if (!state) { state = { full: current, shown: current, hoverStart: -1 }; marqueeStates.set(element, state); }
    // Text this pass didn't put there came from the model: the full text.
    if (current !== state.shown) { state.full = current; state.shown = current; }
    if (element.offsetParent === null) continue;
    const width = element.clientWidth;
    if (width <= 0) continue;
    const style = getComputedStyle(element);
    measureContext ??= document.createElement('canvas').getContext('2d');
    if (!measureContext) continue;
    measureContext.font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
    const measure = (value: string) => measureContext!.measureText(value).width;
    const overflow = measure(state.full) - width;
    const rect = element.getBoundingClientRect();
    const hovered = overflow > 0.5 && pointer.x >= rect.left && pointer.x < rect.right && pointer.y >= rect.top && pointer.y < rect.bottom;
    element.classList.toggle('marquee-overflow', hovered);
    let shown = state.full;
    if (!hovered) {
      if (overflow > 0.5) {
        let fit = 0;
        for (let end = 1; end <= state.full.length; end++) {
          if (measure(state.full.slice(0, end) + ELLIPSIS) > width) break;
          fit = end;
        }
        shown = state.full.slice(0, fit).trimEnd() + ELLIPSIS;
      }
      if (state.hoverStart >= 0) element.scrollLeft = 0;
      state.hoverStart = -1;
    } else {
      if (state.hoverStart < 0) state.hoverStart = now;
      element.scrollLeft = marqueeOffset(now - state.hoverStart, overflow);
      scrolling = true;
    }
    if (text.data !== shown) text.data = shown;
    state.shown = shown;
  }
  return scrolling;
}

/** LineHeightPx: one line of native text in screen pixels (the font's ascent and descent). */
export function nativeLineHeightPx(role: FontRole = 'normal'): number {
  return Math.round(nativeTextPx(role) * 1.164);
}

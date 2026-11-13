// HNMO extra: playing on a phone or tablet. A tap does what a left click does (walk, attack, talk,
// pick up: the game's pointer handlers already take touches); a long press does what a right click
// does (the current skill at what's under the finger); two fingers pinch to zoom and slide sideways
// to turn the camera; a bar of buttons opens what only the keyboard opens (the inventory, the
// character, the mini map, the move list, the party, auto play, the chat).

const LONG_PRESS_MS = 500;
/** How far a finger may move and still be a press, in pixels. */
const PRESS_SLOP = 10;

export interface TouchActions {
  canvas: HTMLCanvasElement;
  /** Multiplies the camera distance (below 1 zooms in). */
  zoom: (factor: number) => void;
  /** Turns the camera by a sideways finger movement in pixels. */
  rotate: (dx: number) => void;
  /** The right mouse button's skill at a screen point. */
  cast: (x: number, y: number) => void;
}

/** The buttons: a label and the key the desktop opens it with. */
const BUTTONS: { label: string; title: string; key: string }[] = [
  { label: 'I', title: 'Inventory', key: 'i' },
  { label: 'C', title: 'Character', key: 'c' },
  { label: 'P', title: 'Party', key: 'p' },
  { label: 'M', title: 'Move', key: 'm' },
  { label: 'Map', title: 'Mini map', key: 'Tab' },
  { label: 'Z', title: 'Auto play settings', key: 'z' },
  { label: '▶', title: 'Auto play on / off', key: 'Home' },
  { label: '💬', title: 'Chat', key: 'Enter' },
];

const CSS = `
#touchBar { position: fixed; right: 8px; top: 50%; transform: translateY(-50%); z-index: 40; display: none;
  flex-direction: column; gap: 6px; pointer-events: auto; }
#touchBar button { min-width: 44px; min-height: 44px; border-radius: 10px; border: 1px solid rgba(255,220,150,.45);
  background: rgba(20,16,10,.72); color: #f3e2bd; font: 600 14px system-ui, sans-serif; touch-action: manipulation; }
#touchBar button:active { background: rgba(80,60,30,.9); }
#touchBar.shown { display: flex; }
`;

export function installTouchControls(actions: TouchActions): HTMLElement {
  const { canvas } = actions;
  canvas.style.touchAction = 'none';
  const style = document.createElement('style');
  style.textContent = CSS;
  document.head.append(style);
  const bar = document.createElement('div');
  bar.id = 'touchBar';
  for (const button of BUTTONS) {
    const element = document.createElement('button');
    element.type = 'button';
    element.textContent = button.label;
    element.title = button.title;
    element.dataset.key = button.key;
    element.addEventListener('pointerdown', (event) => event.stopPropagation());
    element.addEventListener('click', (event) => {
      event.stopPropagation();
      if (button.key === 'Enter') {
        // The chat opens on Enter outside the input: focus it directly (no key event reaches it on a phone).
        document.querySelector<HTMLInputElement>('#chatInput')?.focus();
        return;
      }
      window.dispatchEvent(new KeyboardEvent('keydown', { key: button.key, bubbles: true }));
    });
    bar.append(element);
  }
  document.body.append(bar);
  // Shown on touch screens, and from the first touch on any screen.
  const show = () => bar.classList.add('shown');
  if (window.matchMedia?.('(pointer: coarse)').matches) show();

  const fingers = new Map<number, { x: number; y: number }>();
  let press: { id: number; x: number; y: number; timer: number } | null = null;
  let pinch: { distance: number; midX: number } | null = null;

  const cancelPress = () => { if (press) { window.clearTimeout(press.timer); press = null; } };
  const twoFingers = () => {
    const [a, b] = [...fingers.values()];
    return { distance: Math.hypot(a.x - b.x, a.y - b.y), midX: (a.x + b.x) / 2 };
  };

  canvas.addEventListener('pointerdown', (event) => {
    if (event.pointerType !== 'touch') return;
    show();
    fingers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    if (fingers.size === 1) {
      const id = event.pointerId, x = event.clientX, y = event.clientY;
      press = { id, x, y, timer: window.setTimeout(() => { press = null; actions.cast(x, y); }, LONG_PRESS_MS) };
    } else {
      cancelPress();
      if (fingers.size === 2) pinch = twoFingers();
    }
  });
  window.addEventListener('pointermove', (event) => {
    if (event.pointerType !== 'touch' || !fingers.has(event.pointerId)) return;
    fingers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    if (press && press.id === event.pointerId && Math.hypot(event.clientX - press.x, event.clientY - press.y) > PRESS_SLOP) cancelPress();
    if (pinch && fingers.size === 2) {
      const now = twoFingers();
      if (now.distance > 0 && pinch.distance > 0) actions.zoom(pinch.distance / now.distance);
      actions.rotate(now.midX - pinch.midX);
      pinch = now;
    }
  });
  const lift = (event: PointerEvent) => {
    if (event.pointerType !== 'touch') return;
    fingers.delete(event.pointerId);
    if (press?.id === event.pointerId) cancelPress();
    if (fingers.size < 2) pinch = null;
  };
  window.addEventListener('pointerup', lift);
  window.addEventListener('pointercancel', lift);
  return bar;
}

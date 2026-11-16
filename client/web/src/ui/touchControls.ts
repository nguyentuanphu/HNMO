// HNMO extra: playing on a phone or tablet. A tap does what a left click does (walk, attack, talk,
// pick up: the game's pointer handlers already take touches); a long press does what a right click
// does (the current skill at what's under the finger); two fingers pinch to zoom and slide sideways
// to turn the camera; a bar of buttons opens what only the keyboard opens (the inventory, the
// character, the mini map, the move list, the party, auto play, the chat). More for phones: a fight
// button (the current skill, else an attack, at the nearest monster), the skill keys 1-5 and the
// potion keys Q W E R as buttons, a "more" button with the other windows (command, friends, guild,
// quests, cash shop, Gens, community, top races, gift codes, a screenshot to share), full screen in landscape, and a hint to turn a phone held upright.

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
  /** The current skill (else an attack) at the nearest monster; false when there's none near. */
  fight: () => boolean;
}

/** The windows behind the "more" button. */
const MORE: { label: string; title: string; key: string; event?: string }[] = [
  { label: 'D', title: 'Command window', key: 'd' },
  { label: 'F', title: 'Friends', key: 'f' },
  { label: 'G', title: 'Guild', key: 'g' },
  { label: 'T', title: 'Quests', key: 't' },
  { label: 'X', title: 'Cash shop', key: 'x' },
  { label: 'B', title: 'Gens', key: 'b' },
  { label: 'J', title: 'Missions', key: 'j' },
  { label: 'H', title: 'Event & boss timetable', key: 'h' },
  { label: 'N', title: 'World bosses', key: 'n' },
  { label: 'Y', title: 'New-player guide', key: 'y' },
  { label: 'K', title: 'Community', key: 'k' },
  { label: '🏆', title: 'Top races', key: '', event: 'hnmo-top-races-open' },
  { label: '🎁', title: 'Gift code', key: '', event: 'hnmo-gift-codes-open' },
  { label: '📷', title: 'Screenshot / share', key: '', event: 'hnmo-share-open' },
];
const SKILL_KEYS = ['1', '2', '3', '4', '5'];
const POTION_KEYS = ['q', 'w', 'e', 'r'];

/** The buttons: a label and the key the desktop opens it with. */
const BUTTONS: { label: string; title: string; key: string }[] = [
  { label: 'I', title: 'Inventory', key: 'i' },
  { label: 'C', title: 'Character', key: 'c' },
  { label: 'P', title: 'Party', key: 'p' },
  { label: 'M', title: 'Move', key: 'm' },
  { label: 'Map', title: 'Mini map', key: 'Tab' },
  { label: 'Z', title: 'Auto play settings', key: 'z' },
  { label: '▶', title: 'Auto play on / off', key: 'Home' },
  { label: '⚙', title: 'Settings', key: 'o' },
  { label: '💬', title: 'Chat', key: 'Enter' },
];

const CSS = `
#touchBar { position: fixed; right: 8px; top: 50%; transform: translateY(-50%); z-index: 40; display: none;
  flex-direction: column; gap: 6px; pointer-events: auto; }
#touchBar button { min-width: 44px; min-height: 44px; border-radius: 10px; border: 1px solid rgba(255,220,150,.45);
  background: rgba(20,16,10,.72); color: #f3e2bd; font: 600 14px system-ui, sans-serif; touch-action: manipulation; }
#touchBar button:active { background: rgba(80,60,30,.9); }
#touchBar button.on { background: rgba(140,100,40,.95); border-color: #ffd98a; }
#touchBar.shown { display: flex; }
#touchMore { position: fixed; right: 62px; top: 50%; transform: translateY(-50%); z-index: 40; display: none; flex-direction: column; gap: 6px; }
#touchMore.open { display: flex; }
#touchMore button, #touchFight button { min-width: 44px; min-height: 44px; border-radius: 10px; border: 1px solid rgba(255,220,150,.45);
  background: rgba(20,16,10,.72); color: #f3e2bd; font: 600 14px system-ui, sans-serif; touch-action: manipulation; }
#touchFight { position: fixed; right: 64px; bottom: calc(12vh + 8px); z-index: 40; display: none; grid-template-columns: repeat(5, 40px); gap: 5px; align-items: end; }
#touchFight.shown { display: grid; }
#touchFight button { min-width: 40px; min-height: 40px; padding: 0; }
#touchFight .fight { grid-column: 4 / span 2; grid-row: 1 / span 2; width: 85px; height: 85px; border-radius: 50%; font-size: 30px; background: rgba(110,30,20,.8); }
#touchFight .potion { color: #ffb0a0; }
#touchFight button:active, #touchMore button:active { background: rgba(80,60,30,.9); }
#touchRotate { position: fixed; inset: 0; z-index: 95; display: none; align-items: center; justify-content: center; flex-direction: column; gap: 14px;
  background: rgba(8,8,6,.92); color: #f3e2bd; font: 600 18px system-ui, sans-serif; text-align: center; padding: 24px; }
#touchRotate.shown { display: flex; }
#touchRotate .phone { font-size: 56px; animation: hnmoTurn 1.6s ease-in-out infinite; }
@keyframes hnmoTurn { 0%, 30% { transform: rotate(0); } 60%, 100% { transform: rotate(-90deg); } }
#touchRotate button { all: unset; padding: 8px 16px; border: 1px solid rgba(255,220,150,.45); border-radius: 8px; font-size: 14px; }
`;

const key = (name: string) => window.dispatchEvent(new KeyboardEvent('keydown', { key: name, code: /^[0-9]$/.test(name) ? `Digit${name}` : name.length === 1 ? `Key${name.toUpperCase()}` : name, bubbles: true }));

function button(label: string, title: string, run: () => void, className = ''): HTMLButtonElement {
  const element = document.createElement('button');
  element.type = 'button';
  element.textContent = label;
  element.title = title;
  if (className) element.className = className;
  element.addEventListener('pointerdown', (event) => event.stopPropagation());
  element.addEventListener('click', (event) => { event.stopPropagation(); run(); });
  return element;
}

/** Full screen and, where the browser allows it, landscape. */
function fullScreenLandscape(): void {
  const done = () => { (screen.orientation as ScreenOrientation & { lock?: (o: string) => Promise<void> }).lock?.('landscape').catch(() => undefined); };
  if (document.fullscreenElement) { done(); return; }
  document.documentElement.requestFullscreen?.().then(done, () => undefined);
}

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
      window.dispatchEvent(new KeyboardEvent('keydown', { key: button.key, bubbles: true }));
    });
    bar.append(element);
  }
  // The "more" button and its windows; full screen.
  const more = document.createElement('div');
  more.id = 'touchMore';
  for (const entry of MORE) more.append(button(entry.label, entry.title, () => { more.classList.remove('open'); if (entry.event) window.dispatchEvent(new Event(entry.event)); else key(entry.key); }));
  // Item names on the ground (the desktop's held Alt): a toggle on a touch screen.
  let names = false;
  const namesButton = button('Alt', 'Item names on the ground (Alt)', () => {
    names = !names;
    namesButton.classList.toggle('on', names);
    window.dispatchEvent(new KeyboardEvent(names ? 'keydown' : 'keyup', { key: 'Alt', bubbles: true }));
  });
  bar.append(namesButton, button('⋯', 'More', () => more.classList.toggle('open')), button('⛶', 'Full screen', fullScreenLandscape));
  // The fight button, the skill keys and the potion keys.
  const fight = document.createElement('div');
  fight.id = 'touchFight';
  for (const name of SKILL_KEYS) fight.append(button(name, `Skill ${name}`, () => key(name)));
  for (const name of POTION_KEYS) fight.append(button(name.toUpperCase(), `Potion ${name.toUpperCase()}`, () => key(name), 'potion'));
  fight.append(button('⚔', 'Fight the nearest monster', () => { actions.fight(); }, 'fight'));
  // A phone held upright: a hint to turn it (the game is laid out for landscape).
  const rotate = document.createElement('div');
  rotate.id = 'touchRotate';
  const phone = document.createElement('div');
  phone.className = 'phone';
  phone.textContent = '📱';
  const text = document.createElement('div');
  text.textContent = 'Turn your phone sideways to play.';
  let dismissed = false;
  rotate.append(phone, text, button('Play upright anyway', 'Play upright', () => { dismissed = true; rotate.classList.remove('shown'); }));
  document.body.append(bar, more, fight, rotate);
  const portrait = () => {
    const upright = window.matchMedia?.('(pointer: coarse) and (orientation: portrait) and (max-width: 600px)').matches ?? false;
    rotate.classList.toggle('shown', upright && !dismissed && bar.classList.contains('shown'));
  };
  window.addEventListener('resize', portrait);
  // Shown on touch screens, and from the first touch on any screen.
  const show = () => { bar.classList.add('shown'); fight.classList.add('shown'); portrait(); };
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

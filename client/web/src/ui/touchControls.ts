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
  { label: 'L', title: 'Lucky number', key: 'l' },
  { label: 'N', title: 'World bosses', key: 'n' },
  { label: 'Y', title: 'New-player guide', key: 'y' },
  { label: 'K', title: 'Community', key: 'k' },
  { label: '🏆', title: 'Top races', key: '', event: 'hnmo-top-races-open' },
  { label: '🎁', title: 'Gift code', key: '', event: 'hnmo-gift-codes-open' },
  { label: '💎', title: 'Top-up milestones', key: 'u', event: 'hnmo-topup-milestones-open' },
  { label: '💍', title: 'Marriage', key: '', event: 'hnmo-marriage-open' },
  { label: '🏪', title: 'Item market', key: '', event: 'hnmo-market-open' },
  { label: '🏟', title: 'Arena', key: '', event: 'hnmo-arena-open' },
  { label: '📜', title: 'Escrow contracts', key: '', event: 'hnmo-escrow-open' },
  { label: '💎', title: 'Jewel bank', key: '', event: 'hnmo-jewel-bank-open' },
  { label: 'VIP', title: 'VIP', key: '', event: 'hnmo-vip-open' },
  { label: '📅', title: 'Season plan', key: '', event: 'hnmo-season-plan-open' },
  { label: '♻️', title: 'Combining leftovers & exchange', key: '', event: 'hnmo-leftovers-open' },
  { label: '🐴', title: 'Mount chain collection', key: '', event: 'hnmo-mount-chain-open' },
  { label: '📊', title: 'Player rankings', key: '', event: 'hnmo-ranking-open' },
  { label: '➕', title: 'Add stats (bulk)', key: '', event: 'hnmo-add-stats-open' },
  { label: '📷', title: 'Screenshot / share', key: '', event: 'hnmo-share-open' },
  { label: '📬', title: 'Mailbox / Gift Box', key: '', event: 'hnmo-giftbox-open' },
];
const SKILL_KEYS = ['1', '2', '3', '4', '5'];
/** The second skill page (the modern HUD's 1/2 button): keys 6-0. */
const SKILL_KEYS_PAGE2 = ['6', '7', '8', '9', '0'];
const INSTALL_HINT_KEY = 'hnmo.installHintDismissed';
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
#touchRotate button { all: unset; padding: 10px 18px; border: 1px solid rgba(255,220,150,.6); border-radius: 8px; font-size: 15px; background: rgba(110,80,30,.6); }
#touchRotate .sub { font-size: 12px; opacity: .7; font-weight: 500; }
#touchInstall { position: fixed; left: 50%; bottom: 10px; transform: translateX(-50%); z-index: 90; display: flex; gap: 8px; align-items: center;
  padding: 6px 8px 6px 12px; border-radius: 999px; background: rgba(12,14,22,.94); border: 1px solid rgba(255,220,150,.5); color: #f3e2bd; font: 600 12px system-ui, sans-serif; }
#touchInstall[hidden] { display: none; }
#touchInstall button { all: unset; cursor: pointer; padding: 4px 10px; border-radius: 999px; border: 1px solid rgba(255,220,150,.45); }
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

/** "Add to home screen": a small dismissible hint where the browser offers the install, remembered once closed. */
function installHint(vi: boolean): void {
  let dismissed = false;
  try { dismissed = localStorage.getItem(INSTALL_HINT_KEY) === '1'; } catch { /* not remembered */ }
  if (dismissed || window.matchMedia?.('(display-mode: fullscreen), (display-mode: standalone)').matches) return;
  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault();
    const prompt = event as Event & { prompt?: () => Promise<void> };
    const hint = document.createElement('div');
    hint.id = 'touchInstall';
    const label = document.createElement('span');
    label.textContent = vi ? '📲 Cài HNMO lên màn hình chính' : '📲 Put HNMO on your home screen';
    const close = () => { hint.remove(); try { localStorage.setItem(INSTALL_HINT_KEY, '1'); } catch { /* not remembered */ } };
    hint.append(label,
      button(vi ? 'Cài' : 'Install', 'Install', () => { void prompt.prompt?.().catch(() => undefined); close(); }),
      button('✕', vi ? 'Đóng' : 'Close', close));
    document.body.append(hint);
  }, { once: true });
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
  // The skill buttons follow the skill page (keys 1-5, or 6-0 from the modern HUD's 1/2 button).
  let skillKeys = SKILL_KEYS;
  const skillButtons = SKILL_KEYS.map((name, slot) => button(name, `Skill ${name}`, () => key(skillKeys[slot])));
  fight.append(...skillButtons);
  window.addEventListener('hnmo-skill-page', (event) => {
    skillKeys = (event as CustomEvent<{ page: number }>).detail?.page === 1 ? SKILL_KEYS_PAGE2 : SKILL_KEYS;
    skillButtons.forEach((b, slot) => { b.textContent = skillKeys[slot]; b.title = `Skill ${skillKeys[slot]}`; });
  });
  for (const name of POTION_KEYS) fight.append(button(name.toUpperCase(), `Potion ${name.toUpperCase()}`, () => key(name), 'potion'));
  fight.append(button('⚔', 'Fight the nearest monster', () => { actions.fight(); }, 'fight'));
  // A phone held upright: a hint to turn it (the game is laid out for landscape).
  const rotate = document.createElement('div');
  rotate.id = 'touchRotate';
  const phone = document.createElement('div');
  phone.className = 'phone';
  phone.textContent = '📱';
  const text = document.createElement('div');
  const vi = document.documentElement.lang === 'vi' || (() => { try { return localStorage.getItem('hnmo.language') === 'vi'; } catch { return false; } })();
  text.textContent = vi ? 'Xoay ngang điện thoại để chơi.' : 'Turn your phone sideways to play.';
  const sub = document.createElement('div');
  sub.className = 'sub';
  sub.textContent = vi ? 'Toàn màn hình sẽ khoá chiều ngang nếu trình duyệt cho phép.' : 'Full screen locks it sideways where the browser allows.';
  let dismissed = false;
  // One button: full screen (and landscape where allowed), and the hint goes.
  rotate.append(phone, text, sub, button(vi ? 'Vào toàn màn hình' : 'Enter full screen', 'Full screen', () => { dismissed = true; rotate.classList.remove('shown'); fullScreenLandscape(); }));
  document.body.append(bar, more, fight, rotate);
  installHint(vi);
  const portrait = () => {
    const upright = window.matchMedia?.('(pointer: coarse) and (orientation: portrait) and (max-width: 600px)').matches ?? false;
    rotate.classList.toggle('shown', upright && !dismissed && bar.classList.contains('shown'));
  };
  window.addEventListener('resize', portrait);
  // Shown on touch screens, and from the first touch on any screen.
  const show = () => {
    const first = !bar.classList.contains('shown');
    bar.classList.add('shown');
    fight.classList.add('shown');
    portrait();
    if (first) (actions as TouchActions & { onShow?: () => void }).onShow?.();
  };
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

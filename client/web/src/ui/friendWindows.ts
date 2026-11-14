// The messenger's windows, as the desktop draws them in the window_shell frame (UI/Social):
//  - the friend window (CUIFriendWindow / FriendShell, friend_shell.rml), F: the friends list with
//    each friend's server (add, delete, write), the letter box (write, read, reply, delete the checked
//    letters, sort by any column) and the list of open letter windows; "Refuse Chat".
//  - a letter (CUILetterReadWindow, letter_read.rml): the sender, the date and the text; reply,
//    delete, the previous and next letter.
//  - writing a letter (CUILetterWriteWindow, letter_write.rml): the receiver, the title and the text;
//    send (it costs zen), close (asks when something was written).
// The windows can be dragged by their title; a press brings one to the front.

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { askInGame, noticeInGame, promptInGame } from './socialDialogs.js';
import { keepTypingInside } from './guildWindows.js';
import { LAST_ONLINE_SERVER, LETTER_COST, UNAVAILABLE_SERVER, type Letter, type LetterBody, type Messenger } from '../game/messenger.js';

/** The bottom of the area windows may use (above the HUD's main frame, 10.625% of the height). */
const workspaceBottom = () => window.innerHeight * (1 - 0.10625);
let topLayer = 70;
/** The windows grow with the screen like the game's other windows (dp, "native-text"). */
const shellScale = () => Math.max(0.8, window.innerHeight / REFERENCE_HEIGHT / 1.25);
// The themes size the text in rem: in RmlUi that is the document's text size, the original's 12 px.
const SHELL_CSS = `
#panel { font-size: calc(12px * var(--hnmo-text-scale, 1)); transform-origin: left top; }
#panel button, #panel input, #panel textarea { font-size: inherit; }
#panel button { border: 0; padding-top: 0; padding-bottom: 0; background-color: transparent; }
`;

interface ShellModel extends RmlModel {
  title: string; has_title: boolean; positioned: boolean; root_x: number; root_y: number; maximized: boolean;
}

/** A window in the shared window_shell frame, placed by root_x / root_y and dragged by its title. */
abstract class ShellWindow<M extends ShellModel> {
  protected doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private hidden = false;

  protected constructor(protected readonly documentName: string, protected readonly model: M, protected readonly click?: () => void) {
    prepareDocument(documentName).catch((error: unknown) => console.warn('Interface', error));
  }

  get isOpen(): boolean { return this.doc !== null && !this.hidden; }
  get exists(): boolean { return this.doc !== null; }

  async open(): Promise<void> {
    if (this.doc) { this.show(); return; }
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  /** Where the window stands the first time it is shown. */
  protected abstract initialPlace(width: number, height: number): { x: number; y: number };
  protected refresh(): void { this.doc?.update(); }
  protected onClosed(): void { /* the owner forgets it */ }

  private async load(): Promise<void> {
    const doc = await openDocument(this.documentName, this.model);
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    Object.assign(doc.host.style, { position: 'fixed', left: '0', top: '0', width: '100%', height: '100%', pointerEvents: 'none', zIndex: String(++topLayer) });
    const style = document.createElement('style');
    style.textContent = SHELL_CSS;
    doc.host.shadowRoot?.append(style);
    const panel = doc.$('#panel');
    if (panel) panel.style.transform = `scale(${shellScale().toFixed(3)})`;
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => { event.stopPropagation(); this.toFront(); });
      panel.addEventListener('contextmenu', (event) => event.preventDefault());
      panel.addEventListener('wheel', (event) => event.stopPropagation());
      const grip = doc.$('#drag_strip') ?? doc.$('#window_shell_header');
      if (grip) this.makeDraggable(grip);
    }
    keepTypingInside(doc.host);
    this.model.positioned = true;
    this.refresh();
    const rect = panel?.getBoundingClientRect();
    const at = this.initialPlace(rect?.width ?? 300, rect?.height ?? 220);
    this.model.root_x = Math.max(0, at.x);
    this.model.root_y = Math.max(0, at.y);
    this.refresh();
  }

  private makeDraggable(grip: HTMLElement): void {
    grip.style.pointerEvents = 'auto';
    grip.style.cursor = 'move';
    grip.addEventListener('pointerdown', (event) => {
      if (event.button !== 0) return;
      const start = { x: event.clientX, y: event.clientY, left: this.model.root_x, top: this.model.root_y };
      grip.setPointerCapture(event.pointerId);
      const move = (e: PointerEvent) => {
        this.model.root_x = Math.max(0, Math.min(window.innerWidth - 40, start.left + e.clientX - start.x));
        this.model.root_y = Math.max(0, Math.min(workspaceBottom() - 20, start.top + e.clientY - start.y));
        this.refresh();
      };
      const up = () => { grip.removeEventListener('pointermove', move); grip.removeEventListener('pointerup', up); };
      grip.addEventListener('pointermove', move);
      grip.addEventListener('pointerup', up);
    });
  }

  toFront(): void { if (this.doc) this.doc.host.style.zIndex = String(++topLayer); }

  /** UISTATE_HIDE: minimized, still listed in the friend window's window list. */
  hide(): void {
    if (!this.doc) return;
    this.hidden = true;
    this.doc.host.style.display = 'none';
  }

  show(): void {
    if (!this.doc) return;
    this.hidden = false;
    this.doc.host.style.display = '';
    this.toFront();
  }

  close(): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
    this.hidden = false;
    this.onClosed();
  }
}

// ---------- the friend window ----------

interface FriendRow { name: string; server_label: string }
interface LetterRow { id: number; sender: string; date: string; title: string; read: boolean; checked: boolean }
interface WindowRow { id: number; title: string }

interface FriendShellModel extends ShellModel {
  friends: FriendRow[]; letters: LetterRow[]; windows: WindowRow[];
  selected_friend: string; selected_letter: number; selected_window: number; active_tab: number;
  reject_chat: boolean; check_all: boolean; friend_sort: number; letter_sort: number;
  shell_action: (action: string, value?: string | number) => void;
  [label: string]: unknown;
}

// FriendShell::BindLabels.
const LABELS: Record<string, string> = {
  friends_label: 'Friends List', mail_label: 'Letter Box', windows_label: 'Window List', reject_label: 'Refuse Chat',
  name_label: " Friend's Name", server_label: 'Server', add_label: 'Add Friend', delete_friend_label: 'Delete Friend',
  chat_label: 'Chat', write_label: 'Write', sender_label: ' Sender', date_label: ' Date Rcvd.', title_label: ' Title',
  read_label: 'Read', reply_label: 'Reply', delete_label: 'Delete', window_label: 'Window Title', hide_all_label: 'Hide All', close_label: 'Close',
};

function serverLabel(server: number): string {
  if (server === UNAVAILABLE_SERVER) return 'Cannot Use';
  if (server >= LAST_ONLINE_SERVER) return 'Offline';
  return ` ${String(server + 1).padStart(2, ' ')} Server`;
}

export interface FriendWindowOptions {
  messenger: Messenger;
  heroName: string;
  click?: () => void;
  /** "Chat" with a friend: the desktop opens a chat room on the chat server; the web whispers. */
  chatWith?: (name: string) => void;
}

export class FriendWindow extends ShellWindow<FriendShellModel> {
  private readonly checked = new Set<number>();
  private readonly letterWindows = new Map<number, LetterReadWindow>();
  private readonly writeWindows = new Map<number, LetterWriteWindow>();
  private nextWindowId = 1;
  private lastTab = 0;
  private rejectChat = false;

  constructor(private readonly options: FriendWindowOptions) {
    super('friend_shell', {
      ...LABELS, title: 'My Friend', has_title: true, positioned: false, root_x: 0, root_y: 0, maximized: false,
      friends: [], letters: [], windows: [], selected_friend: '', selected_letter: -1, selected_window: 0, active_tab: 0,
      reject_chat: false, check_all: false, friend_sort: 0, letter_sort: 2,
      shell_action: (action, value) => this.action(action, value),
    }, options.click);
    const { messenger } = options;
    messenger.onChange((event) => {
      switch (event.kind) {
        case 'friends': case 'letters': case 'newLetter': this.refresh(); break;
        case 'notice': void noticeInGame(event.text); break;
        case 'friendRequest': void this.friendRequest(event.name); break;
        case 'letterBody': void this.showLetter(event.body); break;
        case 'letterSent': this.writeWindows.get(event.windowId)?.close(); break;
        case 'letterFailed': this.writeWindows.get(event.windowId)?.sending(false); void noticeInGame(event.notice); break;
        default:
      }
    });
    window.addEventListener('keydown', (event) => {
      if (event.key !== 'Escape' || document.querySelector('.confirm-dialog')) return;
      if (this.isOpen) { this.hide(); this.options.click?.(); }
    });
  }

  get tab(): number { return this.model.active_tab; }
  get openLetters(): LetterReadWindow[] { return [...this.letterWindows.values()]; }
  get openWriters(): LetterWriteWindow[] { return [...this.writeWindows.values()]; }

  /** F (CHotKey): opens it, or hides it again. On opening with new mail, the letter box shows. */
  async toggle(): Promise<void> {
    this.options.click?.();
    if (this.isOpen) { this.lastTab = this.model.active_tab; this.hide(); return; }
    const { messenger } = this.options;
    this.model.active_tab = messenger.newMailAlert ? 1 : this.lastTab;
    messenger.newMailAlert = false;
    if (!messenger.serverEnabled) messenger.requestList();
    await this.open();
    this.refresh();
  }

  protected initialPlace(width: number, height: number): { x: number; y: number } {
    // PlaceAtRest(): the bottom-right corner of the usable area, on top of the bottom HUD.
    return { x: window.innerWidth - width, y: workspaceBottom() - height };
  }

  protected override refresh(): void {
    const { messenger } = this.options;
    const m = this.model;
    m.friends = [...messenger.friends].reverse().map((friend) => ({ name: friend.name, server_label: serverLabel(friend.server) }));
    if (!m.friends.some((row) => row.name === m.selected_friend)) m.selected_friend = '';
    for (const id of [...this.checked]) if (!messenger.letters.some((l) => l.id === id)) this.checked.delete(id);
    m.letters = [...messenger.letters].reverse().map((letter) => ({
      id: letter.id, sender: letter.sender, date: letter.date, title: letter.subject, read: letter.read, checked: this.checked.has(letter.id),
    }));
    if (!m.letters.some((row) => row.id === m.selected_letter)) m.selected_letter = -1;
    m.check_all = m.letters.length > 0 && m.letters.every((row) => row.checked);
    m.friend_sort = messenger.friendSort;
    m.letter_sort = messenger.letterSort;
    m.reject_chat = this.rejectChat;
    m.windows = [...this.letterWindows.values(), ...this.writeWindows.values()].map((w) => ({ id: w.id, title: w.windowTitle }));
    super.refresh();
  }

  /** FriendShell::ActionRequested. */
  private action(action: string, value?: string | number): void {
    const { messenger } = this.options;
    const m = this.model;
    const id = Number(value);
    switch (action) {
      case 'friend_select': case 'friend_open':
        if (m.friends.some((row) => row.name === value)) { m.selected_friend = String(value); if (action === 'friend_open') this.chat(); }
        break;
      case 'letter_select': case 'letter_open':
        if (m.letters.some((row) => row.id === id)) { m.selected_letter = id; if (action === 'letter_open') this.openLetter(id); }
        break;
      case 'window_select': case 'window_open':
        if (m.windows.some((row) => row.id === id)) { m.selected_window = id; if (action === 'window_open') this.activateWindow(id); }
        break;
      case 'tab': if (id >= 0 && id <= 2 && id !== m.active_tab) { this.options.click?.(); m.active_tab = id; } break;
      case 'friend_sort': if (id === 0 || id === 1) messenger.sortFriends(id); break;
      case 'letter_sort': if (id >= 0 && id <= 3) { messenger.sortLetters(id); this.checked.clear(); } break;
      case 'add': void this.addFriend(); break;
      case 'delete_friend': void this.deleteFriend(); break;
      case 'chat': this.chat(); break;
      case 'write_friend': this.write({ to: m.selected_friend }); break;
      case 'write': this.write({}); break;
      case 'reply': { const letter = messenger.letters.find((l) => l.id === m.selected_letter); if (letter) this.reply(letter); break; }
      case 'read': if (m.selected_letter >= 0) this.openLetter(m.selected_letter); break;
      case 'delete_mail': void this.deleteLetters(); break;
      case 'check': if (this.checked.has(id)) this.checked.delete(id); else this.checked.add(id); break;
      case 'check_all': {
        const all = !m.check_all;
        this.checked.clear();
        if (all) for (const row of m.letters) this.checked.add(row.id);
        break;
      }
      case 'hide_all': for (const w of [...this.letterWindows.values(), ...this.writeWindows.values()]) w.hide(); break;
      case 'reject': void this.toggleReject(); break;
      case 'close': case 'minimize': this.lastTab = m.active_tab; this.hide(); break;
      case 'maximize': break;
      default:
    }
    this.refresh();
  }

  private chat(): void {
    const friend = this.options.messenger.friends.find((f) => f.name === this.model.selected_friend);
    if (!friend || friend.server > LAST_ONLINE_SERVER) return;
    this.options.chatWith?.(friend.name);
  }

  private async addFriend(): Promise<void> {
    const name = await promptInGame("Enter the ID of the friend you'd like to add", { maxLength: 10 });
    if (name) this.options.messenger.addFriend(name);
  }

  private async deleteFriend(): Promise<void> {
    const name = this.model.selected_friend;
    if (!name) return;
    if (await askInGame(`Do you really wish to delete this friend? ${name}`)) this.options.messenger.deleteFriend(name);
  }

  // ShowFriendRequest: the window opens and asks.
  private async friendRequest(name: string): Promise<void> {
    if (!this.isOpen) await this.toggle();
    const accepted = await askInGame(`${name} has requested to list you as a friend.`);
    this.options.messenger.answerFriend(name, accepted);
  }

  private async deleteLetters(): Promise<void> {
    const ids = [...this.checked];
    if (!ids.length) { void noticeInGame("Select the letter you'd like to delete"); return; }
    if (!(await askInGame('Are you sure you want to delete the letter?'))) return;
    for (const id of ids) if (this.options.messenger.letters.some((l) => l.id === id)) this.options.messenger.deleteLetter(id);
  }

  private async toggleReject(): Promise<void> {
    if (this.rejectChat) { this.options.messenger.setOnlineState(true); this.rejectChat = false; this.refresh(); return; }
    if (!(await askInGame('If you refuse chat, all chat windows will close!'))) return;
    this.options.messenger.setOnlineState(false);
    this.rejectChat = true;
    this.refresh();
  }

  private activateWindow(id: number): void {
    const window = this.letterWindows.get(id) ?? this.writeWindows.get(id);
    if (!window) return;
    if (!window.isOpen) window.show(); else window.toFront();
  }

  /** OpenLetter: an open letter comes to the front; a read one shows from the cache; else the server sends it. */
  openLetter(id: number): void {
    const { messenger } = this.options;
    if (!messenger.letters.some((l) => l.id === id)) return;
    const open = [...this.letterWindows.values()].find((w) => w.letterId === id);
    if (open) { open.show(); return; }
    const cached = messenger.bodies.get(id);
    if (cached) void this.showLetter(cached);
    else messenger.readLetter(id);
  }

  // UI::Social::ShowLetter.
  private async showLetter(body: LetterBody, at?: { x: number; y: number }): Promise<void> {
    const { messenger } = this.options;
    const letter = messenger.letters.find((l) => l.id === body.id);
    if (!letter) return;
    letter.read = true;
    const existing = [...this.letterWindows.values()].find((w) => w.letterId === body.id);
    if (existing) { existing.show(); return; }
    const id = this.nextWindowId++;
    const window = new LetterReadWindow(id, letter, body.text, {
      click: this.options.click,
      at,
      reply: () => this.reply(letter),
      remove: () => { messenger.deleteLetter(letter.id); window.close(); },
      step: (direction) => this.step(window, direction),
      closed: () => { this.letterWindows.delete(id); this.refresh(); },
    });
    this.letterWindows.set(id, window);
    await window.open();
    this.refresh();
  }

  // StepLetter: the previous or next letter takes this window's place.
  private step(window: LetterReadWindow, direction: -1 | 1): void {
    const { messenger } = this.options;
    const target = messenger.neighbour(window.letterId, direction);
    if (target < 0) { this.model.selected_letter = window.letterId; this.refresh(); return; }
    this.model.selected_letter = target;
    const at = window.position;
    window.close();
    const open = [...this.letterWindows.values()].find((w) => w.letterId === target);
    if (open) { open.show(); return; }
    const cached = messenger.bodies.get(target);
    if (cached) void this.showLetter(cached, at);
    else messenger.readLetter(target);
  }

  reply(letter: Letter): void {
    // "Re: subject", cut to 32 characters.
    this.write({ to: letter.sender, subject: [...`Re: ${letter.subject}`].slice(0, 32).join('') });
  }

  /** A new write window (WriteLetter): to a friend or a reply's sender. */
  write({ to = '', subject = '' }: { to?: string; subject?: string }): LetterWriteWindow {
    const id = this.nextWindowId++;
    const window = new LetterWriteWindow(id, {
      click: this.options.click,
      to, subject,
      send: (receiver, title, text) => this.options.messenger.sendLetter(id, receiver, title, text, (2 << 6) | 15, 0),
      closed: () => { this.writeWindows.delete(id); this.refresh(); },
    });
    this.writeWindows.set(id, window);
    void window.open().then(() => this.refresh());
    return window;
  }
}

// ---------- reading a letter ----------

interface LetterReadModel extends ShellModel {
  header: string; lines: { text: string }[];
  reply_label: string; delete_label: string; prev_label: string; next_label: string; close_label: string;
  letter_action: (action: string) => void;
}

let letterOffset = 0;

export class LetterReadWindow extends ShellWindow<LetterReadModel> {
  constructor(readonly id: number, private readonly letter: Letter, text: string, private readonly actions: {
    click?: () => void; at?: { x: number; y: number }; reply: () => void; remove: () => void; step: (direction: -1 | 1) => void; closed: () => void;
  }) {
    super('letter_read', {
      title: `Read letter: ${letter.subject}`, has_title: true, positioned: false, root_x: 0, root_y: 0, maximized: false,
      header: `Sender: ${letter.sender} (${letter.date} ${letter.time})`,
      // SetLetter(): the text line by line, as the desktop fed its list box.
      lines: text.split('\n').map((line) => ({ text: line })),
      reply_label: 'Reply', delete_label: 'Delete', prev_label: 'Previous', next_label: 'Next', close_label: 'Close',
      letter_action: (action) => this.action(action),
    }, actions.click);
  }

  get letterId(): number { return this.letter.id; }
  get windowTitle(): string { return this.model.title; }
  get position(): { x: number; y: number } { return { x: this.model.root_x, y: this.model.root_y }; }
  get text(): string { return this.model.lines.map((line) => line.text).join('\n'); }

  protected initialPlace(): { x: number; y: number } {
    if (this.actions.at) return this.actions.at;
    letterOffset = (letterOffset + 20) % 120;
    return { x: 100 + letterOffset, y: 100 + letterOffset };
  }

  private action(action: string): void {
    this.click?.();
    switch (action) {
      case 'reply': this.actions.reply(); break;
      case 'delete': void askInGame('Are you sure you want to delete the letter?').then((yes) => { if (yes) this.actions.remove(); }); break;
      case 'prev': this.actions.step(-1); break;
      case 'next': this.actions.step(1); break;
      case 'minimize': this.hide(); break;
      case 'close': this.close(); break;
      default:
    }
  }

  protected override onClosed(): void { this.actions.closed(); }
}

// ---------- writing a letter ----------

interface LetterWriteModel extends ShellModel {
  mailto: string; subject: string; body: string; sending: boolean;
  receiver_label: string; subject_label: string; send_label: string; close_label: string; prev_pose_label: string; next_pose_label: string;
  letter_action: (action: string) => void;
}

const MAX_LETTER_TEXT = 1000;

export class LetterWriteWindow extends ShellWindow<LetterWriteModel> {
  constructor(readonly id: number, private readonly actions: {
    click?: () => void; to: string; subject: string; send: (receiver: string, subject: string, text: string) => void; closed: () => void;
  }) {
    super('letter_write', {
      title: `Write letter (Cost: ${LETTER_COST} zen)`, has_title: true, positioned: false, root_x: 0, root_y: 0, maximized: false,
      mailto: actions.to, subject: actions.subject, body: '', sending: false,
      receiver_label: 'Receiver:', subject_label: ' Title', send_label: 'Send', close_label: 'Close', prev_pose_label: 'Prev. Action', next_pose_label: 'Next Action',
      letter_action: (action) => this.action(action),
    }, actions.click);
  }

  get windowTitle(): string { return this.model.title; }

  protected initialPlace(): { x: number; y: number } {
    letterOffset = (letterOffset + 20) % 120;
    return { x: 100 + letterOffset, y: 100 + letterOffset };
  }

  /** The fields as typed (the bindings write them into the model). */
  private field(name: 'mailto' | 'subject' | 'body'): string {
    const id = { mailto: '#mailto_field', subject: '#subject_field', body: '#body_field' }[name];
    const element = this.doc?.$<HTMLInputElement | HTMLTextAreaElement>(id);
    return (element?.value ?? this.model[name]).trim();
  }

  sending(state: boolean): void { this.model.sending = state; this.refresh(); }

  /** Fills the fields (the tests and SetMailtoText / SetMainTitleText). */
  fill(values: { to?: string; subject?: string; body?: string }): void {
    if (values.to !== undefined) this.model.mailto = values.to;
    if (values.subject !== undefined) this.model.subject = values.subject;
    if (values.body !== undefined) this.model.body = values.body;
    for (const [key, id] of [['mailto', '#mailto_field'], ['subject', '#subject_field'], ['body', '#body_field']] as const) {
      const element = this.doc?.$<HTMLInputElement | HTMLTextAreaElement>(id);
      if (element) element.value = this.model[key];
    }
  }

  private action(action: string): void {
    this.click?.();
    if (action === 'send') this.send();
    else if (action === 'close') this.requestClose();
    else if (action === 'minimize') this.hide();
    // Prev. / Next Action turn the portrait's pose; the web shows no portrait yet.
  }

  // CUILetterWriteWindow::Send: each field in turn, the caret back in the first empty one.
  private send(): void {
    if (this.model.sending) return;
    const focus = (id: string) => this.doc?.$<HTMLElement>(id)?.focus();
    const mailto = this.field('mailto');
    if (!mailto) { void noticeInGame('Enter the name of the receiver.').then(() => focus('#mailto_field')); return; }
    const subject = this.field('subject');
    if (!subject) { void noticeInGame('Enter the title.').then(() => focus('#subject_field')); return; }
    const body = this.field('body');
    if (!body) { void noticeInGame('Enter your message.').then(() => focus('#body_field')); return; }
    this.sending(true);
    this.actions.send(mailto, subject, [...body.replace(/\r\n?/g, '\n')].slice(0, MAX_LETTER_TEXT).join(''));
  }

  // CloseCheck: a draft with nothing in its title or body closes without asking.
  private requestClose(): void {
    if (!this.field('subject') && !this.field('body')) { this.close(); return; }
    void askInGame('Do you wish to quit writing this letter?').then((yes) => { if (yes) this.close(); });
  }

  protected override onClosed(): void { this.actions.closed(); }
}

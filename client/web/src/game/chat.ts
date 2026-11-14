// The chat, as the desktop client draws and runs it:
//  - the chat log (CChatLogWindow, chat_log.rml): the said lines ("name : text"), each kind in its
//    colour, long lines broken in two, at most 200 lines kept; F2 shows only whispers; the frame (F5
//    or its button) adds the backdrop, the scroll bar and the resize handle, F4 / the size button
//    shows 3 more lines (3 to 15), the transparency button steps the backdrop, Page Up / Down
//    scroll; a right click on a line with a name puts the name in the whisper box;
//  - the system log (CSystemLogWindow, system_log.rml): the game's own messages at the top left,
//    the last 6;
//  - the input box (CChatInputBox, chat_input.rml): opened with Enter and closed after sending or
//    with Escape; the channel buttons (normal, party ~, guild @, Gens $), whisper block, system
//    messages on / off, chat log on / off, the frame; the whisper box (a name there whispers the
//    line; F3 turns it off), the line and whisper histories (Up / Down), one line a second.

import { openDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from '../ui/rml.js';

/** How a message shows: said (in public, to the party, guild, alliance or Gens), whispered, from the game, or from a game master. */
export type ChatKind = 'chat' | 'whisper' | 'system' | 'error' | 'party' | 'guild' | 'union' | 'gens' | 'gm';

export type ChatSender = (text: string) => void;

interface Message { id: string; text: string; kind: ChatKind }
interface LineEntry { text: string; kind: string; has_id: boolean }

const MAX_LINES = 200; // MAX_NUMBER_OF_LINES
const SYSTEM_LINES = 6; // CSystemLogWindow's MAX_NUMBER_OF_LINES
const CLIENT_WIDTH = 281 - 7 * 2 - 4 * 2; // WND_WIDTH - 2 scroll bars - 2 edges
const ROW = 15; // SCROLL_MIDDLE_PART_HEIGHT
const CHAT_MAX = 60; // MAX_CHAT_SIZE
const NAME_MAX = 10; // MAX_USERNAME_SIZE
const COOLDOWN_MS = 1000;
const CHANNEL_PREFIX = ['', '~', '@', '$'];
const HINTS = [
  'Regular chat', 'Party chat', 'Guild chat', 'Gens chat on/off', 'Whisper block on/off', 'System message pop-up',
  'Chat display on/off (F2)', 'Chat window background on/off (F5)', 'Size adjustment (F4)', 'Transparency adjustment',
];

interface InputModel extends RmlModel {
  chat_text: string; whisper_id: string; input_msg_type: number;
  block_whisper: boolean; show_system: boolean; show_chat_log: boolean; show_frame: boolean; whisper_send: boolean;
}
interface LogModel extends RmlModel {
  lines: LineEntry[]; panel_height: number; client_height: number; back_alpha: number; show_frame: boolean; pointed_index: number;
}
interface SystemModel extends RmlModel { lines: LineEntry[]; back_alpha: number }

export interface ChatOptions {
  /** Where the windows go. */
  parent?: HTMLElement;
  /** The click sound. */
  click?: () => void;
  /** Whether Enter may open the input box now (the desktop keeps it shut in Chaos Castle while its time window shows). */
  canOpen?: () => boolean;
}

export class Chat {
  send: ChatSender | null = null;
  /** Whispers to this player are sent by the input box (the whisper box's name, when whispering is on). */
  whisper: ((name: string, text: string) => void) | null = null;

  private readonly options: ChatOptions;
  private all: Message[] = [];
  private whispers: Message[] = [];
  private system: Message[] = [];
  private whispersOnly = false; // F2
  private showingLines = 6;
  private backAlpha = 0.6;
  private chatHistory: string[] = [];
  private whisperHistory: string[] = [];
  private chatHistoryAt = 0;
  private whisperHistoryAt = 0;
  private lastSent = 0;
  private input: RmlDocument | null = null;
  private log: RmlDocument | null = null;
  private systemLog: RmlDocument | null = null;
  private enabled = false;
  private building = false;
  private open = false;
  private measure: HTMLElement | null = null;
  private readonly inputModel: InputModel;
  private readonly logModel: LogModel;
  private readonly systemModel: SystemModel;

  constructor(options: ChatOptions = {}) {
    this.options = options;
    const click = () => this.options.click?.();
    this.inputModel = {
      chat_text: '', whisper_id: '', input_msg_type: 0,
      block_whisper: false, show_system: true, show_chat_log: true, show_frame: false, whisper_send: true,
      ...Object.fromEntries(HINTS.map((hint, index) => [`hint_${index}`, hint])),
      chat_set_type: (type: number) => { this.inputModel.input_msg_type = type; click(); },
      chat_toggle_whisper_block: () => { this.inputModel.block_whisper = !this.inputModel.block_whisper; click(); },
      chat_toggle_system: () => { this.inputModel.show_system = !this.inputModel.show_system; this.sync(); click(); },
      chat_toggle_chatlog: () => { this.inputModel.show_chat_log = !this.inputModel.show_chat_log; this.sync(); click(); },
      chat_toggle_frame: () => { this.setFrame(!this.logModel.show_frame); click(); },
      chat_size_step: () => { this.sizeStep(); click(); },
      chat_alpha_step: () => { this.backAlpha += 0.2; if (this.backAlpha > 0.9) this.backAlpha = 0.2; this.sync(); click(); },
    };
    this.logModel = { lines: [], panel_height: 0, client_height: 0, back_alpha: this.backAlpha, show_frame: false, pointed_index: -1 };
    this.systemModel = { lines: [], back_alpha: 0.6 };
    window.addEventListener('keydown', (event) => this.onKey(event), true);
    window.addEventListener('resize', () => this.place());
    // The pointed line (only a line with a name) and a right click on it (UpdatePointedLine).
    window.addEventListener('pointermove', (event) => this.point(event.clientX, event.clientY), { passive: true });
    window.addEventListener('mousedown', (event) => {
      if (event.button !== 2 || this.logModel.pointed_index < 0) return;
      const message = this.shown()[this.logModel.pointed_index];
      if (message?.id) this.setWhisperTarget(message.id);
    }, true);
  }

  /** The windows show and the line is sent to `send` (CheckCommand and the public chat). */
  enable(send: ChatSender): void {
    this.send = send;
    this.enabled = true;
    void this.build();
  }

  /** Adds a line: a said line has the speaker's name, the game's own messages go to the system log. */
  add(text: string, kind: ChatKind = 'chat', sender = ''): void {
    if (!text && !sender) return;
    if ((kind === 'system' || kind === 'error') && !sender) {
      // CSystemLogWindow::AddText
      this.system.push({ id: '', text, kind });
      while (this.system.length > SYSTEM_LINES) this.system.shift();
    } else {
      // CChatLogWindow::AddText / ProcessAddText: a long line becomes two.
      const [first, second] = this.separate(sender, text);
      const parts: Message[] = [];
      if (first) parts.push({ id: sender, text: first, kind });
      if (second) parts.push({ id: '', text: second, kind });
      for (const part of parts) {
        this.all.push(part);
        if (kind === 'whisper') this.whispers.push(part);
      }
      while (this.all.length > MAX_LINES) this.all.shift();
      while (this.whispers.length > MAX_LINES) this.whispers.shift();
    }
    this.sync(true);
  }

  /** Whether incoming whispers are refused (the whisper block button). */
  get blockWhisper(): boolean { return this.inputModel.block_whisper; }
  /** The name in the whisper box. */
  get whisperTarget(): string { return this.inputModel.whisper_send ? this.inputModel.whisper_id.trim() : ''; }
  /** Whether the input box is open. */
  get isOpen(): boolean { return this.open; }
  /** Every kept line of both logs with its kind, the chat log's first (for tests and debugging). */
  get entries(): { kind: ChatKind; text: string }[] {
    return [...this.all, ...this.system].map((m) => ({ kind: m.kind, text: m.id ? `${m.id} : ${m.text}` : m.text }));
  }

  /** The lines of the chat log and of the system log as they show now (for tests). */
  get shownLines(): { chat: string[]; system: string[] } {
    return { chat: this.logModel.lines.map((line) => line.text), system: this.systemModel.lines.map((line) => line.text) };
  }

  /** Puts a name in the whisper box (a right click on a player or a line, a friend's chat button) and opens the input. */
  setWhisperTarget(name: string): void {
    this.inputModel.whisper_id = name.slice(0, NAME_MAX);
    const field = this.input?.$<HTMLInputElement>('#whisper_field');
    if (field) field.value = this.inputModel.whisper_id;
    this.input?.update();
  }

  /** Opens the input box (Enter; the touch bar's chat button). */
  openInput(): void {
    if (!this.enabled || this.open || !this.input) return;
    if (this.options.canOpen && !this.options.canOpen()) return;
    this.open = true;
    this.inputModel.chat_text = '';
    const field = this.input.$<HTMLInputElement>('#chat_field');
    if (field) field.value = '';
    this.sync();
    field?.focus(); // at once, so a phone's keyboard opens from the tap
  }

  /** Closes the input box (ClosingProcess: the frame goes too). */
  close(): void {
    if (!this.open) return;
    this.open = false;
    this.logModel.show_frame = false;
    for (const id of ['#chat_field', '#whisper_field']) this.input?.$<HTMLInputElement>(id)?.blur();
    this.sync();
  }

  // ---------- windows ----------

  private async build(): Promise<void> {
    if (this.building) return;
    this.building = true;
    const parent = this.options.parent ?? document.body;
    [this.systemLog, this.log, this.input] = await Promise.all([
      openDocument('system_log', this.systemModel, { parent }),
      openDocument('chat_log', this.logModel, { parent }),
      openDocument('chat_input', this.inputModel, { parent }),
    ]);
    for (const doc of [this.systemLog, this.log, this.input]) {
      doc.host.style.zIndex = '24';
      doc.host.setAttribute('data-no-i18n', '');
    }
    const lines = this.log.$('#lines');
    if (lines) { lines.setAttribute('data-no-i18n', ''); lines.style.overflowY = 'auto'; }
    this.systemLog.$('#panel')?.setAttribute('data-no-i18n', '');
    // The buttons' hints (data-hint) as the browser's tooltips.
    for (const button of this.input.root.querySelectorAll<HTMLElement>('.cib-btn')) {
      const hint = button.getAttribute('data-hint');
      if (hint) button.title = hint;
    }
    // The fields are the game's flat boxes, not the browser's (no padding, frame or focus ring).
    const fieldStyle = document.createElement('style');
    fieldStyle.textContent = 'input.text-field { padding: 0 2px; margin: 0; border: 0; outline: none; box-sizing: border-box; font: inherit; } input.text-field:disabled { opacity: .45; }';
    this.input.host.shadowRoot?.append(fieldStyle);
    const chatField = this.input.$<HTMLInputElement>('#chat_field');
    const whisperField = this.input.$<HTMLInputElement>('#whisper_field');
    chatField?.setAttribute('maxlength', String(CHAT_MAX));
    whisperField?.setAttribute('maxlength', String(NAME_MAX));
    chatField?.setAttribute('aria-label', 'Chat message');
    whisperField?.setAttribute('aria-label', 'Whisper to');
    // Clicks on the box stay in it (no walking under it).
    this.input.$('#panel')?.addEventListener('pointerdown', (event) => event.stopPropagation());
    this.log.$('#resize_handle')?.addEventListener('pointerdown', (event) => this.resizeDrag(event));
    // The measuring line for long lines (SeparateText), laid out like a chat line.
    this.measure = document.createElement('span');
    Object.assign(this.measure.style, { position: 'absolute', visibility: 'hidden', whiteSpace: 'nowrap', left: '0', top: '0' });
    this.measure.className = 'chat-measure';
    lines?.append(this.measure);
    this.place();
    this.sync(true);
  }

  // The roots stand for the 640 x 480 screen, scaled to the window, like the HUD under them.
  private place(): void {
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    for (const doc of [this.systemLog, this.log, this.input]) {
      if (!doc) continue;
      Object.assign(doc.root.style, {
        width: `${window.innerWidth / scale}px`, height: `${REFERENCE_HEIGHT}px`,
        transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none',
      });
      doc.setScale(scale);
      // A chat line is the text's height, the next one a row (15) below (SyncNativeLineGeometry).
      doc.root.style.setProperty('--line-px', '1.25em');
      doc.root.style.setProperty('--row-px', `${ROW}px`);
    }
  }

  private shown(): Message[] {
    return this.whispersOnly ? this.whispers : this.all;
  }

  // Brings the windows up to date; `added` keeps the log at its end when it was there (or unframed).
  private sync(added = false): void {
    if (!this.log || !this.input || !this.systemLog) return;
    const lines = this.log.$('#lines');
    const follow = !this.logModel.show_frame || !lines || lines.scrollTop + lines.clientHeight >= lines.scrollHeight - ROW;
    const client = ROW * this.showingLines;
    this.logModel.client_height = client;
    this.logModel.panel_height = client + 3 * 2 + 2 * 2;
    this.logModel.back_alpha = this.backAlpha;
    this.logModel.lines = this.shown().map((m) => ({ text: m.id ? `${m.id} : ${m.text}` : m.text, kind: m.kind, has_id: !!m.id }));
    this.inputModel.show_frame = this.logModel.show_frame;
    this.systemModel.lines = this.system.map((m) => ({ text: m.text, kind: m.kind, has_id: false }));
    this.log.update();
    this.input.update();
    this.systemLog.update();
    this.log.host.hidden = !this.enabled || !this.inputModel.show_chat_log;
    this.systemLog.host.hidden = !this.enabled || !this.inputModel.show_system;
    this.input.host.hidden = !this.enabled || !this.open;
    const whisperField = this.input.$<HTMLInputElement>('#whisper_field');
    if (whisperField) whisperField.disabled = !this.inputModel.whisper_send;
    if (lines && (added || follow)) requestAnimationFrame(() => { if (follow) lines.scrollTop = lines.scrollHeight; });
  }

  private setFrame(on: boolean): void {
    this.logModel.show_frame = on;
    this.sync();
  }

  // SetSizeAuto: 3 more lines, from 15 back to 3.
  private sizeStep(): void {
    let lines = Math.floor((this.showingLines + 3) / 3) * 3;
    if (lines < 3) lines = 3;
    if (lines > 15) lines = 3;
    this.showingLines = lines;
    this.sync(true);
  }

  // The handle above the framed log: dragging changes the height in 3-line steps.
  private resizeDrag(start: PointerEvent): void {
    start.preventDefault();
    start.stopPropagation();
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    const from = this.showingLines;
    const move = (event: PointerEvent) => {
      const rows = Math.round((start.clientY - event.clientY) / scale / ROW / 3) * 3;
      const lines = Math.min(15, Math.max(3, from + rows));
      if (lines !== this.showingLines) { this.showingLines = lines; this.sync(true); }
    };
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  }

  // SeparateText: the first part as much as fits after "name : " (cut at a space), the rest on a second line.
  private separate(id: string, text: string): [string, string] {
    if (text.length < 20 || !this.measure) return [text, ''];
    const width = (value: string) => { this.measure!.textContent = value; return this.measure!.offsetWidth; };
    const room = CLIENT_WIDTH - (id ? width(`${id} : `) : 0);
    if (width(text) <= room) return [text, ''];
    const spaces = text.includes(' ');
    let cut = text.length;
    while (cut > 0) {
      cut = spaces ? text.lastIndexOf(' ', cut - 1) : cut - 1;
      if (cut <= 0) return ['', text];
      if (width(text.slice(0, cut)) <= room) break;
    }
    return [text.slice(0, cut), text.slice(cut)];
  }

  private point(x: number, y: number): void {
    let pointed = -1;
    const lines = this.log?.$('#lines');
    if (lines && !this.log!.host.hidden) {
      const well = lines.getBoundingClientRect();
      if (x >= well.left && x < well.right && y >= well.top && y < well.bottom) {
        const rows = [...lines.querySelectorAll<HTMLElement>('.chat-line')].filter((row) => row !== this.measure);
        const scale = window.innerHeight / REFERENCE_HEIGHT;
        rows.some((row, index) => {
          const box = row.getBoundingClientRect();
          if (y < box.top || y >= box.top + ROW * scale) return false;
          if (this.logModel.lines[index]?.has_id) pointed = index;
          return true;
        });
      }
    }
    if (pointed !== this.logModel.pointed_index) { this.logModel.pointed_index = pointed; this.log?.update(); }
  }

  // ---------- keys (CChatInputBox::UpdateKeyEvent) ----------

  private focusedField(): 'chat' | 'whisper' | null {
    const active = this.input?.host.shadowRoot?.activeElement;
    if (!active || !this.open) return null;
    return active.id === 'chat_field' ? 'chat' : active.id === 'whisper_field' ? 'whisper' : null;
  }

  private onKey(event: KeyboardEvent): void {
    if (!this.enabled) return;
    const field = this.focusedField();
    const handled = () => { event.preventDefault(); event.stopImmediatePropagation(); };
    if (event.key === 'F2') {
      this.whispersOnly = !this.whispersOnly;
      this.sync(true);
      this.options.click?.();
      handled();
      return;
    }
    if (event.key === 'F3') {
      this.inputModel.whisper_send = !this.inputModel.whisper_send;
      if (!this.inputModel.whisper_send && this.open) this.input?.$<HTMLInputElement>('#chat_field')?.focus();
      this.sync();
      handled();
      return;
    }
    if (event.key === 'F4' && this.logModel.show_frame) { this.sizeStep(); this.options.click?.(); handled(); return; }
    if (event.key === 'F5') {
      if (this.open) this.setFrame(!this.logModel.show_frame);
      this.options.click?.();
      handled();
      return;
    }
    if (this.logModel.show_frame && (event.key === 'PageUp' || event.key === 'PageDown')) {
      const lines = this.log?.$('#lines');
      if (lines) lines.scrollTop += (event.key === 'PageUp' ? -1 : 1) * ROW * this.showingLines;
      handled();
      return;
    }
    if (!this.open) {
      if (event.key !== 'Enter' || event.repeat) return;
      // Enter opens the box, unless a window or dialog with its own text field has the keys.
      const target = event.composedPath()[0];
      if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) return;
      if (document.querySelector('.confirm-dialog')) return;
      this.openInput();
      if (this.open) handled();
      return;
    }
    // Escape closes the open box wherever the focus is (after a button click too).
    if (event.key === 'Escape') { handled(); this.close(); this.options.click?.(); return; }
    if (!field) return;
    if (event.key === 'Enter') { handled(); this.submit(); return; }
    if (event.key === 'Tab') {
      handled();
      this.input?.$<HTMLInputElement>(field === 'chat' ? '#whisper_field' : '#chat_field')?.focus();
      return;
    }
    if ((event.key === 'ArrowUp' || event.key === 'ArrowDown') && field === 'chat' && this.chatHistory.length) {
      this.chatHistoryAt = this.step(this.chatHistoryAt, this.chatHistory.length, event.key === 'ArrowUp');
      this.setField('#chat_field', this.chatHistory[this.chatHistoryAt]);
      handled();
      return;
    }
    if ((event.key === 'ArrowUp' || event.key === 'ArrowDown') && field === 'whisper' && this.inputModel.whisper_send && this.whisperHistory.length) {
      this.whisperHistoryAt = this.step(this.whisperHistoryAt, this.whisperHistory.length, event.key === 'ArrowUp');
      this.setField('#whisper_field', this.whisperHistory[this.whisperHistoryAt]);
      handled();
      return;
    }
    // Typing stays in the box: no hotkey under it sees the key.
    event.stopImmediatePropagation();
  }

  private step(at: number, length: number, up: boolean): number {
    const next = up ? at - 1 : at + 1;
    return next < 0 ? length - 1 : next >= length ? 0 : next;
  }

  private setField(id: string, value: string): void {
    const field = this.input?.$<HTMLInputElement>(id);
    if (!field) return;
    field.value = value;
    if (id === '#chat_field') this.inputModel.chat_text = value; else this.inputModel.whisper_id = value;
  }

  // Enter in the box: the channel's prefix, then a command, a whisper or the public chat; the box closes.
  private submit(): void {
    const now = performance.now();
    if (now - this.lastSent < COOLDOWN_MS) return; // ChatCooldownMs: Enter does nothing within a second
    this.lastSent = now;
    const text = (this.input?.$<HTMLInputElement>('#chat_field')?.value ?? '').slice(0, CHAT_MAX);
    const target = (this.input?.$<HTMLInputElement>('#whisper_field')?.value ?? '').trim().slice(0, NAME_MAX);
    if (text) {
      const prefixed = text.startsWith('/') ? text : CHANNEL_PREFIX[this.inputModel.input_msg_type] + text;
      if (this.inputModel.whisper_send && target && !text.startsWith('/') && this.whisper) {
        this.whisper(target, prefixed);
        this.remember(this.whisperHistory, target, 5);
      } else {
        this.send?.(prefixed);
        if (!text.startsWith('/')) this.remember(this.chatHistory, prefixed, 12);
      }
    }
    this.setField('#chat_field', '');
    this.chatHistoryAt = this.whisperHistoryAt = 0;
    this.close();
  }

  // AddChatHistory / AddWhsprIDHistory: a repeated entry moves to the end, the oldest goes past the limit.
  private remember(list: string[], entry: string, limit: number): void {
    const at = list.indexOf(entry);
    if (at >= 0) list.splice(at, 1);
    else if (list.length > limit) list.shift();
    list.push(entry);
  }
}

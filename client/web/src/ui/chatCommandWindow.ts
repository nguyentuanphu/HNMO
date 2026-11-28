import { panelKey } from '../game/keyboardShortcuts.js';
// The chat command list (CChatCommandWindow, chat_command.rml): opened from the Command window's
// Special button or by pressing J. Shows server-provided slash commands (`AvailableChatCommand`,
// 0xF5 0x01), parameters with validation/cycling/own-value hints, favourites and templates.
import { openDocument, prepareDocument, type RmlDocument, type RmlModel } from './rml.js';
import { fitTextPx, nativeLineHeightPx, nativeTextPx, dockTop, uiScale } from './nativeText.js';
import { divideStringByPixel, textWidth } from '../game/questData.js';
import { keepTypingInside } from './guildWindows.js';
import type { Connection } from '../net/connection.js';

const PANEL_WIDTH = 190;
const CONTENT_WIDTH = 146;
const CONTENT_TOP = 36;
const CONTENT_BOTTOM = 350;
const ROW_HEIGHT = 15;
const PARAMETER_HEIGHT = 35;
const VISIBLE_ROWS = 20;
const OWN_VALUE_WIDTH = 50;
const NUMBER_VALUE_WIDTH = 36;
const LARGEST_SHOWN_RANGE = 65535;
const FAVOURITES_KEY = 'hnmo.chatCommandFavourites';
const TEMPLATES_KEY = 'hnmo.chatCommandTemplates';

const Page = { COMMANDS: 0, PARAMETERS: 1, TEMPLATES: 2 } as const;
const Action = {
  PICK_COMMAND: 1,
  EDIT_VALUE: 2,
  TOGGLE_FAVOURITE: 3,
  SAVE_TEMPLATE: 4,
  EXECUTE_TEMPLATE: 5,
  REMOVE_TEMPLATE: 6,
  FILL_OWN_VALUE: 7,
} as const;

const ValueRef = {
  NONE: 0,
  CHARACTER_NAME: 1,
  MAP: 4,
  MAP_X: 5,
  MAP_Y: 6,
} as const;

const decoder = new TextDecoder();
function readUtf8Z(bytes: Uint8Array, start: number, length: number): string {
  const slice = bytes.subarray(start, Math.min(bytes.length, start + length));
  const nul = slice.indexOf(0);
  return decoder.decode(nul < 0 ? slice : slice.subarray(0, nul));
}

function readInt64LE(bytes: Uint8Array, offset: number): number {
  const view = new DataView(bytes.buffer, bytes.byteOffset + offset, 8);
  const big = view.getBigInt64(0, true);
  if (big > BigInt(Number.MAX_SAFE_INTEGER)) return Number.MAX_SAFE_INTEGER;
  if (big < BigInt(Number.MIN_SAFE_INTEGER)) return Number.MIN_SAFE_INTEGER;
  return Number(big);
}

export interface ChatCommandParameter {
  isRequired: boolean;
  type: number; // 0: Text, 1: Number, 2: Boolean
  name: string;
  shortName: string;
  validValues: string;
  valueReference: number;
  hasRange: boolean;
  minimum: number;
  maximum: number;
}

export interface AvailableCommand {
  minimumStatus: number;
  command: string;
  name: string;
  description: string;
  parameters: ChatCommandParameter[];
}

export interface CommandTemplate {
  label: string;
  command: string;
  values: string[];
}

interface LineModel { text: string; text_px: number }
interface CommandRowModel { text: string; text_px: number; favourite: boolean }
interface ParameterRowModel {
  label: string; label_text_px: number; missing: boolean;
  value: string; value_text_px: number; placeholder: boolean;
  value_name: string; value_name_text_px: number;
  has_own_value: boolean; edited: boolean;
}

interface ChatCommandModel extends RmlModel {
  text_px: number;
  page: number;
  title: LineModel;
  empty_message: LineModel;
  command_rows: CommandRowModel[];
  description_lines: LineModel[];
  template_rows: LineModel[];
  parameters: ParameterRowModel[];
  favourite_action: LineModel;
  save_action: LineModel;
  own_value_action: LineModel;
  edit_value: string;
  has_left_button: boolean;
  has_right_button: boolean;
  left_text: string;
  right_text: string;
  label_line_px: number;
  exit_tooltip: string;
  chat_command_hit: (action: number, index: number) => void;
  chat_command_exit: () => void;
  chat_command_left: () => void;
  chat_command_right: () => void;
}

export interface ChatCommandWindowOptions {
  connection: Connection;
  earlyPackets: readonly Uint8Array[];
  heroName: string;
  heroCell: () => { x: number; y: number } | null;
  mapNumber: () => number | null;
  click?: () => void;
  onExecute: (line: string) => void;
  message: (text: string) => void;
  onOpen?: () => void;
  onToggle?: (open: boolean) => void;
}

function loadJson<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) as T : fallback;
  } catch {
    return fallback;
  }
}

function saveJson(key: string, value: unknown): void {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* ignore private storage error */ }
}

function parameterAccepts(param: ChatCommandParameter, value: string): boolean {
  if (!value) return true;
  if (param.type === 1) {
    if (!/^-?\d+$/.test(value)) return false;
    const n = Number(value);
    if (param.hasRange && (n < param.minimum || n > param.maximum)) return false;
  }
  if (param.validValues) {
    const options = param.validValues.split('|').map((v) => v.trim()).filter(Boolean);
    if (options.length > 0 && !options.some((o) => o.toLowerCase() === value.toLowerCase())) return false;
  }
  return true;
}

export function buildCommandLine(cmd: AvailableCommand, values: readonly string[]): string {
  let line = cmd.command;
  const useShortNames = cmd.parameters.length > 0 && cmd.parameters[0].shortName.length > 0;
  for (let i = 0; i < cmd.parameters.length && i < values.length; i++) {
    const val = values[i].trim();
    if (!val) continue;
    line += ' ';
    if (useShortNames && cmd.parameters[i].shortName) line += `${cmd.parameters[i].shortName}=`;
    line += val;
  }
  return line;
}

/** CChatCommandWindow (`chat_command.rml`, docked right): J hotkey or Special button in Command Window. */
export class ChatCommandWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private commands: AvailableCommand[] = [];
  private expectedCount = 0;
  private complete = false;
  private favourites = new Set<string>(loadJson<string[]>(FAVOURITES_KEY, []));
  private templates: CommandTemplate[] = loadJson<CommandTemplate[]>(TEMPLATES_KEY, []);
  private commandOrder: number[] = [];
  private page: number = Page.COMMANDS;
  private selectedRow = -1;
  private scrollOffset = 0;
  private paramValues: string[] = [];
  private editedParam = -1;
  private readonly model: ChatCommandModel;
  private readonly onResize = () => this.place();
  private readonly onKeyDown = (event: KeyboardEvent) => {
    if (!this.isOpen) return;
    if (panelKey(event, 'Escape')) {
      event.preventDefault();
      if (this.editedParam >= 0) {
        this.commitEditedValue();
        this.editedParam = -1;
        this.sync();
      } else if (this.page !== Page.COMMANDS) {
        this.showPage(Page.COMMANDS);
      } else {
        this.close();
      }
      this.options.click?.();
      return;
    }
    if (this.editedParam >= 0 && panelKey(event, 'Enter')) {
      event.preventDefault();
      this.commitEditedValue();
      this.editedParam = -1;
      this.sync();
    }
  };

  constructor(private readonly options: ChatCommandWindowOptions) {
    const line = (text = ''): LineModel => ({ text, text_px: nativeTextPx() });
    this.model = {
      text_px: nativeTextPx(),
      page: Page.COMMANDS,
      title: line('Commands'),
      empty_message: line(),
      command_rows: [],
      description_lines: [],
      template_rows: [],
      parameters: [],
      favourite_action: line(),
      save_action: line(),
      own_value_action: line('Own'),
      edit_value: '',
      has_left_button: false,
      has_right_button: false,
      left_text: 'Back',
      right_text: 'Templates',
      label_line_px: nativeLineHeightPx(),
      exit_tooltip: 'Close (J)',
      chat_command_hit: (action, index) => this.handleHit(action, index),
      chat_command_exit: () => { this.options.click?.(); this.close(); },
      chat_command_left: () => {
        this.options.click?.();
        this.commitEditedValue();
        this.editedParam = -1;
        this.showPage(Page.COMMANDS);
      },
      chat_command_right: () => {
        this.options.click?.();
        if (this.page === Page.COMMANDS) this.showPage(Page.TEMPLATES);
        else this.executeSelected();
      },
    };
    prepareDocument('chat_command').catch((error: unknown) => console.warn('Interface', error));
    for (const pkt of options.earlyPackets) this.handlePacket(pkt);
    options.connection.onPacket((pkt) => this.handlePacket(pkt));
    try { options.connection.send('ClientToServer', 'ChatCommandListRequest', {}); } catch { /* ignore if closing */ }
    window.addEventListener('resize', this.onResize);
    window.addEventListener('keydown', this.onKeyDown);
  }

  get isOpen(): boolean { return this.doc !== null; }
  get commandCount(): number { return this.commands.length; }

  async toggle(): Promise<void> {
    this.options.click?.();
    if (this.isOpen) this.close();
    else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    if (this.commands.length === 0) {
      try { this.options.connection.send('ClientToServer', 'ChatCommandListRequest', {}); } catch { /* ignore */ }
    }
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  close(): void {
    if (!this.doc) return;
    this.editedParam = -1;
    this.doc.close();
    this.doc = null;
    this.options.onToggle?.(false);
  }

  private handlePacket(bytes: Uint8Array): void {
    // AvailableChatCommand: C2, code 0xF5 (245), subCode 0x01
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x01 || bytes.length < 345) return;
    const index = bytes[5];
    const count = bytes[6];
    const minStatus = bytes[7];
    const paramCount = bytes[8];
    if (count === 0 || index >= count) return;
    const entrySize = paramCount > 0 ? Math.floor((bytes.length - 345) / paramCount) : 102;
    if (entrySize < 102) return;

    if (index === 0) {
      this.commands = [];
      this.expectedCount = count;
      this.complete = false;
    } else if (this.complete || count !== this.expectedCount || index !== this.commands.length) {
      return;
    }

    const command = readUtf8Z(bytes, 9, 32).trim();
    if (!command) return;
    const name = readUtf8Z(bytes, 41, 48).trim();
    const description = readUtf8Z(bytes, 89, 256).trim();
    const parameters: ChatCommandParameter[] = [];
    for (let i = 0; i < paramCount; i++) {
      const base = 345 + i * entrySize;
      const paramType = bytes[base + 1];
      if (paramType > 2) return;
      const pName = readUtf8Z(bytes, base + 2, 32).trim();
      const shortName = readUtf8Z(bytes, base + 34, 20).trim();
      const validValues = readUtf8Z(bytes, base + 54, 48).trim();
      const hasHints = entrySize >= 120;
      const valueReference = hasHints ? bytes[base + 102] : ValueRef.NONE;
      const minVal = hasHints ? readInt64LE(bytes, base + 104) : 0;
      const maxVal = hasHints ? readInt64LE(bytes, base + 112) : 0;
      const hasRange = hasHints && bytes[base + 103] !== 0 && minVal <= maxVal;
      parameters.push({
        isRequired: bytes[base] !== 0,
        type: paramType,
        name: pName,
        shortName,
        validValues,
        valueReference,
        hasRange,
        minimum: hasRange ? minVal : 0,
        maximum: hasRange ? maxVal : 0,
      });
    }
    this.commands.push({ minimumStatus: minStatus, command, name, description, parameters });
    this.complete = this.commands.length >= count;
    if (this.isOpen) {
      this.rebuildOrder();
      this.sync();
    }
  }

  private async load(): Promise<void> {
    this.options.onOpen?.();
    this.rebuildOrder();
    this.selectedRow = -1;
    this.paramValues = [];
    this.editedParam = -1;
    this.showPage(Page.COMMANDS);
    const doc = await openDocument('chat_command', this.model, { parent: document.body, scale: uiScale() });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
      panel.addEventListener('contextmenu', (event) => event.preventDefault());
      panel.addEventListener('wheel', (event) => {
        event.stopPropagation();
        const hidden = Math.max(0, this.scrollableCount() - VISIBLE_ROWS);
        if (hidden > 0) {
          this.scrollOffset = Math.max(0, Math.min(hidden, this.scrollOffset + (event.deltaY > 0 ? 1 : -1)));
          this.sync();
        }
      }, { passive: true });
    }
    for (const id of ['#btn_exit', '#frame_corner_close', '#btn_left', '#btn_right']) {
      const btn = doc.$(id);
      if (btn) btn.style.pointerEvents = 'auto';
    }
    keepTypingInside(doc.host);
    this.place();
    this.sync();
    this.options.onToggle?.(true);
  }

  private rebuildOrder(): void {
    const favs: number[] = [];
    const rest: number[] = [];
    for (let i = 0; i < this.commands.length; i++) {
      (this.favourites.has(this.commands[i].command) ? favs : rest).push(i);
    }
    this.commandOrder = [...favs, ...rest];
  }

  private commandAt(row: number): AvailableCommand | null {
    if (row < 0 || row >= this.commandOrder.length) return null;
    return this.commands[this.commandOrder[row]] ?? null;
  }

  private selectedCommand(): AvailableCommand | null {
    return this.commandAt(this.selectedRow);
  }

  private scrollableCount(): number {
    return this.page === Page.TEMPLATES ? this.templates.length : this.commandOrder.length;
  }

  private showPage(page: number): void {
    this.commitEditedValue();
    this.editedParam = -1;
    this.page = page;
    this.scrollOffset = 0;
    this.sync();
  }

  private commitEditedValue(): void {
    if (this.editedParam < 0) return;
    const cmd = this.selectedCommand();
    if (!cmd || this.editedParam >= cmd.parameters.length) return;
    let v = this.model.edit_value;
    if (cmd.parameters[this.editedParam].type === 1) v = v.replace(/[^\d-]/g, '');
    this.paramValues[this.editedParam] = v;
  }

  private handleHit(action: number, index: number): void {
    const listIndex = this.scrollOffset + index;
    switch (action) {
      case Action.PICK_COMMAND: {
        const cmd = this.commandAt(listIndex);
        if (!cmd) break;
        this.options.click?.();
        this.selectedRow = listIndex;
        if (cmd.parameters.length === 0) {
          this.options.onExecute(cmd.command);
          this.close();
          return;
        }
        this.paramValues = cmd.parameters.map(() => '');
        this.showPage(Page.PARAMETERS);
        return;
      }
      case Action.EDIT_VALUE: {
        const cmd = this.selectedCommand();
        if (!cmd || index < 0 || index >= cmd.parameters.length) break;
        this.options.click?.();
        const param = cmd.parameters[index];
        if (param.validValues) {
          const choices = param.validValues.split('|').map((v) => v.trim()).filter(Boolean);
          if (choices.length > 0) {
            this.commitEditedValue();
            this.editedParam = -1;
            const cur = this.paramValues[index];
            const nextIdx = choices.indexOf(cur) + 1;
            this.paramValues[index] = nextIdx >= choices.length ? '' : choices[nextIdx];
            this.sync();
            return;
          }
        }
        this.commitEditedValue();
        this.editedParam = index;
        this.model.edit_value = this.paramValues[index] ?? '';
        this.sync();
        const rows = this.doc?.root.querySelectorAll('.cc-parameter');
        const input = rows?.[index]?.querySelector('input') as HTMLInputElement | null;
        input?.focus();
        return;
      }
      case Action.FILL_OWN_VALUE: {
        const cmd = this.selectedCommand();
        if (!cmd || index < 0 || index >= cmd.parameters.length) break;
        this.commitEditedValue();
        this.editedParam = -1;
        const own = this.ownValueOf(cmd.parameters[index]);
        if (own) {
          this.paramValues[index] = own;
          this.options.click?.();
          this.sync();
        }
        return;
      }
      case Action.TOGGLE_FAVOURITE: {
        const cmd = this.selectedCommand();
        if (!cmd) break;
        if (this.favourites.has(cmd.command)) this.favourites.delete(cmd.command);
        else this.favourites.add(cmd.command);
        saveJson(FAVOURITES_KEY, [...this.favourites]);
        const selectedName = cmd.command;
        this.rebuildOrder();
        this.selectedRow = this.commandOrder.findIndex((i) => this.commands[i]?.command === selectedName);
        this.options.click?.();
        this.sync();
        return;
      }
      case Action.SAVE_TEMPLATE: {
        const cmd = this.selectedCommand();
        if (!cmd) break;
        this.commitEditedValue();
        this.templates.push({
          label: buildCommandLine(cmd, this.paramValues),
          command: cmd.command,
          values: [...this.paramValues],
        });
        saveJson(TEMPLATES_KEY, this.templates);
        this.options.click?.();
        this.options.message(`Saved command template: ${buildCommandLine(cmd, this.paramValues)}`);
        return;
      }
      case Action.EXECUTE_TEMPLATE: {
        const entry = this.templates[listIndex];
        if (!entry) break;
        const cmd = this.commands.find((c) => c.command === entry.command);
        if (!cmd) {
          this.options.message('This command is no longer available.');
          return;
        }
        this.options.click?.();
        this.options.onExecute(buildCommandLine(cmd, entry.values));
        this.close();
        return;
      }
      case Action.REMOVE_TEMPLATE: {
        if (listIndex >= 0 && listIndex < this.templates.length) {
          this.templates.splice(listIndex, 1);
          saveJson(TEMPLATES_KEY, this.templates);
          const hidden = Math.max(0, this.templates.length - VISIBLE_ROWS);
          this.scrollOffset = Math.min(this.scrollOffset, hidden);
          this.options.click?.();
          this.sync();
        }
        return;
      }
      default:
    }
  }

  private hasOwnValue(param: ChatCommandParameter): boolean {
    return param.valueReference === ValueRef.CHARACTER_NAME
      || param.valueReference === ValueRef.MAP
      || param.valueReference === ValueRef.MAP_X
      || param.valueReference === ValueRef.MAP_Y;
  }

  private ownValueOf(param: ChatCommandParameter): string {
    switch (param.valueReference) {
      case ValueRef.CHARACTER_NAME: return this.options.heroName;
      case ValueRef.MAP: {
        const map = this.options.mapNumber();
        return map === null ? '' : String(map);
      }
      case ValueRef.MAP_X: {
        const cell = this.options.heroCell();
        return cell ? String(cell.x) : '';
      }
      case ValueRef.MAP_Y: {
        const cell = this.options.heroCell();
        return cell ? String(cell.y) : '';
      }
      default: return '';
    }
  }

  private executeSelected(): void {
    this.commitEditedValue();
    const cmd = this.selectedCommand();
    if (!cmd) return;
    for (let i = 0; i < cmd.parameters.length; i++) {
      if (cmd.parameters[i].isRequired && !this.paramValues[i]?.trim()) {
        this.options.message('Please fill in all required command parameters.');
        return;
      }
      if (!parameterAccepts(cmd.parameters[i], this.paramValues[i]?.trim() ?? '')) {
        this.options.message(`Invalid value for ${cmd.parameters[i].name}.`);
        return;
      }
    }
    this.options.onExecute(buildCommandLine(cmd, this.paramValues));
    this.close();
  }

  private sync(): void {
    const px = nativeTextPx();
    const scale = uiScale();
    const fitLine = (text: string, width: number, bold = false): LineModel => {
      if (!text) return { text: '', text_px: px };
      const measured = textWidth(text) * (bold ? 1.1 : 1) * scale;
      return { text, text_px: fitTextPx(px, measured, width * scale) };
    };
    const m = this.model;
    m.text_px = px;
    m.page = this.page;
    m.label_line_px = nativeLineHeightPx();

    const selected = this.selectedCommand();
    m.title = fitLine(
      this.page === Page.TEMPLATES ? 'Templates' : (this.page === Page.PARAMETERS && selected ? selected.command : 'Commands'),
      PANEL_WIDTH,
      true,
    );
    m.empty_message = { text: '', text_px: px };
    m.command_rows = [];
    m.description_lines = [];
    m.template_rows = [];
    m.parameters = [];
    m.favourite_action = { text: '', text_px: px };
    m.save_action = { text: '', text_px: px };
    m.own_value_action = fitLine('Own', OWN_VALUE_WIDTH);

    if (this.page === Page.COMMANDS) {
      if (this.commandOrder.length === 0) m.empty_message = fitLine('No chat commands supported', CONTENT_WIDTH);
      for (let row = 0; row < VISIBLE_ROWS; row++) {
        const cmd = this.commandAt(this.scrollOffset + row);
        if (!cmd) break;
        const fav = this.favourites.has(cmd.command);
        const label = `${fav ? '* ' : ''}${cmd.command}${cmd.parameters.length > 0 ? ' ...' : ''}`;
        const fitted = fitLine(label, CONTENT_WIDTH);
        m.command_rows.push({ text: fitted.text, text_px: fitted.text_px, favourite: fav });
      }
    } else if (this.page === Page.PARAMETERS && selected) {
      const wrapped = selected.description ? divideStringByPixel(selected.description, CONTENT_WIDTH) : [];
      const reserved = selected.parameters.length * PARAMETER_HEIGHT + 3 * ROW_HEIGHT;
      const maxDesc = Math.max(0, Math.floor((CONTENT_BOTTOM - CONTENT_TOP - reserved) / ROW_HEIGHT));
      for (let i = 0; i < Math.min(maxDesc, wrapped.length); i++) {
        m.description_lines.push(fitLine(wrapped[i], CONTENT_WIDTH));
      }
      for (let i = 0; i < selected.parameters.length; i++) {
        const p = selected.parameters[i];
        const val = this.paramValues[i] ?? '';
        const hasOwn = this.hasOwnValue(p);
        const labelLine = fitLine(`${p.name}${p.isRequired ? ' *' : ''}`, CONTENT_WIDTH - (hasOwn ? OWN_VALUE_WIDTH : 0));
        const placeholder = p.validValues
          || (p.hasRange && p.minimum >= 0 && p.maximum <= LARGEST_SHOWN_RANGE ? `${p.minimum} - ${p.maximum}` : '');
        const valueLine = fitLine(val || placeholder, CONTENT_WIDTH - 4);
        const nameLine = fitLine('', CONTENT_WIDTH - 4 - NUMBER_VALUE_WIDTH);
        m.parameters.push({
          label: labelLine.text,
          label_text_px: labelLine.text_px,
          missing: (p.isRequired && !val.trim()) || !parameterAccepts(p, val.trim()),
          value: valueLine.text,
          value_text_px: valueLine.text_px,
          placeholder: !val,
          value_name: nameLine.text,
          value_name_text_px: nameLine.text_px,
          has_own_value: hasOwn,
          edited: this.editedParam === i,
        });
      }
      const isFav = this.favourites.has(selected.command);
      m.favourite_action = fitLine(isFav ? 'Remove Favourite' : 'Add Favourite', CONTENT_WIDTH);
      m.save_action = fitLine('Save Template', CONTENT_WIDTH);
    } else if (this.page === Page.TEMPLATES) {
      if (this.templates.length === 0) m.empty_message = fitLine('No saved templates', CONTENT_WIDTH);
      for (let row = 0; row < VISIBLE_ROWS; row++) {
        const entry = this.templates[this.scrollOffset + row];
        if (!entry) break;
        m.template_rows.push(fitLine(entry.label, CONTENT_WIDTH - ROW_HEIGHT));
      }
    }

    m.has_left_button = this.page !== Page.COMMANDS;
    m.has_right_button = this.page === Page.COMMANDS ? this.commands.length > 0 : this.page === Page.PARAMETERS;
    m.left_text = 'Back';
    m.right_text = this.page === Page.COMMANDS ? 'Templates' : 'Execute';
    this.doc?.update();
  }

  place(): void {
    if (!this.doc) return;
    const panel = this.doc.$('#panel');
    if (!panel) return;
    const scale = uiScale();
    this.doc.setScale(scale);
    Object.assign(panel.style, {
      left: `${window.innerWidth - PANEL_WIDTH * scale}px`,
      top: `${dockTop()}px`,
      transform: `scale(${scale})`,
      transformOrigin: 'left top',
    });
    this.sync();
  }
}

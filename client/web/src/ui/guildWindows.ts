// The guild windows, docked at the right edge like the desktop's:
//  - the guild window (CGuildInfoWindow, guild_info.rml), G: without a guild, how to join one; with
//    one, the Guild tab (mark, score, members, rival, notices, Leave / Disband), the Members tab
//    (names, positions, servers; the master's Position, Dissolve and Release) and the Alliance tab.
//    Opening it asks for the member list (OpenningProcess).
//  - the guild master's window (CGuildMakeWindow, guild_make.rml), opened by the guild master NPC:
//    the question, then the name and the 8 x 8 mark editor, then the finished mark; Next creates.
// Leaving, disbanding and releasing ask for the account's password, as the desktop does.

import { openDocument, prepareDocument, REFERENCE_HEIGHT, type RmlDocument, type RmlModel } from './rml.js';
import { editorCellColor, GuildStatus, markCellColor, MARK_CELLS, type Guild } from '../game/guild.js';
import { askInGame, chooseInGame, noticeInGame, promptInGame } from './socialDialogs.js';
import { fitTextPx, measureText, nativeTextPx } from './nativeText.js';

const PANEL_WIDTH = 190;

/** RenderText()'s shrink (NativeTextPxInBox): the size a text of `px` screen pixels is drawn at so it
 * fits its box (reference px at the panel's scale), not below the native minimum; 0 means no box. */
export function fitPx(text: string, px: number, box: number, scale: number): number {
  if (!box || !text) return px;
  return fitTextPx(px, measureText(text, px), box * scale);
}

/** The native text size (NativeTextPx), which grows more slowly than the panels. */
const nativePx = (_scale: number) => nativeTextPx();

interface GuildLine { text: string; text_px: number }
interface ActionButton { label: string; shown: boolean }
interface MemberRow { name: string; role: string; role_text_px: number; server: string; selected: boolean; officer: boolean }
interface UnionRow { name: string; member_count: string; count_text_px: number; mark_cells: string[]; selected: boolean }

interface GuildInfoModel extends RmlModel {
  text_px: number; label_line_px: number; no_guild: boolean; tab: number; union_shown: boolean; mark_cells: string[];
  hint_title: GuildLine; hint_line1: GuildLine; hint_line2: GuildLine; hint_line3: GuildLine;
  title: GuildLine; guild_name: GuildLine; tab_info: GuildLine; tab_members: GuildLine; tab_union: GuildLine;
  notice_label: GuildLine; created: GuildLine; score: GuildLine; member_count: GuildLine; rival: GuildLine;
  header_name: GuildLine; header_position: GuildLine; header_server: GuildLine; header_union_name: GuildLine; header_union_members: GuildLine;
  alliance_lines: GuildLine[]; notice_rows: { text: string }[]; member_rows: MemberRow[]; union_rows: UnionRow[];
  guild_out_button: ActionButton; get_position_button: ActionButton; free_position_button: ActionButton; get_out_button: ActionButton;
  union_create_button: ActionButton; union_out_button: ActionButton; exit_tooltip: string;
  guild_info_button: (button: number) => void;
  guild_info_tab: (tab: number) => void;
  guild_info_select_member: (index: number) => void;
  guild_info_select_union: (index: number) => void;
}

const Tab = { INFO: 0, MEMBERS: 1, UNION: 2 } as const;
const TAB_WIDTH = 56; // GuildConstants::UILayout::TAB_WIDTH
const Button = { GUILD_OUT: 0, GET_POSITION: 1, FREE_POSITION: 2, GET_OUT: 3, UNION_CREATE: 4, UNION_OUT: 5, EXIT: 6 } as const;

// Render_Guild_Info()'s explanation of alliances.
const ALLIANCE_LINES = [
  'To make the alliance,', 'Face the guild master', 'of desired guild for guild alliance', 'Enter /alliance or Guild alliance',
  'button in command window.', 'If the opposite is not a guild', 'alliance, opposite alliance should', 'be the main alliance for creating',
  'guild alliance. Request the', 'registration to opposite alliance,', 'if the opposite is guild alliance.',
];

/** Keys typed into a window's fields stay there: the game's hotkeys and the chat's Enter see the
 * shadow host, not the field, so they would fire on every letter typed. */
export function keepTypingInside(host: HTMLElement): void {
  host.addEventListener('keydown', (event) => {
    const field = event.composedPath()[0];
    if (field instanceof HTMLElement && field.matches('input, textarea') && event.key !== 'Escape') event.stopPropagation();
  });
}

const NO_BUTTON: ActionButton = { label: '', shown: false };

export interface DockedWindowOptions {
  /** Opening it closes the inventory, the character and the party windows (HideAllGroupA). */
  onOpen?: () => void;
  /** It stands in the dock-right column: the inventory moves next to it. */
  onToggle?: (open: boolean) => void;
  click?: () => void;
  parent?: HTMLElement;
}

/** A window docked at the right edge, scaled to the window's height like party_info. */
abstract class DockedWindow<M extends RmlModel & { text_px: number }> {
  protected doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;

  protected constructor(private readonly documentName: string, protected readonly model: M, protected readonly docked: DockedWindowOptions) {
    prepareDocument(documentName).catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen && !document.querySelector('.confirm-dialog')) { this.close(); this.docked.click?.(); }
    });
    window.addEventListener('resize', () => this.place());
  }

  get isOpen(): boolean { return this.doc !== null; }

  async toggle(): Promise<void> {
    this.docked.click?.();
    if (this.isOpen) this.close(); else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  protected opened(): void { /* OpenningProcess */ }
  protected closing(): void { /* ClosingProcess */ }
  protected abstract refresh(): void;

  private async load(): Promise<void> {
    this.docked.onOpen?.();
    this.opened();
    this.refresh();
    const doc = await openDocument(this.documentName, this.model, { parent: this.docked.parent ?? document.body, scale: window.innerHeight / REFERENCE_HEIGHT });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
      panel.addEventListener('contextmenu', (event) => event.preventDefault());
    }
    keepTypingInside(doc.host);
    this.place();
    this.refresh();
    this.docked.onToggle?.(true);
  }

  close(): void {
    if (!this.doc) return;
    this.closing();
    this.doc.close();
    this.doc = null;
    this.docked.onToggle?.(false);
  }

  place(): void {
    if (!this.doc) return;
    const panel = this.doc.$('#panel');
    if (!panel) return;
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    this.doc.setScale(scale);
    Object.assign(panel.style, {
      left: `${window.innerWidth - PANEL_WIDTH * scale}px`, top: '0px', transform: `scale(${scale})`, transformOrigin: 'left top',
    });
    this.scale = scale;
    this.model.text_px = nativePx(scale);
    this.refresh();
  }

  protected scale = 1;
  protected get px(): number { return this.model.text_px; }
  /** A line of the window with the box the desktop shrinks it into (0: none). */
  protected fit(text: string, box = 0): number { return fitPx(text, this.px, box, this.scale); }
}

export interface GuildInfoOptions extends DockedWindowOptions {
  guild: Guild;
  heroName: string;
  /** The hero's level, leadership and whether it is a Dark Lord (the member limit). */
  hero: () => { level: number; leadership: number; darkLord: boolean };
  heroId: () => number;
  onMessage: (text: string) => void;
}

export class GuildInfoWindow extends DockedWindow<GuildInfoModel> {
  private tab: number = Tab.MEMBERS;
  private selectedMember = '';
  private selectedUnion = '';
  private unionRequested = false;

  constructor(private readonly options: GuildInfoOptions) {
    const line = (): GuildLine => ({ text: '', text_px: nativeTextPx() });
    super('guild_info', {
      text_px: nativeTextPx(), label_line_px: 14, no_guild: true, tab: Tab.MEMBERS, union_shown: false, mark_cells: [],
      hint_title: line(), hint_line1: line(), hint_line2: line(), hint_line3: line(), title: line(), guild_name: line(),
      tab_info: line(), tab_members: line(), tab_union: line(), notice_label: line(), created: line(), score: line(),
      member_count: line(), rival: line(), header_name: line(), header_position: line(), header_server: line(),
      header_union_name: line(), header_union_members: line(), alliance_lines: [], notice_rows: [], member_rows: [], union_rows: [],
      guild_out_button: NO_BUTTON, get_position_button: NO_BUTTON, free_position_button: NO_BUTTON, get_out_button: NO_BUTTON,
      union_create_button: NO_BUTTON, union_out_button: NO_BUTTON, exit_tooltip: 'Close',
      guild_info_button: (button) => { this.options.click?.(); this.button(button); },
      guild_info_tab: (tab) => { if (tab >= 0 && tab < 3) { this.selectTab(tab); this.options.click?.(); } },
      guild_info_select_member: (index) => { const row = this.model.member_rows[index]; if (row) { this.selectedMember = row.name; this.refresh(); } },
      guild_info_select_union: (index) => { const row = this.model.union_rows[index]; if (row) { this.selectedUnion = row.name; this.refresh(); } },
    }, options);
    options.guild.onChange((what) => {
      if (what === 'left' && this.isOpen) { this.close(); return; }
      if (this.isOpen) this.refresh();
    });
  }

  get currentTab(): number { return this.tab; }

  protected override opened(): void {
    this.tab = Tab.MEMBERS;
    this.options.guild.requestList();
  }

  protected override closing(): void { this.unionRequested = false; }

  private selectTab(tab: number): void {
    this.tab = tab;
    const guild = this.options.guild;
    if (tab === Tab.MEMBERS) guild.requestList();
    else if (tab === Tab.UNION && !this.unionRequested && guild.heroMark?.allianceName) { guild.requestAlliances(); this.unionRequested = true; }
    this.refresh();
  }

  /** Check_Btn(): what each of the window's buttons does. */
  private button(button: number): void {
    const guild = this.options.guild;
    const { heroName } = this.options;
    if (button === Button.EXIT) { this.close(); return; }
    const master = guild.status === GuildStatus.MASTER;
    const selected = guild.members.find((member) => member.name === this.selectedMember);
    if (this.tab === Tab.INFO && button === Button.GUILD_OUT) {
      if (master) {
        const mark = guild.heroMark;
        if (mark && mark.allianceName && mark.allianceName === mark.name) { void noticeInGame("Alliance master can't disband the guild."); return; }
        void askInGame(['Once you disband the guild', 'All the items and zen in the guild vault will disappear', 'Also the guild ranking information will disappear.', 'Would you like to disband the guild?'].join('\n'))
          .then((yes) => { if (yes) this.askPassword(heroName); });
      } else this.askPassword(heroName);
    } else if (this.tab === Tab.MEMBERS && button === Button.GET_OUT) {
      if (master && selected && selected.status !== GuildStatus.MASTER) {
        void askInGame(`Character '${selected.name}'\nWould you like to release?`).then((yes) => { if (yes) this.askPassword(selected.name); });
      }
    } else if (this.tab === Tab.MEMBERS && button === Button.GET_POSITION) {
      if (master && selected && selected.status !== GuildStatus.MASTER) void this.appoint(selected.name, selected.status);
    } else if (this.tab === Tab.MEMBERS && button === Button.FREE_POSITION) {
      if (selected && (selected.status === GuildStatus.SUB_MASTER || selected.status === GuildStatus.BATTLE_MASTER)) {
        void askInGame(`Character '${selected.name}'\nWould you like to cancel the ranking?`).then((yes) => {
          if (yes) guild.assignRole(selected.name, GuildStatus.PERSON, 0x03);
        });
      }
    } else if (this.tab === Tab.UNION && button === Button.UNION_CREATE) {
      const mark = guild.heroMark;
      if (master && this.selectedUnion && mark && this.selectedUnion !== mark.name) {
        const name = this.selectedUnion;
        void askInGame(`${name} guild from the alliance\nWould you like to release?`).then((yes) => { if (yes) guild.removeAlliance(name); });
      }
    } else if (this.tab === Tab.UNION && button === Button.UNION_OUT) {
      const mark = guild.heroMark;
      if (master && mark) {
        if (mark.name === mark.allianceName) void noticeInGame("Alliance master can't withdraw the guild.");
        else guild.changeRelationship(1, 2, this.options.heroId());
      }
    }
  }

  // ShowGuildBreakPasswordDialog: leaving, disbanding and releasing need the password.
  private askPassword(name: string): void {
    void promptInGame('If you want to leave your guild,\nPlease enter your password.', { masked: true, maxLength: 20, allowEmpty: true }).then((password) => {
      if (password === null) return;
      if (password) this.options.guild.kick(name, password);
      else this.options.onMessage('The password you have entered is incorrect.');
    });
  }

  // CGuild_ToPerson_Position: the assistant (the default) or a battle master, then OK.
  private async appoint(name: string, status: number): Promise<void> {
    let role: number = GuildStatus.SUB_MASTER;
    for (;;) {
      const roleName = role === GuildStatus.SUB_MASTER ? 'Assist. M.' : 'Battle M.';
      const choice = await chooseInGame(`'${name}'as a ${roleName}\nDo you want to appoint?`, [
        { label: 'Appoint as assistant guild master', value: 'sub' },
        { label: 'Appoint as a battle master', value: 'battle' },
        { label: 'OK', value: 'ok', primary: true },
        { label: 'Close', value: 'cancel' },
      ]);
      if (choice === 'sub') role = GuildStatus.SUB_MASTER;
      else if (choice === 'battle') role = GuildStatus.BATTLE_MASTER;
      else if (choice === 'ok') {
        this.options.guild.assignRole(name, role, status === GuildStatus.PERSON ? 0x01 : 0x02);
        this.options.guild.requestList();
        return;
      } else return;
    }
  }

  /** SyncContent(): the model from the guild's data. */
  protected refresh(): void {
    const guild = this.options.guild;
    const m = this.model;
    const px = this.px;
    const line = (text: string, box = 0): GuildLine => ({ text, text_px: this.fit(text, box) });
    const empty = line('');
    m.no_guild = guild.status === GuildStatus.NONE;
    m.tab = this.tab;
    m.label_line_px = Math.round(px * 1.2);
    for (const key of ['hint_title', 'hint_line1', 'hint_line2', 'hint_line3', 'title', 'guild_name', 'tab_info', 'tab_members', 'tab_union',
      'notice_label', 'created', 'score', 'member_count', 'rival', 'header_name', 'header_position', 'header_server', 'header_union_name',
      'header_union_members'] as const) m[key] = empty;
    m.mark_cells = [];
    m.alliance_lines = [];
    m.union_shown = false;
    m.guild_out_button = m.get_position_button = m.free_position_button = m.get_out_button = m.union_create_button = m.union_out_button = NO_BUTTON;
    if (m.no_guild) {
      m.hint_title = line('Guild', 190);
      m.hint_line1 = line('Type /guild in front of');
      m.hint_line2 = line('the guild master you want to join');
      m.hint_line3 = line('and you can join the guild.');
    } else {
      const mark = guild.heroMark;
      const master = guild.status === GuildStatus.MASTER;
      m.title = line('Guild', 190);
      m.guild_name = line(`${mark?.name ?? ''} ( Score:${guild.totalScore} )`, 120);
      m.tab_info = line('Guild', TAB_WIDTH);
      m.tab_members = line('Members', TAB_WIDTH);
      m.tab_union = line('Alliance', TAB_WIDTH);
      if (this.tab === Tab.INFO) {
        m.mark_cells = (mark?.cells ?? new Array<number>(MARK_CELLS).fill(0)).map(markCellColor);
        m.guild_out_button = { label: master ? 'Disband' : 'Leave', shown: true };
        m.notice_label = line('Guild announcement', 40);
        m.created = line('Guild creation date :', 40);
        m.score = line(`Guild score: ${guild.totalScore}`, 80);
        const count = Math.max(0, guild.memberCount);
        if (master) {
          const hero = this.options.hero();
          const limit = hero.darkLord ? Math.min(80, Math.floor(hero.level / 10) + Math.floor(hero.leadership / 10)) : Math.floor(hero.level / 10);
          m.member_count = line(`Guild members ${count}/${limit}`, 80);
        } else m.member_count = line(`Guild member: ${count}`, 80);
        m.rival = line(`Hostility guild : ${guild.rivalName || 'None'}`);
      } else if (this.tab === Tab.MEMBERS) {
        m.header_name = line('Name', 40);
        m.header_position = line('Position', 40);
        m.header_server = line('Server', 40);
        if (master) {
          m.get_position_button = { label: 'Position', shown: true };
          m.free_position_button = { label: 'Dissolve', shown: true };
          m.get_out_button = { label: 'Release', shown: true };
        }
      } else if (this.tab === Tab.UNION) {
        m.union_shown = Boolean(mark?.allianceName);
        if (m.union_shown) {
          m.header_union_name = line('NAME', 40);
          m.header_union_members = line('Members', 40);
          m.union_create_button = { label: 'Disband alliance', shown: true };
          m.union_out_button = { label: 'Disband guild alliance', shown: true };
        } else m.alliance_lines = ALLIANCE_LINES.map((text) => line(text));
      }
    }
    m.notice_rows = guild.notices.map((text) => ({ text }));
    if (!guild.members.some((member) => member.name === this.selectedMember)) this.selectedMember = guild.members[0]?.name ?? '';
    m.member_rows = guild.members.map((member) => {
      const role = member.status === GuildStatus.MASTER ? 'Master' : member.status === GuildStatus.SUB_MASTER ? 'Assist. M.' : member.status === GuildStatus.BATTLE_MASTER ? 'Battle M.' : '';
      return { name: member.name, role, role_text_px: this.fit(role, 70), server: member.server >= 0 ? String(member.server + 1) : '', selected: member.name === this.selectedMember, officer: role !== '' };
    });
    m.union_rows = guild.alliances.map((entry) => ({
      name: entry.name, member_count: String(entry.memberCount), count_text_px: this.fit(String(entry.memberCount), 60), mark_cells: entry.cells.map(markCellColor), selected: entry.name === this.selectedUnion,
    }));
    this.doc?.update();
  }
}

interface MakeCell { color: string; empty: boolean }
interface GuildMakeModel extends RmlModel {
  text_px: number; label_line_px: number; page: number; title_text: string; title_px: number; info_text: string; info_px: number;
  make_text: string; back_text: string; next_text: string; name_text: string; result_text: string; result_px: number;
  palette_hint_1: string; palette_hint_2: string; exit_tooltip: string; guild_name: string;
  cells: MakeCell[]; palette: MakeCell[]; selected: MakeCell; mark_cells: string[];
  guild_mark_paint: (cell: number) => void;
  guild_mark_color: (color: number) => void;
  guild_make_button: (button: number) => void;
}

const Page = { INFO: 0, MARK: 1, RESULT: 2 } as const;
const MakeButton = { MAKE: 0, BACK: 1, NEXT: 2, EXIT: 3 } as const;

export interface GuildMakeOptions extends DockedWindowOptions { guild: Guild }

export class GuildMakeWindow extends DockedWindow<GuildMakeModel> {
  private page: number = Page.INFO;
  /** GuildMark[MARK_EDIT]: the name and the mark being made; they stay between openings. */
  private name = '';
  private readonly cells = new Array<number>(MARK_CELLS).fill(0);
  private color = 1;
  private paintButton = -1;

  constructor(private readonly options: GuildMakeOptions) {
    super('guild_make', {
      text_px: nativeTextPx(), label_line_px: 14, page: Page.INFO, title_text: 'Guild', title_px: nativeTextPx(), info_text: 'Do you wish to be the guild master?', info_px: nativeTextPx(),
      make_text: 'Create guild', back_text: 'Back', next_text: 'Next', name_text: 'NAME', result_text: '', result_px: nativeTextPx(),
      palette_hint_1: 'After selecting a color with', palette_hint_2: 'the mouse, please draw.', exit_tooltip: 'Close', guild_name: '',
      cells: [], palette: [], selected: editorCellColor(1), mark_cells: [],
      guild_mark_paint: (cell) => this.paint(cell),
      guild_mark_color: (color) => { if (this.paintButton === 0 && color >= 0 && color < 16) { this.color = color; this.options.click?.(); this.refresh(); } },
      guild_make_button: (button) => this.button(button),
    }, options);
    // A cell paints with the left button and clears with the right, on the press and on every cell
    // entered while the button stays down.
    window.addEventListener('pointerdown', (event) => { this.paintButton = event.button === 0 ? 0 : event.button === 2 ? 1 : -1; }, true);
    window.addEventListener('pointerup', () => { this.paintButton = -1; }, true);
    options.guild.onChange((what) => {
      if (what === 'masterDialog') { this.page = Page.INFO; void this.open(); }
      else if (what === 'created' && this.isOpen) this.hide();
    });
  }

  get currentPage(): number { return this.page; }

  /** Hide() without the "cancel" answer, which ClosingProcess sends anyway, as the desktop does. */
  private hide(): void { this.close(); }

  protected override closing(): void {
    if (this.page === Page.MARK) this.name = this.model.guild_name;
    this.page = Page.INFO;
    this.options.guild.answerMaster(false);
  }

  private paint(cell: number): void {
    if (this.page !== Page.MARK || cell < 0 || cell >= MARK_CELLS) return;
    if (this.paintButton === 0) this.cells[cell] = this.color;
    else if (this.paintButton === 1) this.cells[cell] = 0;
    else return;
    this.refresh();
  }

  private button(button: number): void {
    const guild = this.options.guild;
    if (this.page === Page.INFO) {
      if (button === MakeButton.MAKE) { guild.answerMaster(true); this.page = Page.MARK; this.model.guild_name = this.name; this.focusName(); }
      else if (button === MakeButton.EXIT) { this.close(); return; }
    } else if (this.page === Page.MARK) {
      const typed = this.model.guild_name.trim();
      if (button === MakeButton.BACK) { if (typed) this.name = typed; this.page = Page.INFO; }
      else if (button === MakeButton.NEXT) {
        if (/[^0-9A-Za-z]/.test(typed)) void noticeInGame('Cannot use symbols.');
        else if (typed.length < 4) void noticeInGame('Type more than 4 letters');
        else if (!this.cells.some((cell) => cell !== 0)) void noticeInGame('Please draw your guild emblem');
        else { this.name = typed; this.page = Page.RESULT; }
      } else if (button === MakeButton.EXIT) { this.close(); return; }
    } else if (this.page === Page.RESULT) {
      if (button === MakeButton.BACK) { this.page = Page.MARK; this.model.guild_name = this.name; this.focusName(); }
      else if (button === MakeButton.NEXT) { guild.create(this.name, this.cells); this.close(); return; }
      else if (button === MakeButton.EXIT) { this.close(); return; }
    }
    this.options.click?.();
    this.refresh();
  }

  private focusName(): void {
    setTimeout(() => this.doc?.$<HTMLInputElement>('#name_field')?.focus(), 0);
  }

  protected refresh(): void {
    const m = this.model;
    m.page = this.page;
    m.title_px = this.fit('Guild', 190);
    m.info_px = this.fit(m.info_text, 190);
    m.result_px = this.fit(`NAME : ${this.name}`, 190);
    m.label_line_px = Math.round(this.px * 1.2);
    m.result_text = `NAME : ${this.name}`;
    m.cells = this.cells.map(editorCellColor);
    m.palette = Array.from({ length: 16 }, (_, index) => editorCellColor(index));
    m.selected = editorCellColor(this.color);
    m.mark_cells = this.cells.map(markCellColor);
    this.doc?.update();
  }
}

// The server choice over the 3D stage (the desktop's CServerSelWin, server_select.rml in the legacy theme): the groups
// on the left, the servers of the picked group on the right with their load bars, the group's description under them.
// As the desktop: with several groups none is open at first (the only one is, owner 2026-11-28), a click on a group
// shows its servers, a click on a server picks it at once, and a full or overloaded server says so instead in the
// game's OK box (MESSAGE_SERVER_BUSY). The values are the desktop's: LoadState.h
// (100 % full, 128 % overloaded), LoadBar.cpp (ten segments of 10 %, segment k mixed k tenths of the way from the base
// color to the full color in OKLCH, all of them full color once the server is full) and Data/Local/ServerList.json
// (the button's "({vip} {pvp})" and the load texts).

import { openDocument, themeToken, type RmlDocument, type RmlModel } from './rml.js';

/** At or over this load a server is full (LoadState.h FullLoadPercent). */
export const FULL_LOAD_PERCENT = 100;
/** At or over this load a server is overloaded (LoadState.h OverloadedLoadPercent). */
export const OVERLOADED_LOAD_PERCENT = 128;
/** The load bar's segments (LoadBar.h LoadBarSegmentCount). */
export const LOAD_BAR_SEGMENTS = 10;

export type LoadState = 'open' | 'full' | 'overloaded';

export function loadState(percent: number): LoadState {
  if (percent >= OVERLOADED_LOAD_PERCENT) return 'overloaded';
  return percent >= FULL_LOAD_PERCENT ? 'full' : 'open';
}

/** One segment of a load bar: how much of it is lit (a CSS width) and its color. */
export interface LoadBarSegment { lit: string; color: string }

/** The segments of a server's load bar (LoadBar.cpp GetLoadBarSegments). */
export function loadBarSegments(percent: number, base: string, full: string): LoadBarSegment[] {
  const segments: LoadBarSegment[] = [];
  const open = loadState(percent) === 'open';
  const reached = Math.max(percent, 0) / (100 / LOAD_BAR_SEGMENTS);
  for (let index = 0; index < LOAD_BAR_SEGMENTS; index++) {
    if (!open) { segments.push({ lit: '100%', color: full }); continue; }
    const lit = Math.min(Math.max(reached - index, 0), 1);
    const mix = Math.round((index / LOAD_BAR_SEGMENTS) * 100);
    segments.push({ lit: `${Math.round(lit * 100)}%`, color: mix === 0 ? base : `color-mix(in oklch, ${base}, ${full} ${mix}%)` });
  }
  return segments;
}

/** The text a full or overloaded server shows on its bar's line (ServerList.json load.full / load.overloaded). */
export function loadText(percent: number, vi: boolean): string {
  const state = loadState(percent);
  if (state === 'full') return vi ? '(Đầy)' : '(Full)';
  if (state === 'overloaded') return vi ? 'Quá tải' : 'Overloaded';
  return '';
}

/** A server's button text: its name, then "({vip} {pvp})" when it has either (ServerList.json names.button). */
export function serverButtonName(name: string, vip: boolean, nonPvp: boolean, vi: boolean): string {
  const tags = [vip ? 'VIP' : '', nonPvp ? (vi ? 'không PvP' : 'Non-PvP') : ''].filter(Boolean);
  return tags.length ? `${name} (${tags.join(' ')})` : name;
}

/** A server of a group, as the window shows it. */
export interface ServerSelectServer<T> {
  server: T;
  name: string;
  load: number;
  vip?: boolean;
  nonPvp?: boolean;
}

/** A group of servers (a world, or a season's column when the website names no worlds). */
export interface ServerSelectGroup<T> {
  label: string;
  description: string;
  servers: ServerSelectServer<T>[];
}

export interface ServerSelectOptions<T> {
  groups: ServerSelectGroup<T>[];
  vi: boolean;
  /** A server was clicked that can be joined. */
  onPick: (server: T) => void;
  /** A full or overloaded server was clicked (the desktop's MESSAGE_SERVER_BUSY). */
  onBusy: (server: T) => void;
  /** The click sounds of the buttons (server_select.rcss: a group "select", a server "confirm"). */
  playClick?: (kind: 'select' | 'confirm') => void;
  parent?: HTMLElement;
}

export interface ServerSelectWindow {
  readonly document: RmlDocument;
  /** The index of the open group, or null. */
  readonly openGroup: number | null;
  close(): void;
}

interface ServerRow {
  label: string;
  load_text: string;
  index: number;
  color: { gray: boolean; orange: boolean; silver: boolean; style: string };
  load_color: { gray: boolean; orange: boolean; silver: boolean; style: string };
  load_bold: boolean;
  load_italic: boolean;
  load_slant: boolean;
  segments: LoadBarSegment[];
  empty_style: string;
  text_from: number;
  text_to: number;
}

/** The legacy theme's colors when its tokens can't be read (tokens.ini). */
const FALLBACK_BASE = '#00bd8b';
const FALLBACK_FULL = '#e0483e';
/** ServerList.json colors.loadRed in the legacy theme: the load texts. */
const LOAD_RED = '#FF5A5A';

/** A screen narrower than 520 px and taller than wide (a phone held upright): the stacked layout. */
export function narrowScreen(width = window.innerWidth, height = window.innerHeight): boolean {
  return width < 520 && height > width;
}

/** The stacked window's width in original pixels (its panel and padding). */
const NARROW_WIDTH = 256;

/** One original pixel of the window, as rml.ts writes the documents' rem. */
const rem = (px: number) => `calc(${px} * var(--rml-rem, 12px))`;

/** The stacked layout: the groups over the servers, each column as tall as it needs up to a height, then scrolling. */
const NARROW_CSS = `
.narrow #panel { width: ${rem(236)}; }
.narrow #columns { flex-direction: column; align-items: center; height: auto; gap: ${rem(8)}; }
.narrow #group_col { width: auto; height: auto; max-height: ${rem(124)}; justify-content: flex-start; }
.narrow #server_col { height: calc(var(--narrow-rows, 3) * ${rem(42)}); max-height: ${rem(216)}; padding: 0 ${rem(6)}; }
.narrow #desc_panel { white-space: normal; overflow-wrap: anywhere; }
`;

export async function openServerSelect<T>(options: ServerSelectOptions<T>): Promise<ServerSelectWindow> {
  const { groups, vi } = options;
  const [base, full] = await Promise.all([themeToken('server-load-base'), themeToken('server-load-full')]);
  let open: number | null = null;
  const rows = (group: ServerSelectGroup<T> | undefined): ServerRow[] => (group?.servers ?? []).map((s, index) => {
    const state = loadState(s.load);
    const plain = { gray: false, orange: false, silver: false, style: '' };
    return {
      label: serverButtonName(s.name, !!s.vip, !!s.nonPvp, vi),
      load_text: loadText(s.load, vi),
      index,
      // ServerList.json buttonColors: a VIP server orange, a PvP one without VIP gray.
      color: { ...plain, orange: !!s.vip, gray: !s.vip && s.nonPvp === false },
      load_color: { ...plain, style: LOAD_RED },
      load_bold: state !== 'open',
      load_italic: state === 'overloaded',
      load_slant: false,
      segments: loadBarSegments(s.load, base || FALLBACK_BASE, full || FALLBACK_FULL),
      empty_style: '',
      text_from: 0,
      text_to: 0,
    };
  });
  const model: RmlModel = {
    groups: groups.map((g, index) => ({ label: g.label, btn_pos: index, checked: false })),
    servers: [] as ServerRow[],
    // Every text a button can have, laid out unseen: the buttons are as wide as the longest.
    sizer_names: [...new Set(groups.flatMap((g) => g.servers.map((s) => serverButtonName(s.name, !!s.vip, !!s.nonPvp, vi))))],
    sizer_loads: [{ text: loadText(FULL_LOAD_PERCENT, vi), bold: true, italic: false }, { text: loadText(OVERLOADED_LOAD_PERCENT, vi), bold: true, italic: true }],
    server_list_visible: false,
    pvp_notice: false,
    pvp_notice_line0: '',
    pvp_notice_line1: '',
    pvp_notice_line2: '',
    description_text: '',
    serversel_select_group: (position: number) => {
      if (!groups[position]) return;
      options.playClick?.('select');
      showGroup(position);
    },
    serversel_select_server: (index: number) => {
      const entry = open === null ? undefined : groups[open]?.servers[index];
      if (!entry) return;
      options.playClick?.('confirm');
      if (loadState(entry.load) === 'open') options.onPick(entry.server);
      else options.onBusy(entry.server);
    },
  };
  const document = await openDocument('server_select', model, { parent: options.parent, sceneWidth: () => (narrowScreen() ? NARROW_WIDTH : 640) });
  // .center-both centres the panel in its document, which has no size here: in the window instead.
  const panel = document.$('#panel');
  if (panel) Object.assign(panel.style, { position: 'fixed', left: '50%', top: '50%' });
  // On a phone held upright the two columns stand one over the other, so the window can be as wide as the screen.
  const narrowStyle = window.document.createElement('style');
  narrowStyle.textContent = NARROW_CSS;
  document.root.before(narrowStyle);
  const layOut = () => document.root.classList.toggle('narrow', narrowScreen());
  layOut();
  function showGroup(position: number): void {
    const group = groups[position];
    if (!group) return;
    open = position;
    model.groups = groups.map((g, index) => ({ label: g.label, btn_pos: index, checked: index === position }));
    model.servers = rows(group);
    model.server_list_visible = true;
    // The stacked layout's server column is as tall as its rows (a row and its gap: 42 original pixels), up to a height.
    document.root.style.setProperty('--narrow-rows', String(Math.max(1, group.servers.length)));
    model.description_text = group.description;
    requestAnimationFrame(fitLoadTexts);
  }
  // The only group is open at once (owner 2026-11-28); with several, none is until one is clicked, as on the desktop.
  if (groups.length === 1) { showGroup(0); document.update(); }
  // A full or overloaded server's text hides the bar's segments under it (CServerSelWin::FitLoadTexts).
  function fitLoadTexts(): void {
    const servers = model.servers as ServerRow[];
    const shown = document.root.querySelectorAll<HTMLElement>('.server-row:not(.server-row-sizer)');
    let changed = false;
    shown.forEach((row, index) => {
      const entry = servers[index];
      const text = row.querySelector<HTMLElement>('.server-load');
      if (!entry || !text) return;
      const box = text.getBoundingClientRect();
      const covered = [...row.querySelectorAll<HTMLElement>('.server-gauge-segment')]
        .map((segment, i) => ({ i, rect: segment.getBoundingClientRect() }))
        .filter(({ rect }) => rect.right > box.left && rect.left < box.right)
        .map(({ i }) => i);
      const from = covered.length ? covered[0] : 0;
      const to = covered.length ? covered[covered.length - 1] + 1 : 0;
      if (entry.text_from !== from || entry.text_to !== to) { entry.text_from = from; entry.text_to = to; changed = true; }
    });
    if (changed) document.update();
  }
  // A rem is one original pixel at the window's scale (scene-window-scale): a new window size lays it out again.
  const onResize = () => { layOut(); document.update(); requestAnimationFrame(fitLoadTexts); };
  window.addEventListener('resize', onResize);
  return {
    document,
    get openGroup() { return open; },
    close: () => { window.removeEventListener('resize', onResize); document.close(); },
  };
}

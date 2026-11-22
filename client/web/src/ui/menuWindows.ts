// The bottom HUD's menu window (CWindowMenu, window_menu.rml, U / #btn_window) and the
// system menu (CSysMenuWin, sys_menu.rml, System (Esc) / Escape).
import { openDocument, prepareDocument, type RmlDocument, type RmlModel } from './rml.js';
import { hudScale, nativeTextPx, typographyScale } from './nativeText.js';
import { keepTypingInside } from './guildWindows.js';

interface WindowMenuRow {
  label: string;
  index: number;
}

interface WindowMenuModel extends RmlModel {
  text_px: number;
  rows: WindowMenuRow[];
  windowmenu_select: (index: number) => void;
}

// WindowMenu.cpp kMenuLabels (in order 0..5).
const MENU_LABELS = [
  'System (Esc)',
  'Help (F1)',
  'Guild (G)',
  'Move (M)',
  'Map (Tab)',
  'Gens Info (B)',
] as const;

export interface WindowMenuOptions {
  click?: () => void;
  onToggle?: (open: boolean) => void;
  onSystem: () => void;
  onHelp: () => void;
  onGuild: () => void;
  onMove: () => void;
  onMap: () => void;
  onGens: () => void;
}

/** CWindowMenu (window_menu.rml), opened by #btn_window on the bottom HUD or by U. */
export class WindowMenu {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private readonly model: WindowMenuModel;
  private readonly onResize = () => this.place();
  private readonly onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && this.isOpen) {
      this.close();
      this.options.click?.();
    }
  };

  constructor(private readonly options: WindowMenuOptions) {
    this.model = {
      text_px: nativeTextPx(),
      rows: MENU_LABELS.map((label, index) => ({ label, index })),
      windowmenu_select: (index) => this.select(index),
    };
    prepareDocument('window_menu').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', this.onResize);
    window.addEventListener('keydown', this.onKeyDown);
  }

  get isOpen(): boolean {
    return this.doc !== null;
  }

  async toggle(): Promise<void> {
    this.options.click?.();
    if (this.isOpen) this.close();
    else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  close(): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
    this.options.onToggle?.(false);
  }

  private async load(): Promise<void> {
    this.model.text_px = nativeTextPx();
    const doc = await openDocument('window_menu', this.model, { parent: document.body, scale: hudScale() });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    Object.assign(doc.host.style, {
      position: 'fixed', left: '0', top: '0', width: '100%', height: '100%', pointerEvents: 'none', zIndex: '80',
    });
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
    }
    for (const row of doc.root.querySelectorAll('.menu-row')) {
      (row as HTMLElement).style.pointerEvents = 'auto';
    }
    this.place();
    this.options.onToggle?.(true);
  }

  private place(): void {
    if (!this.doc) return;
    const scale = hudScale();
    this.doc.setScale(scale);
    this.model.text_px = nativeTextPx();
    Object.assign(this.doc.root.style, {
      width: `${window.innerWidth}px`, height: `${window.innerHeight}px`, pointerEvents: 'none',
    });
    this.doc.root.style.setProperty('--hud-scale', String(scale));
    this.doc.root.style.setProperty('--board-scale', '1');
    this.doc.update();
  }

  private select(index: number): void {
    this.options.click?.();
    this.close();
    switch (index) {
      case 0: this.options.onSystem(); break;
      case 1: this.options.onHelp(); break;
      case 2: this.options.onGuild(); break;
      case 3: this.options.onMove(); break;
      case 4: this.options.onMap(); break;
      case 5: this.options.onGens(); break;
      default:
    }
  }
}

interface SysMenuModel extends RmlModel {
  select_server_hidden: boolean;
  system_menu_label: string;
  exit_game_label: string;
  select_server_label: string;
  option_label: string;
  close_label: string;
  sysmenu_exit_game_click: () => void;
  sysmenu_select_server_click: () => void;
  sysmenu_option_click: () => void;
  sysmenu_close_click: () => void;
}

export interface SysMenuOptions {
  click?: () => void;
  onExitGame: () => void;
  onSelectServer?: () => void;
  onOptions: () => void;
}

/** CSysMenuWin (sys_menu.rml), opened from WindowMenu's "System (Esc)" or by pressing Escape when no other window is open. */
export class SysMenu {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private readonly model: SysMenuModel;
  private readonly onResize = () => this.place();

  constructor(private readonly options: SysMenuOptions) {
    this.model = {
      select_server_hidden: !options.onSelectServer,
      system_menu_label: 'System',
      exit_game_label: 'Exit Game',
      select_server_label: 'Select Server',
      option_label: 'Options',
      close_label: 'Close',
      sysmenu_exit_game_click: () => { this.options.click?.(); this.close(); this.options.onExitGame(); },
      sysmenu_select_server_click: () => { this.options.click?.(); this.close(); this.options.onSelectServer?.(); },
      sysmenu_option_click: () => { this.options.click?.(); this.close(); this.options.onOptions(); },
      sysmenu_close_click: () => { this.options.click?.(); this.close(); },
    };
    prepareDocument('sys_menu').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', this.onResize);
  }

  get isOpen(): boolean {
    return this.doc !== null;
  }

  async toggle(): Promise<void> {
    this.options.click?.();
    if (this.isOpen) this.close();
    else await this.open();
  }

  async open(): Promise<void> {
    if (this.doc) return;
    this.opening ??= this.load().finally(() => { this.opening = null; });
    await this.opening;
  }

  close(): void {
    if (!this.doc) return;
    this.doc.close();
    this.doc = null;
  }

  private async load(): Promise<void> {
    const doc = await openDocument('sys_menu', this.model, { parent: document.body });
    if (this.doc) { doc.close(); return; }
    this.doc = doc;
    Object.assign(doc.host.style, {
      position: 'fixed', left: '0', top: '0', width: '100%', height: '100%', pointerEvents: 'none', zIndex: '92',
    });
    const backdrop = doc.$('#backdrop');
    if (backdrop) {
      backdrop.style.pointerEvents = 'auto';
      backdrop.addEventListener('pointerdown', (event) => event.stopPropagation());
    }
    const panel = doc.$('#panel');
    if (panel) {
      panel.style.pointerEvents = 'auto';
      panel.addEventListener('pointerdown', (event) => event.stopPropagation());
    }
    keepTypingInside(doc.host);
    this.place();
  }

  private place(): void {
    if (!this.doc) return;
    // scene-window-scale sets 1rem = 1 original px * typographyScale(); labels stay at nativeTextPx().
    const rem = `${typographyScale().toFixed(3)}px`;
    this.doc.setScale(1);
    this.doc.root.style.setProperty('--rml-rem', rem);
    const panel = this.doc.$('#panel');
    if (panel) {
      panel.style.setProperty('--rml-rem', rem);
      panel.style.fontSize = `${nativeTextPx()}px`;
    }
    this.doc.update();
  }
}

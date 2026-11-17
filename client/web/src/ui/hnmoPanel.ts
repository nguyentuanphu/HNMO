// Shared gold-framed dark modal panel for HNMO game windows (reuses game typography and i18n).

import { onLanguageChange } from '../i18n.js';

const CSS = `
.hnmo-modal {
  position: fixed;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  width: min(640px, 94vw);
  max-height: 82vh;
  display: flex;
  flex-direction: column;
  background: rgba(11, 13, 22, 0.96);
  border: 1px solid #8a6a30;
  border-radius: 10px;
  box-shadow: 0 18px 48px rgba(0, 0, 0, 0.72);
  color: #e6e8ef;
  font: 13px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif;
  z-index: 65;
  pointer-events: auto;
}
.hnmo-modal[hidden] { display: none !important; }
.hnmo-modal-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  border-bottom: 1px solid rgba(201, 162, 74, 0.35);
  background: linear-gradient(180deg, rgba(42, 34, 18, 0.75), rgba(16, 18, 28, 0.75));
  border-radius: 9px 9px 0 0;
}
.hnmo-modal-title { margin: 0; font-size: 15px; font-weight: 700; color: #f1cf7a; }
.hnmo-modal-close {
  border: 1px solid rgba(201, 162, 74, 0.45);
  background: rgba(0, 0, 0, 0.45);
  color: #ffe9a8;
  border-radius: 5px;
  padding: 2px 9px;
  cursor: pointer;
  font: inherit;
}
.hnmo-modal-close:hover { background: rgba(80, 60, 20, 0.65); }
.hnmo-modal-body {
  padding: 12px 14px;
  overflow-y: auto;
  display: grid;
  gap: 10px;
}
.hnmo-card {
  background: rgba(255, 255, 255, 0.035);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 8px;
  padding: 10px;
  display: grid;
  gap: 6px;
}
.hnmo-btn {
  border: 1px solid #8a6a30;
  background: rgba(34, 28, 16, 0.85);
  color: #ffe9a8;
  border-radius: 5px;
  padding: 4px 10px;
  cursor: pointer;
  font: inherit;
}
.hnmo-btn:hover:not(:disabled) { background: rgba(90, 68, 22, 0.85); }
.hnmo-btn:disabled { opacity: 0.45; cursor: default; }
.hnmo-btn-primary {
  border-color: #c9a24a;
  background: #c9a24a;
  color: #171206;
  font-weight: 700;
}
.hnmo-muted { color: #9aa0b2; font-size: 12px; }
.hnmo-ok { color: #7cc98a; }
.hnmo-warn { color: #f1cf7a; }
.hnmo-err { color: #ef7a6c; }
.hnmo-bar { height: 8px; border-radius: 999px; background: rgba(255, 255, 255, 0.12); overflow: hidden; }
.hnmo-bar > div { height: 100%; background: linear-gradient(90deg, #a88232, #f1cf7a); transition: width 0.2s; }
`;

let stylesInjected = false;

function ensureStyles(): void {
  if (stylesInjected) return;
  stylesInjected = true;
  const style = document.createElement('style');
  style.textContent = CSS;
  document.head.append(style);
}

/** Shared modal frame wrapper for HNMO feature windows. */
export class HnmoModalPanel {
  readonly root: HTMLDivElement;
  readonly body: HTMLDivElement;
  private readonly titleEl: HTMLHeadingElement;
  private openState = false;
  private onCloseCallback?: () => void;

  constructor(id: string, title = '', onRender?: () => void) {
    ensureStyles();
    this.root = document.createElement('div');
    this.root.className = 'hnmo-modal';
    this.root.id = id;
    this.root.hidden = true;
    this.root.setAttribute('role', 'dialog');

    const head = document.createElement('div');
    head.className = 'hnmo-modal-head';
    this.titleEl = document.createElement('h2');
    this.titleEl.className = 'hnmo-modal-title';
    this.titleEl.textContent = title;

    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'hnmo-modal-close';
    closeBtn.textContent = '×';
    closeBtn.addEventListener('click', () => this.close());

    head.append(this.titleEl, closeBtn);
    this.body = document.createElement('div');
    this.body.className = 'hnmo-modal-body';
    this.root.append(head, this.body);
    document.body.append(this.root);

    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.openState) {
        event.preventDefault();
        this.close();
      }
    });

    if (onRender) {
      onLanguageChange(() => onRender());
    }
  }

  get isOpen(): boolean {
    return this.openState;
  }

  setTitle(title: string): void {
    this.titleEl.textContent = title;
  }

  onClose(callback: () => void): void {
    this.onCloseCallback = callback;
  }

  open(): void {
    this.openState = true;
    this.root.hidden = false;
  }

  close(): void {
    if (!this.openState) return;
    this.openState = false;
    this.root.hidden = true;
    this.onCloseCallback?.();
  }
}

const PANEL_STYLE_ID = 'hnmo-panel-style';

function ensurePanelStyle(): void {
  if (document.getElementById(PANEL_STYLE_ID)) return;
  const style = document.createElement('style');
  style.id = PANEL_STYLE_ID;
  style.textContent = `
    .hnmo-panel-backdrop {
      position: fixed; inset: 0; z-index: 65; display: flex; align-items: center; justify-content: center;
      background: rgba(6, 7, 10, 0.62); pointer-events: auto; font-family: system-ui, -apple-system, sans-serif;
    }
    .hnmo-panel {
      width: min(680px, calc(100vw - 24px)); max-height: calc(100vh - 36px);
      display: flex; flex-direction: column;
      background: linear-gradient(180deg, #1b1712 0%, #110f0c 100%);
      border: 1px solid #9b7a3c; box-shadow: 0 12px 40px rgba(0,0,0,0.75);
      color: #efe6d2; border-radius: 6px; overflow: hidden;
    }
    .hnmo-panel-head {
      display: flex; align-items: center; justify-content: space-between; gap: 8px;
      padding: 10px 14px; background: #262016; border-bottom: 1px solid #5e4b28;
    }
    .hnmo-panel-title { font-size: 16px; font-weight: 700; color: #f2cf78; margin: 0; }
    .hnmo-panel-close {
      background: #3a2e1d; border: 1px solid #7f6435; color: #efe6d2;
      border-radius: 4px; width: 28px; height: 28px; cursor: pointer; font-size: 15px; line-height: 1;
    }
    .hnmo-panel-close:hover { background: #524026; }
    .hnmo-panel-body {
      padding: 12px 14px; overflow-y: auto; display: flex; flex-direction: column; gap: 10px; font-size: 13px;
    }
    .hnmo-tabs { display: flex; gap: 6px; flex-wrap: wrap; border-bottom: 1px solid #453720; padding-bottom: 8px; }
    .hnmo-tab {
      background: #241e15; border: 1px solid #5e4b28; color: #d6c9ae; padding: 5px 11px;
      border-radius: 4px; cursor: pointer; font-size: 12.5px;
    }
    .hnmo-tab.active { background: #6b5122; color: #fff4d2; border-color: #d4a64a; font-weight: 600; }
    .hnmo-row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .hnmo-row .hnmo-card, .hnmo-panel .hnmo-card {
      background: rgba(0, 0, 0, 0.34); border: 1px solid #493b23; border-radius: 4px; padding: 8px 10px;
      display: flex; align-items: center; justify-content: space-between; gap: 10px;
    }
    .hnmo-input, .hnmo-select {
      background: #14110c; border: 1px solid #69532e; color: #efe6d2; border-radius: 4px;
      padding: 5px 8px; font-size: 13px;
    }
    .hnmo-panel .hnmo-btn {
      background: #4e3b1b; border: 1px solid #b58d41; color: #fff2ce; border-radius: 4px;
      padding: 5px 10px; cursor: pointer; font-size: 12.5px; font-weight: 600;
    }
    .hnmo-panel .hnmo-btn:hover { background: #694f23; }
    .hnmo-btn.danger { background: #4a1f1c; border-color: #b44b42; }
    .hnmo-btn.danger:hover { background: #692c27; }
    .hnmo-panel .hnmo-muted { color: #b2a489; font-size: 12px; }
    .hnmo-badge {
      display: inline-block; padding: 1px 6px; border-radius: 3px; font-size: 11px;
      background: #332919; border: 1px solid #755b2f; color: #f2cf78;
    }
  `;
  document.head.appendChild(style);
}

/** Lightweight overlay panel contract for interactive HNMO windows. */
export interface HnmoPanel {
  readonly backdrop: HTMLDivElement;
  readonly panel: HTMLDivElement;
  readonly titleEl: HTMLHeadingElement;
  readonly body: HTMLDivElement;
  open: () => void;
  close: () => void;
  isOpen: () => boolean;
  setTitle: (text: string) => void;
}

/** Creates an overlay dialog panel attached to document.body. */
export function createHnmoPanel(id: string, title: string, onClose?: () => void): HnmoPanel {
  ensurePanelStyle();
  const backdrop = document.createElement('div');
  backdrop.className = 'hnmo-panel-backdrop';
  backdrop.id = id;
  backdrop.style.display = 'none';

  const panel = document.createElement('div');
  panel.className = 'hnmo-panel';
  panel.setAttribute('role', 'dialog');
  panel.setAttribute('aria-modal', 'true');

  const head = document.createElement('div');
  head.className = 'hnmo-panel-head';

  const titleEl = document.createElement('h2');
  titleEl.className = 'hnmo-panel-title';
  titleEl.textContent = title;

  const closeBtn = document.createElement('button');
  closeBtn.type = 'button';
  closeBtn.className = 'hnmo-panel-close';
  closeBtn.textContent = '×';

  const body = document.createElement('div');
  body.className = 'hnmo-panel-body';

  head.append(titleEl, closeBtn);
  panel.append(head, body);
  backdrop.append(panel);
  document.body.appendChild(backdrop);

  const close = () => {
    if (backdrop.style.display === 'none') return;
    backdrop.style.display = 'none';
    onClose?.();
  };

  closeBtn.addEventListener('click', close);
  backdrop.addEventListener('click', (e) => {
    if (e.target === backdrop) close();
  });
  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && backdrop.style.display !== 'none') {
      e.stopPropagation();
      close();
    }
  });

  return {
    backdrop,
    panel,
    titleEl,
    body,
    open: () => { backdrop.style.display = 'flex'; },
    close,
    isOpen: () => backdrop.style.display !== 'none',
    setTitle: (text: string) => { titleEl.textContent = text; },
  };
}

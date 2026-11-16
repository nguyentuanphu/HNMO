// Shared gold-framed dark modal panel for HNMO game windows (reuses game typography and i18n).

import { onLanguageChange } from '../i18n.js';

const CSS = `
.hnmo-modal {
  position: fixed;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  width: min(560px, 94vw);
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

// Shared lightweight HNMO overlay panel helper.

const STYLE_ID = 'hnmo-panel-style';

function ensureStyle(): void {
  if (document.getElementById(STYLE_ID)) return;
  const style = document.createElement('style');
  style.id = STYLE_ID;
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
    .hnmo-card {
      background: rgba(0, 0, 0, 0.34); border: 1px solid #493b23; border-radius: 4px; padding: 8px 10px;
      display: flex; align-items: center; justify-content: space-between; gap: 10px;
    }
    .hnmo-muted { color: #b2a489; font-size: 12px; }
  `;
  document.head.appendChild(style);
}

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

export function createHnmoPanel(id: string, title: string, onClose?: () => void): HnmoPanel {
  ensureStyle();
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

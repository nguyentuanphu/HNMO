// Shared steps of the browser tests: logging in like a player and reading the game's state.
import { expect, type Page } from '@playwright/test';

// What the tests read of the page's debug object (window.__game, see src/main.ts).
export interface GameView {
  inventory: {
    items: Map<number, { group: number; number: number; width: number; height: number }>;
    vault: Map<number, { group: number; number: number; width: number; height: number }>;
    money: number;
    openWindow: number | null;
    fits(storage: Map<number, unknown>, grid: { firstSlot: number; columns: number; rows: number }, slot: number, item: unknown, ignoreSlot: number | null): boolean;
  };
  hero: { name: string; cell: { x: number; y: number }; group: { uuid: string } };
  online: { characters: Map<number, { id: number; name: string; kind: string; cell: { x: number; y: number } }>; say(text: string): void; talkTo(character: unknown): void };
  itemWindows: { toggle(): Promise<void> };
  mapNumber: number;
}

export const BAG = { firstSlot: 12, columns: 8, rows: 8 };
export const VAULT = { firstSlot: 0, columns: 8, rows: 15 };
const CELL = 20;
const PANEL_WIDTH = 190;

// Logs in, picks the first server and the first character, and waits until the world is drawn.
export async function enterWorld(page: Page, account: string): Promise<void> {
  await page.goto('/');
  await page.fill('#username', account);
  await page.fill('#password', account);
  await page.click('button.primary');
  await page.waitForSelector('#servers .choice, #characters .choice', { timeout: 60_000 });
  if (await page.isVisible('#servers .choice')) await page.click('#servers .choice >> nth=0');
  await page.waitForSelector('#characters .choice', { timeout: 60_000 });
  await page.click('#characters .choice >> nth=0');
  await page.waitForFunction(() => (window as unknown as { __viewerReady?: boolean }).__viewerReady, null, { timeout: 180_000 });
}

export async function openInventory(page: Page): Promise<void> {
  await page.keyboard.press('i');
  await page.waitForFunction(() => document.querySelector('.rml-my_inventory'), null, { timeout: 90_000 });
}

export function bagSlots(page: Page): Promise<number[]> {
  return page.evaluate(() => [...(window as unknown as { __game: GameView }).__game.inventory.items.keys()].sort((a, b) => a - b));
}

// The screen point of a reference point (x, y) of a window's panel (my_inventory or storage).
export async function panelPoint(page: Page, documentName: string, x: number, y: number): Promise<{ x: number; y: number }> {
  return page.evaluate(([name, px, py]) => {
    const panel = document.querySelector(`.rml-${name}`)?.shadowRoot?.querySelector('#panel');
    if (!panel) throw new Error(`window ${name} is not open`);
    const rect = panel.getBoundingClientRect();
    const scale = rect.width / 190;
    return { x: rect.left + Number(px) * scale, y: rect.top + Number(py) * scale };
  }, [documentName, x, y] as const);
}

// The centre of an item at a grid slot, in screen pixels.
export function gridSlotPoint(page: Page, documentName: string, grid: { firstSlot: number; columns: number }, gridLeft: number, gridTop: number, slot: number, size = { width: 1, height: 1 }) {
  const index = slot - grid.firstSlot;
  const x = gridLeft + (index % grid.columns) * CELL + size.width * CELL / 2;
  const y = gridTop + Math.floor(index / grid.columns) * CELL + size.height * CELL / 2;
  return panelPoint(page, documentName, x, y);
}

export const BAG_GRID_ORIGIN = { left: 15, top: 200 };   // my_inventory.rcss #item_grid
export const VAULT_GRID_ORIGIN = { left: 15, top: 36 };  // storage.rcss #item_grid
export const HELM_SLOT_CENTRE = { x: 75 + 23, y: 44 + 23 }; // my_inventory.rcss #slot_helm

// Clicks like a player: move there first, then click.
export async function clickAt(page: Page, point: { x: number; y: number }): Promise<void> {
  await page.mouse.move(point.x, point.y, { steps: 4 });
  await page.mouse.click(point.x, point.y);
}

export async function expectNoPageErrors(errors: string[]): Promise<void> {
  expect(errors, errors.join('\n')).toEqual([]);
}

export { PANEL_WIDTH };

import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

test('vault and trade zen use promptInGame instead of window.prompt', () => {
  const itemWindowsSrc = fs.readFileSync(path.resolve('src/ui/itemWindows.ts'), 'utf8');
  const tradeWindowSrc = fs.readFileSync(path.resolve('src/ui/tradeWindow.ts'), 'utf8');
  assert.equal(itemWindowsSrc.includes('window.prompt('), false);
  assert.equal(itemWindowsSrc.includes('promptInGame('), true);
  assert.equal(tradeWindowSrc.includes('window.prompt('), false);
  assert.equal(tradeWindowSrc.includes('promptInGame('), true);
});

test('Alt key toggles alwaysShowNames in main.ts', () => {
  const mainSrc = fs.readFileSync(path.resolve('src/main.ts'), 'utf8');
  assert.equal(mainSrc.includes('alwaysShowNames = !alwaysShowNames'), true);
});

test('Ultimate HUD has minimize/restore toggle', () => {
  const uapSrc = fs.readFileSync(path.resolve('src/ui/ultimateAutoPlayWindow.ts'), 'utf8');
  assert.equal(uapSrc.includes('data-uap-hud="minimize"'), true);
  assert.equal(uapSrc.includes('this.hudMinimized = !this.hudMinimized'), true);
});

test('AdminPanel ShopWorkspace makes catalogue item icons bigger', () => {
  const css = fs.readFileSync(path.resolve('../../server/src/Web/AdminPanel/Components/ShopWorkspace.razor.css'), 'utf8');
  assert.match(css, /\.picker-add img\s*\{\s*width:\s*64px;\s*height:\s*64px;/);
});

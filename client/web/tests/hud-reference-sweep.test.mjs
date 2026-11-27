// Unit tests for Part 8 Item 6 HUD reference sweep fixes:
//  - Hud poisoned HP state (eDeBuff_Poison), chainfo_alert / friend_alert dots, and [data-hint] -> title localization
//  - Ultimate AutoPlay HUD clearance above 54dp * hudScale() bottom strip
//  - i18n data-hint attribute support
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const web = path.resolve(__dirname, '..');

function installDom() {
  const created = [];
  class FakeElement {
    constructor(tag) {
      this.tagName = tag.toUpperCase();
      this.style = { setProperty: () => {} };
      this.dataset = {};
      this.attrs = new Map();
      this.children = [];
      this.listeners = new Map();
      this.title = '';
      this.innerHTML = '';
    }
    setAttribute(k, v) { this.attrs.set(k, String(v)); }
    getAttribute(k) { return this.attrs.get(k) ?? null; }
    addEventListener(type, fn) {
      const list = this.listeners.get(type) ?? [];
      list.push(fn);
      this.listeners.set(type, list);
    }
    append(...nodes) { this.children.push(...nodes); }
    querySelector() { return null; }
    querySelectorAll(sel) {
      if (sel === '[data-hint]') return this.children.filter((c) => c.attrs?.has('data-hint'));
      return [];
    }
    getBoundingClientRect() { return { width: 1280, height: 720, left: 0, top: 0, right: 200, bottom: 30 }; }
    closest() { return null; }
  }
  globalThis.Element = FakeElement;
  globalThis.localStorage = { getItem: () => null, setItem() {}, removeItem() {} };
  globalThis.window = {
    innerWidth: 1280,
    innerHeight: 720,
    devicePixelRatio: 1,
    addEventListener() {},
  };
  globalThis.document = {
    documentElement: { style: { setProperty() {} } },
    body: new FakeElement('body'),
    createElement: (tag) => { const el = new FakeElement(tag); created.push(el); return el; },
  };
  globalThis.requestAnimationFrame = (cb) => { void cb; return 1; };
  return { FakeElement, created };
}

test('Hud tracks poisoned state, Character/Friend alert dots, and i18n includes data-hint', async () => {
  installDom();
  const { Hud } = await import(new URL('../public/js/ui/hud.js', import.meta.url).href);
  const { CharacterStats } = await import(new URL('../public/js/game/stats.js', import.meta.url).href);
  const stats = new CharacterStats({ onPacket: () => () => {} }, 400, {
    CurrentExperience: 0, ExperienceForNextLevel: 1000, LevelUpPoints: 5,
    Strength: 100, Agility: 100, Vitality: 100, Energy: 100, Leadership: 0,
    CurrentHealth: 100, MaximumHealth: 100, CurrentMana: 50, MaximumMana: 50,
    CurrentShield: 50, MaximumShield: 50, CurrentAbility: 20, MaximumAbility: 20,
    Money: 0, AttackSpeed: 10, MagicSpeed: 10, MaximumAttackSpeed: 100, Resets: 0,
    UsedFruitPoints: 0, MaxFruitPoints: 0, UsedNegativeFruitPoints: 0, MaxNegativeFruitPoints: 0, InventoryExtensions: 0,
  });
  const hud = new Hud(stats, { inventory: () => {} });

  // Access model state through internal fields verified by unit test contract
  assert.equal(hud.model.poisoned, false);
  hud.setPoisoned(true);
  assert.equal(hud.model.poisoned, true);
  hud.setPoisoned(false);
  assert.equal(hud.model.poisoned, false);

  assert.equal(hud.model.chainfo_alert, false);
  assert.equal(hud.model.friend_alert, false);
  hud.setAlertStates({ character: true, friends: true });
  assert.equal(hud.model.chainfo_alert, true);
  assert.equal(hud.model.friend_alert, true);
  hud.setAlertStates({ character: false });
  assert.equal(hud.model.chainfo_alert, false);
  assert.equal(hud.model.friend_alert, true);

  const i18nSrc = fs.readFileSync(path.join(web, 'src/i18n.ts'), 'utf8');
  assert.match(i18nSrc, /ATTRIBUTES\s*=\s*\[[^\]]*'data-hint'/);
});

test('HUD reference sweep wires SOUND_HEART below 20% life, dockedRight for BuffStrip/PartyList, and dynamic Ultimate HUD clearance', () => {
  const mainSrc = fs.readFileSync(path.join(web, 'src/main.ts'), 'utf8');
  assert.match(mainSrc, /setPoisoned\(has\(Buff\.POISON\)\)/);
  assert.match(mainSrc, /sounds\.play\('SOUND_HEART'\)/);
  assert.match(mainSrc, /dockedRight:\s*dockedRightPx/);
  assert.match(mainSrc, /syncFriendAlert/);

  const uapSrc = fs.readFileSync(path.join(web, 'src/ui/ultimateAutoPlayWindow.ts'), 'utf8');
  assert.match(uapSrc, /54 \* hudScale\(\) \+ 8/);

  const hudSrc = fs.readFileSync(path.join(web, 'src/ui/hud.ts'), 'utf8');
  assert.match(hudSrc, /el\.title = translate\(hint\) \?\? hint/);

  const autoPlayBarSrc = fs.readFileSync(path.join(web, 'src/ui/autoPlayBar.ts'), 'utf8');
  assert.match(autoPlayBarSrc, /el\.title = translate\(hint\) \?\? hint/);

  const bannerSrc = fs.readFileSync(path.join(web, 'src/ui/mapNameBanner.ts'), 'utf8');
  assert.match(bannerSrc, /this\.model\.language = currentLanguage\(\)/);
});

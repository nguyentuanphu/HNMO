// Every skill of every class, cast for real at a monster: is it castable, does the server answer with damage, how
// many effect objects it makes, and the frame times while it plays. Usage: node skillsweep.mjs <account> <slot> <label>
import fs from 'node:fs';
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-t110/client/web/package.json');
const { chromium } = require('playwright');
const [ACCOUNT, SLOT, LABEL] = [process.argv[2] ?? 'testgm', Number(process.argv[3] ?? 0), process.argv[4] ?? 'run'];
const OUT = `${process.env.HNMO_WORK || 'C:/hnmo-work'}/skillsweep/${LABEL}`; fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ headless: true, args: ['--use-angle=d3d11', '--ignore-gpu-blocklist'] });
const p = await b.newPage({ viewport: { width: 1280, height: 720 } });
const errors = []; p.on('pageerror', (e) => errors.push(e.message));
const rows = [];
const WAYPOINTS = [{ x: 176, y: 126 }, { x: 190, y: 128 }, { x: 205, y: 140 }, { x: 200, y: 160 }, { x: 185, y: 175 }, { x: 165, y: 185 }, { x: 150, y: 200 }, { x: 205, y: 110 }, { x: 215, y: 90 }];
let wp = 0;
try {
  await p.goto('http://127.0.0.1:8081/index.html');
  await p.fill('#username', ACCOUNT); await p.fill('#password', ACCOUNT); await p.click('button.primary');
  await p.waitForSelector('.season-column .choice, #servers .choice', { timeout: 120000 });
  await p.click('.season-column .choice >> nth=0').catch(() => p.click('#servers .choice >> nth=0'));
  await p.waitForSelector('#characters .choice', { timeout: 120000 });
  await p.click(`#characters .choice >> nth=${SLOT}`);
  await p.waitForFunction(() => window.__viewerReady, null, { timeout: 240000 });
  await p.waitForTimeout(4000);
  const who = await p.evaluate(() => ({ name: window.__game.hero.name, map: window.__game.mapNumber, season: document.documentElement.dataset.season ?? null }));
  console.log('hero', JSON.stringify(who));
  await p.evaluate(() => window.__game.onboarding?.close());
  if (who.map !== 0) { await p.evaluate(() => window.__game.online.say('/move lorencia')); await p.waitForFunction(() => window.__game.mapNumber === 0 && !window.__game.online.changingMap, null, { timeout: 90000 }); }
  await p.waitForTimeout(3000);
  // Through Lorencia's fields until monsters are in view.
  for (const goal of [{ x: 176, y: 126 }, { x: 190, y: 128 }, { x: 205, y: 140 }, { x: 200, y: 160 }, { x: 185, y: 175 }, { x: 165, y: 185 }, { x: 150, y: 200 }, { x: 205, y: 110 }, { x: 215, y: 90 }]) {
    await p.evaluate((g) => window.__game.online.walkTo(g), goal);
    await p.waitForFunction(() => !window.__game.online.goal && !window.__game.online.hero.isWalking, null, { timeout: 90000 }).catch(() => {});
    const seen = await p.evaluate(() => [...window.__game.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead).length);
    if (seen >= 3) break;
  }
  console.log('at field', JSON.stringify(await p.evaluate(() => ({ cell: { ...window.__game.hero.cell }, map: window.__game.mapNumber, monsters: [...window.__game.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead).length }))));
  await p.screenshot({ path: OUT + '/field.png' });
  // Hooks: damage shown, and frame times.
  await p.evaluate(() => {
    const g = window.__game;
    window.__dmg = 0;
    const ev = g.combat.events; const before = ev.onDamage;
    ev.onDamage = (t, texts) => { window.__dmg++; before?.(t, texts); };
    window.__frames = [];
    let last = performance.now();
    const loop = (now) => { window.__frames.push(now - last); last = now; requestAnimationFrame(loop); };
    requestAnimationFrame(loop);
  });
  const skills = await p.evaluate(async () => {
    const { skillInfo } = await import('/js/game/skills.js');
    const g = window.__game; const list = [];
    for (let i = 0; i < 120; i++) { const s = g.skills.skillAt(i); if (s && !list.some((x) => x.n === s)) list.push({ n: s, name: skillInfo(s)?.name ?? '' }); }
    return list;
  });
  console.log('skills', skills.length);
  for (const skill of skills) {
    // A monster in view (the GM kills them fast): on through the fields' waypoints until one shows.
    for (let tries = 0; tries < 12; tries++) {
      const seen = await p.evaluate(() => [...window.__game.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead).length);
      if (seen > 0) break;
      const goal = WAYPOINTS[(wp++) % WAYPOINTS.length];
      await p.evaluate((g) => window.__game.online.walkTo(g), goal);
      await p.waitForFunction(() => !window.__game.online.goal && !window.__game.online.hero.isWalking, null, { timeout: 60000 }).catch(() => {});
      await p.waitForTimeout(1500);
    }
    // Next to a living monster first (a skill's range is short; walking there took longer than the watch).
    await p.evaluate(() => {
      const g = window.__game; const hero = g.online.hero;
      const m = [...g.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead)
        .sort((a, b) => Math.hypot(a.cell.x - hero.cell.x, a.cell.y - hero.cell.y) - Math.hypot(b.cell.x - hero.cell.x, b.cell.y - hero.cell.y))[0];
      if (m && Math.hypot(m.cell.x - hero.cell.x, m.cell.y - hero.cell.y) > 2) { const n = g.online.cellNextTo(m.cell); if (n) g.online.walkTo(n); }
    });
    await p.waitForFunction(() => !window.__game.online.goal && !window.__game.online.hero.isWalking, null, { timeout: 20000 }).catch(() => {});
    const row = await p.evaluate(async (n) => {
      const g = window.__game;
      const hero = g.online.hero;
      const monsters = [...g.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead);
      monsters.sort((a, b) => Math.hypot(a.cell.x - hero.cell.x, a.cell.y - hero.cell.y) - Math.hypot(b.cell.x - hero.cell.x, b.cell.y - hero.cell.y));
      const target = monsters[0] ?? null;
      const dist = target ? Math.round(Math.hypot(target.cell.x - hero.cell.x, target.cell.y - hero.cell.y)) : null;
      window.__dmg = 0; window.__frames = [];
      const sum = () => { const c = g.effects?.counts?.() ?? { effects: 0, particles: 0, joints: 0, sprites: 0 }; return c.effects + c.particles + c.joints + c.sprites; };
      const base = sum(); const baseKinds = { ...(g.effects?.census?.() ?? {}) };
      const cast = g.combat.cast(target, target ? { ...target.cell } : { ...hero.cell }, n);
      let peak = 0; const kinds = new Set();
      const t0 = performance.now();
      while (performance.now() - t0 < 3500) {
        await new Promise((r) => setTimeout(r, 100));
        peak = Math.max(peak, sum() - base);
        for (const [k, v] of Object.entries(g.effects?.census?.() ?? {})) if (v > (baseKinds[k] ?? 0)) kinds.add(k);
      }
      const f = window.__frames.slice(1).sort((a, b) => a - b);
      const p95 = f.length ? Math.round(f[Math.floor(f.length * 0.95)]) : null;
      const max = f.length ? Math.round(f[f.length - 1]) : null;
      return { cast, target: target?.name ?? null, dist, damage: window.__dmg, effectPeak: peak, kinds: [...kinds].slice(0, 6), p95, max, mana: g.stats?.mana ?? null };
    }, skill.n);
    rows.push({ ...skill, ...row });
    console.log(JSON.stringify({ ...skill, ...row }));
    if (row.cast && row.effectPeak > 0 && rows.filter((r) => r.shot).length < 40) {
      // a picture of the skill's effect was taken mid-cast is too late here; the next cast shows the look again
    }
    await p.waitForTimeout(800);
  }
} catch (e) { console.log('STOPPED', e.message.split('\n')[0]); await p.screenshot({ path: `${OUT}/stopped.png` }).catch(() => {}); }
fs.writeFileSync(`${OUT}/rows.json`, JSON.stringify({ rows, errors }, null, 1));
console.log('errors', JSON.stringify(errors.slice(0, 5)));
await b.close();

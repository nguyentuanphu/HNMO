// Casts the given skills at a monster next to the hero and counts the effect objects each cast creates (every
// createEffect / createParticle / createSprite / createJoint call, not the net count, so effects fading from the
// cast before do not hide new ones), whether the cast's effect hook ran, the damage shown, and the frame times.
// Usage: node fxprobe.mjs <account> <slot> <label> <skill,skill,...> [port]
import fs from 'node:fs';
import { createRequire } from 'node:module';
const require = createRequire('C:/Users/nguye/.gemini/antigravity/scratch/HNMO-fx/client/web/package.json');
const { chromium } = require('playwright');
const [ACCOUNT, SLOT, LABEL, SKILLS, PORT] = [process.argv[2], Number(process.argv[3]), process.argv[4], process.argv[5].split(',').map(Number), process.argv[6] ?? '8083'];
const OUT = `${process.env.HNMO_WORK || 'C:/hnmo-work'}/skillsweep/${LABEL}`; fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ headless: true, args: ['--use-angle=d3d11', '--ignore-gpu-blocklist'] });
const p = await b.newPage({ viewport: { width: 1280, height: 720 } });
const errors = []; p.on('pageerror', (e) => errors.push(e.message));
const rows = [];
const WAYPOINTS = [{ x: 176, y: 126 }, { x: 190, y: 128 }, { x: 205, y: 140 }, { x: 200, y: 160 }, { x: 185, y: 175 }, { x: 165, y: 185 }, { x: 150, y: 200 }, { x: 205, y: 110 }, { x: 215, y: 90 }];
let wp = 0;
const living = () => p.evaluate(() => [...window.__game.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead).length);
const idle = (t = 60000) => p.waitForFunction(() => !window.__game.online.goal && !window.__game.online.hero.isWalking, null, { timeout: t }).catch(() => {});
try {
  await p.goto(`http://127.0.0.1:${PORT}/index.html`);
  await p.fill('#username', ACCOUNT); await p.fill('#password', ACCOUNT); await p.click('button.primary');
  await p.waitForSelector('.season-column .choice, #servers .choice', { timeout: 120000 });
  await p.click('.season-column .choice >> nth=0').catch(() => p.click('#servers .choice >> nth=0'));
  await p.waitForSelector('#characters .choice', { timeout: 120000 });
  await p.click(`#characters .choice >> nth=${SLOT}`);
  await p.waitForFunction(() => window.__viewerReady, null, { timeout: 240000 });
  await p.waitForTimeout(4000);
  await p.evaluate(() => window.__game.onboarding?.close());
  const who = await p.evaluate(() => ({ name: window.__game.hero.name, map: window.__game.mapNumber }));
  console.log('hero', JSON.stringify(who));
  if (who.map !== 0) { await p.evaluate(() => window.__game.online.say('/move lorencia')); await p.waitForFunction(() => window.__game.mapNumber === 0 && !window.__game.online.changingMap, null, { timeout: 90000 }); }
  await p.waitForTimeout(3000);
  for (const goal of WAYPOINTS) {
    await p.evaluate((g) => window.__game.online.walkTo(g), goal); await idle(90000);
    if (await living() >= 3) break;
  }
  await p.evaluate(() => {
    const g = window.__game;
    window.__dmg = 0; window.__made = {}; window.__skillEvents = [];
    const ev = g.combat.events;
    const onDamage = ev.onDamage; ev.onDamage = (t, texts) => { if (t !== g.online.hero) window.__dmg++; onDamage?.(t, texts); };
    const onSkill = ev.onSkill; ev.onSkill = (c, s, t, cell) => { window.__skillEvents.push({ s, at: performance.now(), hero: c === g.online.hero }); onSkill?.(c, s, t, cell); };
    for (const m of ['createEffect', 'createParticle', 'createSprite', 'createJoint']) {
      const f = g.effects[m].bind(g.effects);
      g.effects[m] = (kind, ...rest) => { const k = m.replace('create', '').toLowerCase() + ':' + kind; window.__made[k] = (window.__made[k] ?? 0) + 1; return f(kind, ...rest); };
    }
    window.__frames = []; let last = performance.now();
    const loop = (now) => { window.__frames.push(now - last); last = now; requestAnimationFrame(loop); }; requestAnimationFrame(loop);
  });
  await p.evaluate(async () => { const sc = await import('/js/game/skillCast.js'); const sk = await import('/js/game/skills.js'); const g = window.__game;
    window.__plan = (n) => sc.skillCastPlan(n, g.combat.classIndex, g.combat.castContext()); window.__skillInfo = (n) => sk.skillInfo(n); });
  const hands = await p.evaluate(() => { const i = window.__game.inventory?.items; const h = (x) => (x ? `${x.group}:${x.number}` : null); return i ? { right: h(i.get(0)), left: h(i.get(1)) } : null; });
  console.log('hands', JSON.stringify(hands), 'helper', await p.evaluate(() => { const x = window.__game.inventory.items.get(8); return x ? `${x.group}:${x.number}` : null; }));
  // UNMOUNT=1: the helper (slot 8) goes to a free bag slot for the casts and back afterwards (a horn mount blocks
  // some Rage Fighter skills, CMonkSystem::IsRideNotUseSkill).
  let helperSlot = null;
  if (process.env.UNMOUNT === '1') {
    helperSlot = await p.evaluate(() => { const inv = window.__game.inventory; const h = inv.items.get(8); if (!h) return null; const s = inv.firstEmptySlot(inv.items, { firstSlot: 12, columns: 8, rows: 8 }, h); if (s === null) return null; inv.move(0, 8, 0, s); return s; });
    await p.waitForTimeout(2500);
    console.log('unmounted to', helperSlot, 'helper now', await p.evaluate(() => window.__game.inventory.items.has(8)));
  }
  for (const n of SKILLS) {
    for (let tries = 0; tries < 12 && (await living()) === 0; tries++) {
      await p.evaluate((g) => window.__game.online.walkTo(g), WAYPOINTS[(wp++) % WAYPOINTS.length]); await idle(); await p.waitForTimeout(1500);
    }
    await p.evaluate(() => {
      const g = window.__game; const hero = g.online.hero;
      const m = [...g.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead)
        .sort((a, b) => Math.hypot(a.cell.x - hero.cell.x, a.cell.y - hero.cell.y) - Math.hypot(b.cell.x - hero.cell.x, b.cell.y - hero.cell.y))[0];
      if (m && Math.hypot(m.cell.x - hero.cell.x, m.cell.y - hero.cell.y) > 2) { const c = g.online.cellNextTo(m.cell); if (c) g.online.walkTo(c); }
    });
    await idle(20000);
    await p.evaluate(() => { window.__made = {}; window.__ambientFrom = performance.now(); });
    await p.waitForTimeout(1500); // the cast before fades, the hero's last action ends; the ambient rate is measured
    const start = await p.evaluate((n) => {
      const g = window.__game; const hero = g.online.hero;
      const monsters = [...g.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead)
        .sort((a, b) => Math.hypot(a.cell.x - hero.cell.x, a.cell.y - hero.cell.y) - Math.hypot(b.cell.x - hero.cell.x, b.cell.y - hero.cell.y));
      const target = monsters[0] ?? null;
      window.__dmg = 0; window.__skillEvents = []; window.__frames = [];
      const castable = g.combat.castable(n);
      const plan = window.__plan(n); const info = window.__skillInfo(n);
      window.__dbg = { plan: plan && { packet: plan.packet, special: plan.special, equipmentOk: plan.equipmentOk, needsTarget: plan.needsTarget, heroAction: plan.heroAction },
        info: info && { mana: info.mana, ag: info.ability, distance: info.distance, delay: info.delay }, mana: g.combat.skills?.mana(), ag: g.combat.skills?.ability(), safe: g.combat.inSafeZone() };
      // what the fields made in the last 1.5 s with no cast: the ambient rate per kind
      window.__ambient = { ...window.__made }; window.__ambientMs = performance.now() - window.__ambientFrom;
      window.__made = {}; window.__castFrom = performance.now();
      const cast = g.combat.cast(target, target ? { ...target.cell } : { ...hero.cell }, n);
      return { castable, cast, target: target?.name ?? null, dist: target ? Math.round(Math.hypot(target.cell.x - hero.cell.x, target.cell.y - hero.cell.y)) : null };
    }, n);
    const SHOTS = [300, 700, 1100];
    let shot = 0;
    const t0 = Date.now();
    while (Date.now() - t0 < 3500) {
      await p.waitForTimeout(50);
      if (shot < SHOTS.length) {
        const since = await p.evaluate(() => { const e = window.__skillEvents.find((x) => x.hero); return e ? performance.now() - e.at : -1; });
        if (since >= SHOTS[shot]) { await p.screenshot({ path: `${OUT}/${n}-${SHOTS[shot]}.png` }); shot++; }
      }
    }
    if (!shot) await p.screenshot({ path: `${OUT}/${n}-none.png` });
    const row = await p.evaluate(() => {
      // The cast's own: per kind, what was made beyond the ambient rate over the same time (kinds the idle fields
      // never made count in full).
      const scale = (performance.now() - window.__castFrom) / Math.max(1, window.__ambientMs);
      const made = {};
      for (const [k, v] of Object.entries(window.__made)) { const net = Math.round(v - (window.__ambient[k] ?? 0) * scale * 1.5); if (net > 0) made[k] = net; }
      const total = Object.values(made).reduce((a, b) => a + b, 0);
      const f = window.__frames.slice(1).sort((a, b) => a - b);
      return { onSkill: window.__skillEvents.filter((e) => e.hero).map((e) => e.s), effects: total, made, damage: window.__dmg,
        p95: f.length ? Math.round(f[Math.floor(f.length * 0.95)]) : null, max: f.length ? Math.round(f[f.length - 1]) : null, casting: window.__game.combat.casting, dbg: window.__dbg };
    });
    const full = { n, ...start, ...row };
    rows.push(full); console.log(JSON.stringify(full));
    await p.evaluate(() => window.__game.combat.cancel());
  }
  if (helperSlot !== null) {
    await p.evaluate((s) => window.__game.inventory.move(0, s, 0, 8), helperSlot);
    await p.waitForTimeout(2500);
    console.log('helper back', await p.evaluate(() => window.__game.inventory.items.has(8)));
  }
} catch (e) { console.log('STOPPED', e.message.split('\n')[0]); await p.screenshot({ path: `${OUT}/stopped.png` }).catch(() => {}); }
fs.writeFileSync(`${OUT}/rows.json`, JSON.stringify({ rows, errors }, null, 1));
console.log('errors', JSON.stringify(errors.slice(0, 5)));
await b.close();

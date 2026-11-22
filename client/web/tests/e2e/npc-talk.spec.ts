// Exhaustive NPC interaction sweep across every Season 6 (and Season 9 when active) map using
// docs/audit/data/probes/mapcheck/ spawns: warps to each NPC, clicks its 3D model on #view, and
// asserts it either opens its window/dialog or displays NPC dialog/notice text like the desktop.
import fs from 'node:fs';
import path from 'node:path';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors, dismissOnboarding } from './helpers.js';

const REPO_ROOT = path.resolve(process.cwd(), '../..');
const MAPCHECK_DIR = path.join(REPO_ROOT, 'docs/audit/data/probes/mapcheck');

interface AuditRow { number: number; name: string; kind: string }
interface SpawnEntry { npc: number; x: number; y: number }
interface MapSpawns { file: string; spawns: SpawnEntry[] }

interface TargetNpc {
  mapId: number;
  mapFile: string;
  npcNumber: number;
  name: string;
  x: number;
  y: number;
}

// Instance/event-only maps where NPCs only exist during a scheduled instance run
const INSTANCE_ONLY_MAPS = new Set([11, 12, 13, 14, 15, 16, 17, 24, 25, 26, 27, 28, 29, 36, 45, 46, 47, 48, 49, 50, 52]);

function collectMapGroups(): { mapId: number; mapFile: string; npcs: TargetNpc[] }[] {
  const auditS6 = JSON.parse(fs.readFileSync(path.join(MAPCHECK_DIR, 'audit-s6.json'), 'utf8')) as { rows: AuditRow[] };
  const spawns = JSON.parse(fs.readFileSync(path.join(MAPCHECK_DIR, 'spawns.json'), 'utf8')) as Record<string, MapSpawns>;
  const npcNames = new Map<number, string>();
  for (const row of auditS6.rows) {
    if (row.kind === 'npc') npcNames.set(row.number, row.name || `NPC #${row.number}`);
  }
  const groups: { mapId: number; mapFile: string; npcs: TargetNpc[] }[] = [];
  for (const [mapKey, entry] of Object.entries(spawns)) {
    const mapId = Number(mapKey);
    if (INSTANCE_ONLY_MAPS.has(mapId)) continue;
    const seen = new Set<number>();
    const npcs: TargetNpc[] = [];
    for (const s of entry.spawns) {
      if (!npcNames.has(s.npc) || seen.has(s.npc)) continue;
      seen.add(s.npc);
      npcs.push({ mapId, mapFile: entry.file, npcNumber: s.npc, name: npcNames.get(s.npc)!, x: s.x, y: s.y });
    }
    if (npcs.length > 0) groups.push({ mapId, mapFile: entry.file, npcs });
  }
  return groups;
}

async function closeAnyOpenedUi(page: Page): Promise<void> {
  await page.evaluate(() => {
    const HUD_CHROME = new Set([
      'rml-main_frame', 'rml-system_log', 'rml-buff_strip', 'rml-mu_helper_bar',
      'rml-chat_log', 'rml-chat_input', 'rml-hnmo_onboarding', 'rml-map_name',
    ]);
    for (const host of document.querySelectorAll('.rml-document')) {
      const cls = [...host.classList].find((c) => c.startsWith('rml-') && c !== 'rml-document') ?? '';
      if (HUD_CHROME.has(cls)) continue;
      for (const btnSel of ['#gcd_btn_cancel', '#btn_exit', '#btn_close', '#close_bar', '.close', '#igs_close', '#frame_corner_close']) {
        host.shadowRoot?.querySelector<HTMLElement>(btnSel)?.click();
      }
    }
    for (const id of ['#hnmo-teleport-npc-panel button', '#hnmo-market-panel .close', '#hnmo-npc-dialog button']) {
      document.querySelector<HTMLElement>(id)?.click();
    }
    const g = (window as unknown as {
      __game?: {
        itemWindows?: { closeAll(): void };
        online?: { connection: { send(dir: string, name: string, fields: Record<string, unknown>): void } };
      };
    }).__game;
    g?.itemWindows?.closeAll();
    g?.online?.connection?.send('ClientToServer', 'CloseNpcRequest', {});
  });
  // If any non-HUD window is still open, Escape closes it cleanly without opening SysMenu.
  for (let i = 0; i < 3; i++) {
    const stillOpen = await visibleUiSelectors(page);
    if (stillOpen.length === 0) break;
    await page.keyboard.press('Escape');
    await page.waitForTimeout(150);
  }
}

async function visibleUiSelectors(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const HUD_CHROME = new Set([
      'rml-main_frame', 'rml-system_log', 'rml-buff_strip', 'rml-mu_helper_bar',
      'rml-chat_log', 'rml-chat_input', 'rml-hnmo_onboarding', 'rml-map_name',
    ]);
    const out: string[] = [];
    for (const host of document.querySelectorAll('.rml-document')) {
      const panel = host.shadowRoot?.querySelector<HTMLElement>('#panel');
      if (panel && panel.offsetWidth > 0 && panel.offsetHeight > 0) {
        const cls = [...host.classList].find((c) => c.startsWith('rml-') && c !== 'rml-document') ?? host.className;
        if (!HUD_CHROME.has(cls)) out.push(cls);
      }
    }
    for (const id of ['#hnmo-teleport-npc-panel', '#hnmo-market-panel', '#hnmo-npc-dialog']) {
      const el = document.querySelector<HTMLElement>(id);
      if (el && el.offsetWidth > 0 && el.offsetHeight > 0) out.push(id);
    }
    return out;
  });
}

async function systemLogLines(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const g = (window as unknown as {
      __game?: { chat?: { systemHistory?: { text: string }[] } };
    }).__game;
    const hist = g?.chat?.systemHistory;
    if (Array.isArray(hist)) return hist.map((m) => m.text.trim());
    const host = document.querySelector('.rml-system_log');
    return (host?.shadowRoot?.textContent ?? '').split('\n').map((s) => s.trim()).filter(Boolean);
  });
}

async function moveNear(page: Page, mapId: number, x: number, y: number): Promise<boolean> {
  // Always warp to (mapId, x, y) or a nearby walkable offset, waiting until map + distance match.
  const sameMap = await page.evaluate((m) => (window as unknown as { __game: { mapNumber: number | null } }).__game.mapNumber === m, mapId);
  await page.evaluate(({ m, tx, ty, same }) => {
    const g = (window as unknown as {
      __game: {
        hero: { name: string };
        online: { say(t: string): void };
      };
    }).__game;
    if (same) g.online.say(`/teleport ${tx} ${ty}`);
    else g.online.say(`/move ${g.hero.name} ${m} ${tx} ${ty}`);
  }, { m: mapId, tx: x, ty: y, same: sameMap });

  return expect.poll(
    () => page.evaluate(({ m, tx, ty }) => {
      const g = (window as unknown as {
        __game: {
          mapNumber: number | null;
          online?: { hero: { cell: { x: number; y: number } } };
        };
      }).__game;
      if (g.mapNumber !== m || !g.online) return false;
      return Math.hypot(g.online.hero.cell.x - tx, g.online.hero.cell.y - ty) <= 6;
    }, { m: mapId, tx: x, ty: y }),
    { timeout: 15_000 },
  ).toBe(true).then(() => true, () => false);
}

for (const group of collectMapGroups()) {
  test(`NPC clicks on map ${group.mapId} (${group.mapFile})`, async ({ page }) => {
    test.setTimeout(5 * 60_000);
    const errors: string[] = [];
    page.on('pageerror', (e) => errors.push(e.message));

    await enterWorld(page, 'testgm');
    await dismissOnboarding(page);

    await page.evaluate(() => {
      const w = window as unknown as {
        __npcProbe?: { talked: number[]; packets: string[] };
        __game: {
          online: {
            onTalk?: (npc: { monsterNumber: number | null }) => void;
            connection: { onPacket(cb: (bytes: Uint8Array) => void): () => void };
          };
        };
      };
      const probe = { talked: [] as number[], packets: [] as string[] };
      w.__npcProbe = probe;
      const prevOnTalk = w.__game.online.onTalk;
      w.__game.online.onTalk = (npc) => {
        probe.talked.push(npc.monsterNumber ?? -1);
        prevOnTalk?.(npc);
      };
      w.__game.online.connection.onPacket((bytes) => {
        const long = bytes[0] === 0xc2 || bytes[0] === 0xc4;
        const code = bytes[long ? 3 : 2];
        const sub = bytes[long ? 4 : 3];
        if ([0x01, 0x0d, 0x30, 0x31, 0x54, 0x55, 0xa1, 0xa2, 0xf6, 0xf9, 0xbc, 0xbf, 0xd1].includes(code)) {
          probe.packets.push(`${bytes[0].toString(16)}:${code.toString(16)}:${sub?.toString(16) ?? ''}`);
        }
      });
    });

    const failures: string[] = [];
    for (const t of group.npcs) {
      await closeAnyOpenedUi(page);
      await dismissOnboarding(page);

      const sysBefore = await systemLogLines(page);
      const before = await page.evaluate(() => {
        const w = window as unknown as {
          __npcProbe: { talked: number[]; packets: string[] };
          __game: { chat: { entries: { text: string }[] } };
        };
        return {
          talked: w.__npcProbe.talked.length,
          packets: w.__npcProbe.packets.length,
          chat: w.__game.chat.entries.length,
        };
      });

      const moved = await moveNear(page, t.mapId, t.x, t.y);
      if (!moved) {
        const cur = await page.evaluate(() => {
          const g = (window as unknown as { __game: { mapNumber: number | null; online?: { hero: { cell: { x: number; y: number } } } } }).__game;
          return { map: g.mapNumber, cell: g.online?.hero.cell };
        });
        console.log(`WARP_FAIL map=${t.mapId} npc=${t.npcNumber} (${t.name}) goal=(${t.x},${t.y}) cur=${JSON.stringify(cur)}`);
        failures.push(`WARP_FAIL map=${t.mapId} npc=${t.npcNumber} (${t.name})`);
        continue;
      }

      const foundNpc = await page.waitForFunction((npcNum: number) => {
        const g = (window as unknown as {
          __game: { online: { characters: Map<number, { kind: string; monsterNumber: number | null }> } };
        }).__game;
        return [...g.online.characters.values()].some((c) => c.kind === 'npc' && c.monsterNumber === npcNum);
      }, t.npcNumber, { timeout: 3_000 }).then(() => true, () => false);

      if (!foundNpc) {
        console.log(`SKIP_UNSPAWNED map=${t.mapId} npc=${t.npcNumber} (${t.name}) @(${t.x},${t.y})`);
        continue;
      }

      await page.waitForTimeout(300);

      const pt = await page.evaluate(async (npcNum: number) => {
        const THREE = await import('three');
        const g = (window as unknown as {
          __game: {
            camera: InstanceType<typeof THREE.PerspectiveCamera>;
            online: {
              hero: { cell: { x: number; y: number } };
              characters: Map<number, {
                id: number; kind: string; monsterNumber: number | null; cell: { x: number; y: number };
                pickBounds(box: InstanceType<typeof THREE.Box3>): InstanceType<typeof THREE.Box3>;
              }>;
            };
          };
        }).__game;
        const canvas = document.querySelector('#view') as HTMLCanvasElement | null;
        if (!canvas) return { ok: false, reason: 'no_canvas' as const };
        const rect = canvas.getBoundingClientRect();
        const npc = [...g.online.characters.values()].find((c) => c.kind === 'npc' && c.monsterNumber === npcNum);
        if (!npc) return { ok: false, reason: 'missing' as const };
        const box = new THREE.Box3();
        npc.pickBounds(box);
        const valid = !box.isEmpty() && [box.min.x, box.min.y, box.min.z, box.max.x, box.max.y, box.max.z].every(Number.isFinite);
        if (!valid) return { ok: false, reason: 'invalid_bounds' as const, cell: npc.cell, hero: g.online.hero.cell };
        const center = box.getCenter(new THREE.Vector3()).project(g.camera);
        if (center.z < -1 || center.z > 1) return { ok: false, reason: 'off_camera' as const, cell: npc.cell, hero: g.online.hero.cell };
        return {
          ok: true,
          x: rect.left + ((center.x + 1) / 2) * rect.width,
          y: rect.top + ((1 - center.y) / 2) * rect.height,
          cell: npc.cell,
          hero: g.online.hero.cell,
        };
      }, t.npcNumber);

      if (!pt.ok) {
        const line = `NO_CLICK_BOX(${pt.reason}) map=${t.mapId} npc=${t.npcNumber} (${t.name})`;
        console.log(line);
        failures.push(line);
        continue;
      }

      await page.mouse.click(pt.x!, pt.y!);
      // Wait for walk-to-talk + server response + potential first-time RML document load.
      await page.waitForFunction((prevTalked: number) => {
        const w = window as unknown as {
          __npcProbe: { talked: number[] };
          __game: { online: { hero: { isWalking: boolean }; talkTarget: unknown } };
        };
        return w.__npcProbe.talked.length > prevTalked && !w.__game.online.hero.isWalking && !w.__game.online.talkTarget;
      }, before.talked, { timeout: 5_000 }).catch(() => {});

      await expect.poll(async () => {
        const ui = (await visibleUiSelectors(page)).filter((s) => s !== 'rml-my_inventory');
        const sysNow = (await systemLogLines(page)).slice(sysBefore.length).filter((s) => !/entrance is open and closes in/i.test(s));
        const chatLen = await page.evaluate((b) => (window as unknown as { __game: { chat: { entries: unknown[] } } }).__game.chat.entries.length > b, before.chat);
        return ui.length > 0 || sysNow.length > 0 || chatLen;
      }, { timeout: 10_000 }).toBe(true).catch(() => {});

      const after = await page.evaluate((b) => {
        const w = window as unknown as {
          __npcProbe: { talked: number[]; packets: string[] };
          __game: { chat: { entries: { text: string }[] } };
        };
        return {
          talked: w.__npcProbe.talked.slice(b.talked),
          packets: w.__npcProbe.packets.slice(b.packets),
          chat: w.__game.chat.entries.slice(b.chat).map((e) => e.text.trim()).filter(Boolean),
        };
      }, before);

      const sysAdded = (await systemLogLines(page)).slice(sysBefore.length).filter((s) => !/entrance is open and closes in/i.test(s));
      const opened = (await visibleUiSelectors(page)).filter((s) => s !== 'rml-my_inventory');
      const hasUi = opened.length > 0 || sysAdded.length > 0 || after.chat.length > 0;
      const line = `map=${t.mapId} npc=${t.npcNumber} (${t.name}) talked=${JSON.stringify(after.talked)} pkts=[${after.packets.join(',')}] ui=[${opened.join(',')}] sys=${JSON.stringify(sysAdded)}`;
      console.log((after.talked.length > 0 && hasUi ? 'OK:   ' : 'DEAD: ') + line);
      if (after.talked.length === 0 || !hasUi) failures.push(line);
    }

    expect(failures, `NPC click failures on map ${group.mapId}:\n${failures.join('\n')}`).toEqual([]);
    await expectNoPageErrors(errors);
  });
}

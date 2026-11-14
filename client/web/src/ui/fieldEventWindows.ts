// Kalima, Crywolf and the castle siege in the game, wired with one call from main.ts (setUpFieldEvents):
//   - Kalima (game/kalima.ts): the gates a thrown Lost Map opens (the summons, C2 1F) with their
//     "<owner> .N to Kalima" label, the Kalima Ticket's refusals, the Lost Map's sound;
//   - Crywolf (game/crywolf.ts): the HUD (crywolf.rml: altars, dark elves, Balgass, the time, the statue's
//     shield, the ready-state notices, the result), the altar contracts, the map's occupation (terrain
//     light and attributes) and its music;
//   - the castle siege (game/castleSiege.ts, ui/castleSiegeWindows.ts): the guardsman, the senior, the
//     gatekeeper, the gate switch, the catapult, the siege HUD, the crown notices, the gates' cells, the
//     battle's terrain, the life stones' build bars and the owner's mark on the castle flag.

import * as THREE from 'three';
import { Crywolf, CRYWOLF_MAP, type CrywolfView } from '../game/crywolf.js';
import { BATTLE_CASTLE_MAP, CastleSiege } from '../game/castleSiege.js';
import { gateLabel, hellasTooltipLines, isKalimaGate, kalimaTicketRefused, KalimaText, LOST_MAP, summonName } from '../game/kalima.js';
import { GuildStatus, type Guild } from '../game/guild.js';
import { MixType } from '../game/mixRecipes.js';
import { createMonsterCharacter } from '../game/characters.js';
import { read } from '../net/packets.js';
import { fetchBytes } from '../bmd.js';
import { parseTerrainAttributes, TerrainFlag } from '../formats.js';
import { loadLightMap, type TerrainMesh } from '../terrain.js';
import { CatapultWindow, GatekeeperWindow, GateSwitchWindow, GuardWindow, SeniorWindow, SiegeHud } from './castleSiegeWindows.js';
import { centreScreen, FieldDialogs, ProgressBox, screenScale, syncImages, TEXT_PX } from './fieldEventDocs.js';
import { openDocument, prepareDocument, type RmlDocument, type RmlModel } from './rml.js';
import type { Connection } from '../net/connection.js';
import type { OnlineWorld } from '../game/online.js';
import type { Character } from '../game/characters.js';
import type { Inventory, Item } from '../game/inventory.js';
import type { MapAudioState } from '../audio/index.js';

const TERRAIN_SIZE = 256;
const PET_SLOT = 8;
const MOUNTS = [[13, 2], [13, 3], [13, 37]]; // Horn of Uniria, of Dinorant, of Fenrir
const HEALING_FEMALE1 = 238, AT_HEALING1 = 130;

// ---------- the Crywolf HUD ----------

type CrywolfModel = RmlModel & CrywolfView & { normal_text_px: number; bold_text_px: number; bold_line_px: number };

/** CCryWolf's document on the HUD board (layer depth 10, no pointer events). */
export class CrywolfHud {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  readonly model: CrywolfModel = { ...Crywolf.emptyView(), normal_text_px: TEXT_PX, bold_text_px: TEXT_PX, bold_line_px: 14 };

  constructor() {
    prepareDocument('crywolf').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.place());
  }

  get shown(): boolean { return this.doc !== null; }

  show(view: CrywolfView): void {
    if (!view.hud_visible && !view.result_visible) { this.doc?.close(); this.doc = null; return; }
    Object.assign(this.model, view);
    const px = Math.round(TEXT_PX * screenScale());
    this.model.normal_text_px = this.model.bold_text_px = px;
    this.model.bold_line_px = Math.round(14 * screenScale());
    if (this.doc) { this.doc.update(); syncImages(this.doc); return; }
    this.opening ??= openDocument('crywolf', this.model, { parent: document.body }).then((doc) => {
      this.doc = doc;
      doc.host.style.zIndex = '40';
      doc.host.style.pointerEvents = 'none';
      this.place();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  private place(): void {
    if (!this.doc) return;
    centreScreen(this.doc);
    this.doc.update();
    syncImages(this.doc);
  }
}

// ---------- labels and bars over objects in the world ----------

const STYLE = `
.label.kalimaGate { color: rgb(255, 230, 200); background: rgb(100, 0, 0); font-weight: bold; }
.hnmo-build-bar { position: fixed; z-index: 9; width: 50px; height: 2px; background: rgba(40, 40, 40, .8); pointer-events: none; transform: translate(-50%, 12px); }
.hnmo-build-bar > div { height: 100%; background: rgb(255, 200, 50); }
`;

/** The web game's own world and audio pieces the field events change. */
export interface FieldEventsOptions {
  connection: Connection;
  online: OnlineWorld;
  hero: () => { level: number; classIndex: number };
  inventory: Inventory;
  mapNumber: () => number | null;
  world: () => { mapNumber: number; worldNumber: number; flags: Uint16Array; terrain: TerrainMesh } | null;
  worldGroup: () => THREE.Object3D | null;
  camera: THREE.Camera;
  canvas: HTMLCanvasElement;
  audio: { play: (id: string) => void };
  /** The music rule's event states (GameSounds.musicStates). */
  music: (() => Partial<MapAudioState>)[];
  message: (text: string) => void;
  safeZone: () => boolean;
  /** HideAllGroupA: the inventory, character, party and NPC windows close. */
  closeOthers: () => void;
  bag: { interceptRightClick?: (slot: number, item: Item) => boolean };
  guild: () => Guild | null;
  /** The senior's store tab: the castle senior's mix (MIXTYPE_CASTLE_SENIOR). */
  openMix: (type: MixType) => void;
}

export interface FieldEvents {
  /** The Lost Map's and Symbol of Kundun's tooltip lines (RenderHellasItemInfo). */
  kalimaTooltip: typeof hellasTooltipLines;
  crywolf: Crywolf;
  crywolfHud: CrywolfHud;
  siege: CastleSiege;
  dialogs: FieldDialogs;
  progress: ProgressBox;
  guard: GuardWindow;
  senior: SeniorWindow;
  gatekeeper: GatekeeperWindow;
  gateSwitch: GateSwitchWindow;
  catapult: CatapultWindow;
  siegeHud: SiegeHud;
}

interface SummonPacket {
  SummonedMonsters: { Id: number; TypeNumber: number; CurrentPositionX: number; CurrentPositionY: number; TargetPositionX: number; TargetPositionY: number; Rotation: number; OwnerCharacterName: string }[];
}

function lightTexture(light: Float32Array): THREE.DataTexture {
  const bytes = new Uint8Array(TERRAIN_SIZE * TERRAIN_SIZE * 4);
  for (let i = 0; i < TERRAIN_SIZE * TERRAIN_SIZE; i++) {
    bytes[i * 4] = light[i * 3] * 255; bytes[i * 4 + 1] = light[i * 3 + 1] * 255; bytes[i * 4 + 2] = light[i * 3 + 2] * 255; bytes[i * 4 + 3] = 255;
  }
  const texture = new THREE.DataTexture(bytes, TERRAIN_SIZE, TERRAIN_SIZE, THREE.RGBAFormat);
  texture.magFilter = texture.minFilter = THREE.LinearFilter;
  texture.needsUpdate = true;
  return texture;
}

// CreateCastleMark's palette (the guild mark colours, alpha in the high byte, red in the low one).
const MARK_COLORS = [0x00000000, 0xff000000, 0xff808080, 0xffffffff, 0xff0000ff, 0xff0080ff, 0xff00ffff, 0xff00ff80,
  0xff00ff00, 0xff80ff00, 0xffffff00, 0xffff8000, 0xffff0000, 0xffff0080, 0xffff00ff, 0xff8000ff];

/** CreateCastleMark: the flag's art, the mark's 32 x 32 in its middle. */
function castleMarkTexture(cells: number[], width: number, height: number): THREE.DataTexture {
  const bytes = new Uint8Array(width * height * 4);
  const put = (at: number, argb: number) => { bytes[at] = argb & 0xff; bytes[at + 1] = (argb >>> 8) & 0xff; bytes[at + 2] = (argb >>> 16) & 0xff; bytes[at + 3] = (argb >>> 24) & 0xff; };
  for (let i = 0; i < height; i++) {
    for (let j = 0; j < width; j++) {
      const at = (i * width + j) * 4;
      const mx = j - (width / 2 - 16), my = i - (height / 2 - 16);
      if (mx >= 0 && mx < 32 && my >= 0 && my < 32) put(at, MARK_COLORS[cells[Math.floor(mx / 4) + Math.floor(my / 4) * 8] ?? 0]);
      else if (j < 3 || j > width - 4 || i < 10 || i > height - 10) put(at, (0xff << 24) + (Math.floor(50 + i / 100 * 160) << 8) + Math.floor(50 + i / 100 * 255));
      else put(at, (0xff << 24) + (i << 16) + (i << 8) + i);
    }
  }
  const texture = new THREE.DataTexture(bytes, width, height, THREE.RGBAFormat);
  texture.needsUpdate = true;
  return texture;
}

export function setUpFieldEvents(options: FieldEventsOptions): FieldEvents {
  const { connection, online } = options;
  const click = () => options.audio.play('SOUND_CLICK01');
  const style = document.createElement('style');
  style.textContent = STYLE;
  document.head.append(style);
  const dialogs = new FieldDialogs(click);
  const progress = new ProgressBox();

  // ---- Kalima ----
  connection.onPacket((bytes) => {
    if (bytes[0] === 0xc2 && bytes[3] === 0x1f) {
      // ReceiveCreateSummonViewport: a Kalima gate (CreateHellGate) or another summon, owner in its name.
      let packet: SummonPacket;
      try { packet = read<SummonPacket>('ServerToClient', 'AddSummonedMonstersToScope', bytes); } catch (error) { console.warn('Summons', error); return; }
      for (const summon of packet.SummonedMonsters ?? []) {
        const id = summon.Id & 0x7fff, type = summon.TypeNumber;
        void online.spawn(id, async () => {
          const character = await createMonsterCharacter({ id, type });
          character.name = summonName(type, character.name, summon.OwnerCharacterName);
          if (isKalimaGate(type)) {
            // Not a target (the desktop's summons are players to the mouse): walking into it passes it.
            character.kind = 'kalimaGate';
            character.name = gateLabel(character.name, type);
          }
          return character;
        }, { x: summon.CurrentPositionX, y: summon.CurrentPositionY, targetX: summon.TargetPositionX, targetY: summon.TargetPositionY, rotation: summon.Rotation });
      }
    } else if (bytes[0] === 0xc1 && bytes[2] === 0xf3 && bytes[3] === 0x14 && bytes.length > 5) {
      // ReceiveModifyItemExtended: a Lost Map (or the item POTION + 111) made in the bag.
      const item = options.inventory.items.get(bytes[4]);
      if (item && item.group === LOST_MAP.group && (item.number === LOST_MAP.number || item.number === 111)) options.audio.play('SOUND_KUNDUN_ITEM_SOUND');
    }
  });
  const previousRightClick = options.bag.interceptRightClick;
  options.bag.interceptRightClick = (slot, item) => {
    if (previousRightClick?.(slot, item)) return true;
    if (slot >= 12 && kalimaTicketRefused(item, options.safeZone(), options.mapNumber())) { options.message(KalimaText.CanTBeUsedInTheSafeZone); return true; }
    return false;
  };

  // ---- the terrain an event state loads ----
  const applied = new WeakMap<object, string>();
  const loadTerrain = async (world: NonNullable<ReturnType<FieldEventsOptions['world']>>, key: string, attribute: string, light: string | null) => {
    try {
      const [bytes, map] = await Promise.all([fetchBytes(`/data/World${world.worldNumber}/${attribute}`), light ? loadLightMap(`World${world.worldNumber}/${light}`) : Promise.resolve(null)]);
      if (applied.get(world) !== key) return;
      world.flags.set(parseTerrainAttributes(bytes));
      if (map) world.terrain.material.uniforms.lightMap.value = lightTexture(map);
    } catch (error) { console.warn('Event terrain', error); }
  };

  // ---- Crywolf ----
  const mounted = () => { const pet = options.inventory.items.get(PET_SLOT); return !!pet && MOUNTS.some(([g, n]) => pet.group === g && pet.number === n); };
  const crywolf = new Crywolf(connection, {
    mapNumber: options.mapNumber,
    hero: () => ({ ...options.hero(), mounted: mounted() }),
    ok: (lines) => dialogs.ok(lines),
    ask: (lines) => dialogs.ask(lines),
    contractPose: () => {
      const hero = online.hero;
      hero.playOnce(HEALING_FEMALE1);
      const degrees = ((hero.angle * 180) / Math.PI + 360) % 360;
      connection.send('ClientToServer', 'AnimationRequest', { Rotation: Math.floor((degrees + 22.5) / 360 * 8 + 1) % 8, AnimationNumber: AT_HEALING1 });
    },
    applyOccupation: () => undefined, // applied by the frame loop once the map is loaded
    play: options.audio.play,
  });
  crywolf.onResultBox = (lines) => { void dialogs.ok(lines); };
  const crywolfHud = new CrywolfHud();
  options.music.push(() => ({ crywolfState: crywolf.musicState }));

  // ---- the castle siege ----
  const objectOf = (id: number) => online.characters.get(id) ?? null;
  const heroGuild = () => {
    const guild = options.guild();
    const mark = guild?.heroMark ?? null;
    return { status: guild?.status ?? GuildStatus.NONE, name: mark?.name ?? '', alliance: mark?.allianceName ?? '' };
  };
  let guard: GuardWindow | null = null, senior: SeniorWindow | null = null, gatekeeper: GatekeeperWindow | null = null;
  let gateSwitch: GateSwitchWindow | null = null, catapult: CatapultWindow | null = null;
  const siege = new CastleSiege(connection, {
    mapNumber: options.mapNumber,
    message: options.message,
    notice: options.message,
    ok: (lines) => { void dialogs.ok(lines); },
    progress: (lines, ms) => progress.show(lines, ms),
    clearProgress: () => progress.close(),
    object: (id) => { const c = objectOf(id); return c ? { cell: c.cell, name: c.name, monsterNumber: c.monsterNumber } : null; },
    setNoMove: (x, y, width, height, add) => {
      const world = options.world();
      if (!world) return;
      for (let j = 0; j < height; j++) for (let i = 0; i < width; i++) {
        const at = (y + j) * TERRAIN_SIZE + x + i;
        world.flags[at] = add ? world.flags[at] | TerrainFlag.NOMOVE : world.flags[at] & ~TerrainFlag.NOMOVE;
      }
    },
    gateMoved: () => options.audio.play('SOUND_BC_GATE_OPEN'),
    catapultFired: (id) => {
      if (id !== null) objectOf(id)?.playOnce(1);
      options.audio.play('SOUND_BC_CATAPULT_ATTACK');
    },
    battleTerrain: () => undefined, // applied by the frame loop
    open: (which) => {
      if (which === 'guard') guard?.open();
      else if (which === 'senior') senior?.open();
      else if (which === 'gatekeeper') gatekeeper?.open();
      else if (which === 'gateSwitch') gateSwitch?.open();
      else catapult?.open();
    },
    ownerMark: () => { flagApplied = null; },
    taxRate: () => undefined,
  });
  const windowOptions = {
    siege, dialogs, click,
    closeNpc: () => { connection.send('ClientToServer', 'CloseNpcRequest', {}); options.inventory.openWindow = null; },
    closeCrafting: () => { connection.send('ClientToServer', 'CraftingDialogCloseRequest', {}); options.inventory.openWindow = null; },
    closeOthers: () => { options.closeOthers(); for (const w of [guard, senior, gatekeeper, gateSwitch, catapult]) w?.window.close(false); },
    hero: () => { const g = heroGuild(); return { ...options.hero(), money: options.inventory.money, guildStatus: g.status, guildName: g.name, allianceName: g.alliance }; },
    bag: () => options.inventory.items,
    openStore: () => { options.openMix(MixType.CASTLE_SENIOR); return true; },
  };
  guard = new GuardWindow(windowOptions);
  senior = new SeniorWindow(windowOptions);
  gatekeeper = new GatekeeperWindow(windowOptions);
  gateSwitch = new GateSwitchWindow(windowOptions);
  catapult = new CatapultWindow(windowOptions);
  const siegeHud = new SiegeHud({
    siege, click,
    hero: () => online.hero.cell,
    others: () => [...online.characters.values()].filter((c) => !c.dead).map((c) => c.cell),
    commander: () => {
      const g = heroGuild();
      return siege.formation !== 0 && g.status === GuildStatus.MASTER && (g.alliance === '' || g.alliance === g.name);
    },
  });
  options.music.push(() => ({ battleCastleStarted: siege.battleStarted }));

  // The altars: an elf's click during the battle contracts instead of talking (MOVEMENT_TALK).
  const previousTalk = online.beforeTalk;
  online.beforeTalk = (npc) => previousTalk?.(npc) === true || crywolf.talk(npc);

  // ---- every frame ----
  let flagApplied: object | null = null;
  let mapOfSiege: number | null = null;
  const bars = new Map<number, HTMLDivElement>();
  const box = new THREE.Box3();
  const screenOf = (character: Character, above: number): { x: number; y: number } | null => {
    box.setFromObject(character.group);
    if (box.isEmpty()) return null;
    const point = new THREE.Vector3((box.min.x + box.max.x) / 2, box.max.y + above, (box.min.z + box.max.z) / 2).project(options.camera);
    if (point.z > 1) return null;
    const rect = options.canvas.getBoundingClientRect();
    return { x: rect.left + (point.x + 1) / 2 * rect.width, y: rect.top + (1 - point.y) / 2 * rect.height };
  };
  const frame = () => {
    requestAnimationFrame(frame);
    const map = options.mapNumber();
    const world = options.world();
    // Crywolf's occupation and the siege's battle: the terrain they load, once per loaded map.
    if (world && world.mapNumber === map) {
      let key = 'base', attribute = '', light: string | null = null;
      if (map === CRYWOLF_MAP && crywolf.occupation !== 0) {
        key = `crywolf${crywolf.occupation}`; attribute = `EncTerrain${(world.worldNumber) * 10 + crywolf.occupation}.att`; light = `TerrainLight${crywolf.occupation}.jpg`;
      } else if (map === BATTLE_CASTLE_MAP && siege.battleStarted) {
        key = 'battle'; attribute = `EncTerrain${world.worldNumber * 10 + 2}.att`; light = 'TerrainLight2.jpg';
      }
      const was = applied.get(world) ?? 'base';
      if (was !== key) {
        applied.set(world, key);
        if (key !== 'base') void loadTerrain(world, key, attribute, light);
        else if (map === CRYWOLF_MAP) void loadTerrain(world, key, `EncTerrain${world.worldNumber}.att`, 'TerrainLight.jpg');
        else void loadTerrain(world, key, `EncTerrain${world.worldNumber}.att`, null);
      }
    }
    crywolfHud.show(crywolf.frame());
    // The siege map: the owner's mark on entering, the HUD, the life stones' bars, the flag.
    if (map !== mapOfSiege) { mapOfSiege = map; if (map === BATTLE_CASTLE_MAP) siege.enteredMap(); }
    siegeHud.frame(map === BATTLE_CASTLE_MAP && world?.mapNumber === map);
    const group = options.worldGroup();
    if (map === BATTLE_CASTLE_MAP && group && siege.ownerMarkCells && flagApplied !== group) {
      const flags = group.children.filter((child): child is THREE.Mesh => child instanceof THREE.Mesh && child.userData.model === 'Object13.bmd');
      const mesh = flags[1];
      if (mesh && mesh.material instanceof THREE.MeshBasicMaterial) {
        const image = mesh.material.map?.image as { width?: number; height?: number } | undefined;
        mesh.material.map = castleMarkTexture(siege.ownerMarkCells, image?.width ?? 64, image?.height ?? 64);
        mesh.material.needsUpdate = true;
        flagApplied = group;
      } else if (flags.length > 0) flagApplied = group;
    }
    for (const [id, time] of siege.buildTimes) {
      const stone = online.characters.get(id);
      let bar = bars.get(id);
      const at = stone && map === BATTLE_CASTLE_MAP && time < 5 ? screenOf(stone, 100) : null;
      if (!at) { bar?.remove(); bars.delete(id); if (!stone) siege.buildTimes.delete(id); continue; }
      if (!bar) { bar = document.createElement('div'); bar.className = 'hnmo-build-bar'; bar.append(document.createElement('div')); document.body.append(bar); bars.set(id, bar); }
      Object.assign(bar.style, { left: `${at.x}px`, top: `${at.y}px` });
      (bar.firstElementChild as HTMLDivElement).style.width = `${(time / 5) * 100}%`;
    }
  };
  requestAnimationFrame(frame);

  return { kalimaTooltip: hellasTooltipLines, crywolf, crywolfHud, siege, dialogs, progress, guard, senior, gatekeeper, gateSwitch, catapult, siegeHud };
}

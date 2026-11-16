// The web client: log in through the relay and play on the HNMO server. Left-click walks, right-drag turns the camera, the wheel zooms.

import { availableLanguages, currentLanguage, initLanguage, onLanguageChange, savedLanguage, setLanguage, type Language } from './i18n.js';
import { applyGameName, loadGameName } from './gameName.js';
import * as THREE from 'three';
import { TERRAIN_SCALE, TERRAIN_SIZE, TerrainFlag } from './formats.js';
import { sampleHeight, setTerrainLights } from './terrain.js';
import { loadWorldData, createWorld, mapName } from './world.js';
import { createPlayerCharacter, createMonsterCharacter, setPlayerAnimationSpeeds, cellCenter, createItemModel } from './game/characters.js';
import { GuildStatus } from './game/guild.js';
import { findPath } from './game/pathfinding.js';
import { loadMonsterCatalog } from './game/monsterData.js';
import { playSpawnEffects } from './game/spawnEffects.js';
import { prewarmMapMonsters, warmObject, type Warmer } from './game/prewarm.js';
import { itemDefinition, itemName, loadItemCatalog } from './game/items.js';
import { OnlineWorld } from './game/online.js';
import { Chat } from './game/chat.js';
import { eventOfMap } from './game/events.js';
import { glowMaterialCount, loadGlowColors, prewarmItemGlow } from './game/itemGlow.js';
import { drawItem } from './ui/itemIcons.js';
import { BAG, Inventory } from './game/inventory.js';
import { ItemWindows } from './ui/itemWindows.js';
import { CharacterStats } from './game/stats.js';
import { Hud } from './ui/hud.js';
import { setUpGuide } from './ui/guide.js';
import { setUpSettings, type SettingsWindow } from './ui/settingsWindow.js';
import { setKtx2Renderer, clearTextureCache } from './bmd.js';
import { FRAME_SECTIONS, PerfRecorder } from './game/perfRecorder.js';
import { MovementRecorder } from './game/movementRecorder.js';
import { PerfReportWindow } from './ui/perfReportWindow.js';
import { applyPixelRatio, frameDue, performanceFlags } from './render/quality.js';
import { findItemReverse, HOTKEY_LETTERS, isHighValue, jewelApplies, isSellingBan, ItemHotkeys, ItemUser, itemTypesInUseOrder, rightClickBagItem } from './game/itemUse.js';
import { loadSkillData, masterSkillToBaseSkill, skillCooldowns, SkillList, skillInfo, skillRequirementsMet, skillUsableWithEquipment } from './game/skills.js';
import { skillTooltipLines } from './game/skillTooltip.js';
import { loadItemValueData, repairAllCost } from './game/itemValue.js';
import { CharacterWindow } from './ui/characterWindow.js';
import { setupMasterLevel } from './ui/masterLevelWindow.js';
import { AutoPlay } from './game/autoPlay.js';
import { AutoPlayBar } from './ui/autoPlayBar.js';
import { TopUpMilestonesWindow } from './ui/topUpMilestonesWindow.js';
import { AutoPlayExtras } from './game/autoPlayExtras.js';
import { AutoPlayExtrasPanel } from './ui/autoPlayExtrasPanel.js';
import { MissionsWindow } from './ui/missionsWindow.js';
import { WorldBossWindow } from './ui/worldBossWindow.js';
import { TimetableWindow } from './ui/timetableWindow.js';
import { EventAlertsWindow } from './ui/eventAlertsWidget.js';
import { LuckyNumberWindow } from './ui/luckyNumberWindow.js';
import { MarriageWindow } from './ui/marriageWindow.js';
import { MarketWindow } from './ui/marketWindow.js';
import { JewelBankWindow } from './ui/jewelBankWindow.js';
import { GuildLevelsWindow } from './ui/guildLevelsWindow.js';
import { CommunityWindow } from './ui/communityWindow.js';
import { TopRacesWindow } from './ui/topRacesWindow.js';
import { GiftCodesWindow } from './ui/giftCodesWindow.js';
import { CatchupBanner } from './ui/catchupBanner.js';
import { SeasonPlanWindow } from './ui/seasonPlanWindow.js';
import { LeftoversWindow } from './ui/leftoversWindow.js';
import { MountChainWindow } from './ui/mountChainWindow.js';
import { RankingWindow } from './ui/rankingWindow.js';
import { AddStatsWindow } from './ui/addStatsWindow.js';
import { TeleportNpcWindow } from './ui/teleportNpcWindow.js';
import { Resets } from './game/resets.js';
import { AccountSafety, text as safetyText } from './game/accountSafety.js';
import { Screenshots } from './ui/screenshot.js';
import { OnboardingWindow } from './ui/onboardingWindow.js';
import { BuffTracker, loadBuffData } from './game/buffs.js';
import { BuffStrip } from './ui/buffStrip.js';
import { MoveWindow } from './ui/moveWindow.js';
import { loadWarps } from './game/warps.js';
import { prepareDocument } from './ui/rml.js';
import { MapNameBanner } from './ui/mapNameBanner.js';
import { GameSounds } from './game/gameSounds.js';
import { Party, facingPlayer } from './game/party.js';
import { PartyList } from './ui/partyList.js';
import { PartyWindow } from './ui/partyWindow.js';
import { PartyOptionsWindow } from './ui/partyOptionsWindow.js';
import { CommandWindow } from './ui/commandWindow.js';
import { hideLoadingScreen, showLoadingScreen } from './ui/loadingScreen.js';
import { setupSocial, decorateLabel, setCustomRankLookup, type Social } from './game/social.js';
import { createCustomRanks, type CustomRanksManager } from './game/customRanks.js';
import { confirmInGame } from './ui/confirmDialog.js';
import { QuestWindows } from './ui/questWindows.js';
import { setUpEvents, type EventSystem } from './ui/eventWindows.js';
import { setUpInstanceEvents, type InstanceEventSystem } from './ui/instanceEventWindows.js';
import { setUpDuelAndGens, type DuelAndGens } from './ui/duelAndGens.js';
import { setUpFieldEvents, type FieldEvents } from './ui/fieldEventWindows.js';
import { MiniMap } from './ui/miniMap.js';
import { isNewSkill, playNewSkillEffect } from './game/newSkills.js';
import { PlayerActions } from './game/combatLook.js';
import { installTouchControls } from './ui/touchControls.js';
import { GateWatcher } from './game/gates.js';
import { Trade } from './ui/tradeWindow.js';
import { ChaosMachine } from './ui/mixWindow.js';
import { PersonalShop } from './ui/personalShop.js';
import { CashShop } from './ui/cashShop.js';
import { loginMusic } from './audio/index.js';
import { AUTO_PLAY_DATA_LENGTH, decodeAutoPlayConfig, encodeAutoPlayConfig, initialAutoPlayConfig, type AutoPlayConfig } from './game/autoPlayConfig.js';
import { AutoPlaySettingsWindow } from './ui/autoPlayWindows.js';
import { maxDurability, repairCost } from './game/itemValue.js';
import { keepNextGradeSparkles } from './render/effects/nextGrade.js';
import { DarkSideDash } from './render/effects/darkSideDash.js';
import { DarkSideGhosts } from './game/darkSideGhosts.js';
import { EffectSystem, WeaponTrails, ownerFromCharacter as effectOwnerOf, playSkillEffect, playSkillHit, playBuffEffect, playChainLightning, debuffTint, Buff } from './render/effects/index.js';
import { DarkRaven, helperVariant, setCompanionEffects, setZenCollector } from './game/pets.js';
import { Combat } from './game/combat.js';
import { CombatOverlay } from './ui/combatOverlay.js';
import { GroundItems } from './game/groundItems.js';
import { ARROW_RELEASE_DELAY_SECONDS, DeathEffects, ProjectileLayer, arrowKindsForMonster, arrowKindsForWeapons, loadCombatEffectsData } from './game/combatEffects.js';
import { fetchServerList, fetchGameServerAddress, login, loginWithTicket, fetchCharacters, enterWorld, createCharacter, CLASSES } from './net/session.js';
import type { WorldData } from './world.js';
import type { TerrainMesh } from './terrain.js';
import type { Character } from './game/characters.js';
import type { EquipmentAppearance, EquipmentSlotName } from './game/items.js';
import type { CharacterListEntry } from './net/session.js';
import type { Connection } from './net/connection.js';
import { read } from './net/packets.js';
import { HNMO_BUILD_STAMP, HNMO_PROJECT_ID } from './build.js';

// Record the build stamp so it survives bundling/minification and can be read from a
// suspect build (window.__hnmoBuild / the console line). See tools/hnmo/PROTECTION.md.
(globalThis as { __hnmoBuild?: string }).__hnmoBuild = HNMO_BUILD_STAMP;
console.log(`HNMO build ${HNMO_BUILD_STAMP} (project ${HNMO_PROJECT_ID})`);

// The game's name players see (set in the admin panel): the page shows the default until it's here.
applyGameName();
void loadGameName();

// The language (English by default): EN and VN buttons and a list of the other languages, on the
// login screen and in the top bar.
const languagePickers = [...document.querySelectorAll<HTMLElement>('.language-picker')];
const MAIN_LANGUAGES = ['en', 'vi'];
const showLanguage = (language: Language) => {
  for (const picker of languagePickers) {
    for (const button of picker.querySelectorAll<HTMLButtonElement>('button[data-lang]')) button.setAttribute('aria-pressed', String(button.dataset.lang === language));
    const more = picker.querySelector<HTMLSelectElement>('select.language-more');
    if (more) more.value = MAIN_LANGUAGES.includes(language) ? '' : language;
  }
};
const chooseLanguage = (language: Language, from: HTMLElement) => {
  void setLanguage(language).catch(() => showLanguage(currentLanguage()));
  if (!from.closest('#loginLanguage')) from.blur(); // the game's keys (I, C, …) go back to the game
};
// The other languages of public/data/lang/index.json go into the list.
void availableLanguages().then((languages) => {
  const others = languages.filter((language) => !MAIN_LANGUAGES.includes(language.code));
  for (const picker of languagePickers) {
    const more = picker.querySelector<HTMLSelectElement>('select.language-more');
    if (more) more.replaceChildren(new Option('More…', ''), ...others.map((language) => new Option(language.name, language.code)));
  }
  showLanguage(currentLanguage() === 'en' ? savedLanguage() : currentLanguage());
});
for (const picker of languagePickers) {
  for (const button of picker.querySelectorAll<HTMLButtonElement>('button[data-lang]')) {
    button.addEventListener('click', () => chooseLanguage(button.dataset.lang ?? 'en', button));
  }
  const more = picker.querySelector<HTMLSelectElement>('select.language-more');
  more?.addEventListener('change', () => { if (more.value) chooseLanguage(more.value, more); });
}
onLanguageChange(showLanguage);
showLanguage(savedLanguage());
void initLanguage();

// The map the hero is on: its data and the terrain mesh the pointer is cast against.
export type GameWorld = WorldData & { terrain: TerrainMesh };

// The app's state, also exposed as window.__game for automated tests and debugging.
export interface GameState {
  world: GameWorld | null;
  worldGroup?: THREE.Group;
  hero: Character | null;
  online: OnlineWorld | null;
  /** The chat log, system log and input box (game/chat.ts). */
  chat?: Chat;
  mapNumber: number | null;
  camera: THREE.PerspectiveCamera;
  inventory?: Inventory;
  itemWindows?: ItemWindows;
  stats?: CharacterStats;
  hud?: Hud;
  hotkeys?: ItemHotkeys;
  skills?: SkillList;
  combat?: Combat;
  /** Dark Side's dash state and how many of its copies are drawn (for tests). */
  darkSide?: () => { dash: DarkSideDash | null; ghosts: number; made: number };
  overlay?: CombatOverlay;
  ground?: GroundItems;
  projectiles?: ProjectileLayer;
  deaths?: DeathEffects;
  effects?: EffectSystem;
  weaponTrails?: WeaponTrails;
  autoPlay?: AutoPlay;
  autoPlayBar?: AutoPlayBar;
  autoPlayExtras?: AutoPlayExtras;
  missions?: MissionsWindow;
  worldBosses?: WorldBossWindow;
  timetable?: TimetableWindow;
  luckyNumber?: LuckyNumberWindow;
  marriage?: MarriageWindow;
  market?: MarketWindow;
  jewelBank?: JewelBankWindow;
  guildLevels?: GuildLevelsWindow;
  customRanks?: CustomRanksManager;
  teleportNpc?: TeleportNpcWindow;
  community?: CommunityWindow;
  topRaces?: TopRacesWindow;
  giftCodes?: GiftCodesWindow;
  catchupCap?: CatchupBanner;
  seasonPlan?: SeasonPlanWindow;
  leftovers?: LeftoversWindow;
  mountChain?: MountChainWindow;
  ranking?: RankingWindow;
  addStats?: AddStatsWindow;
  resets?: Resets;
  accountSafety?: AccountSafety;
  screenshots?: Screenshots;
  onboarding?: OnboardingWindow;
  buffs?: BuffTracker;
  party?: Party;
  trade?: Trade;
  chaosMachine?: ChaosMachine;
  personalShop?: PersonalShop;
  cashShop?: CashShop;
  miniMap?: MiniMap;
  connection?: Connection;
  cooldowns?: typeof skillCooldowns;
  /** The camera's turn and distance (the mouse and the touch controls change them). */
  view?: { yaw: number; distance: number };
  gates?: GateWatcher;
  partyWindow?: PartyWindow;
  partyOptions?: PartyOptionsWindow;
  commandWindow?: CommandWindow;
  events?: EventSystem;
  instanceEvents?: InstanceEventSystem;
  social?: Social;
  duelAndGens?: DuelAndGens;
  fieldEvents?: FieldEvents;
  topUpMilestones?: TopUpMilestonesWindow;
  /** The performance report window (HNMO extra). */
  perfReport?: PerfReportWindow;
  /** What tests and the console reach in the dev build and the packaged bundle alike. */
  debug?: GameDebug;
}

export interface GameDebug {
  renderer: THREE.WebGLRenderer;
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  createPlayerCharacter: typeof createPlayerCharacter;
  createMonsterCharacter: typeof createMonsterCharacter;
  drawItem: typeof drawItem;
  glowMaterialCount: typeof glowMaterialCount;
  /**
   * Runs `count` frames of the game's work back to back (each `dt` seconds of play, drawn unless `draw` is
   * false) and returns the average milliseconds of each part of the frame (the frame loop's parts).
   */
  benchFrames(count: number, options?: { dt?: number; draw?: boolean }): Record<string, number>;
  /** Turns the culling of characters out of view on or off (to compare). */
  setCulling(on: boolean): void;
  /** Per-frame movement recorder for the hero and the 5 nearest monsters (off by default). */
  movementRecorder: MovementRecorder;
}

declare global {
  interface Window {
    __game: GameState;
    __viewerReady?: boolean;
  }
}

// What the login form holds (its inputs' names).
interface LoginSettings { relay: string; host: string; port: string; username: string; password: string }

THREE.ColorManagement.enabled = false;

const FOG = { color: new THREE.Color(0x0a0d14), near: 2600, far: 4200 };
const CAMERA = { distance: 1250, minDistance: 500, maxDistance: 2600, pitch: 48 * Math.PI / 180, yaw: -45 * Math.PI / 180 };
const LABEL_RANGE_CELLS = 18;
const MAX_FRAME_SECONDS = 0.5; // a hidden tab still catches up on walks, but not on minutes
const SETTINGS_KEY = 'hnmo-login';

// The page's elements: all of them are always in index.html.
const $ = <E extends HTMLElement = HTMLElement>(id: string): E => document.getElementById(id) as E;
const ui = {
  canvas: $<HTMLCanvasElement>('view'), status: $('status'), bar: $('bar'), position: $('position'), fps: $('fps'),
  login: $('login'), loginForm: $<HTMLFormElement>('loginForm'), loginError: $('loginError'), servers: $('servers'), characters: $('characters'),
  hud: $('hud'), heroName: $('heroName'), labels: $('labels'),
};

// A player in view by name (the chat's name menu).
const playerNamed = (name: string) => [...(state.online?.characters.values() ?? [])].find((c) => c.kind === 'player' && c.name === name) ?? null;
const notNearby = (name: string) => chat.add(`${name} is not nearby.`, 'system');
const chat = new Chat({
  click: () => sounds.click(),
  owner: () => state.online?.hero.name ?? '',
  titlePrefix: (name) => state.customRanks?.chatPrefix(name) ?? null,
  nameActions: {
    party: (name) => { const p = playerNamed(name); if (p) state.party?.invite(p); else notNearby(name); },
    trade: (name) => { const p = playerNamed(name); if (p) state.trade?.requestTrade(p.id); else notNearby(name); },
    addFriend: (name) => state.social?.messenger.addFriend(name),
  },
});

const renderer = new THREE.WebGLRenderer({ canvas: ui.canvas, antialias: true });
setKtx2Renderer(renderer);
renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2)); // then render/quality.ts's applyPixelRatio
const scene = new THREE.Scene();
scene.background = FOG.color;
const camera = new THREE.PerspectiveCamera(35, 1, 10, 9000);
const warmer: Warmer = { renderer, camera, scene };
const root = new THREE.Group(); // game coordinates: x east, y north, z up
root.rotation.x = -Math.PI / 2;
scene.add(root);

const state: GameState = { world: null, hero: null, online: null, mapNumber: null, camera };
state.cooldowns = skillCooldowns;
state.chat = chat;
window.__game = state; // for automated tests and debugging from the console

// HNMO extra: the performance report (Settings, or Ctrl+Shift+P): what the frames cost on the player's computer.
let settingsWindow: SettingsWindow | null = null;
const perfRecorder = new PerfRecorder({
  renderer, canvas: ui.canvas,
  scene: () => {
    const counts = { players: 0, monsters: 0, npcs: 0 };
    for (const c of state.online?.characters.values() ?? []) {
      if (c.kind === 'player') counts.players++; else if (c.kind === 'monster') counts.monsters++; else counts.npcs++;
    }
    const fx = state.effects?.counts();
    return {
      map: state.mapNumber, characters: state.online?.characters.size ?? 0, ...counts,
      effects: fx?.effects ?? 0, particles: fx?.particles ?? 0, joints: fx?.joints ?? 0, sprites: fx?.sprites ?? 0,
      labels: ui.labels.childElementCount, chatLines: state.chat?.entries.length ?? 0,
    };
  },
  settings: () => ({ ...(settingsWindow?.current ?? {}), language: currentLanguage() }),
});
const movementRecorder = new MovementRecorder();
state.debug = {
  renderer, scene, camera, createPlayerCharacter, createMonsterCharacter, drawItem, glowMaterialCount,
  movementRecorder,
  setCulling: (on) => { cullingOn = on; },
  benchFrames: (count, { dt = 1 / 60, draw = true } = {}) => {
    const sums = new Float64Array(FRAME_SECTIONS.length);
    const render = renderer.render.bind(renderer);
    if (!draw) renderer.render = () => undefined;
    try {
      for (let i = 0; i < count; i++) {
        step(dt, perfRecorder);
        perfRecorder.lastSections.forEach((ms, k) => { sums[k] += ms; });
      }
    } finally {
      renderer.render = render;
    }
    const out: Record<string, number> = {};
    FRAME_SECTIONS.forEach((name, k) => { out[name] = Math.round(sums[k] / count * 1000) / 1000; });
    out.total = Math.round(sums.reduce((a, b) => a + b, 0) / count * 1000) / 1000;
    return out;
  },
};
const perfWindow = new PerfReportWindow(perfRecorder, { click: () => sounds.click(), beforeRecord: () => settingsWindow?.close() });
state.perfReport = perfWindow;

// The loading line; once something has finished loading (progress 1) it goes away after a few
// seconds, so nothing but the game's own interface stays on screen.
let statusTimer: ReturnType<typeof setTimeout> | undefined;
function setStatus(text: string, progress?: number): void {
  ui.status.textContent = text;
  if (progress !== undefined) ui.bar.style.width = `${Math.round(progress * 100)}%`;
  const box = ui.status.parentElement;
  if (!box) return;
  clearTimeout(statusTimer);
  box.style.transition = 'opacity .6s';
  box.style.opacity = '1';
  if (progress === 1) statusTimer = setTimeout(() => { box.style.opacity = '0'; }, 6000);
}

function resize() {
  const { clientWidth: width, clientHeight: height } = ui.canvas.parentElement!;
  renderer.setSize(width, height, false);
  applyPixelRatio(); // the pixel budget follows the window's size (render/quality.ts)
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
}
window.addEventListener('resize', resize);

// The canvas's place on the page, read once per window size: reading it in the frame loop after the
// HUD's writes would make the browser lay the page out again every frame.
let canvasBox: DOMRect | null = null;
function canvasRect(): DOMRect {
  canvasBox ??= ui.canvas.getBoundingClientRect();
  return canvasBox;
}
window.addEventListener('resize', () => { canvasBox = null; });
document.addEventListener('fullscreenchange', () => { canvasBox = null; });

const toScene = (x: number, y: number, z: number): THREE.Vector3 => new THREE.Vector3(x, z, -y);

// ---------- world ----------

// Sounds and music (the desktop's PlayBuffer / PlayMp3 rules).
const sounds = new GameSounds();
// The map name banner (CUIMapName): shown on entering each map.
const mapBanner = new MapNameBanner();

async function loadMap(mapNumber: number): Promise<void> {
  if (state.mapNumber === mapNumber) return;
  mapsLoading++;
  try { await loadMapNow(mapNumber); } finally { mapsLoading--; }
}

/** Maps being loaded (LoadingWorld): gates wait for them. */
let mapsLoading = 0;

async function loadMapNow(mapNumber: number): Promise<void> {
  if (state.online || ui.login.hidden) void mapBanner.show(mapNumber, mapName(mapNumber));
  if (state.worldGroup) root.remove(state.worldGroup);
  setStatus(`Loading ${mapName(mapNumber)}…`, 0.05);
  const data = await loadWorldData(mapNumber);
  const world = await createWorld(data, FOG, (progress) => setStatus('Loading world objects…', 0.1 + progress * 0.8));
  state.world = { ...data, terrain: world.terrain };
  state.worldGroup = world.group;
  state.mapNumber = mapNumber;
  root.add(world.group);
}

async function placeHero({ id, name, bodySet, equipment, x, y }: {
  id: number; name: string; bodySet: number; equipment: EquipmentAppearance; x: number; y: number;
}): Promise<void> {
  setStatus('Loading character…', 0.95);
  const hero = await createPlayerCharacter({ id, name, bodySet, equipment });
  if (state.hero) root.remove(state.hero.group);
  state.hero = hero;
  hero.placeAt(x, y);
  root.add(hero.group);
  ui.heroName.textContent = name;
}

// The first character of each class to come into view loads its models and textures; done while
// walking, that froze the game for a moment. So once the hero stands in the world, the classes'
// bodies are loaded in the background, one at a time between frames (each later one is then cached).
let charactersWarmed = false;
function warmCharacterModels(): void {
  if (charactersWarmed) return;
  charactersWarmed = true;
  const bodySets = [1, 2, 3, 4, 5, 6, 7];
  const next = () => {
    const bodySet = bodySets.shift();
    if (bodySet === undefined) return;
    void createPlayerCharacter({ id: -bodySet, name: '', bodySet }).catch(() => undefined).finally(() => {
      const idle = (window as { requestIdleCallback?: (callback: () => void, options?: { timeout: number }) => number }).requestIdleCallback;
      if (idle) idle(next, { timeout: 2000 }); else setTimeout(next, 200);
    });
  };
  setTimeout(next, 1500);
}

// The hero wears what the inventory's equipment slots hold (the desktop client draws its own
// character from CharacterMachine->Equipment): put on again when that changes.
const EQUIPMENT_OF_SLOT: EquipmentSlotName[] = ['rightHand', 'leftHand', 'helm', 'armor', 'pants', 'gloves', 'boots', 'wings', 'pet'];
let wornSignature: string | null = null;
/** Keeps the +15 weapons' sparkles ticking (set once the effect system exists). */
let keepSparkles: (() => void) | null = null;

async function wearInventory(character: CharacterListEntry): Promise<void> {
  const equipment: EquipmentAppearance = {};
  EQUIPMENT_OF_SLOT.forEach((name, slot) => {
    const item = state.inventory!.items.get(slot);
    equipment[name] = item ? { group: item.group, number: item.number, glowLevel: item.level, excellent: item.excellent > 0, ancient: item.ancientSet > 0, variant: helperVariant(item.group, item.number, item.excellent) } : null;
  });
  const signature = JSON.stringify(equipment);
  if (signature === wornSignature) return;
  const first = wornSignature === null;
  wornSignature = signature;
  if (first || !state.hero) return; // the hero already wears what the character list showed
  const old = state.hero;
  const hero = await createPlayerCharacter({ id: old.id, name: old.name, bodySet: character.classIndex + 1, equipment });
  if (state.hero !== old) return;
  hero.position.copy(old.position);
  hero.cell = { ...old.cell };
  hero.angle = old.angle;
  hero.path = old.path;
  hero.action = old.action;
  root.remove(old.group);
  root.add(hero.group);
  state.hero = hero;
  if (state.online) state.online.hero = hero;
}

// ---------- camera and input ----------

const view = { yaw: CAMERA.yaw, distance: CAMERA.distance, dragging: false, lastX: 0, rightDownX: 0, rightDownY: 0 };
state.view = view;

function updateCamera(): void {
  // Called by the frame loop once the hero (and so the world) is there.
  const { x, y } = state.hero!.position;
  const z = sampleHeight(state.world!.heights, x, y);
  const horizontal = Math.cos(CAMERA.pitch) * view.distance;
  camera.position.copy(toScene(x + Math.sin(view.yaw) * horizontal, y - Math.cos(view.yaw) * horizontal, z + Math.sin(CAMERA.pitch) * view.distance));
  camera.lookAt(toScene(x, y, z + 100));
  // The effects' camera shake (EarthQuake, added to the camera's pitch) and their light on the ground.
  const effects = state.effects;
  if (effects) {
    if (effects.earthQuake !== 0) camera.rotateX(effects.earthQuake * Math.PI / 180);
    setTerrainLights(state.world!.terrain, effects.terrainLights);
  }
}

const raycaster = new THREE.Raycaster();
const pointer = new THREE.Vector2();

function pickCell(event: MouseEvent): { x: number; y: number } | null {
  const rect = canvasRect();
  pointer.set(((event.clientX - rect.left) / rect.width) * 2 - 1, -((event.clientY - rect.top) / rect.height) * 2 + 1);
  raycaster.setFromCamera(pointer, camera);
  const hit = raycaster.intersectObject(state.world!.terrain, false)[0];
  if (!hit) return null;
  const local = root.worldToLocal(hit.point.clone());
  return { x: Math.floor(local.x / TERRAIN_SCALE), y: Math.floor(local.y / TERRAIN_SCALE) };
}

ui.canvas.addEventListener('contextmenu', (event) => event.preventDefault());
// The NPC under the pointer: the nearest one whose drawn body the pointer is on.
const npcBox = new THREE.Box3();
function pickCharacter(event: MouseEvent, kinds: string[]): Character | null {
  if (!state.online) return null;
  const rect = canvasRect();
  let best: { character: Character; distance: number } | null = null;
  for (const character of state.online.characters.values()) {
    if (!kinds.includes(character.kind) || character.dead) continue;
    npcBox.setFromObject(character.group);
    // A box with NaN in it (a broken model matrix) would pass every bounds test below and take every click.
    if (npcBox.isEmpty() || ![npcBox.min.x, npcBox.min.y, npcBox.min.z, npcBox.max.x, npcBox.max.y, npcBox.max.z].every(Number.isFinite)) continue;
    // The screen rectangle around all eight corners of the box (two corners alone miss part of it when the camera turns).
    let left = Infinity, right = -Infinity, upper = Infinity, lower = -Infinity, behind = false;
    for (let corner = 0; corner < 8 && !behind; corner++) {
      const v = new THREE.Vector3(corner & 1 ? npcBox.max.x : npcBox.min.x, corner & 2 ? npcBox.max.y : npcBox.min.y, corner & 4 ? npcBox.max.z : npcBox.min.z).project(camera);
      if (v.z < -1 || v.z > 1) { behind = true; break; } // behind the camera or past the far plane: projects to nonsense
      const x = rect.left + (v.x + 1) / 2 * rect.width, y = rect.top + (1 - v.y) / 2 * rect.height;
      left = Math.min(left, x); right = Math.max(right, x); upper = Math.min(upper, y); lower = Math.max(lower, y);
    }
    if (behind) continue;
    const pad = 8;
    if (event.clientX < left - pad || event.clientX > right + pad || event.clientY < upper - pad || event.clientY > lower + pad) continue;
    const distance = Math.hypot(event.clientX - (left + right) / 2, event.clientY - (upper + lower) / 2);
    if (!best || distance < best.distance) best = { character, distance };
  }
  return best?.character ?? null;
}

// The monster under the mouse shows its name and health bar at the top (CNameWindow); otherwise the target's.
ui.canvas.addEventListener('pointermove', (event) => {
  if (!state.overlay) return;
  state.ground?.hover(event);
  const hovered = pickCharacter(event, ['monster']);
  state.overlay.showTarget(hovered ?? state.combat?.target ?? null);
});

ui.canvas.addEventListener('pointerdown', (event) => {
  const hero = state.hero;
  if (!hero) return;
  if (event.pointerType === 'touch' && !event.isPrimary) return; // a second finger pinches or turns
  if (event.button === 2) { view.dragging = true; view.lastX = event.clientX; view.rightDownX = event.clientX; view.rightDownY = event.clientY; return; }
  const monster = pickCharacter(event, ['monster']);
  if (monster && state.combat) { state.combat.attack(monster); return; }
  const npc = pickCharacter(event, ['npc']);
  if (npc) {
    state.combat?.cancel();
    state.online!.talkTo(npc);
    return;
  } // an NPC is only found online
  const drop = state.ground?.pick(event);
  if (drop) { state.combat?.cancel(); state.ground!.pickUp(drop); return; }
  const cell = pickCell(event);
  if (!cell) return;
  state.combat?.cancel();
  state.ground?.cancel();
  if (state.online) state.online.walkTo(cell);
  else hero.walk(findPath(state.world!.flags, hero.cell, cell));
});
window.addEventListener('pointerup', (event) => {
  // A right click that didn't turn the camera casts the current skill (the desktop's right mouse
  // button): at the monster under the mouse, else at the ground there (area skills, buffs on the hero).
  if (event.button === 2 && view.dragging && state.combat && state.online && event.target === ui.canvas
    && Math.hypot(event.clientX - view.rightDownX, event.clientY - view.rightDownY) < 6) {
    // A command armed in the command window takes the right click (RunCommand).
    if (state.commandWindow?.rightClick(event)) { view.dragging = false; return; }
    const monster = pickCharacter(event, ['monster']);
    const cell = monster ? { ...monster.cell } : pickCell(event);
    state.combat.cast(monster, cell);
    // A right click on a player puts the name in the chat's whisper box (UpdateWhisperTargetFromRightClick; not in Chaos Castle).
    const player = monster ? null : pickCharacter(event, ['player']);
    if (player && eventOfMap(state.mapNumber ?? null) !== 'chaosCastle') chat.setWhisperTarget(player.name);
  }
  view.dragging = false;
});
window.addEventListener('pointermove', (event) => {
  if (!view.dragging) return;
  view.yaw -= (event.clientX - view.lastX) * 0.006;
  view.lastX = event.clientX;
});
ui.canvas.addEventListener('wheel', (event) => {
  event.preventDefault();
  view.distance = THREE.MathUtils.clamp(view.distance * (event.deltaY > 0 ? 1.1 : 0.9), CAMERA.minDistance, CAMERA.maxDistance);
}, { passive: false });
// Phones and tablets (HNMO extra): long press casts, two fingers zoom and turn, buttons for the keys.
installTouchControls({
  canvas: ui.canvas,
  zoom: (factor) => { view.distance = THREE.MathUtils.clamp(view.distance * factor, CAMERA.minDistance, CAMERA.maxDistance); },
  rotate: (dx) => { view.yaw -= dx * 0.006; },
  cast: (x, y) => {
    if (!state.combat || !state.online) return;
    const at = new MouseEvent('pointermove', { clientX: x, clientY: y });
    const monster = pickCharacter(at, ['monster']);
    state.combat.cast(monster, monster ? { ...monster.cell } : pickCell(at));
  },
  // The nearest live monster within 8 cells: the current skill at it, else a plain attack.
  fight: () => {
    const online = state.online, combat = state.combat;
    if (!online || !combat) return false;
    const hero = online.hero.cell;
    let best: Character | null = null, distance = 9;
    for (const c of online.characters.values()) {
      if (c.kind !== 'monster' || c.dead) continue;
      const d = Math.max(Math.abs(c.cell.x - hero.x), Math.abs(c.cell.y - hero.y));
      if (d < distance) { best = c; distance = d; }
    }
    if (!best) { chat.add('No monster nearby.', 'system'); return false; }
    if (!combat.cast(best, { ...best.cell })) combat.attack(best);
    return true;
  },
});

// ---------- name labels ----------

// As the desktop's name window (CNameWindow::RefreshNameTags): only the player or NPC under the pointer
// has its name over its head (not in Chaos Castle); the pointer is looked at once a frame.
const labelElements = new Map<Character, HTMLSpanElement>();
let labelPointer: MouseEvent | null = null;
ui.canvas.addEventListener('pointermove', (event) => { labelPointer = event; }, { passive: true });
ui.canvas.addEventListener('pointerleave', () => { labelPointer = null; });

const labelsShown = new Set<Character>();
function updateLabels(): void {
  const visible = labelsShown;
  visible.clear();
  const hero = state.hero, world = state.world;
  const pointed = labelPointer && eventOfMap(state.mapNumber ?? null) !== 'chaosCastle' ? pickCharacter(labelPointer, ['npc']) : null;
  if (state.online && hero && world) {
    const rect = canvasRect();
    for (const character of state.online.characters.values()) {
      // A pointed player's name box (with its Gens mark and guild line) is the Gens marks' (ui/gensWindow.ts).
      if ((character !== pointed || character.kind === 'player') && character.kind !== 'kalimaGate') continue; // a Kalima gate keeps its name box
      const dx = character.cell.x - hero.cell.x, dy = character.cell.y - hero.cell.y;
      if (Math.hypot(dx, dy) > LABEL_RANGE_CELLS) continue;
      const point = toScene(character.position.x, character.position.y, sampleHeight(world.heights, character.position.x, character.position.y) + 220).project(camera);
      if (point.z > 1) continue;
      let element = labelElements.get(character);
      if (!element) {
        element = document.createElement('span');
        element.className = `label ${character.kind}`;
        element.textContent = character.name;
        ui.labels.append(element);
        labelElements.set(character, element);
      }
      decorateLabel(character, element);
      element.style.transform = `translate(${(point.x + 1) / 2 * rect.width}px, ${(1 - point.y) / 2 * rect.height}px) translate(-50%, -100%)`;
      visible.add(character);
    }
  }
  for (const [character, element] of labelElements) {
    if (visible.has(character)) continue;
    element.remove();
    labelElements.delete(character);
  }
}

// ---------- frame loop ----------

// Characters out of the camera's view are not posed or drawn (Character.culled): the view of the last
// frame, in the map's coordinates, against a sphere around each one (wide enough for wings, mounts and
// big monsters; a scaled monster's grows with it).
const cullFrustum = new THREE.Frustum();
const cullMatrix = new THREE.Matrix4();
const cullSphere = new THREE.Sphere();
const CULL_RADIUS = 450;
let cullingOn = true;
function cullCharacters(online: OnlineWorld): void {
  const perf = performanceFlags();
  cullMatrix.multiplyMatrices(camera.projectionMatrix, camera.matrixWorldInverse).multiply(root.matrixWorld);
  cullFrustum.setFromProjectionMatrix(cullMatrix);
  for (const character of online.characters.values()) {
    if (perf.hideOtherPlayers && character.kind === 'player') {
      character.culled = true;
      for (const companion of character.companions) companion.group.visible = false;
      continue;
    }
    const group = character.group;
    if (group.parent !== root || !cullingOn) { character.culled = false; continue; }
    cullSphere.center.copy(group.position);
    cullSphere.center.z += 120 * group.scale.z;
    cullSphere.radius = CULL_RADIUS * Math.max(1, group.scale.x);
    character.culled = !cullFrustum.intersectsSphere(cullSphere);
  }
}

let last = performance.now();
let frames = 0, fpsTime = 0;

function frame(now: number): void {
  if (!frameDue(now)) { requestAnimationFrame(frame); return; } // the settings' FPS limit
  const dt = Math.min((now - last) / 1000, MAX_FRAME_SECONDS);
  last = now;
  frames++; fpsTime += dt;
  if (fpsTime >= 1) { ui.fps.textContent = `${Math.round(frames / fpsTime)} fps`; frames = 0; fpsTime = 0; }
  // The performance report measures the frame's parts while it records (game/perfRecorder.ts).
  const rec = perfRecorder.recording ? perfRecorder : null;
  step(dt, rec);
  if (rec) rec.endFrame(now);
  requestAnimationFrame(frame);
}

/** One frame's work: the world moves on by `dt` seconds and is drawn. */
function step(dt: number, rec: PerfRecorder | null): void {
  rec?.beginFrame();
  if (state.hero) {
    state.hero.update(dt, state.world!);
    if (state.online) cullCharacters(state.online);
    state.online?.update(dt);
    if (movementRecorder.enabled) movementRecorder.recordFrame(dt, state.hero, state.online?.characters.values());
    rec?.lap('characters');
    state.combat?.update();
    if (state.ground && state.world) state.ground.update(state.world);
    rec?.lap('combat');
    state.deaths?.update(dt);
    state.projectiles?.update(dt);
    state.effects?.update(dt);
    keepSparkles?.();
    rec?.lap('effects');
    state.autoPlay?.update();
    mapBanner.update(dt);
    if (state.online && state.hero && state.world) {
      const hero = state.hero, world = state.world;
      const index = hero.cell.y * TERRAIN_SIZE + hero.cell.x;
      const numKeys = hero.model.skeleton.actions[hero.action]?.numKeys ?? 0;
      sounds.frame(state.mapNumber, hero, world.mapping.layer1[index] ?? 0, (world.flags[index] & TerrainFlag.SAFEZONE) !== 0, numKeys);
    }
    state.autoPlayExtras?.update();
    if (state.autoPlayBar && state.hero && state.mapNumber !== null) state.autoPlayBar.set(mapName(state.mapNumber), state.hero.cell, state.autoPlay?.active ?? false);
    state.overlay?.update(dt);
    state.miniMap?.update();
    state.gates?.update();
    state.hud?.updateCooldowns();
    const position = `${state.hero.cell.x}, ${state.hero.cell.y}`;
    if (position !== shownPosition) { ui.position.textContent = position; shownPosition = position; }
    rec?.lap('interface');
    updateCamera();
    updateLabels();
    rec?.lap('camera');
  }
  renderer.render(scene, camera);
  rec?.lap('render');
}
let shownPosition = '';
/** The character named in screenshots' captions. */
let screenshotHero = '';

// ---------- login screens ----------

function loadSettings(): Record<string, unknown> {
  try { return JSON.parse(localStorage.getItem(SETTINGS_KEY) ?? 'null') ?? {}; } catch { return {}; }
}

function saveSettings(settings: Record<string, string>): void {
  try { localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings)); } catch { /* private window */ }
}

function showError(error: unknown): void {
  hideLoadingScreen();
  ui.loginError.textContent = error instanceof Error ? error.message : String(error);
  ui.loginError.hidden = false;
}

function chooseFrom<T>(container: HTMLElement, items: T[], describe: (item: T) => string): Promise<T> {
  container.replaceChildren();
  container.hidden = false;
  return new Promise<T>((resolve) => {
    for (const item of items) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'choice';
      button.innerHTML = describe(item);
      button.addEventListener('click', () => resolve(item), { once: true });
      container.append(button);
    }
  });
}

const escapeHtml = (text: unknown): string => String(text).replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
const NEW_CHARACTER = { create: true } as const;
const MAX_CHARACTERS = 5;

// The character list, with a form to create one (the client's CharacterScene).
// The server sends the list once; a created character is added from the server's answer,
// as the client's CharacterScene does.
async function chooseCharacter(connection: Connection): Promise<CharacterListEntry> {
  const characters = await fetchCharacters(connection);
  for (;;) {
    const choices: (CharacterListEntry | typeof NEW_CHARACTER)[] = characters.length < MAX_CHARACTERS ? [...characters, NEW_CHARACTER] : characters;
    const choice = await chooseFrom(ui.characters, choices, (c) => ('create' in c ? '+ Create a character<small>Dark Wizard, Dark Knight, Fairy Elf, …</small>' : `${escapeHtml(c.name)}<small>Level ${c.level} ${CLASSES[c.classIndex]}</small>`));
    if (!('create' in choice)) return choice;
    const created = await askNewCharacter();
    if (!created) continue;
    try {
      const result = await createCharacter(connection, created.name, created.classIndex);
      characters.push({ slot: result.CharacterSlot, name: result.CharacterName || created.name, level: result.Level ?? 1, classIndex: created.classIndex, equipment: {} });
    } catch (error) {
      showError(error);
    }
  }
}

function askNewCharacter(): Promise<{ name: string; classIndex: number } | null> {
  ui.characters.replaceChildren();
  const form = document.createElement('div');
  form.className = 'choices';
  form.innerHTML = `
    <label class="row"><span>Character name</span><input id="newName" maxlength="10" required autocomplete="off"></label>
    <label class="row"><span>Class</span><select id="newClass">${CLASSES.map((name, index) => `<option value="${index}">${name}</option>`).join('')}</select></label>
    <div class="buttons"><button type="button" class="primary" id="createIt">Create</button><button type="button" id="cancelIt">Back</button></div>`;
  ui.characters.append(form);
  // The form's fields were just written above.
  const nameInput = form.querySelector<HTMLInputElement>('#newName')!;
  const classSelect = form.querySelector<HTMLSelectElement>('#newClass')!;
  nameInput.focus();
  return new Promise((resolve) => {
    form.querySelector('#cancelIt')!.addEventListener('click', () => resolve(null), { once: true });
    form.querySelector('#createIt')!.addEventListener('click', () => {
      const name = nameInput.value.trim();
      if (name.length < 4 || name.length > 10) { showError(new Error('A character name has 4 to 10 letters or digits.')); return; }
      ui.loginError.hidden = true;
      resolve({ name, classIndex: Number(classSelect.value) });
    });
  });
}

async function fetchWorldLabels(): Promise<Map<number, string>> {
  const labels = new Map<number, string>();
  try {
    const response = await fetch('/website/data', {
      cache: 'no-store',
      signal: AbortSignal.timeout(1200),
    });
    if (!response.ok) return labels;
    const raw: unknown = await response.json();
    if (typeof raw !== 'object' || raw === null || !('worlds' in raw) || !Array.isArray(raw.worlds)) {
      return labels;
    }
    for (const world of raw.worlds as unknown[]) {
      if (typeof world !== 'object' || world === null) continue;
      const entry = world as { nameEn?: unknown; isOpen?: unknown; openingDateSydney?: unknown; servers?: unknown };
      const name = typeof entry.nameEn === 'string' ? entry.nameEn : '';
      const status = entry.isOpen === false && typeof entry.openingDateSydney === 'string' && entry.openingDateSydney
        ? `Opens ${entry.openingDateSydney}`
        : '';
      const serverList = Array.isArray(entry.servers) ? (entry.servers as unknown[]) : [];
      for (const srv of serverList) {
        if (typeof srv === 'object' && srv !== null && 'id' in srv && typeof (srv as { id: unknown }).id === 'number') {
          const id = (srv as { id: number }).id;
          const full = status ? `${name} (${status})` : name;
          if (full) labels.set(id, full);
        }
      }
    }
  } catch {
    /* Optional grouped world metadata when offline or standalone relay. */
  }
  return labels;
}

async function playOnline(settings: LoginSettings, ticket?: string): Promise<void> {
  state.connection?.close();
  state.connection = undefined;
  const connectServer = { host: settings.host, port: Number(settings.port) };
  setStatus('Asking the connect server for game servers…');
  const [servers, worldLabels] = await Promise.all([
    fetchServerList(settings.relay, connectServer),
    fetchWorldLabels(),
  ]);
  if (!servers.length) throw new Error('The connect server lists no game servers.');
  const server = servers.length === 1
    ? servers[0]
    : await chooseFrom(ui.servers, servers, (s) => {
        const prefix = worldLabels.get(s.id);
        return prefix
          ? `${prefix} — Server ${s.id + 1}<small>${s.load}% load</small>`
          : `Server ${s.id + 1}<small>${s.load}% load</small>`;
      });
  ui.servers.hidden = true;
  const address = await fetchGameServerAddress(settings.relay, connectServer, server.id);
  setStatus(`Logging in to ${address.host}:${address.port}…`);
  const connection = ticket
    ? await loginWithTicket(settings.relay, address, settings.username, ticket)
    : await login(settings.relay, address, settings.username, settings.password);
  state.connection = connection; // tests feed recorded packets through it (dispatch)
  const character = await chooseCharacter(connection);
  ui.characters.hidden = true;
  showLoadingScreen(); // until the world is ready (CLoadingScene)
  await Promise.all([loadMonsterCatalog(), loadItemCatalog(), loadGlowColors(), loadSkillData(), loadBuffData()]);
  // The server sends who is in view right after the character information, while
  // the map is still loading: keep those packets and replay them once it's ready.
  const earlyPackets: Uint8Array[] = [];
  const stopBuffering = connection.onPacket((bytes) => earlyPackets.push(bytes));
  // The inventory follows the server from the start: it sends the items right after the character.
  const inventory = new Inventory(connection);
  state.inventory = inventory;
  // The potion hotkeys come with the key configuration, when the character enters the world.
  const hotkeys = new ItemHotkeys(connection);
  state.hotkeys = hotkeys;
  // The skills come right after the character too; their hotkeys with the key configuration.
  const skills = new SkillList(connection, character.classIndex, (numbers) => hotkeys.saveSkillHotkeys(numbers));
  state.skills = skills;
  hotkeys.onConfiguration = (configuration) => skills.applyKeyConfiguration(configuration);
  // The key the server wants with warp requests (C1 8E 01, ReceiveMoveMapChecksum); it comes early.
  let moveCommandKey = 0;
  connection.onPacket((bytes) => {
    if (bytes[0] === 0xc1 && bytes[2] === 0x8e && bytes[3] === 0x01 && bytes.length >= 8) {
      moveCommandKey = (bytes[4] | (bytes[5] << 8) | (bytes[6] << 16) | (bytes[7] << 24)) >>> 0;
    }
  });
  // Buffs come as they're switched on, from the start too.
  const buffs = new BuffTracker(connection, () => connection.playerId ?? 0);
  state.buffs = buffs;
  const info = await enterWorld(connection, character.name);
  inventory.money = typeof info.Money === 'number' ? info.Money : 0;
  // Level, experience, life, mana and stats from here on (the HUD and the tooltips read them).
  const stats = new CharacterStats(connection, character.level, info);
  state.stats = stats;
  state.itemWindows = new ItemWindows({
    inventory,
    groundCell: (event) => (event.target === ui.canvas ? pickCell(event) : null),
    character: () => ({
      classIndex: character.classIndex, level: stats.level, strength: stats.strength, agility: stats.agility,
      vitality: stats.vitality, energy: stats.energy, leadership: stats.leadership, maxLife: stats.maxLife, maxMana: stats.maxMana,
    }),
  });
  inventory.onChange((what) => { if (what === 'inventory') wearInventory(character); });
  wearInventory(character); // what the hero wears now, from the inventory the server sent with the character
  ui.login.hidden = true;
  const playerId = connection.playerId!; // known since login()
  await loadMap(info.MapId);
  await placeHero({ id: playerId, name: character.name, bodySet: character.classIndex + 1, equipment: character.equipment, x: info.X, y: info.Y });
  stopBuffering();
  // The world and the hero are there now; the map changes only come once state.online is set.
  state.online = new OnlineWorld({
    connection, root, world: state.world!, hero: state.hero!, playerId,
    onMessage: (text, kind, sender) => chat.add(text, kind, sender),
    onMapChanged: async (map, x, y) => {
      state.ground?.clear(); // the server sends the new map's drops
      await loadMap(map);
      state.online!.world = state.world!;
      state.hero!.placeAt(x, y);
      void prewarmMapMonsters(warmer, map, () => state.mapNumber === map);
      chat.add(`Welcome to ${mapName(map)}.`, 'system');
      setStatus(`${mapName(map)}. Enter opens the chat; /move <map> warps.`, 1);
    },
  });
  const online = state.online;
  // A monster or player is drawn first with its textures uploaded and programs ready (no stall the frame it shows).
  online.prepare = (character) => warmObject(warmer, character.group);
  if (state.mapNumber !== null) { const map = state.mapNumber; void prewarmMapMonsters(warmer, map, () => state.mapNumber === map); }
  // Players coming into view bring their buffs; leaving it forgets them.
  online.onEffects = (id, effects) => { if (effects) buffs.replace(id, effects); else buffs.forget(id); };
  const itemWindows = state.itemWindows;
  const num = (name: string) => (typeof info[name] === 'number' ? info[name] as number : 0);
  setPlayerAnimationSpeeds(num('AttackSpeed'), num('MagicSpeed'));
  const overlay = new CombatOverlay(camera, ui.canvas, root);
  state.overlay = overlay;
  state.ground = new GroundItems(connection, root, camera, ui.canvas, {
    hero: () => online.hero,
    walkTo: (cell) => { online.walkTo(cell); },
    send: (id) => connection.send('ClientToServer', 'PickupItemRequest', { ItemId: id }),
  });
  // The Dark Raven's attacks the server reports (ReceivePetAttack, C1 A8): a rush at the target or a missile.
  connection.onPacket((bytes) => {
    if (bytes[0] !== 0xc1 || bytes[2] !== 0xa8 || bytes.length < 9) return;
    const packet = read<{ Pet: number; SkillType: number; OwnerId: number; TargetId: number }>('ServerToClient', 'PetAttack', bytes);
    const ownerId = packet.OwnerId & 0x7fff; // the high bit marks a new object
    const owner = ownerId === online.playerId ? online.hero : online.characters.get(ownerId);
    const target = online.characters.get(packet.TargetId & 0x7fff);
    const raven = owner?.companions.find((c): c is DarkRaven => c instanceof DarkRaven);
    if (!raven || !target) return;
    raven.attack({
      target: () => (target.group.parent ? new THREE.Vector3(target.position.x, target.position.y, target.group.position.z) : null),
      magic: packet.SkillType === 1,
      sound: (id) => sounds.audio.play(id),
    });
  });
  // Rudolph, the Panda, the Unicorn and the Skeleton pick up the hero's zen nearby (PetActionCollecter).
  setZenCollector({
    heroPosition: () => online.hero.position,
    find: (x, y, radius) => {
      for (const drop of state.ground?.drops.values() ?? []) {
        if (drop.item) continue;
        const p = drop.group.position;
        if (Math.hypot(p.x - x, p.y - y) < radius) return { id: drop.id, x: p.x, y: p.y, z: p.z };
      }
      return null;
    },
    exists: (id) => state.ground?.drops.has(id) ?? false,
    pickup: (id) => { if (!state.ground?.pickingUp) connection.send('ClientToServer', 'PickupItemRequest', { ItemId: id }); },
  });
  const heightAt = (x: number, y: number) => (state.world ? sampleHeight(state.world.heights, x, y) : 0);
  void loadCombatEffectsData();
  const projectiles = new ProjectileLayer(root, { heightAt, characters: () => online.characters.values(), hero: () => online.hero });
  const deaths = new DeathEffects(root, { heightAt, map: () => state.mapNumber ?? 0 });
  state.projectiles = projectiles;
  state.deaths = deaths;
  // Skill effects (the desktop's effect system): played when anyone casts.
  const effects = new EffectSystem(root, { camera, heightAt, heroPosition: () => (state.hero ? state.hero.position : null) });
  state.effects = effects;
  // The effect models, textures and programs are made ready in the background, so the first cast of a skill doesn't stall.
  setTimeout(() => { void effects.prewarm((object) => warmObject(warmer, object)); }, 3_000);
  // The items' glow passes (levels, shines, excellent, ancient, the cloak's chrome) likewise, on a sword.
  setTimeout(() => {
    void prewarmItemGlow((level, excellent, ancient, place) => createItemModel({ group: 0, number: 0, glowLevel: level, excellent, ancient }, place),
      (object) => warmObject(warmer, object));
  }, 6_000);
  setCompanionEffects(effects); // pets' and mounts' glows
  // The weapon trails of swinging players (CreateWeaponBlur).
  const weaponTrails = new WeaponTrails(() => (performanceFlags().reducedEffects ? [] : [online.hero, ...online.characters.values()]));
  effects.addLayer(weaponTrails);
  state.weaponTrails = weaponTrails;
  // A character as the effects see it; its aura joints live while it has eBuff_Attack, _Defense or _HelpNpc.
  const ownerFromCharacter = (who: Character) => {
    const has = (buff: number) => buffs.has(who === online.hero ? online.playerId : who.id, buff);
    return effectOwnerOf(who, { hasAura: () => [1, 2, 3].some(has), hasBuff: has });
  };
  // What appears with a monster (its setup's spawnEffects).
  online.onAdded = (who) => { if (who.look?.setup.spawnEffects) void playSpawnEffects(effects, who, ownerFromCharacter(who)); };
  // The +15 weapons' sparkles in the hands of every player shown (not in Chaos Castle).
  keepSparkles = () => keepNextGradeSparkles(effects, function* () {
    if (performanceFlags().reducedEffects) return;
    for (const who of [online.hero, ...online.characters.values()]) {
      if (who !== online.hero && who.kind !== 'player') continue;
      const weapons = who.attachments.filter((a) => (a.slot === 'rightHand' || a.slot === 'leftHand') && (a.item.glowLevel ?? 0) >= 15)
        .map((a) => ({ hand: a.slot === 'rightHand' ? 0 as const : 1 as const, level: a.item.glowLevel ?? 0, bow: a.item.group === 4 }));
      if (weapons.length) yield { owner: ownerFromCharacter(who), weapons };
    }
  }, () => { const map = state.mapNumber ?? -1; return !((map >= 18 && map <= 23) || map === 53); });
  // The buffs' looks (InsertBuffPhysicalEffect / ClearBuffLogicalEffect): Soul Barrier's lights.
  const knownBuffs = new Map<number, number[]>();
  buffs.onChange((id) => {
    const who = id === online.playerId ? online.hero : online.characters.get(id);
    const now = buffs.of(id), before = knownBuffs.get(id) ?? [];
    knownBuffs.set(id, [...now]);
    if (!who) return;
    const player = who.kind === 'player' || who === online.hero;
    // The body's colour while a debuff lasts (poisoned, frozen, stunned...).
    const tint = debuffTint((buff) => now.includes(buff), player);
    who.bodyTint = tint ? new THREE.Color(tint[0], tint[1], tint[2]) : null;
    // Debuffed, the items lose their level's glow; hardened or cloaked also their excellent / ancient glow; cloaked
    // the character is a chrome shell (RenderPartObjectEffect, players' parts).
    const has = (buff: number) => now.includes(buff);
    // A frozen monster (or one under Blow of Destruction) is drawn a second time, glowing in its colour.
    const glowing = !player && tint !== null && (has(Buff.FREEZE) || has(Buff.BLOW_OF_DESTRUCTION));
    who.setBodyGlow(glowing ? new THREE.Color(...(has(Buff.FREEZE) ? tint : [0.3, 0.5, 1] as const)) : null);
    who.setItemLooks({
      debuffed: player && tint !== null, hardened: player && has(Buff.HARDEN), cloaked: player && has(Buff.CLOAKING),
    });
    for (const buff of now) if (!before.includes(buff)) playBuffEffect(effects, buff, ownerFromCharacter(who), true, player);
    for (const buff of before) if (!now.includes(buff)) playBuffEffect(effects, buff, ownerFromCharacter(who), false, player);
  });
  // Dark Side's dash (the desktop keeps one, the hero's): its state ticks with the effects, its copies are drawn here.
  const darkSideGhosts = new DarkSideGhosts(root);
  let darkSide: DarkSideDash | null = null;
  let darkSideTicking = false;
  const heroDarkSide = (): DarkSideDash => {
    const owner = ownerFromCharacter(online.hero);
    if (!darkSide || darkSide.caster !== owner) darkSide = new DarkSideDash(effects, owner);
    if (!darkSideTicking) {
      darkSideTicking = true;
      effects.schedule(() => {
        const dash = heroDarkSide();
        dash.tick();
        if (dash.drawing) online.hero.angle = 45 * (Math.PI / 180); // readying turns the fighter (UpdateDrawnCharacterEffects)
        darkSideGhosts.show(online.hero, dash.placements);
        if (dash.drawing || dash.attackTime > 0) return true;
        darkSideGhosts.hide();
        darkSideTicking = false;
        return false;
      });
    }
    return darkSide;
  };
  state.darkSide = () => ({ dash: darkSide, ghosts: darkSideGhosts.shown, made: darkSideGhosts.made });
  state.combat = new Combat(connection, online, inventory, character.classIndex, {
    onDamage: (target, texts) => {
      overlay.addNumbers(() => Combat.anchorOf(target, 0), texts);
      if (target.dead) return;
      if (target.kind === 'player' || target === online.hero) sounds.playerHurt(target === online.hero ? character.classIndex : target.classIndex ?? 0, false);
      else {
        // SetPlayerShock: a player-shaped monster screams as a player, a skeleton rattles; the set plays for all.
        if (target.skeletonShape) sounds.play('SOUND_BONE1');
        else if (target.playerShaped) sounds.playerHurt(target.classIndex ?? 0, false);
        sounds.monster(target, { moment: 'hit', animation: 'shock' }, online.hero);
      }
    },
    onMessage: (text) => chat.add(text, 'system'),
    onTargetChanged: (target) => overlay.showTarget(target),
    // Arrows leave the bow a moment into the swing (CreateArrow); archers among the monsters shoot theirs.
    onSwing: (actor, target, look) => {
      if (actor.skeletonShape) sounds.play(Math.random() < 0.5 ? 'SOUND_BRANDISH_SWORD01' : 'SOUND_BRANDISH_SWORD02');
      else if (actor.playerShaped) sounds.swing(actor.equipment?.rightHand ?? null, actor.equipment?.leftHand ?? null);
      if (actor.kind === 'monster' || actor.playerShaped) sounds.monster(actor, { moment: 'attack', animation: 'attack1' }, online.hero);
      else if (look) sounds.swing(look.rightHand, look.leftHand);
      else sounds.swing(actor.equipment?.rightHand ?? null, actor.equipment?.leftHand ?? null);
      const hand = (item: { group: number; number: number } | null | undefined) => (item ? { group: item.group, number: item.number } : null);
      const kinds = look ? arrowKindsForWeapons(look.rightHand, look.leftHand)
        : actor.kind === 'player' ? arrowKindsForWeapons(hand(actor.equipment?.rightHand), hand(actor.equipment?.leftHand))
        : actor.monsterNumber !== null ? arrowKindsForMonster(actor.monsterNumber, target !== null) : [];
      if (!kinds.length || !target) return;
      setTimeout(() => { for (const kind of kinds) void projectiles.shoot(actor, target, kind, { fenrir: look?.mount === 'fenrir' }); }, ARROW_RELEASE_DELAY_SECONDS * 1000);
    },
    onKilled: (dead) => {
      deaths.died(dead, dead.kind === 'player' ? null : dead.monsterNumber);
      if (dead.kind === 'player' || dead === online.hero) sounds.playerHurt(dead === online.hero ? character.classIndex : dead.classIndex ?? 0, true);
      else {
        // SetPlayerDie: a skeleton (and the Death Cow) bursts into bones; another player-shaped one screams.
        if (dead.skeletonShape || dead.monsterModel === 'MONSTER_MODEL_DEATH_COW') {
          dead.group.visible = false;
          const at = dead.group.position.clone();
          effects.createEffect('bone1', at, new THREE.Vector3(0, 0, THREE.MathUtils.radToDeg(dead.angle)), new THREE.Vector3(dead.light.r, dead.light.g, dead.light.b));
          for (let j = 0; j < 10; j++) effects.createEffect('bone2', at, new THREE.Vector3(0, 0, THREE.MathUtils.radToDeg(dead.angle)), new THREE.Vector3(dead.light.r, dead.light.g, dead.light.b));
          sounds.play('SOUND_BONE2');
        } else if (dead.playerShaped) sounds.playerHurt(dead.classIndex ?? 0, true);
        sounds.monster(dead, { moment: 'death', animation: 'die' }, online.hero);
      }
    },
    onHeroAttacked: (attacker) => state.autoPlay?.attackedBy(attacker),
    onSkill: (caster, castSkill, target, cell) => {
      sounds.skill(castSkill);
      const skill = masterSkillToBaseSkill(castSkill);
      // Triple Shot's fan of arrows goes through the arrows' own layer (CreateArrows).
      const hands = caster === online.hero
        ? { right: inventory.items.get(0), left: inventory.items.get(1) }
        : { right: caster.equipment?.rightHand ?? undefined, left: caster.equipment?.leftHand ?? undefined };
      const kinds = arrowKindsForWeapons(hands.right ? { group: hands.right.group, number: hands.right.number } : null, hands.left ? { group: hands.left.group, number: hands.left.number } : null);
      if (caster === online.hero && (castSkill === 263 || castSkill === 559)) {
        // UseSkillRagefighter: m_sTargetIndex is the selected target (or none); RageCreateEffect starts the afterimages anew.
        const dash = heroDarkSide();
        dash.mainTarget = target ? ownerFromCharacter(target) : null;
        dash.clearDummies();
        darkSideGhosts.prepare(online.hero);
      }
      const perf = performanceFlags();
      if (caster !== online.hero && caster.kind === 'player' && (perf.hideOtherPlayers || perf.reducedEffects)) return;
      if (isNewSkill(skill)) { playNewSkillEffect(effects, skill, ownerFromCharacter(caster), target ? ownerFromCharacter(target) : null); return; }
      playSkillEffect(effects, skill, ownerFromCharacter(caster), target ? ownerFromCharacter(target) : null, {
        shootArrow: (angleOffsetDegrees) => { if (target) for (const kind of kinds) void projectiles.shoot(caster, target, kind, { angleOffsetDegrees }); },
        bowNumbers: { rightHand: hands.right?.number, leftHand: hands.left?.number },
        cell,
        casterIsHero: caster === online.hero,
        summonBookLevel: caster === online.hero ? inventory.items.get(1)?.level ?? 0 : caster.equipment?.leftHand?.glowLevel ?? 0,
        // GetFenrirType: the horn's look (helperVariant 1 black, 2 blue, 3 gold; else red).
        fenrirColor: [1, 0, 2, 3][caster.equipment?.pet?.variant ?? 0] ?? 1,
      });
    },
    onDarkSideTargets: (_skill, ids) => {
      const named = heroDarkSide().setTargets(ids, (id) => {
        const found = id === online.playerId ? online.hero : online.characters.get(id & 0x7fff);
        return found ? ownerFromCharacter(found) : null;
      });
      if (named) sounds.play('SOUND_RAGESKILL_DARKSIDE_ATTACK');
      return named;
    },
    onRageAttack: (caster, castSkill, target) => {
      if (caster !== online.hero || (castSkill !== 263 && castSkill !== 559)) return;
      const dash = heroDarkSide();
      dash.attackTime = 1; // ReceiveStraightAttack: sc->AttackTime = 1
      dash.mainTarget = target ? ownerFromCharacter(target) : null;
      darkSideGhosts.prepare(online.hero);
    },
    onSkillHit: (caster, castSkill, target) => {
      const perf = performanceFlags();
      if (caster !== online.hero && caster.kind === 'player' && (perf.hideOtherPlayers || perf.reducedEffects)) return;
      playSkillHit(effects, masterSkillToBaseSkill(castSkill), ownerFromCharacter(caster), ownerFromCharacter(target));
    },
    onChainLightning: (caster, _skill, targets) => {
      const perf = performanceFlags();
      if (caster !== online.hero && caster.kind === 'player' && (perf.hideOtherPlayers || perf.reducedEffects)) return;
      playChainLightning(effects, ownerFromCharacter(caster), targets.map(ownerFromCharacter));
    },
    onHeroDied: () => chat.add('You died.', 'system'),
    onRespawn: async (map, x, y) => {
      if (map !== state.mapNumber) { await loadMap(map); online.world = state.world!; }
      state.hero!.placeAt(x, y);
    },
  });
  // Using items: Q W E R and the HUD's potion slots, a right click in the bag.
  const itemUser = new ItemUser(connection, inventory, (text) => chat.add(text, 'system'));
  itemUser.onUsed = (item) => sounds.used(item);
  stats.onChange((change) => { if (change === 'levelUp') sounds.levelUp(); });
  sounds.audio.stopMusic(loginMusic(false)); // the login theme stops on entering the world
  const useHotkey = (slot: number) => {
    if (online.hero.dead) return;
    const bagSlot = hotkeys.slotOf(slot, inventory.items);
    if (bagSlot !== null) itemUser.use(bagSlot);
  };
  const inSafeZone = () => {
    const { x, y } = online.hero.cell;
    return state.world ? (state.world.flags[y * TERRAIN_SIZE + x] & TerrainFlag.SAFEZONE) !== 0 : false;
  };
  itemWindows.bag.onRightClick = (slot, item) => rightClickBagItem(slot, item, {
    inventory, user: itemUser, classIndex: character.classIndex, level: stats.level, strength: stats.strength, energy: stats.energy,
    safeZone: inSafeZone(), message: (text) => chat.add(text, 'system'),
    heroCell: () => (online.hero.dead ? null : { ...online.hero.cell }),
    sell: (slot, item) => itemWindows.sell(slot, item),
  });
  itemWindows.message = (text) => chat.add(text, 'system');
  // Trading with other players (CTrade): requests, the trade window, its packets.
  state.trade = new Trade({
    connection, inventory, itemWindows,
    message: (text) => chat.add(text, 'system'),
    confirm: (text) => confirmInGame(text),
    level: () => stats.level, heroName: () => character.name,
    playerName: (id) => online.characters.get(id)?.name,
  });
  // The chaos machine and the other mix NPCs (CMixInventory).
  state.chaosMachine = new ChaosMachine({ connection, inventory, itemWindows, message: (text) => chat.add(text, 'system'),
    confirm: (text) => confirmInGame(text), level: () => stats.level, play: (id) => sounds.audio.play(id) });
  // The personal store: the own store (S, /personalshop), others' stores (/purchase), the titles over the heads.
  state.personalShop = new PersonalShop({ connection, inventory, itemWindows, message: (text) => chat.add(text, 'system'), confirm: confirmInGame, level: () => stats.level, heroName: character.name, heroId: () => online.playerId, characters: () => online.characters, hero: () => online.hero, mapNumber: () => state.mapNumber ?? 0, camera, canvas: ui.canvas, root, tooltipCharacter: () => ({ classIndex: character.classIndex, level: stats.level, strength: stats.strength, agility: stats.agility, vitality: stats.vitality, energy: stats.energy, leadership: stats.leadership, maxLife: stats.maxLife, maxMana: stats.maxMana }) });
  earlyPackets.forEach((bytes) => state.personalShop?.handle(bytes));
  // The cash shop (X or the HUD's shop button), with HNMO's free coins.
  state.cashShop = new CashShop({ connection, heroName: character.name, safeZone: inSafeZone, walking: () => online.hero.isWalking, closeOthers: () => itemWindows.closeAll(), onToggle: (open) => state.hud?.setCashShopOpen(open), click: () => sounds.click() });
  earlyPackets.forEach((bytes) => state.cashShop?.handle(bytes));
  // Jewels: a jewel dropped on an item is used on it (ConsumeItemRequest with the target slot).
  itemWindows.bag.onDropOnItem = (from, slot, target) => {
    if (from.storage !== 0 || from.slot < 12) return false;
    const applies = jewelApplies(from.item, target);
    if (!applies) return false;
    if (applies === 'confirm' && !window.confirm('Would you like to repair Fenrir\'s horn?')) return true;
    itemUser.use(from.slot, slot);
    return true;
  };
  // What repairing everything costs at a repair shop (RepairAllGold, the NPC's price).
  void loadItemValueData().then(() => { itemWindows.shop.repairAllCost = () => repairAllCost(inventory.items, false); itemWindows.refresh(); })
    .catch((error: unknown) => console.warn('Item prices', error));
  // The smiths and weapon merchants also repair (SetRepairShop: Eo, Zienna, Hanzo, Rhea, Bolo).
  online.onTalk = (npc) => { itemWindows.shop.repairShop = [243, 246, 251, 416, 578].includes(npc.monsterNumber ?? -1); };
  inventory.onChange((what) => { if (what === 'buyFailed') chat.add('You cannot buy this item.', 'system'); });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    const slot = HOTKEY_LETTERS.indexOf(event.key.toUpperCase() as typeof HOTKEY_LETTERS[number]);
    if (slot < 0 || event.altKey || event.metaKey) return;
    event.preventDefault();
    if (event.ctrlKey) {
      // Ctrl + Q/W/E/R over a bag item binds the key to it (CMyInventory::UpdateKeyEvent).
      const hovered = itemWindows.bag.isOpen ? itemWindows.bag.hoverSlot : null;
      const item = hovered !== null && hovered >= 12 ? inventory.items.get(hovered) : undefined;
      if (item) hotkeys.bind(slot, item);
      return;
    }
    if (!event.repeat) useHotkey(slot);
  });
  // The character window (C): the rightmost docked window; the inventory moves left of it while it's open.
  const characterWindow = new CharacterWindow({
    connection, stats, inventory, classIndex: character.classIndex, name: character.name,
    currentSkill: () => skills.skillAt(skills.current),
    onQuest: () => quests.toggleMyQuest(),
    onToggle: (open) => {
      if (open) state.partyWindow?.close();
      state.addStats?.setCharacterWindowOpen(open);
      itemWindows.dockedRight = open ? 1 : 0;
      itemWindows.placeAll();
    },
  });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'c' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void characterWindow.toggle(); }
  });
  state.hud = new Hud(stats, { inventory: () => { void itemWindows.toggle(); }, character: () => { void characterWindow.toggle(); }, cashShop: () => { void state.cashShop?.toggle(); } }, {
    item: (slot) => { const bagSlot = hotkeys.slotOf(slot, inventory.items); return bagSlot === null ? null : inventory.items.get(bagSlot) ?? null; },
    count: (slot) => hotkeys.count(slot, inventory.items),
    use: useHotkey,
  });
  const hud = state.hud;
  // The buff strip: the hero's buffs; hover for what they do, right-click to cancel a skill's buff.
  const buffStrip = new BuffStrip({
    active: () => [...buffs.of(playerId), ...(state.guildLevels?.activeBuffs() ?? [])],
    secondsLeft: (buff) => buffs.secondsLeft(buff),
    tooltip: (lines, box) => {
      if (!lines || !box) { itemWindows.tooltip.hide(); return; }
      void itemWindows.tooltip.showLines(lines, { left: box.left, top: box.top, width: box.width, height: box.height });
    },
    cancel: (skill, name) => {
      if (window.confirm(`Do you want to cancel the ${name} skill?`)) connection.send('ClientToServer', 'MagicEffectCancelRequest', { SkillId: skill, PlayerId: playerId });
    },
    skillName: (skill) => skillInfo(skill)?.name ?? `Skill ${skill}`,
  });
  buffs.onChange((id) => { if (id === playerId) buffStrip.refresh(); });
  void buffStrip.open();
  // Auto play (the desktop's helper): Home starts and stops it, the bar shows where the hero is.
  const autoPlay = new AutoPlay(connection, {
    hero: () => online.hero,
    inSafeZone,
    monsters: () => online.characters.values(),
    cast: (skill, target) => state.combat?.cast(target, { ...target.cell }, skill) ?? false,
    attack: (target) => state.combat?.attack(target),
    busy: () => (state.combat?.busy ?? false) || online.hero.isPlayingOnce,
    skillUsable: (skill) => [...skills.skills.values()].includes(skill) && skillRequirementsMet(skill, stats, character.classIndex),
    knowsSkill: (skill) => [...skills.skills.values()].includes(skill),
    // The helper picks its next action itself after a support cast (CastSkill's Attacking = -1).
    castSupport: (skill, target) => {
      const combat = state.combat;
      if (!combat?.castable(skill)) return false;
      combat.cancel();
      return combat.cast(target, { ...target.cell }, skill);
    },
    actionBusy: () => online.hero.isWalking || online.hero.isPlayingOnce || (state.combat?.casting ?? false),
    hasBuff: (who, buff) => buffs.has(who === online.hero ? playerId : who.id, buff),
    party: () => (state.party?.members ?? []).map((member) => ({
      healthStep: member.healthStep,
      character: member.name === character.name ? online.hero : [...online.characters.values()].find((c) => c.kind === 'player' && c.name === member.name) ?? null,
    })),
    life: () => stats.life, maxLife: () => stats.maxLife, money: () => inventory.money,
    drinkHealingPotion: () => {
      for (const type of itemTypesInUseOrder('healingPotion')) {
        const slot = findItemReverse(inventory.items, type, -1);
        if (slot !== null) return itemUser.use(slot);
      }
      return false;
    },
    drops: () => [...(state.ground?.drops.values() ?? [])],
    pickUp: (id) => { const drop = state.ground?.drops.get(id); if (drop) state.ground!.pickUp(drop); },
    pickingUp: () => state.ground?.pickingUp ?? false,
    walkTo: (cell) => online.walkTo(cell),
    worn: () => {
      const result: { slot: number; percent: number; cost: number }[] = [];
      for (let slot = 0; slot < 12; slot++) {
        const item = inventory.items.get(slot);
        if (!item) continue;
        try {
          const max = maxDurability(item);
          if (max > 0) result.push({ slot, percent: Math.ceil((item.durability * 100) / max), cost: repairCost(item, true) });
        } catch { /* prices not loaded yet */ }
      }
      return result;
    },
    repair: (slot) => inventory.repair(slot, true),
    itemName: (item) => itemName(item.group, item.number),
    isJewel: (item) => itemDefinition(item.group, item.number)?.tags?.includes('jewel') ?? false,
    message: (text) => chat.add(text, 'system'),
  });
  // The settings: the desktop's defaults until the server's copy arrives (MuHelperConfigurationData).
  let autoPlayConfig: AutoPlayConfig = initialAutoPlayConfig();
  autoPlay.setConfig(autoPlayConfig);
  state.autoPlay = autoPlay;
  const settingsWindow = new AutoPlaySettingsWindow({
    skills, classIndex: character.classIndex, config: () => autoPlayConfig,
    onSave: (config) => {
      autoPlayConfig = config;
      autoPlay.setConfig(config);
      connection.send('ClientToServer', 'MuHelperSaveDataRequest', { HelperData: encodeAutoPlayConfig(config) });
    },
    onApply: (config) => autoPlay.setConfig(config),
    onMessage: (text) => chat.add(text, 'system'),
  });
  const applyServerConfig = (bytes: Uint8Array) => {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xae || bytes.length < 4 + AUTO_PLAY_DATA_LENGTH) return;
    autoPlayConfig = decodeAutoPlayConfig(bytes.subarray(4, 4 + AUTO_PLAY_DATA_LENGTH));
    autoPlay.setConfig(autoPlayConfig);
    settingsWindow.load(autoPlayConfig);
  };
  connection.onPacket(applyServerConfig);
  earlyPackets.forEach(applyServerConfig);
  const autoPlayBar = new AutoPlayBar({ settings: () => { void settingsWindow.toggle(); }, toggle: () => autoPlay.toggle() });
  state.autoPlayBar = autoPlayBar;
  void autoPlayBar.open();
  // The move window (M): the warp list; a click warps (WarpCommandRequest).
  const moveWindow = new MoveWindow({
    check: () => ({
      classIndex: character.classIndex, level: stats.level, money: inventory.money, outlaw: false,
      wings: inventory.items.get(7), helper: inventory.items.get(8), rings: [inventory.items.get(10), inventory.items.get(11)],
    }),
    warp: (warp) => connection.send('ClientToServer', 'WarpCommandRequest', { CommandKey: moveCommandKey, WarpInfoIndex: warp.index }),
  });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'm' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void moveWindow.toggle(); }
  });
  inventory.onChange(() => moveWindow.refresh());
  // Gates you walk into (CheckGate): the server takes the hero through (EnterGateRequest).
  state.gates = new GateWatcher({
    hero: () => state.hero && state.mapNumber !== null ? {
      map: state.mapNumber, x: state.hero.cell.x, y: state.hero.cell.y, level: stats.level, classIndex: character.classIndex,
      helper: inventory.items.get(8), wings: inventory.items.get(7),
    } : null,
    busy: () => mapsLoading > 0,
    enter: (gate) => connection.send('ClientToServer', 'EnterGateRequest', { GateNumber: gate, TeleportTargetX: 0, TeleportTargetY: 0 }),
    message: (text) => chat.add(text, 'system'),
  });
  // The mini map (Tab): the whole map with its NPCs and gates, the hero's arrow in the middle.
  const miniMap = new MiniMap({
    hero: () => state.hero ?? null,
    mapNumber: () => state.mapNumber,
    closed: () => connection.send('ClientToServer', 'CloseNpcRequest', {}),
  });
  state.miniMap = miniMap;
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key === 'Tab' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void miniMap.toggle(); }
  });
  // Ready in the background, so M opens at once (compiling a window's stylesheets takes a while).
  void loadWarps().then(() => prepareDocument('move_command')).then(() => prepareDocument('map_name')).catch((error: unknown) => console.warn('Move window', error));
  // HNMO extras: trips to town, selling junk, the stop timer, alerts.
  const healingCount = () => {
    let count = 0;
    for (const [slot, item] of inventory.items) if (slot >= 12 && itemDefinition(item.group, item.number)?.tags?.includes('healingPotion')) count += item.durability;
    return count;
  };
  const autoPlayExtras = new AutoPlayExtras(character.name, {
    active: () => autoPlay.active, start: () => autoPlay.requestStart(), stop: () => { autoPlay.requestStop(); autoPlay.stop(); },
    hero: () => online.hero, mapNumber: () => state.mapNumber, mapWarpName: (map) => mapName(map).toLowerCase(),
    say: (text) => online.say(text), walkTo: (cell) => online.walkTo(cell),
    npcs: () => [...online.characters.values()].filter((c) => c.kind === 'npc'), talkTo: (npc) => online.talkTo(npc),
    shopOpen: () => itemWindows.shop.isOpen && inventory.shop.size > 0, shopItems: () => inventory.shop,
    buy: (slot) => inventory.buy(slot), sell: (slot) => inventory.sell(slot), selling: () => inventory.sellingSlot !== null,
    closeShop: () => itemWindows.closeAll(),
    bagItems: () => [...inventory.items].filter(([slot]) => slot >= 12),
    // "Full": fewer than four free cells left.
    bagFull: () => {
      let free = 0;
      for (let slot = 12; slot < 76; slot++) if (inventory.fits(inventory.items, BAG, slot, { width: 1, height: 1 }, null)) free++;
      return free < 4;
    },
    healingPotions: healingCount,
    isHealingPotion: (item) => itemDefinition(item.group, item.number)?.tags?.includes('healingPotion') ?? false,
    // Kept: everything but plain weapons and armour (groups 0-11) that aren't expensive or unsellable.
    isKeeper: (item) => item.group > 11 || isHighValue(item) || isSellingBan(item),
    message: (text) => chat.add(text, 'system'),
  });
  state.autoPlayExtras = autoPlayExtras;
  const extrasPanel = new AutoPlayExtrasPanel(autoPlayExtras);
  // HNMO's missions window (J): daily and weekly missions, the streak, the login calendar, earned coins.
  const missions = new MissionsWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
    itemName: (group, number) => itemName(group, number),
  });
  state.missions = missions;
  connection.onPacket((bytes) => { missions.handle(bytes); });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'j' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); missions.toggle(); }
  });
  // HNMO's world bosses (N): the line on the screen while a boss is alive, the window, the announcements in the chat.
  const worldBosses = new WorldBossWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
    itemName: (group, number) => itemName(group, number),
    mapName: (map) => mapName(map),
  });
  state.worldBosses = worldBosses;
  connection.onPacket((bytes) => { worldBosses.handle(bytes); });
  worldBosses.start();
  const catchupCap = new CatchupBanner((name, fields) => connection.send('ClientToServer', name, fields));
  state.catchupCap = catchupCap;
  connection.onPacket((bytes) => { catchupCap.handle(bytes); });
  window.addEventListener('hnmo-catchup-open', () => { catchupCap.open(); });
  const seasonPlan = new SeasonPlanWindow((name, fields) => connection.send('ClientToServer', name, fields));
  state.seasonPlan = seasonPlan;
  connection.onPacket((bytes) => { seasonPlan.handle(bytes); });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea, select')) return;
    if (event.key === 'F8' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); seasonPlan.toggle(); }
  });
  window.addEventListener('hnmo-season-plan-open', () => { seasonPlan.open(); });
  const leftovers = new LeftoversWindow((name, fields) => connection.send('ClientToServer', name, fields));
  state.leftovers = leftovers;
  connection.onPacket((bytes) => { leftovers.handle(bytes); });
  window.addEventListener('hnmo-leftovers-open', () => { leftovers.open(); });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea, select')) return;
    if (event.key === 'F10' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); leftovers.toggle(); }
  });
  const mountChain = new MountChainWindow((name, fields) => connection.send('ClientToServer', name, fields));
  state.mountChain = mountChain;
  connection.onPacket((bytes) => { mountChain.handle(bytes); });
  window.addEventListener('hnmo-mount-chain-open', () => { mountChain.open(); });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea, select')) return;
    if ((event.key === 'U' || event.key === 'u') && event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); mountChain.toggle(); }
  });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'n' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); worldBosses.toggle(); }
  });
  // HNMO's event & boss timetable (H): every event, invasion and world boss with Sydney/local times, maps and drops.
  const eventAlerts = new EventAlertsWindow();
  const timetable = new TimetableWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    onOpenAlerts: () => eventAlerts.open(),
  });
  window.addEventListener('hnmo-event-alerts-open', () => eventAlerts.open());
  state.timetable = timetable;
  connection.onPacket((bytes) => { timetable.handle(bytes); });
  timetable.start();
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'h' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); timetable.toggle(); }
  });
  window.addEventListener('hnmo-timetable-open', () => timetable.open());
  // HNMO's daily lucky number (L): pick a number, view draw odds/prizes and claim waiting jewels.
  const luckyNumber = new LuckyNumberWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
  });
  state.luckyNumber = luckyNumber;
  connection.onPacket((bytes) => { luckyNumber.handle(bytes); });
  luckyNumber.start();
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'l' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); luckyNumber.toggle(); }
  });
  // HNMO's marriage system (Shift+M, /marry): proposals, Devias Church ceremonies, couple title, hunting bonus.
  const marriage = new MarriageWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
  });
  state.marriage = marriage;
  connection.onPacket((bytes) => { marriage.handle(bytes); });
  marriage.start();
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'm' && event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); marriage.toggle(); }
  });
  window.addEventListener('hnmo-marriage-open', () => marriage.open());
  // HNMO's item market (F6, or NPC 547 Market Julia): escrow listings for Zen or CashShop coins.
  const market = new MarketWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
  });
  state.market = market;
  connection.onPacket((bytes) => { market.handle(bytes); });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea, select')) return;
    if (event.key === 'F6' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); market.toggle(); }
  });
  window.addEventListener('hnmo-market-open', () => { market.open(); });
  // HNMO's Jewel Bank & master skill clear (F7).
  const jewelBank = new JewelBankWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
  });
  state.jewelBank = jewelBank;
  connection.onPacket((bytes) => { jewelBank.handle(bytes); });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea, select')) return;
    if (event.key === 'F7' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); jewelBank.toggle(); }
  });
  window.addEventListener('hnmo-jewel-bank-open', () => { jewelBank.open(); });
  // HNMO's guild levels: a panel next to the guild window (G) with the level, the experience, this week's ladder rank and
  // the buff (also on the buff strip); level-ups and ladder rewards are told in the chat.
  const guildLevels = new GuildLevelsWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
    itemName: (group, number) => itemName(group, number),
    buffsChanged: () => buffStrip.refresh(),
  });
  state.guildLevels = guildLevels;
  connection.onPacket((bytes) => { guildLevels.handle(bytes); });
  for (const bytes of earlyPackets) guildLevels.handle(bytes);
  guildLevels.start();
  const customRanks = createCustomRanks(connection);
  state.customRanks = customRanks;
  setCustomRankLookup((name) => customRanks.nameplateBadge(name));
  connection.onPacket((bytes) => { customRanks.handle(bytes); });
  for (const bytes of earlyPackets) customRanks.handle(bytes);
  customRanks.start();
  const teleportNpc = new TeleportNpcWindow((name, fields) => connection.send('ClientToServer', name, fields));
  state.teleportNpc = teleportNpc;
  connection.onPacket((bytes) => { teleportNpc.handle(bytes); });
  // Screenshots (Print Screen, as the desktop) and HNMO's share panel; the community window (K, when the inventory
  // isn't open): the Discord and Zalo coins, community events and their codes.
  screenshotHero = character.name;
  const screenshots = state.screenshots ?? new Screenshots({
    capture: () => {
      renderer.render(scene, camera);
      const source = renderer.domElement;
      const copy = document.createElement('canvas');
      copy.width = source.width;
      copy.height = source.height;
      copy.getContext('2d')?.drawImage(source, 0, 0);
      return copy;
    },
    message: (text) => state.chat?.add(text, 'system'),
    where: () => ({ character: screenshotHero, map: state.mapNumber !== null ? mapName(state.mapNumber) : '' }),
  });
  if (!state.screenshots) onLanguageChange(() => screenshots.texts());
  state.screenshots = screenshots;
  const community = new CommunityWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
    itemName: (group, number) => itemName(group, number),
    share: () => screenshots.open(),
  });
  state.community = community;
  connection.onPacket((bytes) => { community.handle(bytes); });
  for (const bytes of earlyPackets) community.handle(bytes);
  community.start();
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'k' && !event.ctrlKey && !event.altKey && !event.metaKey && !itemWindows.bag.isOpen) { event.preventDefault(); community.toggle(); }
  });
  // HNMO's top races (F9, a key the desktop doesn't use; also the touch "more" menu): the running races, the player's
  // rank, the top 10, the prizes, the time left, the past winners; starts, ends and prizes are told in the chat.
  const topRaces = new TopRacesWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
    itemName: (group, number) => itemName(group, number),
  });
  state.topRaces = topRaces;
  connection.onPacket((bytes) => { topRaces.handle(bytes); });
  for (const bytes of earlyPackets) topRaces.handle(bytes);
  topRaces.start();
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key === 'F9' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); topRaces.toggle(); }
  });
  window.addEventListener('hnmo-top-races-open', () => topRaces.toggle());
  // HNMO's gift codes: a window (the community window's button, the touch "more" menu, /code without a code) with a field
  // for a code, the result, the rewards waiting for room in the bag and the account's last codes. /code CODE goes to the
  // server's chat command, which answers in the chat.
  const giftCodes = new GiftCodesWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
    itemName: (group, number) => itemName(group, number),
  });
  state.giftCodes = giftCodes;
  connection.onPacket((bytes) => { giftCodes.handle(bytes); });
  for (const bytes of earlyPackets) giftCodes.handle(bytes);
  giftCodes.start();
  window.addEventListener('hnmo-gift-codes-open', () => giftCodes.open());
  // HNMO's top-up milestones ("thưởng mốc nạp", U key): cumulative VND rewards claimed once per step.
  const topUpMilestones = new TopUpMilestonesWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
  });
  state.topUpMilestones = topUpMilestones;
  connection.onPacket((bytes) => { topUpMilestones.handlePacket(bytes); });
  for (const bytes of earlyPackets) topUpMilestones.handlePacket(bytes);
  topUpMilestones.requestState();
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'u' && !event.ctrlKey && !event.altKey && !event.metaKey) {
      event.preventDefault();
      topUpMilestones.toggle();
    }
  });
  // HNMO's reset options (/reset, /reset info, /relife, /autoreset): the server answers in the chat; the reset state keeps the
  // character window's reset count right and adds a notice with today's count after a reset, a relife or delegated resets.
  const resets = new Resets({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
    setResets: (count) => stats.setResets(count),
  });
  state.resets = resets;
  connection.onPacket((bytes) => { resets.handle(bytes); });
  for (const bytes of earlyPackets) resets.handle(bytes);
  resets.start();
  // HNMO's account safety and the vault's lock (as the desktop's CStorageInventory): the lock button sets or removes the
  // vault PIN (the second password), a locked vault asks it before an item leaves it, the server asks for it when a trade
  // or the store needs it; locked items get a tooltip line; Alt + right click locks an item or asks its unlock.
  const accountSafety = new AccountSafety({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => { void confirmInGame(text, true); },
    changed: () => itemWindows.refresh(),
  });
  state.accountSafety = accountSafety;
  itemWindows.vault.isProtected = () => accountSafety.vaultProtected;
  itemWindows.vault.titleText = (locked) => `${safetyText('Storage')} (${safetyText(locked ? 'Close' : 'Open')})`;
  itemWindows.vault.model.storage_lock_click = () => { void accountSafety.lockButton(); };
  itemWindows.vault.model.lock_tooltip = safetyText('Warehouse Lock/Unlock');
  itemWindows.vault.onItemClick = () => {
    if (!accountSafety.vaultNeedsPin) return false;
    void accountSafety.verifyVaultPin();
    return true;
  };
  const takeZen = itemWindows.vault.model.storage_take_click;
  itemWindows.vault.model.storage_take_click = () => {
    if (accountSafety.vaultNeedsPin) void accountSafety.verifyVaultPin();
    else takeZen();
  };
  itemWindows.extraTooltipLines = (storage, slot) => accountSafety.tooltipLines(storage, slot);
  for (const window of [itemWindows.bag, itemWindows.vault]) {
    window.onAltRightClick = (slot) => { void accountSafety.toggleLock(window.storageKind, slot); return true; };
  }
  connection.onPacket((bytes) => { accountSafety.handle(bytes); });
  for (const bytes of earlyPackets) accountSafety.handle(bytes);
  accountSafety.start();
  // HNMO's new-player guide (Y): the current step, where to go (an arrow on the screen), the rewards; it opens by itself
  // for a character whose guide runs. The (i) guide and the touch "more" menu open it too.
  const onboarding = new OnboardingWindow({
    send: (name, fields) => connection.send('ClientToServer', name, fields),
    message: (text) => chat.add(text, 'system'),
    itemName: (group, number) => itemName(group, number),
    mapName: (map) => mapName(map),
    mapNumber: () => state.mapNumber,
    confirm: (text) => confirmInGame(text),
    direction: (x, y) => {
      const hero = online.hero, world = state.world;
      if (!hero || !world) return null;
      const rect = canvasRect();
      const from = toScene(hero.position.x, hero.position.y, sampleHeight(world.heights, hero.position.x, hero.position.y)).project(camera);
      const tx = cellCenter(x), ty = cellCenter(y);
      const to = toScene(tx, ty, sampleHeight(world.heights, tx, ty)).project(camera);
      const dx = (to.x - from.x) * rect.width, dy = (to.y - from.y) * rect.height;
      return { angle: Math.atan2(dx, dy), steps: Math.max(Math.abs(x - hero.cell.x), Math.abs(y - hero.cell.y)) };
    },
  });
  state.onboarding = onboarding;
  connection.onPacket((bytes) => { onboarding.handle(bytes); });
  onboarding.start();
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'y' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); onboarding.toggle(); }
  });
  window.addEventListener('hnmo-onboarding-open', () => onboarding.open());
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key === 'Home') { event.preventDefault(); autoPlay.toggle(); }
    if (event.key.toLowerCase() === 'z' && event.shiftKey) { event.preventDefault(); extrasPanel.toggle(); }
    else if (event.key.toLowerCase() === 'z' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void settingsWindow.toggle(); }
  });
  hud.setSkills({
    list: skills,
    usable: (skill) => skillRequirementsMet(skill, stats, character.classIndex) && skillUsableWithEquipment(skill, {
      right: inventory.items.get(0) ?? null, left: inventory.items.get(1) ?? null, helper: inventory.items.get(8) ?? null,
      strength: stats.strength, agility: stats.agility,
    }, (item) => itemDefinition(item.group, item.number)?.weaponKind),
    hasPet: () => false, // the Dark Lord's raven comes with the pets
    tooltip: (index, anchor) => {
      const view = itemWindows.tooltip;
      if (index === null || !anchor) { view.hide(); return; }
      const skill = index >= 120 && index < 124 ? index : skills.skillAt(index);
      void view.showLines(skillTooltipLines(skill, stats, character.classIndex), { left: anchor.x, top: anchor.y, width: 0, height: 0 }, { index }, { anchor });
    },
  });
  stats.onChange(() => hud.refreshSkills());
  // Casting: the right mouse button uses the current skill (its number from the skill list).
  if (state.combat) state.combat.skills = {
    current: () => { const skill = skills.skillAt(skills.current); return skill > 0 ? skill : null; },
    mana: () => stats.mana, ability: () => stats.ability,
    outOfMana: () => {
      // FindManaItemIndex: drink the best mana potion there is.
      for (const type of itemTypesInUseOrder('manaPotion')) {
        const slot = findItemReverse(inventory.items, type, -1);
        if (slot !== null) { itemUser.use(slot); return; }
      }
    },
  };
  // The number keys pick the current skill (CSkillList::UpdateKeyEvent); Ctrl + a number over a skill
  // of the popup puts it on that key.
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (!/^Digit[0-9]$/.test(event.code) || event.altKey || event.metaKey) return;
    const hotkey = Number(event.code.slice(5));
    event.preventDefault();
    if (event.ctrlKey) { if (hud.hoveredSkill >= 0) skills.setHotkey(hotkey, hud.hoveredSkill); return; }
    skills.useHotkey(hotkey);
  });
  inventory.onChange(() => { hud.refreshItemSlots(); hud.refreshSkills(); });
  hotkeys.onChange(() => hud.refreshItemSlots());
  void hud.open();
  // ReceiveQuestPrize (LegacyQuestReward, C1 A3): the class change quests' rewards — level-up
  // points (200, 202) for the hero; a class change (201-203) salutes; all of them light up the player.
  connection.onPacket((bytes) => {
    if (bytes[0] !== 0xc1 || bytes[2] !== 0xa3 || bytes.length < 7) return;
    const id = ((bytes[3] << 8) | bytes[4]) & 0x7fff, reward = bytes[5], count = bytes[6];
    if (reward < 200 || reward > 203) return;
    const who = id === online.playerId ? online.hero : online.characters.get(id);
    if (!who) return;
    if (who === online.hero && (reward === 200 || reward === 202)) stats.addLevelUpPoints(count);
    if (reward !== 200) who.playOnce(PlayerActions.SALUTE1);
    const owner = ownerFromCharacter(who);
    const up = owner.position.clone();
    up.z += 200;
    for (let i = 0; i < 15; i++) {
      effects.createSprite('light', owner.position.clone().setZ(owner.position.z + 40 + i * 12), 1.5, new THREE.Vector3(1, 0.9, 0.6));
      if (reward !== 200) effects.createSprite('shiny2', up, 1.2, new THREE.Vector3(1, 0.9, 0.6));
    }
    effects.createEffect('magicGround2', owner.position, owner.angle, new THREE.Vector3(1, 1, 1), 0, owner);
    sounds.audio.play('SOUND_CHANGE_UP');
  });
  // The party: invitations, the member list, /party.
  const party = new Party(connection, {
    confirm: (text) => confirmInGame(text),
    message: (text) => chat.add(text, 'system'),
    nameOf: (id) => online.characters.get(id)?.name ?? 'Someone',
  });
  state.party = party;
  // A party kept while the hero was away comes back with its list on entering (the server's auto rejoin).
  earlyPackets.forEach((bytes) => party.handle(bytes));
  const partyOptions = new PartyOptionsWindow((name, fields) => connection.send('ClientToServer', name, fields));
  state.partyOptions = partyOptions;
  connection.onPacket((bytes) => { partyOptions.handle(bytes); });
  window.addEventListener('hnmo-party-options-open', () => { partyOptions.open(); });
  // HNMO's bulk Add Stats window (Shift+A) & Player Rankings panel (Shift+R / /rank).
  const addStats = new AddStatsWindow(
    stats,
    character.classIndex,
    (name, fields) => connection.send('ClientToServer', name, fields),
    (text) => chat.add(text, 'system'),
  );
  state.addStats = addStats;
  const ranking = new RankingWindow(
    (name, fields) => connection.send('ClientToServer', name, fields),
    () => addStats.open(),
  );
  state.ranking = ranking;
  connection.onPacket((bytes) => { ranking.handle(bytes); });
  for (const bytes of earlyPackets) ranking.handle(bytes);
  window.addEventListener('hnmo-ranking-open', () => { ranking.open(); });
  window.addEventListener('hnmo-add-stats-open', () => { addStats.open(); });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea, select')) return;
    if (event.key.toLowerCase() === 'r' && event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) {
      event.preventDefault();
      ranking.toggle();
    } else if (event.key.toLowerCase() === 'a' && event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) {
      event.preventDefault();
      addStats.toggle();
    }
  });
  // The party window (P): docked at the right; opening it closes the inventory and the character window.
  const partyWindow = new PartyWindow({
    connection, party, heroName: character.name, mapName,
    click: () => sounds.click(),
    onOpen: () => { itemWindows.closeAll(); characterWindow.close(); },
    onToggle: (open) => { itemWindows.dockedRight = open ? 1 : 0; itemWindows.placeAll(); partyOptions.setPartyWindowOpen(open); },
  });
  state.partyWindow = partyWindow;
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'p' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void partyWindow.toggle(); }
  });
  // The command window (D): a button arms a command for the next right click on a player (ui/commandWindow.ts).
  const say = (text: string, kind: 'system' | 'error' = 'system') => chat.add(text, kind);
  const guildStatus = () => state.social?.guild.status ?? GuildStatus.NONE;
  const statusOf = (player: Character) => state.social?.guild.guildOf(player.id)?.player.status ?? GuildStatus.NONE;
  const masterChecked = (player: Character, run: () => void) => {
    if (guildStatus() !== GuildStatus.MASTER) { say('Not a guild master'); return; }
    if (statusOf(player) !== GuildStatus.MASTER) { say('The user is not a guild master.'); return; }
    run();
  };
  state.commandWindow = new CommandWindow({
    click: () => sounds.click(),
    hero: () => online.hero,
    playerAt: (event) => pickCharacter(event, ['player']),
    allowed: () => eventOfMap(state.mapNumber ?? null) !== 'chaosCastle',
    onOpen: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); },
    onToggle: (open) => { itemWindows.dockedRight = open ? 1 : 0; itemWindows.placeAll(); },
    actions: {
      trade: (player) => state.trade?.requestTrade(player.id),
      purchase: (player) => state.personalShop?.requestList(player.id),
      party: (player) => {
        if (party.members.length > 0 && party.members[0].name !== character.name) { say('You are already in a party', 'error'); return; }
        party.invite(player);
      },
      whisper: (player) => chat.setWhisperTarget(player.name),
      guild: (player) => {
        if (guildStatus() !== GuildStatus.NONE) { say('You are already in a guild'); return; }
        if (!state.social?.guild.guildOf(player.id) || statusOf(player) !== GuildStatus.MASTER) { say('The user is not a guild master.'); return; }
        state.social.guild.requestJoin(player);
      },
      alliance: (player) => {
        if (guildStatus() !== GuildStatus.MASTER) { say('Not a guild master'); return; }
        if (!state.social?.guild.guildOf(player.id)) { say('This does not belong to the guild.'); return; }
        masterChecked(player, () => state.social?.guild.changeRelationship(1, 1, player.id));
      },
      rival: (player) => masterChecked(player, () => state.social?.guild.changeRelationship(2, 1, player.id)),
      rivalOff: (player) => masterChecked(player, () => state.social?.guild.changeRelationship(2, 2, player.id)),
      addFriend: (player) => { if (state.social?.messenger.serverEnabled) state.social.messenger.addFriend(player.name); },
      follow: (player) => { online.followId = player.id; },
      duel: (player) => state.duelAndGens?.duel.challenge(player),
    },
  });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'd' && !event.ctrlKey && !event.altKey && !event.metaKey && !event.repeat) { event.preventDefault(); void state.commandWindow?.toggle(); }
  });
  // NPC dialogues and quests (npc_dialogue, quest_progress, my_quest_info on T, npc_quest; ui/questWindows.ts).
  const quests = new QuestWindows({ connection, message: (text) => chat.add(text, 'system'), sound: (name) => sounds.audio.play(name),
    hero: { name: character.name, baseClass: character.classIndex, level: () => stats.level, money: () => inventory.money, bagItems: () => [...inventory.items].filter(([slot]) => slot >= 12).map(([, item]) => item) },
    closeOthers: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); }, showItem: (item, box) => void itemWindows.tooltip.show(item, box), hideItem: () => itemWindows.tooltip.hide() });
  earlyPackets.forEach((bytes) => quests.handle(bytes));
  // Blood Castle, Devil Square, Chaos Castle: entry windows, tickets, time, countdowns, results (ui/eventWindows.ts).
  state.events = setUpEvents({ connection, heroName: character.name, hero: () => ({ level: stats.level, classIndex: character.classIndex, master: character.master ?? false }),
    mapNumber: () => state.mapNumber, flags: () => state.world?.flags ?? null, safeZone: inSafeZone, message: (text) => chat.add(text, 'system'), audio: sounds.audio,
    closeOthers: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); }, bag: itemWindows.bag, inventory });
  // Illusion Temple, Doppelganger, Imperial Guardian (ui/instanceEventWindows.ts).
  state.instanceEvents = setUpInstanceEvents({ connection, events: state.events, heroName: character.name, hero: () => ({ level: stats.level, classIndex: character.classIndex, master: character.master ?? false }), heroCharacter: () => online.hero, heroId: () => online.playerId, characters: () => online.characters.values(), mapNumber: () => state.mapNumber, message: (text, kind) => chat.add(text, kind), audio: sounds.audio, sounds, closeOthers: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); }, bag: itemWindows.bag, inventory, online, pickPlayer: (event) => pickCharacter(event, ['player']) });
  // The master level and skill tree (A, the character window's button; ui/masterLevelWindow.ts).
  setupMasterLevel({ connection, classNumber: character.classNumber ?? character.classIndex * 4, classIndex: character.classIndex, stats, skills, inventory, hud, characterWindow, earlyPackets, click: () => sounds.click(), closeOthers: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); } });
  void new PartyList(party, {
    heroName: character.name,
    inSight: (name) => name === character.name || [...online.characters.values()].some((c) => c.kind === 'player' && c.name === name),
  });
  chat.enable((text) => {
    const command = text.trim().toLowerCase();
    if (command === '/code') { state.giftCodes?.open(); return; }
    if (command === '/party' || command === '/pt') { party.command(online.hero, online.characters.values(), character.name); return; }
    if (state.personalShop?.command(text, () => facingPlayer(online.hero, online.characters.values()))) return;
    if (command === '/trade') {
      const partner = facingPlayer(online.hero, online.characters.values());
      if (partner) state.trade?.requestTrade(partner.id);
      else chat.add('Type /trade with the mouse cursor on a player to trade with them.', 'system');
      return;
    }
    online.say(text);
  });
  // Guild, friends, letters and whispers (game/social.ts): G, F, /guild, the guild master NPC.
  state.social = setupSocial({ connection, chat, heroName: character.name, earlyPackets, click: () => sounds.click(), heroCharacter: () => online.hero, players: () => online.characters.values(), hero: () => ({ level: stats.level, leadership: stats.leadership, classIndex: character.classIndex }), closeDocked: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); }, dockedChanged: (open) => { itemWindows.dockedRight = open ? 1 : 0; itemWindows.placeAll(); }, guildWindowToggled: (open) => { if (open) state.guildLevels?.open(); else state.guildLevels?.close(); } });
  // Duels (/DuelChallenge, the score window, Doorkeeper Titus) and Gens (the stewards, B); ui/duelAndGens.ts.
  state.duelAndGens = setUpDuelAndGens({ connection, heroName: character.name, hero: () => online.hero, players: () => online.characters.values(), level: () => stats.level, mapNumber: () => state.mapNumber, chat, buffs, quests, camera, canvas: ui.canvas, click: () => sounds.click(), sound: (id) => sounds.audio.play(id), duelBlocked: () => !!state.chaosMachine?.isOpen || itemWindows.vault.isOpen, closeOthers: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); }, earlyPackets });
  // Kalima's gates, Crywolf's HUD and altars, the castle siege's windows (ui/fieldEventWindows.ts).
  state.fieldEvents = setUpFieldEvents({ connection, online, hero: () => ({ level: stats.level, classIndex: character.classIndex }), inventory, mapNumber: () => state.mapNumber, world: () => state.world, worldGroup: () => state.worldGroup ?? null, camera, canvas: ui.canvas, audio: sounds.audio, music: sounds.musicStates, message: (text) => chat.add(text, 'system'), safeZone: inSafeZone, closeOthers: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); }, bag: itemWindows.bag, guild: () => state.social?.guild ?? null, openMix: (type) => { void state.chaosMachine?.open(type); } });
  chat.add(`Welcome to ${mapName(info.MapId)}. Try /move devias or /move noria.`, 'system');
  earlyPackets.forEach((bytes) => online.handle(bytes));
  hideLoadingScreen();
  connection.onClose = () => setStatus('Disconnected from the server. Reload the page to log in again.');
  warmCharacterModels();
  setStatus(`${character.name} entered the world. Left-click to walk, right-drag to turn, wheel to zoom, I for the inventory, click an NPC to talk.`, 1);
}

function startGame(play: () => Promise<void>): void {
  ui.loginError.hidden = true;
  ui.loginForm.querySelector('fieldset')!.disabled = true;
  play().then(() => {
    document.body.classList.add('ready');
    ui.hud.hidden = false;
    window.__viewerReady = true;
  }).catch((error) => {
    state.connection?.close();
    state.connection = undefined;
    console.error(error);
    ui.login.hidden = false;
    ui.servers.hidden = ui.characters.hidden = true;
    ui.loginForm.querySelector('fieldset')!.disabled = false;
    showError(error);
  });
}

function readTicketFromHash(): { account: string; ticket: string } | null {
  const hash = location.hash.startsWith('#') ? location.hash.slice(1) : location.hash;
  if (!hash) return null;
  const params = new URLSearchParams(hash);
  const ticket = params.get('ticket')?.trim() ?? '';
  const account = params.get('account')?.trim() ?? '';
  if (!ticket) return null;
  // Remove the one-time ticket from the address bar right away so it doesn't linger in history.
  history.replaceState(null, '', location.pathname + location.search);
  return { account, ticket };
}

function updateWebsiteLoginButtonLanguage(): void {
  const button = document.getElementById('websiteLoginButton');
  if (button) button.textContent = currentLanguage() === 'vi' ? 'Đăng nhập trên trang web' : 'Log in on the website';
}

function initLogin() {
  const saved = loadSettings();
  // HNMO.Web serves the page and the relay from the same address.
  const defaults = { relay: `${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/relay`, host: '127.0.0.1', port: 44406, username: '' };
  const initial = { ...defaults, ...saved };
  for (const [key, value] of Object.entries(initial)) {
    const input = ui.loginForm.elements.namedItem(key) as HTMLInputElement | null;
    if (input) input.value = String(value);
  }
  updateWebsiteLoginButtonLanguage();
  onLanguageChange(() => updateWebsiteLoginButtonLanguage());

  const websiteSettingsPromise = fetch('/website/settings', { cache: 'no-cache', signal: AbortSignal.timeout(3000) })
    .then((r) => (r.ok ? (r.json() as Promise<{ requireWebsiteLogin?: boolean; connectServerPort?: number }>) : null))
    .catch(() => null);

  void websiteSettingsPromise.then((d) => {
    if (d?.requireWebsiteLogin) {
      const fieldset = ui.loginForm.querySelector('fieldset');
      if (fieldset) fieldset.hidden = true;
    }
    if (d?.connectServerPort && d.connectServerPort > 0 && !saved.port) {
      const portInput = ui.loginForm.elements.namedItem('port') as HTMLInputElement | null;
      if (portInput && portInput.value === '44406') portInput.value = String(d.connectServerPort);
    }
  });

  const resolveConnectPort = async (form: LoginSettings): Promise<LoginSettings> => {
    if (!saved.port && form.port === '44406') {
      const d = await websiteSettingsPromise;
      if (d?.connectServerPort && d.connectServerPort > 0) {
        return { ...form, port: String(d.connectServerPort) };
      }
    }
    return form;
  };

  const pendingTicket = readTicketFromHash();
  if (pendingTicket) {
    const form: LoginSettings = {
      username: pendingTicket.account || String(initial.username ?? ''),
      password: '',
      host: String(initial.host),
      port: String(initial.port),
      relay: String(initial.relay),
    };
    const usernameInput = ui.loginForm.elements.namedItem('username') as HTMLInputElement | null;
    if (usernameInput && form.username) usernameInput.value = form.username;
    startGame(async () => playOnline(await resolveConnectPort(form), pendingTicket.ticket));
  }

  ui.loginForm.addEventListener('submit', (event) => {
    event.preventDefault();
    // The form's text inputs, by name.
    const data = new FormData(ui.loginForm);
    const field = (name: keyof LoginSettings) => String(data.get(name) ?? '');
    const form: LoginSettings = { username: field('username'), password: field('password'), host: field('host'), port: field('port'), relay: field('relay') };
    const { password, ...remembered } = form;
    saveSettings(remembered);
    startGame(async () => playOnline(await resolveConnectPort(form)));
  });
}

resize();
initLogin();
setUpGuide();
settingsWindow = setUpSettings(
  { renderer, scene, camera, effects: () => state.effects?.root },
  {
    performanceReport: () => { void perfWindow.open().then(() => perfWindow.record()); },
    textureQualityChanged: () => { clearTextureCache(); },
  },
);
// The login theme (it starts with the first click or key: browsers hold sound back until then).
sounds.audio.playMusic(loginMusic(false));
// The sound button: everything on or off, kept in the browser.
const soundButton = document.getElementById('soundButton');
if (soundButton) {
  const show = () => soundButton.setAttribute('aria-pressed', String(sounds.audio.getSettings().muted));
  soundButton.addEventListener('click', () => { sounds.audio.setMuted(!sounds.audio.getSettings().muted); show(); });
  show();
}
requestAnimationFrame(frame);

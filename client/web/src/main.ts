// The web client: log in through the relay and play on the HNMO server, or explore
// Lorencia offline. Left-click walks, right-drag turns the camera, the wheel zooms.

import * as THREE from 'three';
import { TERRAIN_SCALE, TERRAIN_SIZE, TerrainFlag } from './formats.js';
import { sampleHeight } from './terrain.js';
import { loadWorldData, createWorld, mapName, KNOWN_MAPS } from './world.js';
import { createPlayerCharacter, setPlayerAnimationSpeeds } from './game/characters.js';
import { findPath } from './game/pathfinding.js';
import { loadMonsterCatalog } from './game/monsterData.js';
import { itemDefinition, itemName, loadItemCatalog, itemsWithModels, ItemGroup, ARMOR_GROUPS, WEAPON_GROUPS } from './game/items.js';
import { OnlineWorld } from './game/online.js';
import { Chat } from './game/chat.js';
import { loadGlowColors } from './game/itemGlow.js';
import { BAG, Inventory } from './game/inventory.js';
import { ItemWindows } from './ui/itemWindows.js';
import { CharacterStats } from './game/stats.js';
import { Hud } from './ui/hud.js';
import { setUpGuide } from './ui/guide.js';
import { setUpSettings } from './ui/settingsWindow.js';
import { frameDue } from './render/quality.js';
import { findItemReverse, HOTKEY_LETTERS, isHighValue, jewelApplies, isSellingBan, ItemHotkeys, ItemUser, itemTypesInUseOrder, rightClickBagItem } from './game/itemUse.js';
import { loadSkillData, skillCooldowns, SkillList, skillInfo, skillRequirementsMet, skillUsableWithEquipment } from './game/skills.js';
import { skillTooltipLines } from './game/skillTooltip.js';
import { loadItemValueData, repairAllCost } from './game/itemValue.js';
import { CharacterWindow } from './ui/characterWindow.js';
import { setupMasterLevel } from './ui/masterLevelWindow.js';
import { AutoPlay } from './game/autoPlay.js';
import { AutoPlayBar } from './ui/autoPlayBar.js';
import { AutoPlayExtras } from './game/autoPlayExtras.js';
import { AutoPlayExtrasPanel } from './ui/autoPlayExtrasPanel.js';
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
import { confirmInGame } from './ui/confirmDialog.js';
import { QuestWindows } from './ui/questWindows.js';
import { setUpEvents, type EventSystem } from './ui/eventWindows.js';
import { MiniMap } from './ui/miniMap.js';
import { isNewSkill, playNewSkillEffect } from './game/newSkills.js';
import { PlayerActions } from './game/combatLook.js';
import { installTouchControls } from './ui/touchControls.js';
import { GateWatcher } from './game/gates.js';
import { Trade } from './ui/tradeWindow.js';
import { ChaosMachine } from './ui/mixWindow.js';
import { loginMusic } from './audio/index.js';
import { AUTO_PLAY_DATA_LENGTH, decodeAutoPlayConfig, encodeAutoPlayConfig, initialAutoPlayConfig, type AutoPlayConfig } from './game/autoPlayConfig.js';
import { AutoPlaySettingsWindow } from './ui/autoPlayWindows.js';
import { maxDurability, repairCost } from './game/itemValue.js';
import { EffectSystem, ownerFromCharacter, playSkillEffect } from './render/effects/index.js';
import { Combat } from './game/combat.js';
import { CombatOverlay } from './ui/combatOverlay.js';
import { GroundItems } from './game/groundItems.js';
import { ARROW_RELEASE_DELAY_SECONDS, DeathEffects, ProjectileLayer, arrowKindsForMonster, arrowKindsForWeapons, loadCombatEffectsData } from './game/combatEffects.js';
import { fetchServerList, fetchGameServerAddress, login, fetchCharacters, enterWorld, createCharacter, CLASSES } from './net/session.js';
import type { WorldData } from './world.js';
import type { TerrainMesh } from './terrain.js';
import type { Character } from './game/characters.js';
import type { AppearanceItem, EquipmentAppearance, EquipmentSlotName, ItemChoice } from './game/items.js';
import type { CharacterListEntry } from './net/session.js';
import type { Connection } from './net/connection.js';

// The map the hero is on: its data and the terrain mesh the pointer is cast against.
export type GameWorld = WorldData & { terrain: TerrainMesh };

// The app's state, also exposed as window.__game for automated tests and debugging.
export interface GameState {
  world: GameWorld | null;
  worldGroup?: THREE.Group;
  hero: Character | null;
  online: OnlineWorld | null;
  mapNumber: number | null;
  camera: THREE.PerspectiveCamera;
  inventory?: Inventory;
  itemWindows?: ItemWindows;
  stats?: CharacterStats;
  hud?: Hud;
  hotkeys?: ItemHotkeys;
  skills?: SkillList;
  combat?: Combat;
  overlay?: CombatOverlay;
  ground?: GroundItems;
  projectiles?: ProjectileLayer;
  deaths?: DeathEffects;
  effects?: EffectSystem;
  autoPlay?: AutoPlay;
  autoPlayBar?: AutoPlayBar;
  autoPlayExtras?: AutoPlayExtras;
  buffs?: BuffTracker;
  party?: Party;
  trade?: Trade;
  chaosMachine?: ChaosMachine;
  miniMap?: MiniMap;
  connection?: Connection;
  cooldowns?: typeof skillCooldowns;
  /** The camera's turn and distance (the mouse and the touch controls change them). */
  view?: { yaw: number; distance: number };
  gates?: GateWatcher;
  partyWindow?: PartyWindow;
  events?: EventSystem;
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

const OFFLINE_START = { map: 0, x: 133, y: 126 };
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
  offline: $<HTMLButtonElement>('offline'), hud: $('hud'), heroName: $('heroName'), labels: $('labels'), wardrobe: $('wardrobe'), bodySet: $<HTMLSelectElement>('bodySet'),
  armorSet: $<HTMLSelectElement>('armorSet'), rightHand: $<HTMLSelectElement>('rightHand'), leftHand: $<HTMLSelectElement>('leftHand'), wings: $<HTMLSelectElement>('wings'),
  previousSet: $<HTMLButtonElement>('previousSet'), nextSet: $<HTMLButtonElement>('nextSet'),
  holdWeapons: $<HTMLInputElement>('holdWeapons'), itemLevel: $<HTMLSelectElement>('itemLevel'), excellent: $<HTMLInputElement>('excellent'), ancient: $<HTMLInputElement>('ancient'),
  mapPicker: $('mapPicker'), mapSelect: $<HTMLSelectElement>('mapSelect'),
};

const chat = new Chat({ log: $('chatLog'), form: $<HTMLFormElement>('chatForm'), input: $<HTMLInputElement>('chatInput') });

const renderer = new THREE.WebGLRenderer({ canvas: ui.canvas, antialias: true });
renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
const scene = new THREE.Scene();
scene.background = FOG.color;
const camera = new THREE.PerspectiveCamera(35, 1, 10, 9000);
const root = new THREE.Group(); // game coordinates: x east, y north, z up
root.rotation.x = -Math.PI / 2;
scene.add(root);

const state: GameState = { world: null, hero: null, online: null, mapNumber: null, camera };
state.cooldowns = skillCooldowns;
window.__game = state; // for automated tests and debugging from the console

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
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
}
window.addEventListener('resize', resize);

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
  hero.holdWeapons = !state.online && ui.holdWeapons.checked;
  hero.placeAt(x, y);
  root.add(hero.group);
  ui.heroName.textContent = name;
}

// The hero wears what the inventory's equipment slots hold (the desktop client draws its own
// character from CharacterMachine->Equipment): put on again when that changes.
const EQUIPMENT_OF_SLOT: EquipmentSlotName[] = ['rightHand', 'leftHand', 'helm', 'armor', 'pants', 'gloves', 'boots', 'wings'];
let wornSignature: string | null = null;
async function wearInventory(character: CharacterListEntry): Promise<void> {
  const equipment: EquipmentAppearance = {};
  EQUIPMENT_OF_SLOT.forEach((name, slot) => {
    const item = state.inventory!.items.get(slot);
    equipment[name] = item ? { group: item.group, number: item.number, glowLevel: item.level, excellent: item.excellent > 0, ancient: item.ancientSet > 0 } : null;
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
}

const raycaster = new THREE.Raycaster();
const pointer = new THREE.Vector2();

function pickCell(event: MouseEvent): { x: number; y: number } | null {
  const rect = ui.canvas.getBoundingClientRect();
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
  const rect = ui.canvas.getBoundingClientRect();
  let best: { character: Character; distance: number } | null = null;
  for (const character of state.online.characters.values()) {
    if (!kinds.includes(character.kind) || character.dead) continue;
    npcBox.setFromObject(character.group);
    if (npcBox.isEmpty()) continue;
    const top = npcBox.max.clone().project(camera), bottom = npcBox.min.clone().project(camera);
    const toScreen = (v: THREE.Vector3) => ({ x: rect.left + (v.x + 1) / 2 * rect.width, y: rect.top + (1 - v.y) / 2 * rect.height });
    const a = toScreen(top), b = toScreen(bottom);
    const left = Math.min(a.x, b.x), right = Math.max(a.x, b.x), upper = Math.min(a.y, b.y), lower = Math.max(a.y, b.y);
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
    const monster = pickCharacter(event, ['monster']);
    const cell = monster ? { ...monster.cell } : pickCell(event);
    state.combat.cast(monster, cell);
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
});

// ---------- name labels ----------

const labelElements = new Map<Character, HTMLSpanElement>();

function updateLabels(): void {
  const visible = new Set<Character>();
  const hero = state.hero, world = state.world;
  if (state.online && hero && world) {
    const rect = ui.canvas.getBoundingClientRect();
    for (const character of state.online.characters.values()) {
      if (character.kind === 'monster') continue;
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

let last = performance.now();
let frames = 0, fpsTime = 0;

function frame(now: number): void {
  if (!frameDue(now)) { requestAnimationFrame(frame); return; } // the settings' FPS limit
  const dt = Math.min((now - last) / 1000, MAX_FRAME_SECONDS);
  last = now;
  frames++; fpsTime += dt;
  if (fpsTime >= 1) { ui.fps.textContent = `${frames} fps`; frames = 0; fpsTime = 0; }
  if (state.hero) {
    state.hero.update(dt, state.world!);
    state.online?.update(dt);
    state.combat?.update();
    if (state.ground && state.world) state.ground.update(state.world);
    state.deaths?.update(dt);
    state.projectiles?.update(dt);
    state.effects?.update(dt);
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
    ui.position.textContent = `${state.hero.cell.x}, ${state.hero.cell.y}`;
    updateCamera();
    updateLabels();
  }
  renderer.render(scene, camera);
  requestAnimationFrame(frame);
}

// ---------- login screens ----------

function loadSettings(): Record<string, unknown> {
  try { return JSON.parse(localStorage.getItem(SETTINGS_KEY) ?? 'null') ?? {}; } catch { return {}; }
}

function saveSettings(settings: Record<string, string>): void {
  try { localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings)); } catch { /* private window */ }
}

function showError(error: unknown): void {
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

async function playOnline(settings: LoginSettings): Promise<void> {
  const connectServer = { host: settings.host, port: Number(settings.port) };
  setStatus('Asking the connect server for game servers…');
  const servers = await fetchServerList(settings.relay, connectServer);
  if (!servers.length) throw new Error('The connect server lists no game servers.');
  const server = servers.length === 1 ? servers[0] : await chooseFrom(ui.servers, servers, (s) => `Server ${s.id + 1}<small>${s.load}% load</small>`);
  ui.servers.hidden = true;
  const address = await fetchGameServerAddress(settings.relay, connectServer, server.id);
  setStatus(`Logging in to ${address.host}:${address.port}…`);
  const connection = await login(settings.relay, address, settings.username, settings.password);
  state.connection = connection; // tests feed recorded packets through it (dispatch)
  const character = await chooseCharacter(connection);
  ui.characters.hidden = true;
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
    onMessage: (text, kind) => chat.add(text, kind),
    onMapChanged: async (map, x, y) => {
      state.ground?.clear(); // the server sends the new map's drops
      await loadMap(map);
      state.online!.world = state.world!;
      state.hero!.placeAt(x, y);
      chat.add(`Welcome to ${mapName(map)}.`, 'system');
      setStatus(`${mapName(map)}. Enter opens the chat; /move <map> warps.`, 1);
    },
  });
  const online = state.online;
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
  const heightAt = (x: number, y: number) => (state.world ? sampleHeight(state.world.heights, x, y) : 0);
  void loadCombatEffectsData();
  const projectiles = new ProjectileLayer(root, { heightAt, characters: () => online.characters.values(), hero: () => online.hero });
  const deaths = new DeathEffects(root, { heightAt, map: () => state.mapNumber ?? 0 });
  state.projectiles = projectiles;
  state.deaths = deaths;
  // Skill effects (the desktop's effect system): played when anyone casts.
  const effects = new EffectSystem(root, { camera, heightAt });
  state.effects = effects;
  state.combat = new Combat(connection, online, inventory, character.classIndex, {
    onDamage: (target, texts) => {
      overlay.addNumbers(() => Combat.anchorOf(target, 0), texts);
      if (target.dead) return;
      if (target.kind === 'player' || target === online.hero) sounds.playerHurt(target === online.hero ? character.classIndex : target.classIndex ?? 0, false);
      else if (target.kind === 'monster') sounds.monster(target, { moment: 'hit', animation: 'shock' }, online.hero);
    },
    onMessage: (text) => chat.add(text, 'system'),
    onTargetChanged: (target) => overlay.showTarget(target),
    // Arrows leave the bow a moment into the swing (CreateArrow); archers among the monsters shoot theirs.
    onSwing: (actor, target, look) => {
      if (actor.kind === 'monster') sounds.monster(actor, { moment: 'attack', animation: 'attack1' }, online.hero);
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
      if (dead.kind === 'monster') sounds.monster(dead, { moment: 'death', animation: 'die' }, online.hero);
      else sounds.playerHurt(dead === online.hero ? character.classIndex : dead.classIndex ?? 0, true);
    },
    onHeroAttacked: (attacker) => state.autoPlay?.attackedBy(attacker),
    onSkill: (caster, skill, target) => {
      sounds.skill(skill);
      // Triple Shot's fan of arrows goes through the arrows' own layer (CreateArrows).
      const hands = caster === online.hero
        ? { right: inventory.items.get(0), left: inventory.items.get(1) }
        : { right: caster.equipment?.rightHand ?? undefined, left: caster.equipment?.leftHand ?? undefined };
      const kinds = arrowKindsForWeapons(hands.right ? { group: hands.right.group, number: hands.right.number } : null, hands.left ? { group: hands.left.group, number: hands.left.number } : null);
      if (isNewSkill(skill)) { playNewSkillEffect(effects, skill, ownerFromCharacter(caster), target ? ownerFromCharacter(target) : null); return; }
      playSkillEffect(effects, skill, ownerFromCharacter(caster), target ? ownerFromCharacter(target) : null, {
        shootArrow: (angleOffsetDegrees) => { if (target) for (const kind of kinds) void projectiles.shoot(caster, target, kind, { angleOffsetDegrees }); },
        bowNumbers: { rightHand: hands.right?.number, leftHand: hands.left?.number },
      });
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
    onToggle: (open) => { if (open) state.partyWindow?.close(); itemWindows.dockedRight = open ? 1 : 0; itemWindows.placeAll(); },
  });
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'c' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void characterWindow.toggle(); }
  });
  state.hud = new Hud(stats, { inventory: () => { void itemWindows.toggle(); }, character: () => { void characterWindow.toggle(); } }, {
    item: (slot) => { const bagSlot = hotkeys.slotOf(slot, inventory.items); return bagSlot === null ? null : inventory.items.get(bagSlot) ?? null; },
    count: (slot) => hotkeys.count(slot, inventory.items),
    use: useHotkey,
  });
  const hud = state.hud;
  // The buff strip: the hero's buffs; hover for what they do, right-click to cancel a skill's buff.
  const buffStrip = new BuffStrip({
    active: () => buffs.of(playerId),
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
    tooltip: (index, box) => {
      const view = itemWindows.tooltip;
      if (index === null || !box) { view.hide(); return; }
      const skill = index >= 120 && index < 124 ? index : skills.skillAt(index);
      void view.showLines(skillTooltipLines(skill, stats, character.classIndex), { left: box.left, top: box.top, width: box.width, height: box.height }, { index }, { above: true });
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
  // The party window (P): docked at the right; opening it closes the inventory and the character window.
  const partyWindow = new PartyWindow({
    connection, party, heroName: character.name, mapName,
    click: () => sounds.click(),
    onOpen: () => { itemWindows.closeAll(); characterWindow.close(); },
    onToggle: (open) => { itemWindows.dockedRight = open ? 1 : 0; itemWindows.placeAll(); },
  });
  state.partyWindow = partyWindow;
  window.addEventListener('keydown', (event) => {
    if (event.target instanceof Element && event.target.closest('input, textarea')) return;
    if (event.key.toLowerCase() === 'p' && !event.ctrlKey && !event.altKey && !event.metaKey) { event.preventDefault(); void partyWindow.toggle(); }
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
  // The master level and skill tree (A, the character window's button; ui/masterLevelWindow.ts).
  setupMasterLevel({ connection, classNumber: character.classNumber ?? character.classIndex * 4, classIndex: character.classIndex, stats, skills, inventory, hud, characterWindow, earlyPackets, click: () => sounds.click(), closeOthers: () => { itemWindows.closeAll(); characterWindow.close(); partyWindow.close(); } });
  void new PartyList(party, {
    heroName: character.name,
    inSight: (name) => name === character.name || [...online.characters.values()].some((c) => c.kind === 'player' && c.name === name),
  });
  chat.enable((text) => {
    const command = text.trim().toLowerCase();
    if (command === '/party' || command === '/pt') { party.command(online.hero, online.characters.values(), character.name); return; }
    if (command === '/trade') {
      const partner = facingPlayer(online.hero, online.characters.values());
      if (partner) state.trade?.requestTrade(partner.id);
      else chat.add('Type /trade with the mouse cursor on a player to trade with them.', 'system');
      return;
    }
    online.say(text);
  });
  chat.add(`Welcome to ${mapName(info.MapId)}. Try /move devias or /move noria.`, 'system');
  earlyPackets.forEach((bytes) => online.handle(bytes));
  connection.onClose = () => setStatus('Disconnected from the server. Reload the page to log in again.');
  setStatus(`${character.name} entered the world. Left-click to walk, right-drag to turn, wheel to zoom, I for the inventory, click an NPC to talk.`, 1);
}

// ---------- wardrobe (offline) ----------

const NOTHING = '';

function fillSelect(select: HTMLSelectElement, items: ItemChoice[], { none = true } = {}): void {
  select.replaceChildren();
  if (none) select.append(new Option('Nothing', NOTHING));
  for (const item of items) select.append(new Option(`${item.name}`, `${item.group}:${item.number}`));
}

function selectedItem(select: HTMLSelectElement): { group: number; number: number } | null {
  if (select.value === NOTHING) return null;
  const [group, number] = select.value.split(':').map(Number);
  return { group, number };
}

// An armour set is the same item number in the helm, armour, pants, gloves and boots groups.
function armorSetEquipment(number: number | null): EquipmentAppearance {
  if (number === null) return {};
  const slots: EquipmentSlotName[] = ['helm', 'armor', 'pants', 'gloves', 'boots'];
  const equipment: EquipmentAppearance = {};
  ARMOR_GROUPS.forEach((group, index) => {
    if (itemsWithModels(group).some((item) => item.number === number)) equipment[slots[index]] = { group, number };
  });
  return equipment;
}

// The level and options every worn item gets, as the appearance bytes would carry them.
function wardrobeEquipment(): EquipmentAppearance {
  const armor = selectedItem(ui.armorSet);
  const options = { glowLevel: Number(ui.itemLevel.value), excellent: ui.excellent.checked, ancient: ui.ancient.checked };
  const withOptions = (item: AppearanceItem | null | undefined): AppearanceItem | null => (item ? { ...item, ...options } : null);
  const equipment: EquipmentAppearance = {
    ...armorSetEquipment(armor ? armor.number : null),
    rightHand: selectedItem(ui.rightHand),
    leftHand: selectedItem(ui.leftHand),
    wings: selectedItem(ui.wings),
  };
  return Object.fromEntries(Object.entries(equipment).map(([slot, item]) => [slot, withOptions(item)]));
}

const WING_NAME = /wing|cape|cloak|mantle/i;

function initWardrobe() {
  fillSelect(ui.armorSet, itemsWithModels(ItemGroup.ARMOR).map((item) => ({ ...item, name: item.name.replace(/ Armor$/, '') })));
  fillSelect(ui.rightHand, WEAPON_GROUPS.flatMap((group) => itemsWithModels(group)));
  fillSelect(ui.leftHand, [ItemGroup.SHIELD, ...WEAPON_GROUPS].flatMap((group) => itemsWithModels(group)));
  fillSelect(ui.wings, itemsWithModels(ItemGroup.WING).filter((item) => WING_NAME.test(item.name)));
  ui.armorSet.selectedIndex = 1;
  ui.rightHand.selectedIndex = 1;
  ui.leftHand.value = `${ItemGroup.SHIELD}:0`;
  ui.wings.selectedIndex = 1;
  const redress = async () => {
    if (state.online || !state.hero) return;
    const { x, y } = state.hero.cell;
    await placeHero({ id: 0, name: 'Offline', bodySet: Number(ui.bodySet.value), equipment: wardrobeEquipment(), x, y });
  };
  for (let level = 0; level <= 15; level++) ui.itemLevel.append(new Option(`+${level}`, String(level)));
  for (const select of [ui.bodySet, ui.armorSet, ui.rightHand, ui.leftHand, ui.wings, ui.itemLevel, ui.excellent, ui.ancient]) select.addEventListener('change', redress);
  ui.holdWeapons.addEventListener('change', () => { if (state.hero) state.hero.holdWeapons = ui.holdWeapons.checked; });
  const step = (delta: number) => {
    const count = ui.armorSet.options.length;
    ui.armorSet.selectedIndex = (ui.armorSet.selectedIndex + delta + count) % count;
    redress();
  };
  ui.previousSet.addEventListener('click', () => step(-1));
  ui.nextSet.addEventListener('click', () => step(1));
}

// Where to stand on a map without a server: the walkable cell nearest the middle,
// preferring a safe zone (towns are safe zones).
function offlineStartCell(world: WorldData): { x: number; y: number } {
  const center = TERRAIN_SIZE / 2;
  let best: { x: number; y: number; score: number } | null = null;
  for (let y = 0; y < TERRAIN_SIZE; y++) {
    for (let x = 0; x < TERRAIN_SIZE; x++) {
      const flags = world.flags[y * TERRAIN_SIZE + x];
      if (flags & (TerrainFlag.NOMOVE | TerrainFlag.NOGROUND)) continue;
      const score = Math.hypot(x - center, y - center) - (flags & TerrainFlag.SAFEZONE ? 1000 : 0);
      if (!best || score < best.score) best = { x, y, score };
    }
  }
  return best ?? { x: center, y: center };
}

function initMapPicker() {
  for (const map of KNOWN_MAPS) ui.mapSelect.append(new Option(mapName(map), String(map)));
  ui.mapSelect.value = String(OFFLINE_START.map);
  ui.mapPicker.hidden = false;
  ui.mapSelect.addEventListener('change', async () => {
    const map = Number(ui.mapSelect.value);
    try {
      await loadMap(map);
      const cell = map === OFFLINE_START.map ? OFFLINE_START : offlineStartCell(state.world!);
      state.hero!.placeAt(cell.x, cell.y);
      setStatus(`Offline: ${mapName(map)}. Left-click to walk, right-drag to turn, wheel to zoom.`, 1);
    } catch (error) {
      setStatus(`${mapName(map)} couldn't be loaded: ${error instanceof Error ? error.message : String(error)}`);
    }
  });
}

async function playOffline() {
  ui.login.hidden = true;
  await Promise.all([loadItemCatalog(), loadGlowColors()]);
  initWardrobe();
  await loadMap(OFFLINE_START.map);
  await placeHero({ id: 0, name: 'Offline', bodySet: Number(ui.bodySet.value), equipment: wardrobeEquipment(), x: OFFLINE_START.x, y: OFFLINE_START.y });
  ui.wardrobe.hidden = false;
  initMapPicker();
  setStatus('Offline: Lorencia without a server. Left-click to walk, right-drag to turn, wheel to zoom.', 1);
}

function startGame(play: () => Promise<void>): void {
  ui.loginError.hidden = true;
  ui.loginForm.querySelector('fieldset')!.disabled = true;
  play().then(() => {
    document.body.classList.add('ready');
    ui.hud.hidden = false;
    window.__viewerReady = true;
  }).catch((error) => {
    console.error(error);
    ui.login.hidden = false;
    ui.servers.hidden = ui.characters.hidden = true;
    ui.loginForm.querySelector('fieldset')!.disabled = false;
    showError(error);
  });
}

function initLogin() {
  const saved = loadSettings();
  // HNMO.Web serves the page and the relay from the same address.
  const defaults = { relay: `${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/relay`, host: '127.0.0.1', port: 44406, username: '' };
  for (const [key, value] of Object.entries({ ...defaults, ...saved })) {
    const input = ui.loginForm.elements.namedItem(key) as HTMLInputElement | null;
    if (input) input.value = String(value);
  }
  ui.loginForm.addEventListener('submit', (event) => {
    event.preventDefault();
    // The form's text inputs, by name.
    const data = new FormData(ui.loginForm);
    const field = (name: keyof LoginSettings) => String(data.get(name) ?? '');
    const form: LoginSettings = { username: field('username'), password: field('password'), host: field('host'), port: field('port'), relay: field('relay') };
    const { password, ...remembered } = form;
    saveSettings(remembered);
    startGame(() => playOnline(form));
  });
  ui.offline.addEventListener('click', () => startGame(playOffline));
  const autostart = new URLSearchParams(location.search).get('offline');
  if (autostart !== null) startGame(playOffline);
}

resize();
initLogin();
setUpGuide();
setUpSettings({ renderer, scene, camera, effects: () => state.effects?.root });
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

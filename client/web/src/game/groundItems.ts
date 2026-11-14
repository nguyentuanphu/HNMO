// Items and zen on the ground, as the desktop client shows them (CreateItemDrop, ItemAngle,
// RenderItemName, GameLogic::Automation::PickUpItem):
// - each drop is the item's own model, turned and scaled as its "ground" display says;
// - its name label, coloured by the ground label styles (Data/Items/Tooltips/GroundLabels.json:
//   the item's own rule for its level, else the first standard rule that holds), shows over it when
//   the mouse is on it, and over every drop while Alt is held;
// - clicking a drop walks there and, within 2 cells, asks the server for it (one request at a time).

import * as THREE from 'three';
import { read } from '../net/packets.js';
import type { Connection } from '../net/connection.js';
import { sampleHeight } from '../terrain.js';
import { createItemModel, cellCenter, type Character, type CharacterWorld } from './characters.js';
import { decodeItem, type Item } from './inventory.js';
import { itemDefinition, itemModel } from './items.js';
import { itemConditionsHold, itemNameText, loadTooltipData } from './itemTooltip.js';
import { REFERENCE_HEIGHT } from '../ui/rml.js';
import type { AnimatedModel } from '../bmd.js';

const DEG = Math.PI / 180;
const PICKUP_REACH = 2;            // cells (Pickup.h PickupReach)
const LABEL_RAISE = 15;            // reference pixels above the drop (RenderItemName)
const ZEN = { group: 14, number: 15 };
const ID_MASK = 0x7fff;

interface LabelStyle { color?: string; background?: string; bold?: boolean }
interface LabelRule { when?: string[]; style: string }
interface GroundLabelsFile { styles: Record<string, LabelStyle>; standard: LabelRule[] }
interface ItemLabelRule { style: string; levels?: [number, number] }

interface MoneyPacket { IsFreshDrop: boolean; Id: number; PositionX: number; PositionY: number; Amount: number }

export interface GroundDrop {
  id: number;
  cell: { x: number; y: number };
  item: Item | null;
  zen: number;
  group: THREE.Group;
  model: AnimatedModel | null;
  label: HTMLDivElement;
}

let labels: GroundLabelsFile | null = null;
async function loadLabels(): Promise<GroundLabelsFile> {
  if (!labels) {
    const response = await fetch('/data/Items/Tooltips/GroundLabels.json');
    labels = (await response.json()) as GroundLabelsFile;
  }
  return labels;
}

// The default size of a dropped item (ItemObjectAttribute): 0.8, spears to staffs 0.7, a few others.
function defaultScale(item: { group: number; number: number }): number {
  if (item.group >= 3 && item.group <= 5) return 0.7;
  if (item.group === 15 && item.number === 0) return 0.7;
  if (item.group === 14 && item.number === 0) return 1;
  if ((item.group === 14 && item.number === 21) || (item.group === 12 && item.number === 2)) return 0.5;
  return 0.8;
}

// The label's text and look (BuildGroundItemLabelDescriptor).
function describeLabel(item: Item | null, zen: number): { text: string; style: LabelStyle } {
  const file = labels;
  if (!item) return { text: `Zen ${zen}`, style: file?.styles.zen ?? {} };
  const definition = itemDefinition(item.group, item.number) as { groundLabels?: ItemLabelRule[] } | null;
  const own = definition?.groundLabels?.find((rule) => !rule.levels || (item.level >= rule.levels[0] && item.level <= rule.levels[1]));
  let text = itemNameText(item);
  if (own) return { text, style: file?.styles[own.style] ?? {} };
  const rule = file?.standard.find((r) => itemConditionsHold(item, r.when));
  if (item.skill) text += item.group === 13 && item.number === 3 ? ' +Knight specific skill' : '+Skill';
  if (item.optionLevel > 0) text += '+Option';
  if (item.luck) text += '+Luck';
  return { text, style: (rule && file?.styles[rule.style]) || {} };
}

export class GroundItems {
  readonly drops = new Map<number, GroundDrop>();
  private readonly layer: HTMLDivElement;
  private pickupTarget: GroundDrop | null = null;
  private requested: number | null = null;
  private altHeld = false;
  private hovered: GroundDrop | null = null;
  private readonly stop: () => void;

  constructor(
    connection: Connection,
    private readonly root: THREE.Object3D,
    private readonly camera: THREE.Camera,
    private readonly canvas: HTMLCanvasElement,
    private readonly actions: { hero: () => Character; walkTo: (cell: { x: number; y: number }) => void; send: (id: number) => void },
  ) {
    this.layer = document.createElement('div');
    Object.assign(this.layer.style, { position: 'fixed', inset: '0', pointerEvents: 'none', zIndex: '14', overflow: 'hidden' });
    document.body.append(this.layer);
    void loadLabels();
    void loadTooltipData();
    this.stop = connection.onPacket((bytes) => this.handle(bytes));
    window.addEventListener('keydown', (event) => { if (event.key === 'Alt') { this.altHeld = true; event.preventDefault(); } });
    window.addEventListener('keyup', (event) => { if (event.key === 'Alt') this.altHeld = false; });
    window.addEventListener('blur', () => { this.altHeld = false; });
  }

  handle(bytes: Uint8Array): void {
    try {
      if (bytes[0] === 0xc2 && bytes[3] === 0x20) this.dropped(bytes);
      else if (bytes[0] === 0xc1 && bytes[2] === 0x2f) {
        const p = read<MoneyPacket>('ServerToClient', 'MoneyDroppedExtended', bytes);
        void this.add(p.Id & ID_MASK, { x: p.PositionX, y: p.PositionY }, null, p.Amount);
      } else if (bytes[0] === 0xc2 && bytes[3] === 0x21) {
        const count = bytes[4];
        for (let i = 0; i < count; i++) this.remove(((bytes[5 + i * 2] << 8) | bytes[6 + i * 2]) & ID_MASK);
      } else if (bytes[0] === 0xc3 && bytes[2] === 0x22 && bytes.length <= 4) {
        this.requested = null; // ItemPickUpRequestFailed
      } else if (bytes[0] === 0xc3 && bytes[2] === 0x22) {
        this.requested = null; // the item (or zen) came into the inventory
      }
    } catch (error) {
      console.warn('Ground item packet not handled:', error);
    }
  }

  // ItemsDropped: count, then per drop its id (top bit: a fresh drop), cell and item bytes.
  private dropped(bytes: Uint8Array): void {
    const count = bytes[4];
    let at = 5;
    for (let i = 0; i < count && at < bytes.length; i++) {
      const id = ((bytes[at] << 8) | bytes[at + 1]) & ID_MASK;
      const cell = { x: bytes[at + 2], y: bytes[at + 3] };
      const item = decodeItem(bytes, at + 4);
      at += 4 + item.length;
      void this.add(id, cell, item, 0);
    }
  }

  private async add(id: number, cell: { x: number; y: number }, item: Item | null, zen: number): Promise<void> {
    this.remove(id);
    const look = item ?? { ...ZEN, level: 0, excellent: 0, ancientSet: 0 };
    const group = new THREE.Group();
    const label = document.createElement('div');
    Object.assign(label.style, { position: 'absolute', transform: 'translate(-50%, -100%)', whiteSpace: 'nowrap', display: 'none', padding: '0 2px', fontFamily: 'Tahoma, Arial, sans-serif' });
    const drop: GroundDrop = { id, cell, item, zen, group, model: null, label };
    this.drops.set(id, drop);
    this.root.add(group);
    this.layer.append(label);
    await Promise.all([loadLabels(), loadTooltipData()]);
    const { text, style } = describeLabel(item, zen);
    label.textContent = text;
    Object.assign(label.style, { color: style.color ?? '#fff', background: style.background ?? '#000', fontWeight: style.bold ? 'bold' : 'normal' });
    try {
      const model = await createItemModel({ group: look.group, number: look.number, glowLevel: look.level, excellent: look.excellent > 0, ancient: look.ancientSet > 0 }, 'ground');
      if (!model || this.drops.get(id) !== drop) return;
      const display = itemModel(look.group, look.number)?.ground;
      const [rx, ry, rz] = display?.rotation ?? [0, 0, 0];
      model.group.rotation.set(rx * DEG, ry * DEG, rz * DEG, 'ZYX');
      model.group.scale.setScalar(display?.scale ?? defaultScale(look));
      model.pose(0, 0);
      model.setLight(new THREE.Color(1, 1, 1));
      group.add(model.group);
      drop.model = model;
    } catch (error) {
      console.warn('Ground item model', look.group, look.number, error);
    }
  }

  private remove(id: number): void {
    const drop = this.drops.get(id);
    if (!drop) return;
    this.root.remove(drop.group);
    drop.label.remove();
    this.drops.delete(id);
    if (this.pickupTarget === drop) this.pickupTarget = null;
    if (this.hovered === drop) this.hovered = null;
  }

  private screenOf(drop: GroundDrop): { x: number; y: number } | null {
    const point = drop.group.position.clone();
    this.root.localToWorld(point);
    point.project(this.camera);
    if (point.z > 1) return null;
    const rect = this.canvas.getBoundingClientRect();
    return { x: rect.left + (point.x + 1) / 2 * rect.width, y: rect.top + (1 - point.y) / 2 * rect.height };
  }

  // The drop under the mouse: its label or its spot on the ground.
  pick(event: MouseEvent): GroundDrop | null {
    let best: { drop: GroundDrop; distance: number } | null = null;
    const radius = 22 * (window.innerHeight / REFERENCE_HEIGHT);
    for (const drop of this.drops.values()) {
      const rect = drop.label.style.display === 'block' ? drop.label.getBoundingClientRect() : null;
      if (rect && event.clientX >= rect.left && event.clientX <= rect.right && event.clientY >= rect.top && event.clientY <= rect.bottom) return drop;
      const at = this.screenOf(drop);
      if (!at) continue;
      const distance = Math.hypot(event.clientX - at.x, event.clientY - at.y);
      if (distance < radius && (!best || distance < best.distance)) best = { drop, distance };
    }
    return best?.drop ?? null;
  }

  hover(event: MouseEvent): void {
    this.hovered = this.pick(event);
  }

  // Walks to a drop and picks it up there.
  pickUp(drop: GroundDrop): void {
    this.pickupTarget = drop;
    if (this.distance(drop) > PICKUP_REACH) this.actions.walkTo(drop.cell);
  }

  cancel(): void {
    this.pickupTarget = null;
  }

  /** Whether the hero is on the way to a drop (or its pick-up was just asked). */
  get pickingUp(): boolean {
    return this.pickupTarget !== null || this.requested !== null;
  }

  private distance(drop: GroundDrop): number {
    const hero = this.actions.hero();
    return Math.hypot(drop.cell.x - hero.cell.x, drop.cell.y - hero.cell.y);
  }

  update(world: CharacterWorld): void {
    const scale = window.innerHeight / REFERENCE_HEIGHT;
    for (const drop of this.drops.values()) {
      const x = cellCenter(drop.cell.x), y = cellCenter(drop.cell.y);
      drop.group.position.set(x, y, sampleHeight(world.heights, x, y));
      const show = this.altHeld || this.hovered === drop;
      const at = show ? this.screenOf(drop) : null;
      if (!at) { drop.label.style.display = 'none'; continue; }
      Object.assign(drop.label.style, { display: 'block', left: `${at.x}px`, top: `${at.y - LABEL_RAISE * scale}px`, fontSize: `${12 * scale}px` });
    }
    const target = this.pickupTarget;
    const hero = this.actions.hero();
    if (target && this.requested === null && !hero.isWalking && this.distance(target) <= PICKUP_REACH) {
      this.requested = target.id;
      this.pickupTarget = null;
      this.actions.send(target.id);
      setTimeout(() => { if (this.requested === target.id) this.requested = null; }, 3000);
    }
  }

  clear(): void {
    for (const id of [...this.drops.keys()]) this.remove(id);
  }

  dispose(): void {
    this.stop();
    this.clear();
    this.layer.remove();
  }
}

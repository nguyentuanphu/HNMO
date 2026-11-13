// What the desktop client draws over the world in a fight:
// - the selected monster's name at the top centre on a dark red backing, with its 20-segment
//   health bar under it (CNameWindow::Render, DrawHealthBar), filled from the share of life the
//   server's last hit reported;
// - the damage numbers that rise from whoever got hit.

import * as THREE from 'three';
import { REFERENCE_HEIGHT } from './rml.js';

export interface FloatingText { text: string; color: [number, number, number]; scale: number }

const BAR = { steps: 20, span: 80, border: 2, gap: 1, height: 5, nameTop: 2, barTop: 15 };
const FLOAT_SECONDS = 1.2;
const FLOAT_RISE = 60;        // reference pixels a number rises over its life
const TEXT_UNITS = 12;

export class CombatOverlay {
  private readonly root: HTMLDivElement;
  private readonly name: HTMLDivElement;
  private readonly bar: HTMLDivElement;
  private readonly segments: HTMLDivElement[] = [];
  private readonly numbers: { element: HTMLDivElement; anchor: THREE.Vector3; age: number; offset: number }[] = [];

  constructor(private readonly camera: THREE.Camera, private readonly canvas: HTMLCanvasElement, parent: HTMLElement = document.body) {
    this.root = document.createElement('div');
    Object.assign(this.root.style, { position: 'fixed', inset: '0', pointerEvents: 'none', zIndex: '15', overflow: 'hidden' });
    this.name = document.createElement('div');
    Object.assign(this.name.style, {
      position: 'absolute', left: '50%', transform: 'translateX(-50%)', color: 'rgb(255,230,200)', background: 'rgb(100,0,0)',
      fontFamily: 'Tahoma, Arial, sans-serif', fontWeight: 'bold', padding: '0 4px', whiteSpace: 'nowrap', display: 'none',
    });
    this.bar = document.createElement('div');
    Object.assign(this.bar.style, { position: 'absolute', left: '50%', transform: 'translateX(-50%)', background: '#330000', boxShadow: '1px 1px 0 rgba(0,0,0,.5)', display: 'none' });
    for (let i = 0; i < BAR.steps; i++) {
      const segment = document.createElement('div');
      Object.assign(segment.style, { position: 'absolute', background: '#fa0a00' });
      this.bar.append(segment);
      this.segments.push(segment);
    }
    this.root.append(this.name, this.bar);
    parent.append(this.root);
  }

  private get scale(): number { return window.innerHeight / REFERENCE_HEIGHT; }

  // The selected monster's name and health bar; null hides them. health < 0: not known yet (full bar).
  showTarget(target: { name: string; healthStatus: number } | null): void {
    if (!target) { this.name.style.display = 'none'; this.bar.style.display = 'none'; return; }
    const s = this.scale;
    this.name.textContent = target.name;
    Object.assign(this.name.style, { display: 'block', top: `${BAR.nameTop * s}px`, fontSize: `${TEXT_UNITS * s}px`, lineHeight: `${TEXT_UNITS * s * 1.1}px` });
    const border = BAR.border * s, gap = BAR.gap * s, perStep = (BAR.span * s) / BAR.steps;
    const stepsWidth = BAR.span * s - 2 * gap;
    Object.assign(this.bar.style, { display: 'block', top: `${BAR.barTop * s}px`, width: `${stepsWidth + border * 2}px`, height: `${BAR.height * s}px` });
    const filled = Math.floor((target.healthStatus < 0 ? 1 : target.healthStatus) * BAR.steps);
    this.segments.forEach((segment, k) => {
      Object.assign(segment.style, {
        display: k < filled ? 'block' : 'none', left: `${border + k * perStep}px`, top: `${2 * s}px`, width: `${perStep - gap}px`, height: `${2 * s}px`,
      });
    });
  }

  // Numbers that rise from a point of the world (in the scene's world coordinates).
  addNumbers(anchor: THREE.Vector3, texts: FloatingText[]): void {
    texts.forEach((text, index) => {
      const element = document.createElement('div');
      const [r, g, b] = text.color;
      Object.assign(element.style, {
        position: 'absolute', color: `rgb(${r},${g},${b})`, fontFamily: 'Tahoma, Arial, sans-serif', fontWeight: 'bold',
        fontSize: `${TEXT_UNITS * 1.4 * text.scale * this.scale}px`, textShadow: '1px 1px 0 #000, -1px -1px 0 #000, 1px -1px 0 #000, -1px 1px 0 #000',
        whiteSpace: 'nowrap', transform: 'translate(-50%, -100%)',
      });
      element.textContent = text.text;
      this.root.append(element);
      this.numbers.push({ element, anchor: anchor.clone(), age: 0, offset: index * TEXT_UNITS * 1.3 });
    });
  }

  update(dt: number): void {
    const rect = this.canvas.getBoundingClientRect();
    const point = new THREE.Vector3();
    for (let i = this.numbers.length - 1; i >= 0; i--) {
      const number = this.numbers[i];
      number.age += dt;
      if (number.age >= FLOAT_SECONDS) { number.element.remove(); this.numbers.splice(i, 1); continue; }
      point.copy(number.anchor).project(this.camera);
      const x = rect.left + (point.x + 1) / 2 * rect.width;
      const y = rect.top + (1 - point.y) / 2 * rect.height - (number.offset + (number.age / FLOAT_SECONDS) * FLOAT_RISE) * this.scale;
      number.element.style.left = `${x}px`;
      number.element.style.top = `${y}px`;
      number.element.style.opacity = String(Math.min(1, 2 * (1 - number.age / FLOAT_SECONDS)));
    }
  }
}

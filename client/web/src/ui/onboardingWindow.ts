// HNMO's new-player guide in the web game: a small panel on the right (Y) with the current step, its hint, the count,
// the reward, where to go (with an arrow on the screen towards the NPC or spot), the other steps, the final reward and
// the rewards waiting for room in the bag; "Skip step" for optional steps and "Skip guide" (after a confirmation). It
// has no desktop window, so it is HNMO's own interface document (public/data/Interface/RmlUi/hnmo_onboarding.rml) in
// the game's message box frame (window_shell). The game asks for the state once when it enters the world
// (HnmoOnboardingRequest, F5 0E); the server then sends it (HnmoOnboardingState, C2 F5 0F) whenever something changes.
// The panel opens by itself for a character whose guide runs; steps done, rewards and the starter kit are told in the
// chat. The buttons send HnmoOnboardingAction (F5 10).

import { currentLanguage, onLanguageChange } from '../i18n.js';
import { read, type PacketValues } from '../net/packets.js';
import { openDocument, type RmlDocument, type RmlModel } from './rml.js';
import { typographyScale } from './nativeText.js';
import { tr, rewardText } from './missionsWindow.js';

type CoinType = 'WCoinC' | 'WCoinP' | 'GoblinPoints';
interface RewardItem { group: number; number: number; level: number; count: number; durability?: number; skill?: boolean; luck?: boolean; optionLevel?: number; excellent?: number }
interface Reward { zen: number; coins: number; coinType: CoinType; items: RewardItem[] }
export interface OnboardingStep {
  id: string; kind: string; titleEn: string; titleVi: string; hintEn: string; hintVi: string;
  target: number; optional: boolean; done: boolean; skipped: boolean; reward: Reward;
}
export interface OnboardingWhere { map: number; x: number; y: number; name?: string }
export interface OnboardingState {
  v: number; status: 'active' | 'done' | 'skipped' | 'none' | 'off';
  steps: OnboardingStep[];
  current?: { id: string; value: number; target: number; where?: OnboardingWhere };
  done: number; total: number; final: Reward;
  owed: { kind: string; titleEn: string; titleVi: string; zen: number; coins: number; coinType: CoinType; items: RewardItem[]; buffMinutes: number }[];
  kit?: { perAccount: boolean; zen: number; coins: number; coinType: CoinType; points: number; buffMinutes: number; items: RewardItem[] };
  event?: { type: string; result?: string; ids?: string[]; count?: number; coins?: number };
}

export interface OnboardingWindowOptions {
  /** Sends a client packet (ClientToServer). */
  send: (name: string, fields: PacketValues) => void;
  /** A line in the chat (system). */
  message: (text: string) => void;
  /** The name of an item. */
  itemName: (group: number, number: number) => string;
  /** The name of a map (the game's own). */
  mapName?: (map: number) => string;
  /** The map the hero is on. */
  mapNumber?: () => number | null;
  /** The way from the hero to a cell of the current map: the angle on the screen (0 is up, clockwise) and the steps. */
  direction?: (x: number, y: number) => { angle: number; steps: number } | null;
  /** Asks the player to confirm (the game's own dialog). */
  confirm?: (text: string) => Promise<boolean>;
  /** The hero's name: the guide opens by itself once per character (remembered in this browser). */
  characterName?: () => string | null;
}

interface StepLine { id: string; line: string; done: boolean; current: boolean }
interface OwedLine { line: string }

interface OnboardingModel extends RmlModel {
  has_title: boolean; title: string; positioned: boolean; root_x: number; root_y: number;
  loading: boolean; running: boolean; can_skip_step: boolean;
  step_head: string; step_title: string; step_hint: string; where_text: string; width: string; count_text: string; reward_text: string;
  status_text: string; steps_head: string; steps: StepLine[]; final_text: string;
  owed_head: string; owed: OwedLine[]; has_owed: boolean;
  loading_label: string; claim_label: string; skip_label: string; skip_step_label: string; close_label: string;
  ob_claim: () => void; ob_skip: () => void; ob_skip_step: () => void; ob_close: () => void;
}

/** Action numbers of HnmoOnboardingAction. */
const ACTION = { SkipGuide: 1, SkipStep: 2, ClaimRewards: 3 } as const;

/** Steps counted by events, whose count is shown ("3 / 10"). */
const COUNTED = new Set(['KillMonsters', 'UseItem', 'UseSkill', 'LearnSkill', 'AddStatPoints', 'ReachLevel']);

function vi(): boolean { return currentLanguage() === 'vi'; }

function pick(en: string, viText: string): string { return (vi() ? viText : en) || en; }

export class OnboardingWindow {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private wanted = false;
  private state: OnboardingState | null = null;
  private autoOpened = false;
  private lastCurrent: string | null = null;
  private readonly model: OnboardingModel;
  private readonly arrow: HTMLDivElement;
  private readonly button: HTMLButtonElement;
  private timer: number | null = null;

  constructor(private readonly options: OnboardingWindowOptions) {
    this.model = {
      has_title: true, title: 'New-player guide', positioned: false, root_x: 0, root_y: 0,
      loading: true, running: false, can_skip_step: false,
      step_head: '', step_title: '', step_hint: '', where_text: '', width: '0%', count_text: '', reward_text: '',
      status_text: '', steps_head: '', steps: [], final_text: '', owed_head: '', owed: [], has_owed: false,
      loading_label: '', claim_label: '', skip_label: '', skip_step_label: '', close_label: '',
      ob_claim: () => this.options.send('HnmoOnboardingAction', { Action: ACTION.ClaimRewards, Argument: '' }),
      ob_skip: () => { void this.skipGuide(); },
      ob_skip_step: () => this.options.send('HnmoOnboardingAction', { Action: ACTION.SkipStep, Argument: '' }),
      ob_close: () => this.close(),
    };
    this.arrow = document.createElement('div');
    this.arrow.className = 'hnmo-guide-arrow';
    this.arrow.setAttribute('role', 'status');
    this.arrow.hidden = true;
    Object.assign(this.arrow.style, {
      position: 'fixed', left: '50%', top: '130px', transform: 'translateX(-50%)', zIndex: '70', pointerEvents: 'none',
      padding: '2px 10px', background: 'rgba(0, 0, 0, 0.55)', border: '1px solid #4a7aa0', color: '#cfe8ff',
      font: '12px Tahoma, Verdana, sans-serif', whiteSpace: 'nowrap', textShadow: '1px 1px 0 #000',
    });
    const pointer = document.createElement('span');
    pointer.className = 'hnmo-guide-pointer';
    pointer.textContent = '➤';
    Object.assign(pointer.style, { display: 'inline-block', marginRight: '6px', color: '#ffe9a8', transition: 'transform 0.2s linear' });
    const label = document.createElement('span');
    label.className = 'hnmo-guide-label';
    this.arrow.append(pointer, label);
    document.body.append(this.arrow);
    this.button = document.createElement('button');
    this.button.type = 'button';
    this.button.className = 'hnmo-guide-button';
    this.button.hidden = true;
    this.button.addEventListener('click', () => this.open());
    this.button.addEventListener('pointerdown', (event) => event.stopPropagation());
    document.body.append(this.button);
    window.addEventListener('resize', () => this.place());
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && this.isOpen) { event.preventDefault(); this.close(); }
    });
    onLanguageChange(() => this.fill());
  }

  get isOpen(): boolean { return this.wanted; }

  /** The last state from the server. */
  get current(): OnboardingState | null { return this.state; }

  /** The arrow on the screen (for tests). */
  get arrowLine(): HTMLDivElement { return this.arrow; }

  /** Asks the server for the state once; from then on it sends the changes. */
  start(): void {
    this.options.send('HnmoOnboardingRequest', {});
  }

  toggle(): void {
    if (this.wanted) this.close(); else this.open();
  }

  close(): void {
    this.wanted = false;
    this.doc?.close();
    this.doc = null;
    this.tick();
    this.updateButton();
  }

  open(): void {
    this.wanted = true;
    this.updateButton();
    this.options.send('HnmoOnboardingRequest', {});
    this.fill();
    this.tick();
    if (this.doc) { this.doc.update(); return; }
    this.opening ??= openDocument('hnmo_onboarding', this.model, { parent: document.body }).then((doc) => {
      if (!this.wanted) { doc.close(); return; }
      this.doc = doc;
      doc.host.classList.add('hnmo-onboarding');
      doc.host.setAttribute('role', 'dialog');
      doc.host.setAttribute('aria-label', tr('New-player guide'));
      doc.root.addEventListener('pointerdown', (event) => event.stopPropagation());
      doc.root.addEventListener('wheel', (event) => event.stopPropagation());
      this.place();
      this.fill();
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  /** A packet from the server: the guide's state (C2 F5 0F). Returns whether it was one. */
  handle(bytes: Uint8Array): boolean {
    if (bytes[0] !== 0xc2 || bytes[3] !== 0xf5 || bytes[4] !== 0x0f) return false;
    const { Json } = read<{ Json: string }>('ServerToClient', 'HnmoOnboardingState', bytes);
    let state: OnboardingState;
    try {
      state = JSON.parse(Json) as OnboardingState;
    } catch {
      console.warn('Onboarding: unreadable state');
      return true;
    }
    const previous = this.state;
    this.state = state;
    this.model.loading = false;
    if (state.kit) this.tellKit(state.kit);
    if (state.event) this.notice(state, previous, state.event);
    this.tellNext(state);
    // The guide shows itself once for a character whose guide runs (its first time in the world); after that it waits
    // as a small button with the step (or Y). It used to open on every visit, over the game's own windows.
    if (state.status === 'active' && state.current && !this.autoOpened) {
      this.autoOpened = true;
      if (!this.wanted && !this.shownBefore()) {
        this.rememberShown();
        this.open();
      }
    }
    this.fill();
    this.tick();
    return true;
  }

  private stepTitle(step: { titleEn: string; titleVi: string }): string { return pick(step.titleEn, step.titleVi); }

  private stepOf(id: string | undefined, state: OnboardingState | null = this.state): OnboardingStep | undefined {
    return id ? state?.steps.find((s) => s.id === id) : undefined;
  }

  private async skipGuide(): Promise<void> {
    const question = tr('Skip the new-player guide? Its steps and rewards end for this character (rewards you already earned still come).');
    const yes = this.options.confirm ? await this.options.confirm(question) : window.confirm(question);
    if (yes) this.options.send('HnmoOnboardingAction', { Action: ACTION.SkipGuide, Argument: '' });
  }

  private reward(reward: Reward): string { return rewardText(reward, this.options.itemName); }

  private kitItems(items: RewardItem[]): string {
    return items.map((item) => {
      // Stacked potions and the like count their pieces: 3 stacks of 3 are "x9".
      const total = item.count * (item.durability && item.durability > 1 && item.group === 14 ? item.durability : 1);
      return `${this.options.itemName(item.group, item.number)}${item.level > 0 ? ` +${item.level}` : ''}${total > 1 ? ` x${total}` : ''}`;
    }).join(', ');
  }

  private tellKit(kit: NonNullable<OnboardingState['kit']>): void {
    const parts: string[] = [];
    if (kit.items.length > 0) parts.push(this.kitItems(kit.items));
    if (kit.zen > 0) parts.push(tr('%s zen', kit.zen.toLocaleString(vi() ? 'vi-VN' : 'en-AU')));
    if (kit.coins > 0) parts.push(`${kit.coins} ${kit.coinType === 'GoblinPoints' ? tr('Goblin Points') : kit.coinType === 'WCoinP' ? tr('W Coin (P)') : tr('W Coin (C)')}`);
    if (kit.points > 0) parts.push(tr('%d stat points', kit.points));
    if (kit.buffMinutes > 0) parts.push(tr('an EXP buff for %d minutes', kit.buffMinutes));
    this.options.message(tr('Welcome! Your starter kit: %s.', parts.join(' · ')));
  }

  /** Tells the next step (and where to go) in the chat when it changes. */
  private tellNext(state: OnboardingState): void {
    const id = state.status === 'active' ? state.current?.id ?? null : null;
    if (id === this.lastCurrent) return;
    this.lastCurrent = id;
    const step = this.stepOf(id ?? undefined, state);
    if (!step) return;
    const hint = pick(step.hintEn, step.hintVi);
    this.options.message(`[${tr('Guide')}] ${tr('Next: %s.', this.stepTitle(step))}${hint ? ` ${hint}` : ''}`);
    const where = this.whereText(state.current?.where);
    if (where) this.options.message(`[${tr('Guide')}] ${where}`);
  }

  private notice(state: OnboardingState, previous: OnboardingState | null, event: NonNullable<OnboardingState['event']>): void {
    const say = (text: string) => this.options.message(`[${tr('Guide')}] ${text}`);
    const bagFull = () => say(tr('Your bag is full: the guide\'s rewards wait. Make room, then take them in the guide (Y).'));
    switch (event.type) {
      case 'step': case 'final':
        for (const id of event.ids ?? []) {
          const step = this.stepOf(id, state) ?? this.stepOf(id, previous);
          if (!step) continue;
          const reward = this.reward(step.reward);
          say(tr('Step done: %s.', this.stepTitle(step)) + (reward ? ` ${reward}` : ''));
        }
        if (!this.wanted) this.flashButton();
        if (event.type === 'final') {
          const reward = this.reward(state.final);
          say(tr('You finished the new-player guide!') + (reward ? ` ${reward}` : ''));
        }
        if (event.result === 'BagFull') bagFull();
        break;
      case 'reward':
        if ((event.count ?? 0) > 0) say(tr('Guide rewards received (%d).', event.count ?? 0));
        if (event.result === 'BagFull') bagFull();
        break;
      case 'claim':
        if (event.result === 'Ok') say(tr('Guide rewards received (%d).', event.count ?? 0));
        else if (event.result === 'BagFull') bagFull();
        else say(tr('No guide rewards are waiting.'));
        break;
      case 'skip':
        if (event.result === 'Ok') say(tr('The guide was skipped. You can still read it with Y.'));
        break;
      case 'skipStep':
        if (event.result === 'Ok') say(tr('Step skipped.'));
        break;
    }
  }

  private whereText(where: OnboardingWhere | undefined): string {
    if (!where) return '';
    const map = this.options.mapName?.(where.map) || tr('Map %d', where.map);
    const here = this.options.mapNumber?.();
    if (where.x < 0) return tr('Go to %s.', map);
    const name = where.name ? `${where.name}: ` : '';
    if (here !== undefined && here !== null && here !== where.map) return tr('%s%s (%d, %d) — go to %s first.', name, map, where.x, where.y, map);
    return tr('%s%s (%d, %d).', name, map, where.x, where.y);
  }

  private fill(): void {
    const m = this.model;
    m.title = tr('New-player guide');
    m.loading_label = tr('Loading...');
    m.claim_label = tr('Take');
    m.skip_label = tr('Skip guide');
    m.skip_step_label = tr('Skip step');
    m.close_label = tr('Close');
    m.owed_head = tr('Rewards waiting for room in your bag');
    m.steps_head = tr('All steps');
    const state = this.state;
    if (!state) { this.doc?.update(); return; }
    const current = state.status === 'active' ? state.current : undefined;
    const step = this.stepOf(current?.id);
    m.running = !!(current && step);
    m.can_skip_step = m.running && !!step?.optional;
    if (current && step) {
      m.step_head = tr('Step %d of %d', Math.min(state.done + 1, state.total), state.total);
      m.step_title = this.stepTitle(step);
      m.step_hint = pick(step.hintEn, step.hintVi);
      m.where_text = this.whereText(current.where);
      const target = Math.max(1, current.target);
      m.width = `${Math.round(Math.min(1, current.value / target) * 100)}%`;
      m.count_text = COUNTED.has(step.kind) ? `${current.value} / ${target}` : current.value >= target ? tr('Done') : tr('Not yet');
      m.reward_text = this.reward(step.reward);
    } else {
      m.step_head = ''; m.step_title = ''; m.step_hint = ''; m.where_text = ''; m.width = '0%'; m.count_text = ''; m.reward_text = '';
    }
    m.status_text = state.status === 'done' ? tr('You finished the new-player guide. Good hunting!')
      : state.status === 'skipped' ? tr('You skipped the new-player guide.')
        : state.status === 'off' ? tr('The new-player guide is switched off right now.')
          : state.status === 'none' ? tr('The new-player guide is for new characters.') : '';
    const showSteps = state.status === 'active' || state.status === 'done' || state.status === 'skipped';
    m.steps = showSteps ? state.steps.map((s) => ({
      id: s.id,
      done: s.done,
      current: s.id === current?.id,
      line: `${s.done ? (s.skipped ? '–' : '✓') : s.id === current?.id ? '▸' : '·'} ${this.stepTitle(s)}${s.optional ? ` (${tr('optional')})` : ''}`,
    })) : [];
    const final = this.reward(state.final);
    m.final_text = showSteps && final ? `${tr('When every step is done')}: ${final.replace(`${tr('Reward')}: `, '')}` : '';
    m.owed = state.owed.map((o) => {
      const parts: string[] = [];
      const reward = this.reward({ zen: o.zen, coins: o.coins, coinType: o.coinType, items: [] }).replace(`${tr('Reward')}: `, '');
      if (reward) parts.push(reward);
      if (o.items.length > 0) parts.push(this.kitItems(o.items));
      if (o.buffMinutes > 0) parts.push(tr('an EXP buff for %d minutes', o.buffMinutes));
      const title = o.kind === 'kit' ? tr('Starter kit') : o.kind === 'final' ? tr('New-player guide finished') : pick(o.titleEn, o.titleVi);
      return { line: `${title}: ${parts.join(' · ')}` };
    });
    m.has_owed = m.owed.length > 0;
    this.doc?.update();
    this.updateButton();
  }

  /** The browser's key for "the guide has opened by itself for this character". */
  private shownKey(): string | null {
    const name = this.options.characterName?.();
    return name ? `hnmo.onboarding.shown.${name}` : null;
  }

  private shownBefore(): boolean {
    const key = this.shownKey();
    if (!key) return false;
    try { return localStorage.getItem(key) === '1'; } catch { return false; }
  }

  private rememberShown(): void {
    const key = this.shownKey();
    if (!key) return;
    try { localStorage.setItem(key, '1'); } catch { /* private window: it opens again next time */ }
  }

  /** The small button the guide waits in while its panel is closed: the step it's at ("Guide 2/10"). */
  private updateButton(): void {
    const state = this.state;
    const running = state?.status === 'active' && !!state.current;
    this.button.hidden = !running || this.wanted;
    if (!running || !state) return;
    this.button.textContent = tr('Guide %d/%d', Math.min(state.done + 1, state.total), state.total);
    this.button.title = `${tr('New-player guide')} (Y)`;
  }

  /** A step done while the panel is closed: the button glows for a moment instead of the panel opening. */
  private flashButton(): void {
    this.button.classList.remove('hnmo-guide-button-flash');
    void this.button.offsetWidth;
    this.button.classList.add('hnmo-guide-button-flash');
  }

  /** The arrow towards the current step's NPC or spot, while the panel is open and it's on this map. */
  private tick(): void {
    const where = this.state?.status === 'active' ? this.state.current?.where : undefined;
    const here = this.options.mapNumber?.();
    const way = where && where.x >= 0 && this.wanted && (here === undefined || here === null || here === where.map)
      ? this.options.direction?.(where.x, where.y) ?? null : null;
    if (way && where) {
      const pointer = this.arrow.querySelector<HTMLSpanElement>('.hnmo-guide-pointer');
      const label = this.arrow.querySelector<HTMLSpanElement>('.hnmo-guide-label');
      // ➤ points right; the angle is from "up".
      if (pointer) pointer.style.transform = `rotate(${Math.round((way.angle * 180) / Math.PI - 90)}deg)`;
      const name = where.name || tr('Target');
      if (label) label.textContent = way.steps <= 2 ? tr('%s is here', name) : tr('%s · %d steps', name, way.steps);
      this.arrow.hidden = false;
    } else {
      this.arrow.hidden = true;
    }
    const needed = !!where && this.wanted;
    if (needed && this.timer === null) this.timer = window.setInterval(() => this.tick(), 250);
    if (!needed && this.timer !== null) { window.clearInterval(this.timer); this.timer = null; }
  }

  // A dp window: the window in dp, scaled by the dp ratio (TypographyScale), as the desktop's documents.
  private place(): void {
    const doc = this.doc;
    if (!doc) return;
    const scale = typographyScale();
    Object.assign(doc.host.style, {
      position: 'fixed', left: '0px', top: '0px', width: `${window.innerWidth / scale}px`, height: `${window.innerHeight / scale}px`,
      transform: `scale(${scale})`, transformOrigin: 'left top', pointerEvents: 'none', zIndex: '24',
    });
    doc.setScale(scale);
    doc.update();
  }
}

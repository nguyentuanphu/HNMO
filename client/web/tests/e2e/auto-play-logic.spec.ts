// The helper's buff, heal, Drain Life and pet steps (CMuHelper::Work: ActivatePet, Buff, RecoverHealth)
// against a made-up world, without a browser or server: what is cast at whom, and when the rest of the
// work waits.
import { test, expect } from '@playwright/test';
import { AutoPlay, HelperBuff, HelperSkill, type AutoPlayPartyMember, type AutoPlayWorld } from '../../src/game/autoPlay.js';
import { defaultAutoPlayConfig, type AutoPlayConfig } from '../../src/game/autoPlayConfig.js';
import type { Character } from '../../src/game/characters.js';
import type { Connection } from '../../src/net/connection.js';

interface Fake { id: number; name: string; kind: string; dead: boolean; cell: { x: number; y: number }; path: { x: number; y: number }[]; isWalking: boolean }
const character = (id: number, kind: string, x: number, y: number, name = `c${id}`): Character =>
  ({ id, name, kind, dead: false, cell: { x, y }, path: [], isWalking: false } satisfies Fake) as unknown as Character;

class Setup {
  readonly hero = character(1, 'player', 100, 100, 'hero');
  readonly sent: { name: string; values: Record<string, unknown> }[] = [];
  readonly casts: { skill: number; target: Character }[] = [];
  readonly attacks: Character[] = [];
  readonly buffs = new Map<Character, Set<number>>();
  readonly known = new Set<number>();
  monsters: Character[] = [];
  party: AutoPlayPartyMember[] = [];
  busy = false;
  life = 100;
  readonly autoPlay: AutoPlay;
  private now = 0;

  constructor(config: Partial<AutoPlayConfig>) {
    const connection = {
      onPacket: () => () => undefined,
      send: (_group: string, name: string, values: Record<string, unknown> = {}) => { this.sent.push({ name, values }); },
    } as unknown as Connection;
    const world: AutoPlayWorld = {
      hero: () => this.hero, inSafeZone: () => false, monsters: () => this.monsters,
      cast: () => false, attack: (target) => { this.attacks.push(target); }, busy: () => this.busy,
      skillUsable: (skill) => this.known.has(skill), knowsSkill: (skill) => this.known.has(skill),
      castSupport: (skill, target) => { this.casts.push({ skill, target }); this.busy = true; return true; },
      actionBusy: () => this.busy,
      hasBuff: (who, buff) => this.buffs.get(who)?.has(buff) ?? false,
      party: () => this.party,
      life: () => this.life, maxLife: () => 100, money: () => 0, drinkHealingPotion: () => false,
      drops: () => [], pickUp: () => undefined, pickingUp: () => false, walkTo: () => false,
      worn: () => [], repair: () => undefined, itemName: () => '', isJewel: () => false, message: () => undefined,
    };
    this.autoPlay = new AutoPlay(connection, world);
    this.autoPlay.setConfig({ ...defaultAutoPlayConfig(), huntingRange: 6, ...config });
    this.now = performance.now();
    this.autoPlay.start();
  }

  /** One tick, after the given seconds; a cast finishes before it (busy ends). */
  tick(seconds = 0.1, castDone = true): void {
    if (castDone) this.busy = false;
    this.now += seconds * 1000;
    this.autoPlay.update(this.now);
  }

  give(who: Character, buff: number): void {
    const set = this.buffs.get(who) ?? new Set<number>();
    set.add(buff);
    this.buffs.set(who, set);
  }
}

test('a missing buff is cast on the hero, and holds up the work while it is cast', () => {
  const s = new Setup({ buffs: [HelperSkill.SWELL_LIFE, 0, 0], fallbackBasicAttack: true });
  s.known.add(HelperSkill.SWELL_LIFE);
  s.monsters = [character(50, 'monster', 102, 100)];
  s.tick();
  expect(s.casts).toEqual([{ skill: HelperSkill.SWELL_LIFE, target: s.hero }]);
  // While the cast plays nothing else happens; once the buff is on it isn't cast again and the attack follows.
  const attacksBefore = s.attacks.length;
  s.tick(0.1, false);
  s.tick(0.1, false);
  expect(s.attacks.length).toBe(attacksBefore);
  s.give(s.hero, HelperBuff.LIFE);
  s.tick();
  s.tick();
  expect(s.casts).toHaveLength(1);
  expect(s.attacks.length).toBeGreaterThan(0);
});

test('an unknown buff skill is skipped without holding up the work', () => {
  const s = new Setup({ buffs: [HelperSkill.SOUL_BARRIER, 0, 0] });
  s.tick();
  s.tick();
  expect(s.casts).toHaveLength(0);
});

test('the timed refresh recasts a present buff, then waits for the interval', () => {
  // A round ends when the buff index comes back to the first slot, whichever slot it started at (as on
  // the desktop), so with one buff in one slot a round can pass without it: all three slots hold it.
  const s = new Setup({ buffs: [HelperSkill.SWELL_LIFE, HelperSkill.SWELL_LIFE, HelperSkill.SWELL_LIFE], buffDuration: false, buffCastInterval: 5 });
  s.known.add(HelperSkill.SWELL_LIFE);
  s.give(s.hero, HelperBuff.LIFE);
  for (let i = 0; i < 3; i++) s.tick();
  expect(s.casts).toHaveLength(3); // the round at second 0, one cast per slot
  for (let i = 0; i < 30; i++) s.tick(0.1); // 3 seconds
  expect(s.casts).toHaveLength(3);
  for (let i = 0; i < 40; i++) s.tick(0.1); // 7 seconds: the next round, 5 seconds after the first ended
  expect(s.casts.length).toBeGreaterThan(3);
});

test('with "buff duration" only an expired buff is recast', () => {
  const s = new Setup({ buffs: [HelperSkill.SWELL_LIFE, 0, 0], buffDuration: true, buffCastInterval: 5 });
  s.known.add(HelperSkill.SWELL_LIFE);
  s.give(s.hero, HelperBuff.LIFE);
  for (let i = 0; i < 80; i++) s.tick(0.1);
  expect(s.casts).toHaveLength(0);
});

test('party support buffs each member in view and within ten cells in turn', () => {
  const s = new Setup({ buffs: [HelperSkill.DEFENSE, 0, 0], supportParty: true });
  s.known.add(HelperSkill.DEFENSE);
  const near = character(2, 'player', 103, 104, 'near');
  const far = character(3, 'player', 130, 100, 'far');
  s.party = [{ character: s.hero, healthStep: 10 }, { character: near, healthStep: 10 }, { character: far, healthStep: 10 }, { character: null, healthStep: 10 }];
  s.give(s.hero, HelperBuff.DEFENSE);
  for (let i = 0; i < 8; i++) s.tick();
  expect(s.casts.map((c) => c.target)).toEqual([near]); // the hero has it, the far one and the one out of view are skipped
  expect(s.casts[0].skill).toBe(HelperSkill.DEFENSE);
});

test('a self-aimed party buff (Swell Life) is cast at the hero for a member that lacks it', () => {
  const s = new Setup({ buffs: [HelperSkill.SWELL_LIFE, 0, 0], supportParty: true });
  s.known.add(HelperSkill.SWELL_LIFE);
  const member = character(2, 'player', 101, 101, 'member');
  s.party = [{ character: s.hero, healthStep: 10 }, { character: member, healthStep: 10 }];
  s.give(s.hero, HelperBuff.LIFE);
  for (let i = 0; i < 4; i++) s.tick();
  expect(s.casts).toEqual([{ skill: HelperSkill.SWELL_LIFE, target: s.hero }]);
});

test('auto heal heals the hero under the threshold, and party members by their health step', () => {
  const s = new Setup({ autoHeal: true, healThreshold: 50, autoHealParty: true, healPartyThreshold: 40 });
  s.known.add(HelperSkill.HEALING);
  const hurt = character(2, 'player', 102, 100, 'hurt');
  const fine = character(3, 'player', 102, 101, 'fine');
  s.party = [{ character: s.hero, healthStep: 10 }, { character: hurt, healthStep: 4 }, { character: fine, healthStep: 5 }];
  s.life = 80;
  for (let i = 0; i < 3; i++) s.tick();
  expect(s.casts.map((c) => c.target)).toEqual([hurt]);
  s.life = 50;
  for (let i = 0; i < 3; i++) s.tick();
  expect(s.casts.map((c) => c.target)).toEqual([hurt, s.hero, hurt]);
});

test('without party healing only the hero is healed', () => {
  const s = new Setup({ autoHeal: true, healThreshold: 30 });
  s.known.add(HelperSkill.HEALING);
  s.life = 31;
  s.tick();
  expect(s.casts).toHaveLength(0);
  s.life = 30;
  s.tick();
  expect(s.casts).toEqual([{ skill: HelperSkill.HEALING, target: s.hero }]);
});

test('Drain Life goes at the nearest monster in the hunting range under the heal threshold', () => {
  const s = new Setup({ useDrainLife: true, healThreshold: 40 });
  s.known.add(HelperSkill.ALICE_DRAINLIFE);
  const near = character(50, 'monster', 103, 100), farther = character(51, 'monster', 105, 100);
  s.monsters = [farther, near];
  s.life = 60;
  s.tick();
  expect(s.casts).toHaveLength(0);
  s.life = 40;
  s.tick();
  expect(s.casts).toEqual([{ skill: HelperSkill.ALICE_DRAINLIFE, target: near }]);
});

test('the Dark Raven gets its command once per start', () => {
  const s = new Setup({ useDarkRaven: true, darkRavenMode: 2 });
  s.tick();
  s.tick();
  const pet = s.sent.filter((p) => p.name === 'PetCommandRequest');
  expect(pet).toEqual([{ name: 'PetCommandRequest', values: { PetType: 0, CommandMode: 2, TargetId: 0xffff } }]);
  s.autoPlay.stop();
  s.autoPlay.start();
  s.tick();
  expect(s.sent.filter((p) => p.name === 'PetCommandRequest')).toHaveLength(2);
});

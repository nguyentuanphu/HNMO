// The skills' effects as the desktop draws them (render/effects): each ported skill is played
// through the game's own cast hook (Combat's onSkill, as when the server shows a cast) by the hero
// at the nearest monster; its effect objects appear and are gone again once it has played.
// Screenshots of each skill mid-effect go to SHOTS_DIR (default test-results/skill-shots).
import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { enterWorld } from './helpers.js';

interface Fighter { id: number; kind: string; dead: boolean; cell: { x: number; y: number } }
interface EffectsGame {
  online: { characters: Map<number, Fighter>; hero: Fighter & { isWalking: boolean }; playerId: number; walkTo(cell: { x: number; y: number }): boolean; say(text: string): void };
  connection: { dispatch(bytes: Uint8Array): void };
  combat: { events: { onSkill(caster: unknown, skill: number, target: unknown, cell: { x: number; y: number } | null): void; onSkillHit(caster: unknown, skill: number, target: unknown): void } };
  effects: { ready: Promise<void>; counts(): { effects: number; particles: number; joints: number; sprites: number }; census(): Record<string, number> };
}
type Win = Window & { __game: EffectsGame };

/** A skill, the objects its effect must make (census keys), and when to take its picture (ms after the cast). */
interface Case {
  skill: number; name: string; expect: string[]; shotAt: number; area?: boolean; chain?: boolean; action?: number;
  /** Aimed at another character (a monster, else the nearest NPC or player), never at the hero itself. */
  other?: boolean;
  /** An effect type whose model the picture waits for (a slow first load on the test machine). */
  model?: string;
  /** Models (directory, file) loaded with their textures before the cast. */
  warm?: [string, string][];
  /** Buffs (eBuffState) the hero has from just before the cast until the picture's effects have ended. */
  buffs?: number[];
  /** Effects that may still live after the case (they last while a buff does): left out of the count. */
  lasting?: string[];
  /** The action's keys a second (played slowly, held against the monsters' hits, so the few frames a second of the
   * test machine don't step over the keys the effect is timed by). */
  actionSpeed?: number;
  /** The server also shows the target struck (SkillAnimation at it: ReceiveMagic's effects at the target). */
  hit?: boolean;
}

const WIZARD: Case[] = [
  { skill: 2, name: 'meteorite', expect: ['effect:fireModel'], shotAt: 1100 },
  { skill: 7, name: 'ice', expect: ['effect:ice', 'effect:iceSmall'], shotAt: 900 },
  { skill: 1, name: 'poison', expect: ['effect:poison', 'particle:smoke'], shotAt: 900 },
  { skill: 5, name: 'flame', expect: ['effect:flame', 'particle:flame'], shotAt: 1100, area: true },
  { skill: 8, name: 'twister', expect: ['effect:storm', 'joint:jointThunder'], shotAt: 1300 },
  { skill: 9, name: 'evil-spirit', expect: ['joint:jointSpirit', 'effect:laser'], shotAt: 1200 },
  { skill: 10, name: 'hellfire', expect: ['effect:circle', 'effect:circleLight'], shotAt: 1000 },
  { skill: 12, name: 'aqua-beam', expect: ['effect:bossLaser', 'sprite:spark3'], shotAt: 800 },
  { skill: 14, name: 'inferno', expect: ['effect:skillInferno', 'particle:explosion'], shotAt: 800 },
  { skill: 39, name: 'ice-storm', expect: ['effect:blizzardModel'], shotAt: 1000, area: true },
  { skill: 6, name: 'teleport', expect: ['effect:spark3', 'particle:spark3'], shotAt: 150 },
  { skill: 38, name: 'decay', expect: ['effect:fireModel', 'joint:smokeTrail'], shotAt: 1000, area: true },
  { skill: 40, name: 'nova', expect: ['joint:jointSpirit'], shotAt: 750 },
  // Players only: the swell stands on the hero.
  { skill: 233, name: 'expansion-of-wizardry', expect: ['effect:swellOfMagicPower', 'effect:shockWave', 'effect:twlight', 'effect:arrowsre6', 'joint:twoLineGhost'], shotAt: 520, model: 'swellOfMagicPower', warm: [['Effect', 'magic_powerup.bmd'], ['Effect', 'arrowsre06.bmd']] },
  { skill: 13, name: 'cometfall', expect: ['effect:skillBlast', 'joint:jointEnergy', 'particle:shiny4', 'particle:explosion', 'effect:stone1'], shotAt: 560, area: true, model: 'skillBlast', warm: [['Skill', 'Blast01.bmd']] },
  // Last: its lights circle the hero while the buff lasts (here, until the page closes).
  { skill: 16, name: 'soul-barrier', expect: ['joint:spearSkill'], shotAt: 1200 },
];

const KNIGHT: Case[] = [
  { skill: 41, name: 'twisting-slash', expect: ['effect:skillWheel1', 'effect:skillWheel2', 'joint:jointSpark'], shotAt: 900 },
  { skill: 42, name: 'rageful-blow', expect: ['effect:skillFuryStrike', 'effect:earthQuake1', 'effect:earthQuake5', 'effect:earthQuake7', 'effect:wave'], shotAt: 1050 },
  { skill: 43, name: 'death-stab', expect: ['joint:spearSkill', 'effect:lightSpear', 'joint:flare'], shotAt: 420 },
  { skill: 47, name: 'impale', expect: ['effect:lightSpear', 'joint:flare', 'effect:spearSkillModel'], shotAt: 600 },
  { skill: 48, name: 'swell-life', expect: ['joint:jointSpirit', 'effect:magicGround2'], shotAt: 600 },
  { skill: 59, name: 'combo', expect: ['effect:combo', 'joint:lightJoint'], shotAt: 250 },
  { skill: 22, name: 'cyclone', expect: ['particle:spark', 'particle:spark3'], shotAt: 650 },
  // Its blades follow the swing's keys (PLAYER_ATTACK_ONE_FLASH), played by the hero here.
  { skill: 57, name: 'spiral-slash', expect: ['joint:flare'], shotAt: 520, action: 136 },
  { skill: 232, name: 'strike-of-destruction', expect: ['effect:blowOfDestruction', 'effect:nightWater1', 'effect:knightPlancrackA', 'effect:knightPlancrackB', 'effect:raklionBossCrackEffect', 'particle:waterfall5', 'particle:smoke'], shotAt: 840, area: true, model: 'knightPlancrackA', warm: [['Effect', 'nightwater01.bmd'], ['Effect', 'knight_plancrack_a.bmd'], ['Effect', 'knight_plancrack_b.bmd'], ['Effect', 'knight_plancrack_grand.bmd']] },
  { skill: 44, name: 'crescent-moon-slash', expect: ['joint:jointSpark', 'particle:fire', 'effect:swordForce', 'joint:jointForce'], shotAt: 600, action: 137 },
];

const ELF: Case[] = [
  { skill: 52, name: 'penetration', expect: ['effect:gathering', 'particle:spark3'], shotAt: 240 },
  { skill: 235, name: 'multi-shot', expect: ['effect:multiShot1', 'effect:multiShot2', 'effect:multiShot3', 'effect:bladeSkill'], shotAt: 200, model: 'bladeSkill', warm: [['Effect', 'bladetonedo.bmd']] },
  { skill: 77, name: 'infinity-arrow', expect: ['effect:infinityArrow', 'effect:infinityArrow1', 'effect:infinityArrow4'], shotAt: 300 },
  { skill: 30, name: 'summon', expect: ['effect:magicGround2', 'joint:jointHealing'], shotAt: 800 },
  // Starfall: the glowing arrow over the elf, then two flashes falling on the target and their rings.
  { skill: 46, name: 'starfall', expect: ['effect:arrowImpact', 'joint:flash', 'effect:shockWave'], shotAt: 760, other: true, model: 'arrowImpact', warm: [['Skill', 'ArrowImpact.bmd']] },
  // Fire Breath (on a Dinorant): sparks, flames either side flying ahead, the burst at its end.
  { skill: 49, name: 'fire-breath', expect: ['effect:shotgun', 'joint:jointSpark', 'particle:fire3', 'particle:explosion2'], shotAt: 680 },
  { skill: 234, name: 'recovery', expect: ['joint:flare', 'effect:summon', 'effect:twlight', 'effect:flare', 'particle:light'], shotAt: 800, model: 'summon', warm: [['Skill', 'nightmaresum.bmd']] },
];

const GLADIATOR: Case[] = [
  { skill: 55, name: 'fire-slash', expect: ['effect:gathering', 'effect:swordForce', 'joint:jointForce', 'joint:jointThunder'], shotAt: 400 },
  { skill: 56, name: 'power-slash', expect: ['effect:magic2'], shotAt: 250 },
  { skill: 237, name: 'gigantic-storm', expect: ['effect:jointThunderEffect', 'joint:jointThunder', 'effect:magicGround2'], shotAt: 800 },
  // The flame follows the gladiator's swing (PLAYER_SKILL_FLAMESTRIKE), played by the hero here.
  // Released at key 3 of PLAYER_ATTACK_DEATH_CANNON, played by the hero here.
  { skill: 73, name: 'mana-rays', expect: ['joint:jointForce', 'joint:flareBlueJoint'], shotAt: 640, action: 138 },
  // The Flame Strike debuff (eDeBuff_FlameStrikeDamage = 83): flames rising from the body's bones.
  { skill: 56, name: 'flame-strike-debuff', expect: ['effect:fireCursedLich', 'particle:fireHik1', 'particle:fireCursedLich', 'particle:fireHik3'], shotAt: 400, buffs: [83] },
  // The burning blade and its blurs (CreateObjectBlur: two in motion_blur_r, one in Lava) while keys 5 to 13 play.
  { skill: 236, name: 'flame-strike', expect: ['effect:flameStrike', 'blur:object'], shotAt: 720, action: 184, actionSpeed: 10, model: 'flameStrike', warm: [['Effect', 'FlameStrike.bmd']] },
];

const LORD: Case[] = [
  { skill: 66, name: 'force-wave', expect: ['effect:waves', 'effect:piercing2', 'joint:piercing'], shotAt: 680 },
  { skill: 61, name: 'fire-burst', expect: ['effect:pierPart', 'effect:darkLordSkill'], shotAt: 680, model: 'pierPart', warm: [['Skill', 'PierPart.bmd'], ['Skill', 'DarkLordSkill.bmd']] },
  { skill: 64, name: 'critical-damage', expect: ['effect:darkLordSkill'], shotAt: 620, model: 'darkLordSkill', warm: [['Skill', 'DarkLordSkill.bmd']] },
  // The buff's sparks spiral out of the held weapons every 1.2 s while it lasts (eBuff_AddCriticalDamage = 5).
  { skill: 64, name: 'critical-damage-buff', expect: ['joint:flareForceJoint', 'sprite:flare'], shotAt: 1400, buffs: [5] },
  // Released at key 5.5 of PLAYER_SKILL_FLASH, played by the hero here.
  { skill: 65, name: 'electric-spike', expect: ['joint:flareForceJoint'], shotAt: 900, action: 152 },
  // The dark horse's stomp (shock waves, the strike's cracks at its 19th tick, the stones while keys 8-9.5 play).
  { skill: 62, name: 'earthshake', expect: ['effect:darkHorseSkill', 'effect:shockWave', 'effect:skillFuryStrike', 'effect:earthQuake1'], shotAt: 1040, action: 87, warm: [['Skill', 'GroundStone.bmd'], ['Skill', 'GroundStone2.bmd']] },
  // Released at key 3 of PLAYER_ATTACK_STRIKE: the burning trail flies at the target.
  { skill: 74, name: 'fire-blast', expect: ['effect:pierPart', 'joint:jointForce', 'effect:skillInferno'], shotAt: 680, action: 80 },
  { skill: 78, name: 'fire-scream', expect: ['effect:darkScream', 'effect:darkScreamFire', 'joint:jointForce', 'particle:flame', 'particle:blueBlur'], shotAt: 640, model: 'darkScream', warm: [['Skill', 'darkfirescrem01.bmd'], ['Skill', 'darkfirescrem02.bmd']] },
  // Plasma Storm (on a Fenrir): bolts from over the Fenrir's head to the target, and flickering bolts over it (those
  // only for a black, red or blue Fenrir: the desktop has none for gold, which the test hero may ride).
  { skill: 76, name: 'plasma-storm', expect: ['joint:fenrirSkillThunder'], shotAt: 640, other: true },
  { skill: 67, name: 'stun', expect: ['joint:flash', 'particle:explosion2', 'joint:jointThunder', 'particle:advSmoke2'], shotAt: 680 },
  // The Stun debuff on the hero (eDeBuff_Stun = 61): three lights circling up over the head.
  { skill: 67, name: 'stun-debuff', expect: ['joint:spearSkill'], shotAt: 600, buffs: [61] },
  { skill: 68, name: 'cancel-stun', expect: ['joint:flash', 'effect:skinShell'], shotAt: 900, other: true, warm: [['Skill', 'SkinShell.bmd']] },
  // Invisibility on its target: lights rising from its bones and rings turning with it (BITMAP_MAGIC+1 6 and 7).
  { skill: 70, name: 'invisibility', expect: ['effect:magicGround2', 'particle:light'], shotAt: 1200, other: true },
  { skill: 69, name: 'swell-mana', expect: ['joint:jointSpirit', 'effect:magicGround2'], shotAt: 680 },
  { skill: 63, name: 'summon', expect: ['effect:circle', 'effect:circleLight', 'particle:flareBlue', 'joint:flareBlueJoint'], shotAt: 900, lasting: ['effect:circle', 'effect:circleLight'] },
  { skill: 238, name: 'chaotic-diseier', expect: ['effect:shiny5', 'joint:twoLineGhost', 'particle:smoke', 'effect:desair'], shotAt: 900, model: 'desair', warm: [['Skill', 'desair.bmd'], ['Skill', 'darkwing_hetachi.bmd']] },
];

const SUMMONER: Case[] = [
  { skill: 214, name: 'drain-life', expect: ['effect:aliceDrainLife', 'particle:light3', 'joint:drainLifeGhost', 'joint:jointEnergy', 'particle:lightning2'], shotAt: 1000 },
  // Its links come with the server's ChainLightningHitInfo, fed to the game here.
  { skill: 215, name: 'chain-lightning', expect: ['effect:chainLightning', 'joint:jointThunder', 'particle:light'], shotAt: 200, chain: true },
  // Damage Reflection's cast, then its buff (eBuff_Thorns = 71): thorns of light rise from the hero while it lasts.
  { skill: 217, name: 'damage-reflection', expect: ['effect:magicGround2', 'effect:aliceBuffSkillEffect', 'joint:flare'], shotAt: 900, buffs: [71] },
  // Blind's cast, then the debuff's look (eDeBuff_Blind = 73) and Sleep's (eDeBuff_Sleep = 72) on the hero.
  { skill: 220, name: 'blind', expect: ['effect:magicGround2', 'effect:aliceBuffSkillEffect', 'particle:light3'], shotAt: 900, buffs: [73] },
  { skill: 219, name: 'sleep-debuff', expect: ['effect:aliceBuffSkillEffect', 'particle:twintailWater'], shotAt: 1200, buffs: [72] },
  // Expansion of Wizardry's buff (eBuff_SwellOfMagicPower = 82): purple lights on every bone while it lasts.
  { skill: 233, name: 'expansion-buff', expect: ['effect:swellOfMagicPowerBuffEffect', 'sprite:light'], shotAt: 1400, buffs: [82] },
  { skill: 45, name: 'lance', expect: ['effect:skillJavelin', 'particle:poundingBall'], shotAt: 1000, model: 'skillJavelin', warm: [['Skill', 'Javelin.bmd']] },
  { skill: 230, name: 'lightning-shock', expect: ['effect:lightningShock', 'particle:lightningMega', 'effect:magic', 'effect:damage01Mono'], shotAt: 240 },
  { skill: 219, name: 'sleep', expect: ['effect:aliceBuffSkillEffect', 'effect:aliceBuffSkillEffect2', 'effect:magicGround2', 'joint:jointHealing'], shotAt: 1000 },
  // The Berserker buff (eBuff_Berserker = 81): auras turning at the hands, sparks, red marks on the body.
  { skill: 218, name: 'berserker-buff', expect: ['effect:orora', 'particle:orora', 'effect:sparks', 'effect:lightMarks', 'sprite:lightMarks'], shotAt: 1200, buffs: [81], lasting: ['effect:orora', 'effect:sparks'] },
  { skill: 218, name: 'berserker', expect: ['effect:aliceBuffSkillEffect', 'effect:magicGround2', 'joint:jointHealing'], shotAt: 1000 },
  { skill: 221, name: 'weakness', expect: ['effect:magicZin', 'effect:summonerCastingEffect2', 'effect:shiny5', 'particle:pinLightParticle'], shotAt: 1100 },
  // Requiem: the casting circles, Neil rising before the Summoner with its blade's blur, the knives and their marks at the tile.
  { skill: 224, name: 'requiem', expect: ['effect:magic', 'effect:summonerCastingEffect1', 'effect:summonerCastingEffect4', 'effect:summonerCastingEffect2', 'effect:summonerSummonNeil', 'effect:summonerSummonNeilKnife1', 'effect:summonerSummonNeilGround1', 'blur:object', 'sprite:light'], shotAt: 1300, area: true, model: 'summonerSummonNeil',
    warm: [['Skill', 'summon_neil.bmd'], ['Skill', 'nelleff_nife01.bmd'], ['Skill', 'nell_nifegrund01.bmd'], ['Effect', 'Suhwanzin1.bmd'], ['Effect', 'Suhwanzin4.bmd']] },
  // Pollution: the poison's smoke, clouds and drops over the tile, Lagul's head riding a spirit's trail with its blue trails.
  { skill: 225, name: 'pollution', expect: ['effect:summonerSummonLagul', 'joint:jointSpirit', 'joint:jointEnergy', 'effect:cloud', 'particle:smoke', 'particle:twintailWater'], shotAt: 1600, area: true, model: 'summonerSummonLagul',
    warm: [['Skill', 'summon_lagul.bmd']] },
  { skill: 222, name: 'innovation', expect: ['effect:magicZin', 'effect:summonerCastingEffect222', 'particle:shiny5Particle'], shotAt: 1100 },
];

const FIGHTER: Case[] = [
  { skill: 260, name: 'killing-blow', expect: ['effect:wolfHeadEffect', 'effect:sbumb', 'particle:sbumb', 'joint:pinLightJoint'], shotAt: 240 },
  { skill: 261, name: 'beast-uppercut', expect: ['effect:downAttackDummyR', 'effect:downAttackDummyL', 'joint:forcePillar', 'particle:sahamuttExplosion'], shotAt: 520 },
  // Its timing follows the fighter's kick (PLAYER_SKILL_DRAGONKICK), played by the hero here. The energy and sparks at
  // the target (particle:energy, spark3) need the kick to pass key 5 with a monster targeted: not asserted (the test
  // server's monsters and the hero's mount make that uncertain).
  { skill: 265, name: 'dragon-slasher', expect: ['effect:dragonKickDummy', 'effect:multiShot1', 'particle:lightningMega', 'joint:groundWind'], shotAt: 680, action: 252 },
  { skill: 266, name: 'increase-attack', expect: ['effect:swordEffectMono', 'particle:swordEffectMonoParticle', 'effect:shockWaveGround1', 'effect:eventCloud', 'effect:windForce', 'effect:magic'], shotAt: 400 },
  { skill: 267, name: 'increase-health', expect: ['effect:swordEffectMono', 'effect:windForce', 'effect:eventCloud'], shotAt: 400 },
  { skill: 268, name: 'increase-defense', expect: ['effect:swordEffectMono', 'effect:windForce', 'effect:shockWaveGround1'], shotAt: 400 },
  // While Increase Attack lasts (eBuff_Att_up_Ourforces = 129) red smoke lines rise from the fists.
  { skill: 266, name: 'increase-attack-buff', expect: ['effect:lightRed', 'particle:smokeLine1'], shotAt: 1600, buffs: [129] },
  { skill: 269, name: 'charge', expect: ['effect:multiShot1', 'effect:multiShot2', 'effect:multiShot3', 'joint:jointSpark'], shotAt: 240, action: 137 },
  { skill: 263, name: 'dark-side', expect: ['effect:eventCloud', 'effect:magic', 'effect:windForce'], shotAt: 300 },
  // Chain Drive follows the giant swing (PLAYER_SKILL_GIANTSWING), played by the hero here: the fist's trail, the first
  // blow's wave and wolf heads at key 6, the second wave and spinning waves at key 11.
  { skill: 262, name: 'chain-drive', expect: ['effect:swordEffect', 'joint:swordEff', 'effect:shockWave1', 'effect:wolfHeadEffect2', 'effect:shockWave2', 'effect:shockWaveSpin1'], shotAt: 1500, action: 249, actionSpeed: 8,
    warm: [['Effect', 'shockwave01.bmd'], ['Effect', 'shockwave02.bmd'], ['Effect', 'shockwave_spin01.bmd'], ['Effect', 'wolf_head_effect2.bmd']] },
  // Dragon Roar follows PLAYER_SKILL_DRAGONLORE, played by the hero here: the burning fists, the red wave and ring, the
  // column of fire, the crack toward the target; the struck target (shown by the server) gets a volcano and burns.
  { skill: 264, name: 'dragon-roar', expect: ['effect:lava', 'joint:lavaJoint', 'effect:shockWave1', 'effect:shockWaveGround1', 'effect:blowOfDestruction', 'effect:dragonLowerDummy', 'effect:volcanoOfMonk', 'effect:volcanoOfMonkCore', 'effect:volcanoStone', 'effect:targetMonsterEffect', 'effect:lightRed', 'particle:fireHik1'], shotAt: 1500, action: 253, actionSpeed: 5, hit: true, model: 'dragonLowerDummy',
    warm: [['Effect', 'knight_plancrack_dragon.bmd'], ['Effect', 'volcano_of_monk.bmd'], ['Effect', 'volcano_stone.bmd'], ['Effect', 'shockwave_ground01.bmd']] },
];

const CASES: Record<string, Case[]> = { wizard: WIZARD, knight: KNIGHT, elf: ELF, gladiator: GLADIATOR, lord: LORD, summoner: SUMMONER, fighter: FIGHTER };
const SHOTS_DIR = process.env.SHOTS_DIR ?? 'test-results/skill-shots';

const game = <T>(page: Page, read: (g: EffectsGame) => T): Promise<T> => page.evaluate(`(${read.toString()})(window.__game)`);

async function goHunting(page: Page): Promise<void> {
  await page.evaluate(() => (window as unknown as Win).__game.online.say('/move lorencia'));
  await page.waitForTimeout(6_000);
  for (const goal of [{ x: 150, y: 100 }, { x: 155, y: 85 }, { x: 160, y: 72 }]) {
    await page.evaluate((cell) => (window as unknown as Win).__game.online.walkTo(cell), goal);
    await expect.poll(() => game(page, (g) => g.online.hero.isWalking), { timeout: 60_000 }).toBe(false);
  }
}

/** Plays the skill as the server shows it: the hero casts at the nearest monster (or a tile ahead). */
function cast(page: Page, skill: number, area: boolean, chain = false, action?: number, other = false, hit = false, actionSpeed?: number): Promise<boolean> {
  return page.evaluate(({ skill, area, chain, action, other, hit, actionSpeed }) => {
    const g = (window as unknown as Win).__game;
    const hero = g.online.hero;
    const play = (): void => {
      if (action === undefined) return;
      const actor = hero as unknown as { playOnce(action: number, keysPerSecond?: number): void; __held?: boolean };
      if (actionSpeed === undefined) { actor.playOnce(action); return; }
      const playOnce = Object.getPrototypeOf(actor).playOnce as (this: unknown, a: number, k?: number) => void;
      playOnce.call(actor, action, actionSpeed);
      actor.playOnce = () => { /* held for the test */ };
      setTimeout(() => { delete (actor as { playOnce?: unknown }).playOnce; }, 15 / actionSpeed * 1000 + 1000);
    };
    play();
    let best: Fighter | null = null, bestDistance = 8;
    for (const c of g.online.characters.values()) {
      if (c.kind !== 'monster' || c.dead) continue;
      const d = Math.hypot(c.cell.x - hero.cell.x, c.cell.y - hero.cell.y);
      if (d < bestDistance) { best = c; bestDistance = d; }
    }
    const cell = best ? { ...best.cell } : { x: hero.cell.x + 3, y: hero.cell.y - 3 };
    if (chain) {
      // ChainLightningHitInfo (C1 BF 0A): the hero's lightning through the nearest monsters (or the hero).
      const near = [...g.online.characters.values()].filter((c) => c.kind === 'monster' && !c.dead)
        .sort((a, b) => Math.hypot(a.cell.x - hero.cell.x, a.cell.y - hero.cell.y) - Math.hypot(b.cell.x - hero.cell.x, b.cell.y - hero.cell.y));
      const ids = near.slice(0, 3).map((c) => c.id);
      const self = g.online.playerId;
      const packet = [0xc1, 10 + ids.length * 2, 0xbf, 0x0a, skill >> 8, skill & 0xff, self & 0xff, self >> 8, ids.length, 0];
      for (const id of ids.length ? ids : [self]) packet.push(id & 0xff, id >> 8);
      packet[1] = packet.length;
      packet[8] = (packet.length - 10) / 2;
      g.connection.dispatch(new Uint8Array(packet));
      return best !== null;
    }
    if (other && !best) {
      for (const c of g.online.characters.values()) {
        if (c === hero || c.dead) continue;
        const d = Math.hypot(c.cell.x - hero.cell.x, c.cell.y - hero.cell.y);
        if (!best || d < bestDistance) { best = c; bestDistance = d; }
      }
    }
    // Without a monster close by, a targeted skill is aimed at the hero itself.
    g.combat.events.onSkill(hero, skill, area ? null : best ?? hero, cell);
    // The cast may play its own action (a Fenrir rider's): the case's action is played after it.
    if (actionSpeed === undefined) play();
    if (hit) g.combat.events.onSkillHit(hero, skill, best ?? hero);
    return best !== null;
  }, { skill, area, chain, action, other, hit, actionSpeed });
}

const groups = process.env.SKILL_GROUPS?.split(',');
for (const [group, cases] of Object.entries(CASES)) {
  if (groups && !groups.includes(group)) continue;
  test(`the ${group}'s skills draw their effects`, async ({ page }) => {
    test.setTimeout(10 * 60_000);
    mkdirSync(SHOTS_DIR, { recursive: true });
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await enterWorld(page, 'test400');
    await page.evaluate(() => (window as unknown as Win).__game.effects.ready);
    await goHunting(page);
    const only = process.env.SKILLS?.split(',');
    for (const c of cases) {
      if (only && !only.includes(c.name)) continue;
      await expect.poll(() => game(page, (g) => g.effects.counts().effects + g.effects.counts().joints), { timeout: 30_000 }).toBe(0);
      if (c.warm) {
        // The test machine draws a few frames a second and loads a model's textures slowly: load them first.
        await page.evaluate(async (models) => {
          type Bmd = {
            loadModel(dir: string, file: string): Promise<unknown>; computeBoneMatrices(model: unknown, action: number, time: number): unknown;
            buildGeometries(model: unknown, bones: unknown): { textureName: string }[]; createMaterial(dirs: string[], name: string, options: { glow: boolean }): Promise<unknown>;
          };
          const bmd = await import('/js/bmd.js' as string) as Bmd;
          for (const [dir, file] of models) {
            const model = await bmd.loadModel(dir, file);
            for (const part of bmd.buildGeometries(model, bmd.computeBoneMatrices(model, 0, 0))) await bmd.createMaterial(['Skill', 'Effect', 'Item'], part.textureName, { glow: true });
          }
        }, c.warm);
      }
      // The effect system's tick is watched from the page: what each tick holds is gathered, and at
      // the picture's tick the effects stop moving (they are still drawn) until the picture is taken,
      // so the pictures don't depend on how fast the test machine draws.
      await page.evaluate((freezeAt) => {
        type Ticking = { step(): void; census(): Record<string, number>; __seen?: Set<string>; __frozen?: boolean; __ticks?: number; __step?: () => void };
        const e = (window as unknown as { __game: { effects: Ticking } }).__game.effects;
        e.__step ??= e.step;
        const step = e.__step;
        e.__seen = new Set<string>();
        e.__frozen = false;
        e.__ticks = 0;
        e.step = function (this: Ticking) {
          if (this.__frozen) return;
          step.call(this);
          for (const key of Object.keys(this.census())) this.__seen?.add(key);
          if (++this.__ticks! === freezeAt) this.__frozen = true;
        };
      }, Math.max(1, Math.round(c.shotAt / 40)));
      if (c.buffs) await page.evaluate((list) => { const g = (window as unknown as { __game: { buffs: { replace(id: number, b: number[]): void }; online: { playerId: number } } }).__game; g.buffs.replace(g.online.playerId, list); }, c.buffs);
      await cast(page, c.skill, c.area ?? false, c.chain ?? false, c.action, c.other ?? false, c.hit ?? false, c.actionSpeed);
      await page.waitForFunction(() => (window as unknown as { __game: { effects: { __frozen?: boolean } } }).__game.effects.__frozen === true, null, { timeout: 30_000 });
      if (c.model) {
        await page.waitForFunction((type) => (window as unknown as { __game: { effects: { effects: { type: string; model: unknown }[] } } }).__game.effects.effects
          .some((e) => e.type === type && e.model !== null), c.model, { timeout: 180_000 });
      }
      await page.waitForTimeout(400); // a frame or two drawn
      await page.screenshot({ path: `${SHOTS_DIR}/${group}-${c.name}.png` });
      await page.evaluate(() => { (window as unknown as { __game: { effects: { __frozen?: boolean } } }).__game.effects.__frozen = false; });
      await page.waitForTimeout(3_000);
      if (c.buffs) await page.evaluate(() => { const g = (window as unknown as { __game: { buffs: { replace(id: number, b: number[]): void }; online: { playerId: number } } }).__game; g.buffs.replace(g.online.playerId, []); });
      const seen = new Set(await page.evaluate(() => [...((window as unknown as { __game: { effects: { __seen?: Set<string> } } }).__game.effects.__seen ?? [])]));
      for (const key of c.expect) expect(seen, `${c.name} makes ${key}`).toContain(key);
      // Everything it made ends (Soul Barrier's lights last while the buff does).
      if (c.lasting) {
        // The summoning circle stays its ten seconds: end it before the next case.
        await expect.poll(() => game(page, (g) => g.effects.counts().effects + g.effects.counts().joints), { timeout: 30_000 }).toBe(0);
      } else if (c.name !== 'soul-barrier') {
        await expect.poll(() => game(page, (g) => g.effects.counts().effects + g.effects.counts().joints), { timeout: 20_000 }).toBe(0);
      }
    }
    if (group === 'knight' && (!only || only.includes('weapon-trail'))) {
      // The weapon trail of a sword skill's swing (CreateWeaponBlur): Cyclone's action, PLAYER_ATTACK_SKILL_SWORD4,
      // played slowly (2 keys a second) so the test machine's few frames a second see it; the monsters'
      // hits don't interrupt it with the hero's shock meanwhile.
      await page.evaluate(() => {
        type Hero = { playOnce(action: number, keysPerSecond?: number): void };
        const hero = (window as unknown as { __game: { online: { hero: Hero } } }).__game.online.hero;
        const playOnce = hero.playOnce.bind(hero);
        playOnce(63, 2);
        hero.playOnce = () => { /* held for the test */ };
        setTimeout(() => { hero.playOnce = playOnce; }, 4_000);
      });
      await expect.poll(() => page.evaluate(() => (window as unknown as { __game: { weaponTrails?: { count: number } } }).__game.weaponTrails?.count ?? 0), { timeout: 5_000, intervals: [50] }).toBeGreaterThan(0);
      await page.screenshot({ path: `${SHOTS_DIR}/knight-weapon-trail.png` });
      await expect.poll(() => page.evaluate(() => (window as unknown as { __game: { weaponTrails?: { count: number } } }).__game.weaponTrails?.count ?? 0), { timeout: 10_000 }).toBe(0);
    }
    expect(errors).toEqual([]);
  });
}

// The buffs' and debuffs' looks on a character (InsertBuffPhysicalEffect, the body colours of RenderObject): the
// nearest monster is poisoned, then frozen, then weakened, as the server's magic-effect state would show it.
test('debuffs colour a monster and weakness leaves its lights', async ({ page }) => {
  if (groups && !groups.includes('buffs')) test.skip();
  test.setTimeout(5 * 60_000);
  mkdirSync(SHOTS_DIR, { recursive: true });
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await enterWorld(page, 'test400');
  await page.evaluate(() => (window as unknown as Win).__game.effects.ready);
  await goHunting(page);
  type Tinted = { id: number; kind: string; dead: boolean; cell: { x: number; y: number }; bodyTint: { r: number; g: number; b: number } | null; bodyGlow: unknown[] | null };
  const setDebuffs = (list: number[]) => page.evaluate((list) => {
    const g = (window as unknown as { __game: { buffs: { replace(id: number, b: number[]): void }; online: { hero: { cell: { x: number; y: number } }; characters: Map<number, Tinted> } } }).__game;
    const hero = g.online.hero;
    // The same monster each time while it lives (the nearest one moves about).
    const held = (window as unknown as { __debuffTarget?: number }).__debuffTarget;
    let best: Tinted | null = held !== undefined ? g.online.characters.get(held) ?? null : null;
    if (best?.dead) best = null;
    let bestDistance = 12;
    for (const c of best ? [] : g.online.characters.values()) {
      if (c.kind !== 'monster' || c.dead) continue;
      const d = Math.hypot(c.cell.x - hero.cell.x, c.cell.y - hero.cell.y);
      if (d < bestDistance) { best = c; bestDistance = d; }
    }
    if (!best) return null;
    (window as unknown as { __debuffTarget?: number }).__debuffTarget = best.id;
    g.buffs.replace(best.id, list);
    return { id: best.id, tint: best.bodyTint ? [best.bodyTint.r, best.bodyTint.g, best.bodyTint.b] : null, glow: best.bodyGlow?.length ?? 0 };
  }, list);
  const poisoned = await setDebuffs([55]);
  expect(poisoned, 'a monster close by').not.toBeNull();
  expect(poisoned!.tint).toEqual([0.3, 1, 0.5]); // eDeBuff_Poison
  await page.waitForTimeout(800);
  await page.screenshot({ path: `${SHOTS_DIR}/buffs-poison.png` });
  const frozen = await setDebuffs([56]);
  expect(frozen!.tint).toEqual([0.3, 0.5, 1]); // eDeBuff_Freeze
  expect(frozen!.glow, 'a frozen body is drawn again, glowing').toBeGreaterThan(0);
  await page.screenshot({ path: `${SHOTS_DIR}/buffs-freeze.png` });
  await setDebuffs([57]); // eDeBuff_Harden: two ice shells stay on the monster while it lasts
  await expect.poll(() => game(page, (g) => g.effects.census()['effect:ice'] ?? 0), { timeout: 10_000 }).toBe(2);
  // The first shell sheds sparkles circling the hardened one (BITMAP_FIRE+2 SubType 10).
  await expect.poll(() => game(page, (g) => g.effects.census()['particle:fire3'] ?? 0), { timeout: 10_000 }).toBeGreaterThan(0);
  await page.waitForTimeout(800);
  await page.screenshot({ path: `${SHOTS_DIR}/buffs-harden.png` });
  await setDebuffs([]);
  await expect.poll(() => game(page, (g) => g.effects.census()['effect:ice'] ?? 0), { timeout: 10_000 }).toBe(0);
  await setDebuffs([76]); // eDeBuff_AttackDown (Weakness)
  await expect.poll(() => game(page, (g) => g.effects.census()['effect:shiny5'] ?? 0), { timeout: 10_000 }).toBeGreaterThan(0);
  await expect.poll(() => game(page, (g) => g.effects.census()['particle:shiny5Particle'] ?? 0), { timeout: 10_000 }).toBeGreaterThan(0);
  await page.waitForTimeout(800);
  await page.screenshot({ path: `${SHOTS_DIR}/buffs-weakness.png` });
  const cleared = await setDebuffs([]);
  expect(cleared!.tint).toBeNull();
  await expect.poll(() => game(page, (g) => (g.effects.census()['effect:shiny5'] ?? 0) + (g.effects.census()['effect:pinLight'] ?? 0)), { timeout: 10_000 }).toBe(0);
  expect(errors).toEqual([]);
});

// The effects' light reaches the models standing in it (AddTerrainLight on the terrain light that RequestTerrainLight
// gives a character): a red light laid at the hero every tick reddens the hero's body light.
test('effect light reaches the characters in it', async ({ page }) => {
  if (groups && !groups.includes('light')) test.skip();
  test.setTimeout(3 * 60_000);
  await enterWorld(page, 'test400');
  await page.evaluate(() => (window as unknown as Win).__game.effects.ready);
  type Lit = { light: { r: number; g: number; b: number }; position: { x: number; y: number } };
  type LightGame = { online: { hero: Lit }; effects: { schedule(task: (tick: number) => boolean): void; groundLight(at: { x: number; y: number }, r: number, g: number, b: number, range: number): void } };
  const before = await page.evaluate(() => { const l = (window as unknown as { __game: LightGame }).__game.online.hero.light; return { r: l.r, g: l.g, b: l.b }; });
  await page.evaluate(() => {
    const g = (window as unknown as { __game: LightGame }).__game;
    const w = window as unknown as { __lightTicks: number };
    w.__lightTicks = 0;
    g.effects.schedule(() => { g.effects.groundLight(g.online.hero.position, 1, -1, -1, 3); return ++w.__lightTicks < 150; });
  });
  // The light is laid by the effect ticks, which run with the frames: on a machine drawing a frame or two a second
  // the first ones come late. Wait until the light has been laid a few times, then for the hero's colour.
  await expect.poll(() => page.evaluate(() => (window as unknown as { __lightTicks: number }).__lightTicks), { timeout: 120_000 }).toBeGreaterThanOrEqual(10);
  await expect.poll(() => page.evaluate(() => { const l = (window as unknown as { __game: LightGame }).__game.online.hero.light; return l.r - l.g; }), { timeout: 20_000 })
    .toBeGreaterThan(before.r - before.g + 0.3);
  // When the light ends the body takes the ground's own light again. Its 150 ticks take longer when the machine
  // draws few frames (at most 10 ticks run per frame), so wait for them to be over first.
  await expect.poll(() => page.evaluate(() => (window as unknown as { __lightTicks: number }).__lightTicks), { timeout: 120_000 }).toBeGreaterThanOrEqual(150);
  await expect.poll(() => page.evaluate(() => { const l = (window as unknown as { __game: LightGame }).__game.online.hero.light; return Math.abs(l.r - l.g); }), { timeout: 20_000 })
    .toBeLessThan(Math.abs(before.r - before.g) + 0.05);
});

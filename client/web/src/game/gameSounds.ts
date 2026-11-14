// The game's sounds where the desktop plays them (see audio/index.ts for the list): map music and
// ambience every frame, the hero's footsteps on frames 1.5 and 4.5 of the walk, swings by weapon,
// screams when hit and on death, monster attack / hit / death / idle sounds from their sound sets,
// potions, pick-ups, level up and the skills' own sounds.

import {
  ambientCalls, attackSwingSound, audioSystem, backgroundMusicCalls, heldWeaponOf, heroFootstepSound,
  loadMonsterSounds, loadSoundEvents, monsterSoundPlays, type AudioSystem, type MapAudioState, type MonsterSoundSituation,
} from '../audio/index.js';
import type { Character } from './characters.js';
import type { Item } from './inventory.js';
import { isFemaleClass } from './combatLook.js';
import { isNewSkill, NEW_SKILL_LOOKS } from './newSkills.js';

/** A skill's own sound (ReceiveMagic / AttackEffect), by skill number. */
const SKILL_SOUNDS: Record<number, string> = {
  2: 'SOUND_METEORITE01', 3: 'SOUND_THUNDER01', 4: 'SOUND_METEORITE01', 5: 'SOUND_FLAME', 7: 'SOUND_ICE', 8: 'SOUND_STORM',
  9: 'SOUND_EVIL', 10: 'SOUND_HELLFIRE', 11: 'SOUND_MAGIC', 12: 'SOUND_FLASH', 16: 'SOUND_SOULBARRIER', 17: 'SOUND_MAGIC',
  19: 'SOUND_SKILL_SWORD1', 20: 'SOUND_SKILL_SWORD2', 21: 'SOUND_SKILL_SWORD3', 22: 'SOUND_SKILL_SWORD4', 23: 'SOUND_SKILL_SWORD4',
  43: 'SOUND_SKILL_SWORD2', 47: 'SOUND_RIDINGSPEAR', 48: 'SOUND_SWELLLIFE', 52: 'SOUND_PIERCING', 56: 'SOUND_SKILL_SWORD4',
  61: 'SOUND_ATTACK_FIRE_BUST', 64: 'SOUND_CRITICAL', 77: 'SOUND_INFINITYARROW', 78: 'SOUND_FIRE_SCREAM',
  214: 'SOUND_SKILL_DRAIN_LIFE', 215: 'SOUND_SKILL_CHAIN_LIGHTNING', 234: 'SOUND_SKILL_RECOVER',
};

export class GameSounds {
  readonly audio: AudioSystem = audioSystem();
  private lastStepFrame = -1;
  /** The events' part of the music rule (the temple's state, the Doppelganger running), set by the events. */
  eventMusicState: () => Partial<MapAudioState> = () => ({});

  constructor() {
    void this.audio.init().catch(() => undefined);
    void loadSoundEvents().catch(() => undefined);
    void loadMonsterSounds().catch(() => undefined);
  }

  /** Every frame: the listener, map music and ambience (ManageBackgroundMusic), footsteps. */
  frame(map: number | null, hero: Character | null, tile: number, safeZone: boolean, numKeys: number): void {
    if (map === null || !hero) return;
    this.audio.setListener({ x: hero.position.x, y: hero.position.y, z: 0 }, (hero.angle * 180) / Math.PI);
    const state: MapAudioState = { ...this.eventMusicState(), world: map, safeZone, heroTile: tile, heroX: hero.cell.x, heroY: hero.cell.y };
    this.audio.applyMusicCalls(backgroundMusicCalls(state));
    this.audio.applySoundCalls(ambientCalls(state));
    // Footsteps at walk frames 1.5 and 4.5 (PlayWalkSound).
    if (hero.isWalking && !hero.dead && numKeys > 1) {
      const frame = hero.time % numKeys;
      for (const at of [1.5, 4.5]) {
        if (this.lastStepFrame >= 0 && this.lastStepFrame < at && frame >= at) {
          const sound = heroFootstepSound(map, tile, safeZone);
          if (sound) this.audio.play(sound);
        }
      }
      this.lastStepFrame = frame < this.lastStepFrame ? -1 : frame;
    } else {
      this.lastStepFrame = -1;
    }
  }

  /** A player's normal attack (SetPlayerAttack): the swing of what it holds. */
  swing(right: { group: number; number: number } | null, left: { group: number; number: number } | null): void {
    const bow = left && left.group === 4 && left.number !== 7 && left.number !== 15 ? 'bow'
      : right && right.group === 4 && right.number >= 8 && right.number !== 15 ? 'crossbow' : null;
    const sound = attackSwingSound(heldWeaponOf(right, left, bow));
    if (sound) this.audio.play(sound, { anchored: true });
  }

  /** A player hit (SetPlayerShock) or killed (SetPlayerDie). */
  playerHurt(classIndex: number, died: boolean): void {
    const female = isFemaleClass(classIndex);
    const darkLord = classIndex === 4;
    let sound: string;
    if (died) sound = female ? 'SOUND_FEMALE_SCREAM02' : darkLord ? 'SOUND_DARKLORD_DEAD' : 'SOUND_HUMAN_SCREAM04';
    else if (female) sound = Math.random() < 0.5 ? 'SOUND_FEMALE_SCREAM01' : 'SOUND_FEMALE_SCREAM02';
    else if (darkLord && Math.random() < 0.8) sound = 'SOUND_DARKLORD_PAIN';
    else sound = ['SOUND_HUMAN_SCREAM01', 'SOUND_HUMAN_SCREAM02', 'SOUND_HUMAN_SCREAM03'][Math.floor(Math.random() * 3)];
    this.audio.play(sound, { anchored: true });
  }

  /** A monster's moment (stand, walk, attack, hit, death), from its sound set. */
  monster(monster: Character, situation: MonsterSoundSituation, hero: Character | null): void {
    if (monster.monsterNumber === null) return;
    const heroDistance = hero ? Math.hypot(hero.position.x - monster.position.x, hero.position.y - monster.position.y) : undefined;
    for (const play of monsterSoundPlays(monster.monsterNumber, { ...situation, heroDistance })) {
      this.audio.play(play.file, { channels: play.channels, anchored: play.atMonster });
    }
  }

  /** An item used (TryConsumeItem): an apple is eaten, potions drunk. */
  used(item: Item): void {
    if (item.group !== 14) return;
    if (item.number === 0) this.audio.play('SOUND_EAT_APPLE01');
    else if (item.number <= 9 || (item.number >= 35 && item.number <= 40)) this.audio.play('SOUND_DRINK01');
  }

  /** An item picked up (ReceiveGetItem): jewels ring. */
  pickedUp(item: Item | null, jewel: boolean): void {
    this.audio.play(item && jewel ? 'SOUND_JEWEL01' : 'SOUND_GET_ITEM01', { anchored: true });
  }

  skill(skill: number): void {
    // HNMO's own skills have their own sounds (made for them); the game's sound is the spare.
    const sound = isNewSkill(skill) ? `Sound/hnmo/${skill}.mp3` : SKILL_SOUNDS[skill] ?? NEW_SKILL_LOOKS[skill]?.sound;
    if (sound) this.audio.play(sound, { anchored: true });
  }

  levelUp(): void { this.audio.play('SOUND_LEVEL_UP'); }
  click(): void { this.audio.play('SOUND_CLICK01'); }
  repair(): void { this.audio.play('SOUND_REPAIR'); }
}

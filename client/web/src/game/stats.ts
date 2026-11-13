// The character's level, experience, life, mana, shield (SD), ability (AG) and stats, as the
// server sends them (the desktop client's CharacterAttribute): first with the character
// information, then whenever they change.

import { read, type PacketValues } from '../net/packets.js';
import type { Connection } from '../net/connection.js';

export interface StatValues {
  level: number;
  experience: number;
  nextExperience: number;
  levelUpPoints: number;
  strength: number; agility: number; vitality: number; energy: number; leadership: number;
  life: number; maxLife: number;
  mana: number; maxMana: number;
  shield: number; maxShield: number;
  ability: number; maxAbility: number;
  money: number;
}

export type StatsChange = 'stats' | 'experience' | 'levelUp' | 'respawn';

const num = (values: PacketValues, name: string): number => {
  const value = values[name];
  return typeof value === 'number' ? value : 0;
};

export class CharacterStats implements StatValues {
  level = 1; experience = 0; nextExperience = 0; levelUpPoints = 0;
  strength = 0; agility = 0; vitality = 0; energy = 0; leadership = 0;
  life = 0; maxLife = 1; mana = 0; maxMana = 1; shield = 0; maxShield = 1; ability = 0; maxAbility = 1;
  money = 0;
  private readonly listeners = new Set<(change: StatsChange, detail?: PacketValues) => void>();
  private readonly stop: () => void;

  constructor(connection: Connection, level: number, info: PacketValues) {
    this.level = level;
    this.experience = num(info, 'CurrentExperience');
    this.nextExperience = num(info, 'ExperienceForNextLevel');
    this.levelUpPoints = num(info, 'LevelUpPoints');
    this.strength = num(info, 'Strength'); this.agility = num(info, 'Agility'); this.vitality = num(info, 'Vitality');
    this.energy = num(info, 'Energy'); this.leadership = num(info, 'Leadership');
    this.life = num(info, 'CurrentHealth'); this.maxLife = num(info, 'MaximumHealth') || 1;
    this.mana = num(info, 'CurrentMana'); this.maxMana = num(info, 'MaximumMana') || 1;
    this.shield = num(info, 'CurrentShield'); this.maxShield = num(info, 'MaximumShield') || 1;
    this.ability = num(info, 'CurrentAbility'); this.maxAbility = num(info, 'MaximumAbility') || 1;
    this.money = num(info, 'Money');
    this.stop = connection.onPacket((bytes) => this.handle(bytes));
  }

  onChange(listener: (change: StatsChange, detail?: PacketValues) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed(change: StatsChange, detail?: PacketValues): void {
    for (const listener of this.listeners) listener(change, detail);
  }

  handle(bytes: Uint8Array): void {
    if (bytes[0] === 0xc1 && bytes[2] === 0x26 && bytes[3] === 0xff) {
      const p = read('ServerToClient', 'CurrentStatsExtended', bytes);
      this.life = num(p, 'Health'); this.shield = num(p, 'Shield'); this.mana = num(p, 'Mana'); this.ability = num(p, 'Ability');
      this.changed('stats');
    } else if (bytes[0] === 0xc1 && bytes[2] === 0x26 && bytes[3] === 0xfe) {
      const p = read('ServerToClient', 'MaximumStatsExtended', bytes);
      this.maxLife = num(p, 'Health') || 1; this.maxShield = num(p, 'Shield') || 1; this.maxMana = num(p, 'Mana') || 1; this.maxAbility = num(p, 'Ability') || 1;
      this.changed('stats');
    } else if (bytes[0] === 0xc1 && bytes[2] === 0x26 && bytes[3] === 0xfd) {
      const p = read('ServerToClient', 'ItemConsumptionFailedExtended', bytes);
      this.life = num(p, 'Health'); this.shield = num(p, 'Shield');
      this.changed('stats');
    } else if (bytes[0] === 0xc3 && bytes[2] === 0x16) {
      const p = read('ServerToClient', 'ExperienceGainedExtended', bytes);
      this.experience += num(p, 'AddedExperience');
      this.changed('experience', p);
    } else if (bytes[0] === 0xc1 && bytes[2] === 0xf3 && bytes[3] === 0x05) {
      const p = read('ServerToClient', 'CharacterLevelUpdateExtended', bytes);
      this.level = num(p, 'Level'); this.levelUpPoints = num(p, 'LevelUpPoints');
      this.maxLife = num(p, 'MaximumHealth') || 1; this.maxMana = num(p, 'MaximumMana') || 1;
      this.maxShield = num(p, 'MaximumShield') || 1; this.maxAbility = num(p, 'MaximumAbility') || 1;
      // The next level's experience, as the server's experience table gives it (the desktop's formula).
      this.nextExperience = experienceForLevel(this.level + 1);
      this.changed('levelUp', p);
    } else if (bytes[0] === 0xc1 && bytes[2] === 0xf3 && bytes[3] === 0x04) {
      const p = read('ServerToClient', 'RespawnAfterDeathExtended', bytes);
      this.life = num(p, 'CurrentHealth'); this.mana = num(p, 'CurrentMana'); this.shield = num(p, 'CurrentShield');
      this.ability = num(p, 'CurrentAbility'); this.experience = num(p, 'Experience'); this.money = num(p, 'Money');
      this.changed('respawn', p);
    } else if (bytes[0] === 0xc1 && bytes[2] === 0xf3 && bytes[3] === 0x32) {
      const p = read('ServerToClient', 'BaseStatsExtended', bytes);
      this.strength = num(p, 'Strength'); this.agility = num(p, 'Agility'); this.vitality = num(p, 'Vitality');
      this.energy = num(p, 'Energy'); this.leadership = num(p, 'Command');
      this.changed('stats');
    }
  }

  dispose(): void {
    this.stop();
  }
}

// The total experience a level needs: (9 + L) * L * L * 10, plus (9 + L') * L' * L' * 1000 above
// level 255 with L' = L - 255 (the desktop client's experience bar uses the same formula).
export function experienceForLevel(level: number): number {
  const previous = level - 1;
  if (previous <= 0) return 0;
  let experience = (9 + previous) * previous * previous * 10;
  if (previous > 255) {
    const over = previous - 255;
    experience += (9 + over) * over * over * 1000;
  }
  return experience;
}

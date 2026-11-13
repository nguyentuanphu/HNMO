// HNMO sound events: which sound the game plays when, from Data/Audio/SoundEvents.json, as the
// desktop reads them (client/src/source/Data/GameData/SoundData/SoundEvents.cpp MatchRule,
// Audio/SoundEventPlay.cpp PickEventSound): the first rule of the event that matches gives the
// sounds, and one of them plays, picked at random when there are more.
//
// The events and where the desktop asks for them:
//  - heroFootstep: PlayWalkSound (Engine/Object/ZzzCharacter.cpp), the hero only, twice per walk
//    or run cycle (animation frames 1.5 and 4.5), not while flying. PlayBuffer(sound), no object.
//  - playerAttackSwing: SetPlayerAttack at animation frame 0, a player (not a monster model):
//    PlayBuffer(sound, character). Nothing plays bare-handed (no rule names "none").
//  - interfaceClick: a click on a window button with a click kind (UI/RmlBridge/RmlClickSounds.cpp).
//  - characterSelected: a click on a character of the character select (Scenes/CharacterScene.cpp).

/** What a player holds at an attack's start (Data::Sounds::HeldWeapon, Audio::HeldWeaponOf). */
export type HeldWeapon = 'none' | 'bow' | 'crossbow' | 'magicCrossbow' | 'saberOrSpear' | 'other';

/** What the game knows when an event happens (SoundEventContext). */
export interface SoundEventContext {
  world?: number;
  tile?: number;
  safeZone?: boolean;
  holds?: HeldWeapon;
  /** The clicked button's click kind ("confirm", "cancel", "select", "menu"). */
  kind?: string;
}

interface SoundEventRule {
  worlds?: number[];
  tiles?: number[];
  safeZone?: boolean;
  holds?: HeldWeapon;
  kinds?: string[];
  plays: string[];
}

interface SoundEventsJson {
  events: { event: string; rules: SoundEventRule[] }[];
}

/** The events as the file has them (2026-11), used until loadSoundEvents() reads the file. */
const BUILT_IN: SoundEventsJson = {
  events: [
    {
      event: 'heroFootstep',
      rules: [
        { worlds: [2], tiles: [0, 1, 2, 4, 5, 6, 7, 8, 9], plays: ['SOUND_HUMAN_WALK_SNOW'] },
        { worlds: [0], tiles: [0], plays: ['SOUND_HUMAN_WALK_GRASS'] },
        { worlds: [3], tiles: [0], plays: ['SOUND_HUMAN_WALK_GRASS'] },
        { worlds: [7, 24, 25, 26, 27, 28, 29, 36, 67], safeZone: false, plays: ['SOUND_HUMAN_WALK_SWIM'] },
        { worlds: [57, 58, 65], plays: ['SOUND_HUMAN_WALK_SNOW'] },
        { worlds: [62], plays: ['SOUND_HUMAN_WALK_SNOW'] },
        { plays: ['SOUND_HUMAN_WALK_GROUND'] },
      ],
    },
    {
      event: 'playerAttackSwing',
      rules: [
        { holds: 'bow', plays: ['SOUND_BOW01'] },
        { holds: 'crossbow', plays: ['SOUND_CROSSBOW01'] },
        { holds: 'magicCrossbow', plays: ['SOUND_MAGIC'] },
        { holds: 'saberOrSpear', plays: ['SOUND_BRANDISH_SWORD03'] },
        { holds: 'other', plays: ['SOUND_BRANDISH_SWORD01', 'SOUND_BRANDISH_SWORD02'] },
      ],
    },
    {
      event: 'interfaceClick',
      rules: [
        { kinds: ['confirm'], plays: ['SOUND_CLICK01'] },
        { kinds: ['cancel'], plays: ['SOUND_CLICK01'] },
        { kinds: ['select'], plays: ['SOUND_CLICK01'] },
        { kinds: ['menu'], plays: ['SOUND_CLICK01'] },
      ],
    },
    { event: 'characterSelected', rules: [{ plays: ['SOUND_CLICK01'] }] },
  ],
};

let events = new Map<string, SoundEventRule[]>(BUILT_IN.events.map((entry) => [entry.event, entry.rules]));
let loading: Promise<void> | null = null;

/** Reads Data/Audio/SoundEvents.json (once); until then the built-in copy of its rules answers. */
export function loadSoundEvents(dataRoot = '/data'): Promise<void> {
  if (loading === null) {
    loading = fetch(`${dataRoot}/Audio/SoundEvents.json`)
      .then(async (response) => {
        if (!response.ok) return;
        const data = (await response.json()) as SoundEventsJson;
        events = new Map(data.events.map((entry) => [entry.event, entry.rules]));
      })
      .catch(() => undefined);
  }
  return loading;
}

function allows(list: readonly number[] | undefined, value: number | undefined): boolean {
  return list === undefined || list.length === 0 || (value !== undefined && list.includes(value));
}

/** The first rule of the event that matches (MatchRule); null for none. */
function matchRule(event: string, context: SoundEventContext): SoundEventRule | null {
  for (const rule of events.get(event) ?? []) {
    if (allows(rule.worlds, context.world) && allows(rule.tiles, context.tile)
      && (rule.safeZone === undefined || rule.safeZone === (context.safeZone ?? false))
      && (rule.holds === undefined || rule.holds === (context.holds ?? 'none'))
      && (rule.kinds === undefined || rule.kinds.length === 0 || (context.kind !== undefined && rule.kinds.includes(context.kind)))) {
      return rule;
    }
  }
  return null;
}

/** The sound (SOUND_* id) of the event, picked as PickEventSound does; null for none. */
export function pickEventSound(event: string, context: SoundEventContext, rand: () => number = Math.random): string | null {
  const rule = matchRule(event, context);
  if (rule === null || rule.plays.length === 0) return null;
  const pick = rule.plays.length > 1 ? Math.min(rule.plays.length - 1, Math.floor(rand() * rule.plays.length)) : 0;
  return rule.plays[pick];
}

/** heroFootstep: the hero's step on the map (world), its ground tile (HeroTile) and safe zone. */
export function heroFootstepSound(world: number, tile: number, safeZone: boolean, rand?: () => number): string | null {
  return pickEventSound('heroFootstep', { world, tile, safeZone }, rand);
}

/** playerAttackSwing: a player's swing at the start of a normal attack, by what it holds. */
export function attackSwingSound(weaponKind: HeldWeapon, rand?: () => number): string | null {
  return pickEventSound('playerAttackSwing', { holds: weaponKind }, rand);
}

/** interfaceClick: a click on a button of a click kind ("confirm", "cancel", "select", "menu"). */
export function interfaceClickSound(kind: string, rand?: () => number): string | null {
  return pickEventSound('interfaceClick', { kind }, rand);
}

/** characterSelected: a click on a character in the character select. */
export function characterSelectedSound(rand?: () => number): string | null {
  return pickEventSound('characterSelected', {}, rand);
}

/** An item as heldWeaponOf reads it: its group (0 swords, 3 spears, 4 bows and crossbows) and number. */
export interface HeldItem {
  group: number;
  number: number;
}

/**
 * What a player holds for the swing (Audio::HeldWeaponOf), the first that fits:
 *  - 'bow' / 'crossbow': CCharacterManager::GetEquipedBowType, i.e. the left-hand item's weapon
 *    kind in the item data is Bow, or else the right-hand item's is Crossbow. The item data
 *    decides, so the caller passes its answer as `bowType`;
 *  - 'magicCrossbow': the bow group's items 13 and 14 in the right hand (MODEL_BLUEWING_CROSSBOW
 *    up to MODEL_ARROWS);
 *  - 'saberOrSpear': a Light Saber (swords 10) or the spear group's first item (MODEL_SPEAR) in
 *    the right hand;
 *  - 'other': any weapon in either hand; 'none': empty hands.
 */
export function heldWeaponOf(right: HeldItem | null, left: HeldItem | null, bowType: 'bow' | 'crossbow' | null): HeldWeapon {
  if (bowType === 'bow') return 'bow';
  if (bowType === 'crossbow') return 'crossbow';
  if (right !== null && right.group === 4 && right.number >= 13 && right.number < 15) return 'magicCrossbow';
  if (right !== null && ((right.group === 0 && right.number === 10) || (right.group === 3 && right.number === 0))) return 'saberOrSpear';
  if (right !== null || left !== null) return 'other';
  return 'none';
}

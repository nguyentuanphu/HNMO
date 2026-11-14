// Illusion Temple, Doppelganger and Imperial Guardian, as the desktop keeps them (UI/Events:
// CCursedTempleEnter, CCursedTempleSystem, CCursedTempleResult, CursedTempleUpdates, CDoppelGangerWindow,
// CDoppelGangerFrame, DoppelgangerUpdates, EmpireGuardianUpdates; UI/NPCs: CEmpireGuardianNPC,
// CEmpireGuardianTimer; GameLogic/Events/w_CursedTemple; the handlers of WSclient.cpp).
//
// What the server sends and what the client makes of it:
//   C3 30 14 / 23 / 24  the Illusion Temple / Doppelganger / Imperial Guardian entry window (ReceiveNpcWindow)
//   C1 BF 00            the Illusion Temple entry result (ReceiveCursedTempleEnterResult)
//   C1 BF 01            the temple's state: time, the sacred item's carrier, the points, the team, the party
//   C1 BF 02 / 07 / 06  a temple skill used / ended, the kill points
//   C1 BF 03            the members of each temple (the entry window's count)
//   C1 BF 04            the temple's result
//   C1 BF 09            the temple's state change (wait, ready, play, end)
//   C1 BF 0E-14         Doppelganger: entry result, the monsters' position, the state, the Ice Walker,
//                       the time and the party, the result, the monsters that got through
//   C1 F7 02 / 04 / 06  Imperial Guardian: entry result, the timer, the result
// No DOM here: the windows are ui/instanceEventWindows.ts.

import type { Connection } from '../net/connection.js';
import type { Item } from './inventory.js';
import type { EventHero } from './events.js';
import { format } from './events.js';

/** CMapManager::IsCursedTemple, the Doppelganger and Imperial Guardian maps. */
export const ILLUSION_TEMPLE_MAPS = [45, 46, 47, 48, 49, 50];
export const DOPPELGANGER_MAPS = [65, 66, 67, 68];
export const IMPERIAL_GUARDIAN_MAPS = [69, 70, 71, 72];

// The desktop's texts (Localization/Game.en.resx), by their I18N names.
export const T = {
  DoYouWishToGoToTheIllusionTemple: 'Do you wish to go to the Illusion Temple?', Enter: 'Enter', Close: 'Close',
  TheDIllusionTemple: 'The %d Illusion Temple', MasterLevel: 'Master Level', LevelDD: 'Level %d-%d',
  EntranceEnabled: 'Entrance enabled.', EntranceDisabled: 'Entrance disabled.', CurrentMembersD: 'Current members: %d',
  YouMustBeOfTheMinimumLevel220ToEnterTheZone: 'You must be of the minimum level 220 to enter the zone.',
  TheAdmissionAndScrollLevelsDoNotMatch: 'The admission and scroll levels do not match.',
  YouCannotEnterTheZoneWithTheNumberOfMembersExceedingTheLimit: 'You cannot enter the zone with the number of members exceeding the limit.',
  YouMayEnterOnlyDTimesPerDay: 'You may enter only %d times per day.',
  YouCannotEnterIfYouAreA1stStageOutlaw: 'You cannot enter if you are a 1st Stage Outlaw.',
  YouCanTWarpWearingTheRingOfTransformation: "You can't warp wearing the Ring of Transformation.",
  SkillDescriptions: ['Absorb damage with the protection shield.', 'Mobility disabled.', 'Relocate to the character that carries the sacred item.', 'Shield gage reduced of 50%.'],
  RequiredKillPoint: 'Required Kill Point', AchievedKillPoint: 'Achieved Kill Point',
  TutorialSteps: [
    ['<STEP 1: Battle Begins>', 'The stone statue appears randomly from one of the two locations.', 'The sacred item may be achieved by clicking on the stone statue.', 'Be cautious of the fact that the mobility slows down while carrying the sacred items.'],
    ['<STEP 2: Storage of the Sacred Item>', 'Click on the storage of the sacred item from the start location; and you will gain the points accordingly.', 'The goal is to achieve as many points as possible within the given period.', 'The stone statue reappears after the storage. Look for the statue.'],
    ['<STEP 3: Official Skills>', 'You may achieve the kill points from hunting monsters and the opponent players in their zone.', 'Mouse wheel button ? change skill types, Shift + mouse right-click ? use', 'There are 4 types of skills that can be appropriately used for each situation.'],
  ],
  KillPointIsnTSufficient: "Kill Point isn't sufficient.",
  TheAlliesAreAdvancingOnWeAreNotFarFromTheVictoryChargeOn: 'The allies are advancing on. We are not far from the victory! Charge on!',
  AlthoughWeHaveLostThisBattle: 'Although we have lost this battle, we will continue on until the allies win!',
  HoorayForTheIllusionSorceryWe: 'Hooray for the Illusion Sorcery! We are almost there! Come to the frontier now!',
  YouMustNotLoseTheTemple: 'You must not lose the temple. Let us prepare for the battle to secure this temple.',
  KillPointDAchieved: 'Kill Point %d achieved.', NoItem: 'No item',
  WeHaveEnteredTheHeartOf: 'We have entered the heart of the Illusion Temple. The sacred items of this temple are our ultimate goal. Move as many sacred items as you can to our storage. The brave one will certainly be compensated',
  ListenToThisTheAlliesHave: 'Listen to this. The allies have approached the entrance of this temple. We must fight with all our might and keep the temple from them so that we may secure the sacred items as they are.',
  YouAreCurrentGainingTheSacredItem: 'You are current gaining the sacred item.', YouAreCurrentlyStoringTheSacredItem: 'You are currently storing the sacred item.',
  MUAlliance: 'HNMO alliance', IllusionSorcery: 'Illusion Sorcery', HeroList: 'Hero List', Camp: 'Camp', Character: 'Character', Class: 'Class', EXP: 'EXP', Point: 'point',
  YouMayBeCompensatedByClickingOnTheCloseButton: 'You may be compensated by clicking on the Close button.',
  // Doppelganger
  BattleHasAlreadyCommencedYouCannotEnter: 'Battle has already commenced. You cannot enter.',
  DoppelgangerStart: ['3 monsters reaching the magic circle,', ' ', 'the character dying, the server disconnecting, or using the warp command', ' ', 'will result in Doppelganger defense failure.'],
  DoppelgangerSuccess: ['Congratulations!', ' ', "You've successfully defended Doppelganger."],
  DoppelgangerDefenseFailed: 'Doppelganger defense failed.',
  DoppelgangerMonstersEscaped: ['You failed to fend off monsters and', ' ', 'allowed them to reach the point line.'],
  Lugard: 'Lugard', OnlyThoseInPossessionOfAMirrorOfDimensions: 'Only those in possession of a Mirror of Dimensions',
  MayPassThroughTheDoppelgangerGate: 'may pass through the Doppelganger gate.', WillYouShowMeYourMirror: 'Will you show me your mirror?',
  MirrorOfDimensions: 'Mirror of Dimensions', EntryTime: 'Entry Time', YouMayNowEnter: 'You may now enter.', EnterAfterDMinutes: 'Enter after %d minutes',
  MonstersPassedDD: 'Monsters Passed: ( %d/%d )', TimeLeft: 'Time Left',
  // Imperial Guardian
  JerintTheAssistant: 'Jerint the Assistant', WithoutGaionSOrder: "Without Gaion's Order,",
  YouCannotEnterTheFortressOfEmpireGuardians: 'you cannot enter the Fortress of Empire Guardians.', WillYouShowMeTheOrder: 'Will you show me the order?',
  GaionSOrder: "Gaion's Order", Warning: '!! Warning !!', TheRound7MapSundayCanOnly: 'The round 7 map (Sunday) can only',
  BeAccessedIfYouHaveA: 'be accessed if you have a', CompleteSecromicon: 'Complete Secromicon.',
  YouCanOnlyEnterAsAMemberOfAParty: 'You can only enter as a member of a party.', EntryTimeColon: 'Entry Time:',
  QuestItemMissing: 'Quest Item Missing', CapacityExceeded: 'Capacity Exceeded', ThereIsStillTimeRemainingInThisZone: 'There is still time remaining in this zone.',
  YouHaveFailedToConquerThe: 'You have failed to conquer the', FortressOfEmpireGuardians: 'Fortress of Empire Guardians.',
  FortressOfEmpireGuardiansRoundD: 'Fortress of Empire Guardians Round %d', ZoneDCleared: 'Zone %d cleared', HasBeenCleared: 'has been cleared.',
  RewardedExpD: 'Rewarded Exp: %d', RoundDZoneD: 'Round %d (Zone %d)', StandbyTime: 'Standby Time', RemainingMonsters: 'Remaining Monsters',
} as const;

const u16 = (bytes: Uint8Array, at: number) => bytes[at] | (bytes[at + 1] << 8);
const u16be = (bytes: Uint8Array, at: number) => (bytes[at] << 8) | bytes[at + 1];
const u32 = (bytes: Uint8Array, at: number) => (bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24)) >>> 0;
const text = (bytes: Uint8Array, at: number, length: number) => {
  const raw = bytes.subarray(at, at + length);
  const end = raw.indexOf(0);
  return new TextDecoder().decode(end < 0 ? raw : raw.subarray(0, end));
};

// ---------- Illusion Temple ----------

/** eCursedTempleState: the server's state + 1. */
export const TempleState = { NONE: 0, WAIT: 1, READY: 2, PLAY: 3, END: 4 } as const;
/** eCursedTempleTeam. */
export const Team = { ALLIED: 0, ILLUSION: 1, NONE: 2 } as const;
/** The temple skills (AT_SKILL_CURSED_TEMPLE_PRODECTION … SUBLIMATION). */
export const TempleSkill = { PROTECTION: 210, RESTRAINT: 211, TELEPORT: 212, SUBLIMATION: 213 } as const;
/** The temple's NPCs (CCursedTempleSystem). */
export const TempleNpc = { STATUE: 380, ALLIED: 381, ILLUSION: 382, ALLIED_STORAGE: 383, ILLUSION_STORAGE: 384 } as const;
const NO_ONE = 0xffff;
const MAX_PARTY = 5;

const ENTER_LEVELS = [[220, 270], [271, 320], [321, 350], [351, 380], [381, 400]];

/** CCursedTempleEnter::CheckEnterLevel: the temple (1-5, 6 for a master), or -1 below level 220. */
export function templeEnterLevel(hero: EventHero): number {
  if (hero.master) return 6;
  const index = ENTER_LEVELS.findIndex(([low, high]) => hero.level >= low && hero.level <= high);
  return index < 0 ? -1 : index + 1;
}

export interface TempleEntryContent {
  title: string;
  eligible: boolean;
  templeLine: string;
  bands: { text: string; hero: boolean }[];
  membersLine: string;
  notice: string;
}

/** CCursedTempleEnter::SyncLines: the hero's temple, the six bands (the hero's marked) and the members. */
export function templeEntry(hero: EventHero, members: number): TempleEntryContent {
  const level = templeEnterLevel(hero);
  const content: TempleEntryContent = { title: T.DoYouWishToGoToTheIllusionTemple, eligible: level > 0, templeLine: '', bands: [], membersLine: '', notice: '' };
  if (!content.eligible) { content.notice = T.YouMustBeOfTheMinimumLevel220ToEnterTheZone; return content; }
  content.templeLine = format(T.TheDIllusionTemple, level);
  for (let i = 0; i < ENTER_LEVELS.length + 1; i++) {
    const band = i === ENTER_LEVELS.length ? T.MasterLevel : format(T.LevelDD, ENTER_LEVELS[i][0], ENTER_LEVELS[i][1]);
    const heroBand = level === i + 1;
    content.bands.push({ text: `${band} ${heroBand ? T.EntranceEnabled : T.EntranceDisabled}`, hero: heroBand });
  }
  content.membersLine = format(T.CurrentMembersD, members);
  return content;
}

export interface TempleMate { id: number; map: number; x: number; y: number }
export interface TemplePlayerResult { name: string; map: number; team: number; classLine: number; classStep: number; experience: number }
export interface TempleResult { allied: number; illusion: number; players: TemplePlayerResult[]; myTeam: number; winState: number }

/** The packed class of the result (ChangeServerClassTypeToClientClassType): the line, its step 0-2. */
function unpackClass(packed: number): { line: number; step: number } {
  const line = packed & 0x0f, evolution = packed >> 4;
  return { line: line > 6 ? 0 : line, step: evolution >= 3 ? 2 : evolution === 2 ? 1 : 0 };
}

/** CCursedTempleResult::SetResult: 1 the hero's team won, 2 it lost (or got fewer than two points). */
export function templeWinState(myTeam: number, allied: number, illusion: number): number {
  if (myTeam === Team.ALLIED) return illusion < allied && allied >= 2 ? 1 : 2;
  return allied < illusion && illusion >= 2 ? 1 : 2;
}

/** CCursedTempleSystem and CursedTemple: the temple's state while the hero is in it. */
export class IllusionTemple {
  state: number = TempleState.NONE;
  /** The game system HUD (INTERFACE_CURSEDTEMPLE_GAMESYSTEM). */
  shown = false;
  time = 0;
  carrier = NO_ONE;
  carrierX = 0;
  carrierY = 0;
  allied = 0;
  illusion = 0;
  myTeam: number = Team.NONE;
  party: TempleMate[] = [];
  skillPoints = 0;
  /** Hero->m_CursedTempleCurSkill and m_CursedTempleCurSkillPacket. */
  skill: number = TempleSkill.PROTECTION;
  skillPending = false;
  /** The transparency button's alpha (m_Alph). */
  alpha = 1;
  /** The score effect (StartScoreEffect / UpdateScore): state 0 fading in, 1 shown, 2 fading out. */
  score = { active: false, alpha: 0, state: 0, start: 0 };
  /** The tutorial step (StartTutorialStep / UpdateTutorialStep). */
  tutorial = { active: false, step: 0, start: 0 };
  /** The entry window's minutes and members (SetEntryOffer / SetEntryCounts). */
  entryTime = 0;
  entryCount = 0;
  result: TempleResult | null = null;

  /** ResetCursedTempleSystemInfo. */
  reset(): void {
    Object.assign(this, { time: 0, carrier: NO_ONE, carrierX: 0, carrierY: 0, allied: 0, illusion: 0, myTeam: Team.NONE, party: [], alpha: 1, skillPoints: 0 });
    this.tutorial = { active: false, step: 0, start: 0 };
    this.score = { active: false, alpha: 0, state: 0, start: 0 };
  }

  /** UpdateScore / UpdateTutorialStep, every frame (dt in 25 fps frames, the desktop's FPS_ANIMATION_FACTOR). */
  step(now: number, frames: number): void {
    const score = this.score;
    if (score.active) {
      if (score.state === 0) {
        score.alpha += 0.015 * frames;
        if (score.alpha > 1) { score.state = 1; score.alpha = 1; score.start = now; }
      } else if (score.state === 1) {
        if (now - score.start >= 5000) score.state = 2;
      } else {
        score.alpha -= 0.015 * frames;
        if (score.alpha < 0) this.score = { active: false, alpha: 0, state: 0, start: 0 };
      }
    }
    if (this.tutorial.active && now - this.tutorial.start >= 10_000) {
      this.tutorial.step += 1;
      this.tutorial.start = now;
      if (this.tutorial.step === 3) this.tutorial = { active: false, step: 0, start: 0 };
    }
  }

  /** CheckHeroSkillType: the wheel or a button steps the skill within the four. */
  stepSkill(up: boolean): void {
    if (up && this.skill < TempleSkill.SUBLIMATION) this.skill += 1;
    else if (!up && this.skill > TempleSkill.PROTECTION) this.skill -= 1;
  }

  /** The score messages of SetMatchStatus: which line a change of points brings. */
  scoreMessage(allied: number, illusion: number): string | null {
    if (this.myTeam === Team.ALLIED) {
      if (this.allied !== allied) return T.TheAlliesAreAdvancingOnWeAreNotFarFromTheVictoryChargeOn;
      if (this.illusion !== illusion) return T.AlthoughWeHaveLostThisBattle;
    } else {
      if (this.illusion !== illusion) return T.HoorayForTheIllusionSorceryWe;
      if (this.allied !== allied) return T.YouMustNotLoseTheTemple;
    }
    return null;
  }
}

/** The hero as the progress box's CheckHeroAction sees it and the temple skill needs it. */
export interface TempleHero { id: number; cell: { x: number; y: number }; busy: () => boolean }

// ---------- Doppelganger ----------

/** CDoppelGangerFrame: the monsters, the Ice Walker and the party along the path. */
export class DoppelgangerFrame {
  shown = false;
  entered = 0;
  max = 0;
  gauge = 0;
  gaugeReceived = 0;
  time = 1800;
  timerStopped = false;
  iceWalker = { enabled: false, position: 0, received: 0 };
  /** The party's markers by player id: shown and received positions, received -1 when not in the last update. */
  party = new Map<number, { position: number; received: number }>();
  /** EnabledDoppelGangerEvent: the event's music plays while it's on. */
  enabled = false;

  /** OpenningProcess. */
  open(): void {
    Object.assign(this, { shown: true, entered: 0, max: 0, gauge: 0, gaugeReceived: 0, time: 600, timerStopped: false, enabled: true });
    this.party.clear();
    this.iceWalker = { enabled: false, position: 0, received: 0 };
  }

  close(): void {
    this.shown = false;
    this.enabled = false;
  }

  /** StepGauges: everything moves 0.01 towards where it was last reported, each frame drawn. */
  step(): void {
    const approach = (value: number, target: number) => (value + 0.01 <= target ? value + 0.01 : value - 0.01 >= target ? value - 0.01 : target);
    this.gauge = approach(this.gauge, this.gaugeReceived);
    if (this.iceWalker.enabled) this.iceWalker.position = approach(this.iceWalker.position, this.iceWalker.received);
    for (const marker of this.party.values()) if (marker.received !== -1) marker.position = approach(marker.position, marker.received);
  }

  /** SetIceWalkerMap. */
  setIceWalker(present: boolean, position: number): void {
    if (present) {
      if (!this.iceWalker.enabled) this.iceWalker.position = position;
      this.iceWalker.received = position;
      this.iceWalker.enabled = true;
    } else {
      this.iceWalker = { enabled: false, position: this.iceWalker.position, received: 0 };
    }
  }

  /** SetPartyMemberRcvd + SetPartyMemberInfo. */
  setParty(members: { id: number; position: number }[]): void {
    for (const marker of this.party.values()) marker.received = -1;
    for (const member of members) {
      const marker = this.party.get(member.id);
      if (marker) marker.received = member.position;
      else this.party.set(member.id, { position: 0, received: member.position });
    }
  }
}

// ---------- Imperial Guardian ----------

/** CEmpireGuardianTimer. */
export class ImperialGuardianTimer {
  shown = false;
  /** Milliseconds left (m_dTime). */
  time = 600_000;
  type = 1;
  day = 1;
  zone = 1;
  monsters = 0;
}

/** CEmpireGuardianTimer::SyncView: the round line, the caption and the time with its colour. */
export function imperialTimerLines(timer: ImperialGuardianTimer): { round: string; caption: string; captionState: string; time: string; timeState: string } {
  const caption = timer.type === 2 ? `${T.TimeLeft} (${T.RemainingMonsters})` : timer.type <= 1 ? T.StandbyTime : '';
  const captionState = timer.type === 2 ? 'normal' : timer.type <= 1 ? 'standby' : '';
  const seconds = Math.floor(timer.time / 1000), minutes = Math.floor(seconds / 60);
  const timeState = minutes > 2 ? 'normal' : minutes > 0 ? 'closing' : 'expiring';
  return { round: format(T.RoundDZoneD, timer.day, timer.zone), caption, captionState, time: `${format('%.2d:%.2d', minutes, seconds % 60)}(${timer.monsters})`, timeState };
}

// ---------- the packets ----------

export interface InstanceEventActions {
  mapNumber: () => number | null;
  hero: () => EventHero;
  heroId: () => number;
  /** A box of lines with OK (UI::Dialogs::ShowConfirm). */
  ok: (lines: string[]) => void;
  /** A line in the system log; error lines are red (TYPE_ERROR_MESSAGE). */
  message: (text: string, error?: boolean) => void;
  /** Opens an entry window (C3 30 14 / 23 / 24). */
  openEntry: (kind: 'illusionTemple' | 'doppelganger' | 'imperialGuardian') => void;
  /** HideAll: the windows, the entry windows included. */
  closeWindows: () => void;
  play: (id: string) => void;
  /** The temple skill's look: the user's animation, the target's (ResolveSkill). */
  skillLook: (skill: number, source: number, target: number) => void;
}

/** The three events' state, fed by the connection's packets. */
export class InstanceEvents {
  readonly temple = new IllusionTemple();
  readonly doppelganger = new DoppelgangerFrame();
  readonly imperial = new ImperialGuardianTimer();
  /** The Doppelganger entry window's minutes and its locked Enter (SetRemainTime, LockEnterButton). */
  doppelgangerEntry = { time: 0, locked: false };
  /** The last box shown, for tests. */
  lastBox: string[] | null = null;
  private mapOfEvents: number | null = null;

  constructor(private readonly connection: Connection, private readonly actions: InstanceEventActions) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  private ok(lines: string[]): void {
    this.lastBox = lines;
    this.actions.ok(lines);
  }

  /** The map changes of ReceiveJoinMapServer / ReceiveTeleport / ReceiveRevival: the event HUDs close off their maps. */
  syncMap(): void {
    const map = this.actions.mapNumber();
    if (map === this.mapOfEvents) return;
    const before = this.mapOfEvents;
    this.mapOfEvents = map;
    const inTemple = (m: number | null) => m !== null && ILLUSION_TEMPLE_MAPS.includes(m);
    if (inTemple(before) && !inTemple(map)) { this.temple.state = TempleState.NONE; this.temple.reset(); this.temple.shown = false; }
    if (map === null || !DOPPELGANGER_MAPS.includes(map)) this.doppelganger.close();
    if (map === null || !IMPERIAL_GUARDIAN_MAPS.includes(map)) this.imperial.shown = false;
  }

  handle(bytes: Uint8Array): void {
    this.syncMap();
    const c1 = bytes[0] === 0xc1, c3 = bytes[0] === 0xc3;
    if (!c1 && !c3) return;
    const code = bytes[2];
    if (code === 0x30 && bytes.length >= 4) {
      if (bytes[3] === 0x14) { this.temple.entryTime = bytes[4] ?? 0; this.temple.entryCount = bytes[5] ?? 0; this.actions.openEntry('illusionTemple'); }
      else if (bytes[3] === 0x23) { this.doppelgangerEntry = { time: bytes[4] ?? 0, locked: (bytes[4] ?? 0) !== 0 }; this.actions.openEntry('doppelganger'); }
      else if (bytes[3] === 0x24) this.actions.openEntry('imperialGuardian');
      return;
    }
    if (code === 0xbf && bytes.length >= 4) this.miniGame(bytes[3], bytes);
    else if (code === 0xf7 && bytes.length >= 4) this.imperialGuardian(bytes[3], bytes);
  }

  private miniGame(sub: number, bytes: Uint8Array): void {
    const temple = this.temple;
    switch (sub) {
      case 0x00: if (bytes.length >= 5) this.templeEnterResult(bytes[4]); break;
      case 0x01: if (bytes.length >= 14) this.templeStatus(bytes); break;
      case 0x02: if (bytes.length >= 12) this.templeSkillResult(bytes[4] !== 0, u16be(bytes, 5), u16(bytes, 8), u16(bytes, 10)); break;
      case 0x03: if (bytes.length >= 10) { const level = templeEnterLevel(this.actions.hero()); if (level > 0) temple.entryCount = bytes[4 + level - 1]; } break;
      case 0x04: if (bytes.length >= 7) this.templeResult(bytes); break;
      case 0x06: if (bytes.length >= 5) this.templeSkillPoints(bytes[4]); break;
      case 0x07: if (bytes.length >= 8) { /* EndSkill: the buffs' end comes with the buff packets. */ } break;
      case 0x09: if (bytes.length >= 6) this.templeState((bytes[5] + 1) & 0xff); break;
      case 0x0e: if (bytes.length >= 5) this.doppelgangerEnterResult(bytes[4]); break;
      case 0x0f: if (bytes.length >= 5) this.doppelganger.gaugeReceived = bytes[4] / 22; break;
      case 0x10: if (bytes.length >= 5 && bytes[4] === 2) { this.doppelganger.open(); this.ok([...T.DoppelgangerStart]); } break;
      case 0x11: if (bytes.length >= 6) this.doppelganger.setIceWalker(bytes[4] === 0, bytes[4] === 0 ? (22 - bytes[5]) / 22 : 0); break;
      case 0x12: if (bytes.length >= 8) this.doppelgangerPlayInfo(bytes); break;
      case 0x13: if (bytes.length >= 5) this.doppelgangerResult(bytes[4]); break;
      case 0x14: if (bytes.length >= 6) { this.doppelganger.max = bytes[4]; this.doppelganger.entered = bytes[5]; } break;
      default: break;
    }
  }

  // ReceiveCursedTempleEnterResult + CursedTemple::UpdateTempleSystemMsg
  private templeEnterResult(result: number): void {
    if (result === 0) this.actions.closeWindows();
    const texts: Record<number, string> = {
      3: T.TheAdmissionAndScrollLevelsDoNotMatch, 4: T.YouCannotEnterTheZoneWithTheNumberOfMembersExceedingTheLimit,
      5: format(T.YouMayEnterOnlyDTimesPerDay, 6), 7: T.YouCannotEnterIfYouAreA1stStageOutlaw, 8: T.YouCanTWarpWearingTheRingOfTransformation,
    };
    if (texts[result]) this.actions.message(texts[result], true);
  }

  // ReceiveCursedTempleInfo: CCursedTempleSystem::SetMatchStatus and CursedTemple::ReceiveCursedTempleInfo.
  private templeStatus(bytes: Uint8Array): void {
    const temple = this.temple;
    const carrier = u16(bytes, 6), allied = bytes[10], illusion = bytes[11], team = bytes[12], count = bytes[13];
    temple.time = u16(bytes, 4);
    // The carrier's arrival plays its sound (the sacred item's effect on it, SOUND_CURSEDTEMPLE_GAMESYSTEM3).
    if (carrier !== NO_ONE && carrier !== temple.carrier) this.actions.play('SOUND_CURSEDTEMPLE_GAMESYSTEM3');
    temple.carrier = carrier;
    temple.carrierX = bytes[8];
    temple.carrierY = bytes[9];
    temple.myTeam = team;
    const line = temple.scoreMessage(allied, illusion);
    if (line) {
      this.actions.play('SOUND_CURSEDTEMPLE_GAMESYSTEM4');
      temple.score = { active: true, alpha: 0, state: 0, start: 0 };
      this.actions.message(line, true);
    }
    temple.allied = allied;
    temple.illusion = illusion;
    const party: TempleMate[] = [];
    for (let i = 0; i < Math.min(count, MAX_PARTY) && 14 + (i + 1) * 6 <= bytes.length; i++) {
      const at = 14 + i * 6;
      const id = u16(bytes, at);
      if (id !== NO_ONE) party.push({ id, map: bytes[at + 2], x: bytes[at + 3], y: bytes[at + 4] });
      else if (temple.party[i]) party.push(temple.party[i]);
    }
    temple.party = party;
  }

  // ReceiveCursedTempMagicResult: CCursedTempleSystem::ResolveSkill.
  private templeSkillResult(success: boolean, skill: number, source: number, target: number): void {
    if (source === this.actions.heroId()) this.temple.skillPending = false;
    if (!success) return;
    this.actions.skillLook(skill, source, target);
  }

  // ReceiveCursedTempSkillPoint: SetSkillPoints.
  private templeSkillPoints(points: number): void {
    if (this.temple.skillPoints < points) this.actions.message(format(T.KillPointDAchieved, points - this.temple.skillPoints));
    this.temple.skillPoints = points;
  }

  // ReceiveCursedTempleGameResult: ShowMatchResult.
  private templeResult(bytes: Uint8Array): void {
    const allied = bytes[4], illusion = bytes[5], count = bytes[6];
    const players: TemplePlayerResult[] = [];
    for (let i = 0; i < count && 7 + (i + 1) * 20 <= bytes.length; i++) {
      const at = 7 + i * 20;
      const { line, step } = unpackClass(bytes[at + 12]);
      players.push({ name: text(bytes, at, 10), map: bytes[at + 10], team: bytes[at + 11], classLine: line, classStep: step, experience: u32(bytes, at + 16) });
    }
    this.actions.closeWindows();
    // The team is the HUD's while it is open (ResetGameResultInfo + SetMyTeam), else none.
    const myTeam = this.temple.shown ? this.temple.myTeam : Team.NONE;
    this.temple.shown = false;
    this.actions.play('SOUND_CURSEDTEMPLE_GAMESYSTEM5');
    this.temple.result = { allied, illusion, players, myTeam, winState: templeWinState(myTeam, allied, illusion) };
  }

  // ReceiveCursedTempleState: the ready phase opens the HUD with its tutorial (BeginReadyPhase).
  templeState(state: number, now = performance.now()): void {
    const temple = this.temple;
    if (state === TempleState.READY) {
      this.actions.closeWindows();
      temple.reset();
      temple.tutorial = { active: true, step: 0, start: now };
      this.actions.play('SOUND_CURSEDTEMPLE_GAMESYSTEM1');
      temple.shown = true;
    }
    temple.state = state;
  }

  // ReceiveEnterDoppelGangerEvent
  private doppelgangerEnterResult(result: number): void {
    if (result === 1) this.doppelgangerEntry.locked = true;
    else if (result === 2) { this.actions.message(T.BattleHasAlreadyCommencedYouCannotEnter, true); this.doppelgangerEntry.locked = true; }
    else if (result === 3) { this.actions.message(T.YouCannotEnterIfYouAreA1stStageOutlaw, true); this.doppelgangerEntry.locked = true; }
    else if (result === 4) this.doppelgangerEntry.locked = false;
  }

  // ReceiveDoppelGangerTimePartyState: UpdateParty.
  private doppelgangerPlayInfo(bytes: Uint8Array): void {
    this.doppelganger.time = u16(bytes, 4);
    const members: { id: number; position: number }[] = [];
    for (let i = 0; i < bytes[6] && 8 + (i + 1) * 4 <= bytes.length; i++) members.push({ id: u16(bytes, 8 + i * 4), position: (22 - bytes[8 + i * 4 + 3]) / 22 });
    this.doppelganger.setParty(members);
  }

  // ReceiveDoppelGangerResult: FinishMatch and its box.
  private doppelgangerResult(result: number): void {
    this.actions.play('SOUND_CHAOS_END');
    this.doppelganger.timerStopped = true;
    this.doppelganger.enabled = false;
    if (result === 0) { this.doppelganger.time = 0; this.ok([...T.DoppelgangerSuccess]); }
    else if (result === 1) this.ok([T.DoppelgangerDefenseFailed]);
    else if (result === 2) this.ok([...T.DoppelgangerMonstersEscaped]);
  }

  private imperialGuardian(sub: number, bytes: Uint8Array): void {
    const timer = this.imperial;
    if (sub === 0x02 && bytes.length >= 12) {
      // ReceiveEnterEmpireGuardianEvent
      const result = bytes[4];
      if (result === 0) { timer.day = bytes[5]; timer.zone = bytes[6]; timer.time = u32(bytes, 8); }
      else if (result === 1) this.ok([T.EntryTimeColon, ' ', format(T.EnterAfterDMinutes, Math.floor(u32(bytes, 8) / 60000))]);
      else if (result === 2) this.ok([T.QuestItemMissing]);
      else if (result === 3) this.ok([T.CapacityExceeded]);
      else if (result === 4) this.ok([T.ThereIsStillTimeRemainingInThisZone]);
      else if (result === 5) this.ok([T.YouCanOnlyEnterAsAMemberOfAParty]);
    } else if (sub === 0x04 && bytes.length >= 13) {
      // ReceiveRemainTickEmpireGuardian: UpdateTimer.
      timer.shown = true;
      timer.type = bytes[4];
      timer.time = u32(bytes, 8);
      timer.monsters = bytes[12];
    } else if (sub === 0x06 && bytes.length >= 12) {
      // ReceiveResultEmpireGuardian
      const result = bytes[4];
      if (result === 0) this.ok([T.YouHaveFailedToConquerThe, T.FortressOfEmpireGuardians]);
      else if (result === 1) this.ok([format(T.FortressOfEmpireGuardiansRoundD, timer.day), format(T.ZoneDCleared, timer.zone)]);
      else if (result === 2) this.ok([format(T.FortressOfEmpireGuardiansRoundD, timer.day), T.HasBeenCleared, format(T.RewardedExpD, u32(bytes, 8))]);
      timer.shown = false;
    }
  }

  // ---------- requests ----------

  /** CCursedTempleEnter's Enter: the hero's temple, the ticket left to the server; else the mismatch line. */
  enterTemple(): void {
    const level = templeEnterLevel(this.actions.hero());
    if (level > 0) this.connection.send('ClientToServer', 'IllusionTempleEnterRequest', { MapNumber: level, ItemSlot: 0xff });
    else this.actions.message(T.TheAdmissionAndScrollLevelsDoNotMatch, true);
  }

  /** CDoppelGangerWindow's Enter (when not locked). */
  enterDoppelganger(): void {
    this.connection.send('ClientToServer', 'DoppelgangerEnterRequest', { TicketItemSlot: 0xff });
  }

  /** CEmpireGuardianNPC's Enter (SendEnterEmpireGuardianEvent's default slot). */
  enterImperialGuardian(): void {
    this.connection.send('ClientToServer', 'EnterEmpireGuardianEvent', { ItemSlot: 0x01 });
    this.actions.play('SOUND_INTERFACE01');
  }

  /** CCursedTempleResult::ClosingProcess: the reward. */
  requestTempleReward(): void {
    this.connection.send('ClientToServer', 'IllusionTempleRewardRequest', {});
    this.temple.result = null;
  }

  /**
   * CCursedTempleSystem::SetCursedTempleSkill (Shift + right click): the kill points, the carrier for
   * the teleport, the target for restraint and sublimation (not a team mate), then the request.
   * Returns false when nothing was sent.
   */
  useTempleSkill(target: { id: number; cell: { x: number; y: number } } | null, heroCell: { x: number; y: number }, killCount: number, distance: number): boolean {
    const temple = this.temple;
    if (temple.skillPending) return false;
    if (temple.skillPoints < killCount) { this.actions.message(T.KillPointIsnTSufficient, true); return false; }
    if (temple.skill === TempleSkill.TELEPORT && temple.carrier === NO_ONE) return false;
    const targeted = temple.skill === TempleSkill.RESTRAINT || temple.skill === TempleSkill.SUBLIMATION;
    let targetId = this.actions.heroId();
    if (targeted) {
      if (!target) return false;
      // CheckTile: the target within the skill's distance.
      if (Math.hypot(target.cell.x - heroCell.x, target.cell.y - heroCell.y) > distance) return false;
      if (temple.party.some((mate) => mate.id === target.id)) return false;
      targetId = target.id;
    }
    this.connection.send('ClientToServer', 'IllusionTempleSkillRequest', { SkillNumber: temple.skill, TargetObjectIndex: targetId, Distance: Math.round(distance) });
    temple.skillPending = true;
    return true;
  }

  /**
   * The temple's own NPCs (CheckTalkProgressNpc): the statue and the hero's storage start the 10-second
   * progress box ("gain" / "store"), the other storage does nothing, the generals answer in a box;
   * null for every other NPC (talked to as usual).
   */
  templeTalk(npcNumber: number, hasSacredItem: boolean, boxOpen: boolean): 'gain' | 'store' | 'handled' | null {
    if (!ILLUSION_TEMPLE_MAPS.includes(this.actions.mapNumber() ?? -1)) return null;
    const team = this.temple.myTeam;
    if (npcNumber === TempleNpc.STATUE) return 'gain';
    if (npcNumber === TempleNpc.ALLIED_STORAGE || npcNumber === TempleNpc.ILLUSION_STORAGE) {
      const own = (npcNumber === TempleNpc.ALLIED_STORAGE && team === Team.ALLIED) || (npcNumber === TempleNpc.ILLUSION_STORAGE && team === Team.ILLUSION);
      // The other team's storage: nothing (the original returned before talking).
      if (!own) return 'handled';
      if (hasSacredItem) return 'store';
      this.actions.message(T.NoItem, true);
      return 'handled';
    }
    if (npcNumber === TempleNpc.ALLIED || npcNumber === TempleNpc.ILLUSION) {
      if (!boxOpen) this.ok([npcNumber === TempleNpc.ALLIED ? T.WeHaveEnteredTheHeartOf : T.ListenToThisTheAlliesHave]);
      return 'handled';
    }
    return null;
  }

  /** The event items right-clicked in the bag (CInventoryActionController): true when it is one. */
  useTicket(item: Item): boolean {
    const send = (level: number) => this.connection.send('ClientToServer', 'MiniGameOpeningStateRequest', { EventType: 5, EventLevel: level });
    if (item.group === 13 && item.number === 61) {
      // ITEM_ILLUSION_TEMPLE_TICKET: CaculateFreeTicketLevel(FREETICKET_TYPE_CURSEDTEMPLE)
      const level = templeEnterLevel(this.actions.hero());
      send(level > 0 ? level : 0);
      return true;
    }
    if (item.group === 13 && item.number === 51) {
      // ITEM_SCROLL_OF_BLOOD
      send(item.level);
      return true;
    }
    return false;
  }
}

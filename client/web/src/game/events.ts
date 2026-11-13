// The three classic events, as the desktop keeps them (GameLogic/Events: matchEvent, CSBaseMatch,
// CNewBloodCastleSystem, CNewChaosCastleSystem, CSDevilSquareMatch; UI/Events: CEnterBloodCastle,
// CEnterDevilSquare, CBloodCastle, CChaosCastleTime; the event handlers of WSclient.cpp).
//
// What the server sends and what the client makes of it:
//   C3 30 4 / 6   the Devil Square / Blood Castle entry window (ReceiveNpcWindow)
//   C1 90 / 9A    the Devil Square / Blood Castle entry result (ReceiveMoveToDevilSquareResult, ReceiveMoveToEventMatchResult)
//   C1 AF 01      the Chaos Castle entry result (ReceiveMoveToEventMatchResult2)
//   C1 91         an event's opening state, after using a ticket (ReceiveEventZoneOpenTime)
//   C1 92         a state change in 30 seconds: the countdown line (ReceiveDevilSquareCountDown)
//   C1 93         the score: Devil Square's ranking, Blood Castle's (FF) or Chaos Castle's (FE) result
//   C1 9B         an event's state: time, kills, the weapon's carrier, the walls (ReceiveMatchGameCommand)
//   C1 46         terrain attributes switched on or off: the gate and the bridge, the falling walls (ReceiveSetAttribute)
//   C1 F3 40 1    an event NPC's line (ReceiveServerCommand)
// No DOM here: the windows are ui/eventWindows.ts.

import type { Connection } from '../net/connection.js';
import type { Item } from './inventory.js';

export type EventKind = 'bloodCastle' | 'chaosCastle' | 'devilSquare';

// CMapManager::InBloodCastle / InChaosCastle / InDevilSquare (map 32 is the second Devil Square).
const BLOOD_CASTLE_MAPS = [11, 12, 13, 14, 15, 16, 17, 52];
const CHAOS_CASTLE_MAPS = [18, 19, 20, 21, 22, 23, 53];
const DEVIL_SQUARE_MAPS = [9, 32];
const UNITED_MARKET_PLACE = 79;

/** The event a map belongs to, null for the others (matchEvent::CreateEventMatch). */
export function eventOfMap(map: number | null): EventKind | null {
  if (map === null) return null;
  if (BLOOD_CASTLE_MAPS.includes(map)) return 'bloodCastle';
  if (CHAOS_CASTLE_MAPS.includes(map)) return 'chaosCastle';
  if (DEVIL_SQUARE_MAPS.includes(map)) return 'devilSquare';
  return null;
}

// The desktop's texts (Localization/Game.en.resx), by their I18N names.
export const T = {
  MessengerOfArchangel: 'Messenger of Archangel',
  DevilSquare: 'Devil Square', BloodCastle: 'Blood Castle', ChaosCastle: 'Chaos Castle',
  YourWillToHelpTheArchangel: 'Your will to help the Archangel is appreciated. But be careful, young warrior for Blood Castle is a dangerous place. May God be with you.',
  CastleDLevelDD: 'Castle %d (level %d-%d)', CastleNoDMasterLevel: 'Castle no. %d (Master Level)',
  TheDSquareDDLevel: 'The %d Square (%d-%d level)', SquareNoDMasterLevel: 'Square no. %d (Master Level)',
  DevilSquareLines: [
    "You've been given a chance to prove your bravery.", 'No one has ever entered the Devil Square yet.', 'No human has ever gone there.',
    'Do not believe anything you see in there.', 'Only trust your bravery and strength', 'Only your bravery and strength will keep you alive.',
  ],
  Close: 'Close',
  MonsterDD: 'Monster: ( %d/%d )', MagicSkeletonDD: 'Magic Skeleton: ( %d/%d )', CharacterDD: 'Character: ( %d/%d )', TimeLeft: 'Time Left',
  YouWillEnterDevilSquareDSecondsFromNow: 'You will enter Devil Square (%d seconds from now)',
  TheGateOfDevilSquareWillCloseDownInDSeconds: 'The gate of Devil Square will close down in %d seconds',
  TheGateOfDevilSquareIsClosingDownDSecondsRemaining: 'The gate of Devil Square is closing down (%d seconds remaining)',
  SClosingInDSeconds: '%s Closing (in %d seconds)', SInfiltrationInDSeconds: '%s Infiltration (in %d seconds)',
  SEventEndsInDSeconds: '%s Event ends (in %d seconds)', SEventShutsDownInDSeconds: '%s Event shuts down (in %d seconds)',
  SPenetrationInDSeconds: '%s Penetration (in %d seconds)', Congratulations: 'Congratulations!',
  BloodCastleQuestCompleted: 'Congratulations! You have successfully completed the Blood Castle Quest!',
  BloodCastleQuestFailed: 'Unfortunately, you have failed to complete the Blood Castle Quest.',
  RewardedExpD: 'Rewarded Exp: %d', RewardedZenD: 'Rewarded Zen: %d', BloodCastlePointD: 'Blood Castle Point: %d',
  TheSpiritOfTheGuardHasBeenPurified: 'The spirit of the guard has been purified', QuestCompletedSuccessfully: 'You have successfully completed the quest.',
  QuestFailedUnfortunately: 'Unfortunately, you have failed the quest.', TryAgainNextTime: 'Try again next time',
  MonsterKillCountD: 'Monster Kill count: %d', PlayersKillCountD: 'Players Kill count: %d',
  SYourBraveryIsProvenInDevilSquare: '%s, Your bravery is proven in Devil Square.', Rank: 'Rank', Point: 'point', EXP: 'EXP', Reward: 'Reward', MyInfo: 'My Info',
  BringTheDevilSInvitationToEnter: "Bring the Devil's invitation to enter.",
  YouVeComeTooLateToEnterTheDevilSquare: "You've come too late to enter the Devil Square.",
  YouReUnderestimatingYourselfChooseAnotherSquare: "You're underestimating yourself. Choose another square.",
  IfYouWishToStayAliveChooseAnotherSquare: 'If you wish to stay alive, choose another square.',
  DevilSquareIsFull: 'Devil Square is full.', KillersAreRestrictedToEnterS: 'Killers are restricted to enter %s.',
  TheLevelOfTheCloakOfInvisibilityIsIncorrect: 'The level of the Cloak of Invisibility is incorrect.',
  TheTimeToEnterSHasPassed: 'The time to enter %s has passed.',
  TheMaximumCapacityOfSHasBeenReachedTheMaxNumberAllowedIsD: 'The maximum capacity of %s has been reached. The max. number allowed is %d.',
  YouAreNotAllowedToEnterMoreThanDTimesInOneDay: 'You are not allowed to enter more than %d times in one day.',
  YouAreShortOfZen: 'You are short of Zen.', YouCanEnterDevilSquareNow: 'You can enter Devil Square now!!',
  DevilSquareWillOpenInDMinutes: 'Devil Square will open in %d minutes.', YouCanEnterSNow: 'You can enter %s now.',
  AfterDMinutesYouMayEnterS: 'After %d minutes you may enter %s.', InSCurrentlyDDEntered: 'In %s, Currently [%d/%d] entered.', WhenD: 'when %d',
  YouCanOnlyUseThisInASafeZone: 'You can only use this in a safe zone.', IncorrectItem: 'Incorrect item',
  YouCannotEnterChaosCastle: 'You cannot enter Chaos Castle', FromTheMarketInLorencia: 'from the market in Lorencia.',
} as const;

// ServerMessageTexts.cpp: the event NPCs' lines (server command 1), Devil Square's from 0 and Blood
// Castle's from 20.
const DEVIL_SQUARE_NPC_LINES = [
  'Rumor has it that recently monsters have been wandering around Noria. I thought those creatures only existed as part of a legend... I wonder what could have brought them here.',
  'If you want to find out about the Devil Square, go meet Charon in Noria',
  'The Devil Square is a place where warriors prove their courage. Qualified warriors will be given the Devil invitation. Go to the Chaos Goblin in Noria with the Devil eye, Devil key, Jewel of Chaos and enough Zen.',
  'You have come too soon. You may be able to enter the Devil Square if you wait for your time and revisit later.',
  'Some say that the Devil eyes have been found on some monsters on the MU continent. Try to hunt as many monsters as possible to get Devil eyes. If you get one, go meet Charon in Noria.',
  'Many adventurers and warriors are crowding into Devil Square to prove their courage. Warrior, are you on your way to Devil Square?',
  'Do not you want to prove that you are the bravest of the bravest. Opportunity lies ahead of you. If you get the Devil Eye and Key, you will be one step closer to that opportunity.',
  'Thousands of years ago, the Devil Eye and Key had once existed in the MU Continent and disappeared. But now they can be seen all over the continent. Go and find them, and bring them to the Chaos Goblin in Noria',
  'Are you looking for the Devil Eye too? The Devil Eye can be found on most monsters on the MU continent. If God is with you, you will be able to find what you seek.',
  "Bring me the Devil Eye, Devil's Key, and a Jewel of Chaos. The Devil's invitation will be granted to you only after you bring me those three items.",
  "You've brought the Devil's treasure! May the Goddess of fortune be with you so you may find treasure that suits your needs.",
  'Finally, the gate of Devil Square has opened again. Charon the gate keeper will allow you to enter Devil Square',
  "Many adventurers are looking for the Devil's Eye and Key. You need both of them to receive a ticket that will get you into Devil Square.",
];
const FIRST_BLOOD_CASTLE_LINE = 20;
const BLOOD_CASTLE_NPC_LINES = [
  'I see that you have the Cloak of Invisibility. But you need to wait till the gate opens to enter the Blood Castle.',
  'Your courage is admirable but you need a Cloak of Invisibility to enter Blood Castle. You need more than just courage, warrior.',
  T.YourWillToHelpTheArchangel,
  "Ah! Great warrior. Thanks to your help, we have been able to protect the lands from Kundun's soldiers. As a token of our appreciation, I will share my experience with you.",
  "You're a warrior in training, I see. I will trust in your courage. Go ahead and bring down those evil creatures and bring me back my weapon.",
  "If you think you are brave enough a warrior, go to the Blood Castle. They say you can receive the Archangel's blessing.",
  "You've come to purchase a Cloak of Invisibility? Acquire the 'Scroll of Archangel' and a 'Blood Bone' and visit the Chaos Goblin. You'll be able to get one there.",
  "Have you come to repair something? I don't know where Blood Castle is. Why don't you go ask the Messenger of Archangel in Devias.",
  'Blood Castle is an extremely dangerous place. You might want to go with others as courageous as you if you want to help the Archangel.',
  'Are you going to the Blood Castle? Please help out the Archangel. Please!',
  "You'll find the 'Scroll of Archangel' and 'Blood Bone' by hunting monsters on the Continent of Mu.",
  "The Archangel has been protecting this land from the evil hands of Kundun since the very beginning. I'm sure he'd be glad to find aid.",
  'It seems as though the only one who can help the Archangel is you.',
  'The liquor that I sell here strengthens the warriors. Help yourselves to defeat the evil creatures in Blood Castle.',
  "I heard the creatures in Blood Castle are vicious. And you're going there? Quite a brave soul, you are.",
  'The Archangel is fighting the evil creatures of Kundun in the Blood Castle alone! If you are indeed as brave as you say you are, go help out the Archangel!',
  T.MessengerOfArchangel, T.CastleDLevelDD, 'Castle %d (over level %d)', 'Archangel', T.YouCanEnterSNow, T.AfterDMinutesYouMayEnterS,
  T.TheTimeToEnterSHasPassed, T.TheMaximumCapacityOfSHasBeenReachedTheMaxNumberAllowedIsD, T.TheLevelOfTheCloakOfInvisibilityIsIncorrect,
  "Even if you die or use the 'transport command' or the 'Town Portal Scroll' during the quest, do not disconnect until the quest is finished. If you disconnect, you will not be able to receive any reward for completing the quest.",
  "The weapon has been found. Thank you. You'd better get yourself out of here quickly.",
];

/** Network::ServerMessages::GetEventNpcLine: empty past the last line. */
export function eventNpcLine(number: number): string {
  if (number < FIRST_BLOOD_CASTLE_LINE) return DEVIL_SQUARE_NPC_LINES[number] ?? '';
  return BLOOD_CASTLE_NPC_LINES[number - FIRST_BLOOD_CASTLE_LINE] ?? '';
}

/** swprintf for the desktop's formats: %d, %lu, %s with an optional width (%2d, %10lu) or %.2d. */
export function format(pattern: string, ...args: (string | number)[]): string {
  let next = 0;
  return pattern.replace(/%(\.)?(\d+)?(l?[dus])/g, (_, dot: string | undefined, width: string | undefined) => {
    const value = args[next++];
    const text = value === undefined ? '' : String(value);
    if (!width) return text;
    return dot ? text.padStart(Number(width), '0') : text.padStart(Number(width), ' ');
  });
}

// ---------- the entry windows ----------

export interface EntryButton { label: string; enabled: boolean }
export interface EntryContent { title: string; lines: string[]; buttons: EntryButton[]; active: number }

/** The hero as the entry windows read it. */
export interface EventHero {
  level: number;
  /** Index into CLASSES (session.ts): 3 Magic Gladiator, 4 Dark Lord, 6 Rage Fighter. */
  classIndex: number;
  /** A third class (IsMasterLevel). */
  master: boolean;
}

// The level bands of CEnterBloodCastle and CEnterDevilSquare: the classes that start higher (Magic
// Gladiator, Dark Lord, Rage Fighter) have their own.
const BLOOD_CASTLE_LEVELS = [
  [[15, 80], [81, 130], [131, 180], [181, 230], [231, 280], [281, 330], [331, 400]],
  [[10, 60], [61, 110], [111, 160], [161, 210], [211, 260], [261, 310], [311, 400]],
];
const DEVIL_SQUARE_LEVELS = [
  [[15, 130], [131, 180], [181, 230], [231, 280], [281, 330], [331, 400]],
  [[15, 110], [111, 160], [161, 210], [211, 260], [261, 310], [311, 400]],
];

const isSpecialClass = (classIndex: number) => classIndex === 3 || classIndex === 4 || classIndex === 6;

// CheckLimitLV: the band of the hero's level (the first when none), the last button for a master.
function activeBand(bands: number[][], hero: EventHero): number {
  if (hero.master) return bands.length;
  const index = bands.findIndex(([low, high]) => hero.level >= low && hero.level <= high);
  return index < 0 ? 0 : index;
}

/** CEnterBloodCastle::OpenningProcess: eight castles, only the hero's enabled. */
export function bloodCastleEntry(hero: EventHero): EntryContent {
  const bands = BLOOD_CASTLE_LEVELS[isSpecialClass(hero.classIndex) ? 1 : 0];
  const active = activeBand(bands, hero);
  const buttons = bands.map(([low, high], i) => ({ label: format(T.CastleDLevelDD, i + 1, low, high), enabled: false }));
  buttons.push({ label: format(T.CastleNoDMasterLevel, 8), enabled: false });
  buttons[active].enabled = true;
  return { title: T.MessengerOfArchangel, lines: [T.YourWillToHelpTheArchangel], buttons, active };
}

/** CEnterDevilSquare::OpenningProcess: seven squares, only the hero's enabled. */
export function devilSquareEntry(hero: EventHero): EntryContent {
  const bands = DEVIL_SQUARE_LEVELS[isSpecialClass(hero.classIndex) ? 1 : 0];
  const active = activeBand(bands, hero);
  const buttons = bands.map(([low, high], i) => ({ label: format(T.TheDSquareDDLevel, i + 1, low, high), enabled: false }));
  buttons.push({ label: format(T.SquareNoDMasterLevel, 7), enabled: false });
  buttons[active].enabled = true;
  return { title: T.DevilSquare, lines: [...T.DevilSquareLines], buttons, active };
}

// ---------- the countdown ----------

// MATCH_TYPE (_enum.h): the countdown types the server's state (+1) stands for.
const Match = {
  NONE: 0, DEVIL_ENTER_START: 1, DEVIL_CLOSE: 3, CASTLE_ENTER_CLOSE: 4, CASTLE_END: 7, CASTLE_CONGRATULATIONS: 8,
  CHAOS_ENTER_START: 11, CHAOS_END: 14, END: 21,
} as const;
const COUNTDOWN_MS = 30_000;
const DEVIL_SQUARE_COUNTDOWNS = [T.YouWillEnterDevilSquareDSecondsFromNow, T.TheGateOfDevilSquareWillCloseDownInDSeconds, T.TheGateOfDevilSquareIsClosingDownDSecondsRemaining];
const BLOOD_CASTLE_COUNTDOWNS = [T.SClosingInDSeconds, T.SInfiltrationInDSeconds, T.SEventEndsInDSeconds, T.SEventShutsDownInDSeconds];
const CHAOS_CASTLE_COUNTDOWNS = [T.SClosingInDSeconds, T.SPenetrationInDSeconds, T.SEventEndsInDSeconds, T.SEventShutsDownInDSeconds];

/** CSBaseMatch::CountdownText: the line of a countdown type with the seconds left. */
export function countdownText(type: number, secondsLeft: number): string {
  if (type >= Match.DEVIL_ENTER_START && type <= Match.DEVIL_CLOSE) return format(DEVIL_SQUARE_COUNTDOWNS[type - Match.DEVIL_ENTER_START], secondsLeft);
  if (type >= Match.CASTLE_ENTER_CLOSE && type <= Match.CASTLE_END) return format(BLOOD_CASTLE_COUNTDOWNS[type - Match.CASTLE_ENTER_CLOSE], T.BloodCastle, secondsLeft);
  if (type === Match.CASTLE_CONGRATULATIONS) return T.Congratulations;
  if (type >= Match.CHAOS_ENTER_START && type <= Match.CHAOS_END) return format(CHAOS_CASTLE_COUNTDOWNS[type - Match.CHAOS_ENTER_START], T.ChaosCastle, secondsLeft);
  return '';
}

// ---------- the results ----------

/** One text of a result box, in its font and colour (MatchResultCell). */
export interface ResultCell { text: string; bold: boolean; color: string }
/** One line: its role places it ("message", "reward", "header", "row", "my-info", "my-row"). */
export interface ResultLine { role: string; cells: ResultCell[] }
export interface MatchResult {
  /** The message box's kind (the theme places it by that). */
  kind: 'blood-castle-result' | 'chaos-castle-result' | 'devil-square-rank';
  lines: ResultLine[];
}
export interface RankEntry { name: string; score: number; experience: number; money: number }

const cell = (text: string, color: string, bold = false): ResultCell => ({ text, bold, color });
const line = (role: string, ...cells: ResultCell[]): ResultLine => ({ role, cells });

/** CNewBloodCastleSystem::CollectMatchResult. */
export function bloodCastleResult(success: boolean, entry: RankEntry): MatchResult {
  const lines = [line('message', cell(success ? T.BloodCastleQuestCompleted : T.BloodCastleQuestFailed, 'rgb(128, 255, 128)'))];
  lines.push(line('reward', cell(format(T.RewardedExpD, entry.experience), 'rgb(210, 255, 210)', true)));
  if (success) lines.push(line('reward', cell(format(T.RewardedZenD, entry.money), 'rgb(255, 210, 210)', true)));
  lines.push(line('reward', cell(format(T.BloodCastlePointD, entry.score), 'rgb(210, 210, 255)', true)));
  return { kind: 'blood-castle-result', lines };
}

/** CNewChaosCastleSystem::CollectMatchResult: the score is the monsters killed, the money the players. */
export function chaosCastleResult(success: boolean, entry: RankEntry): MatchResult {
  const green = 'rgb(128, 255, 128)', reward = 'rgb(210, 255, 210)';
  const lines = success
    ? [line('message', cell(T.TheSpiritOfTheGuardHasBeenPurified, green)), line('message', cell(T.QuestCompletedSuccessfully, green))]
    : [line('message', cell(T.QuestFailedUnfortunately, green)), line('message', cell(T.TryAgainNextTime, green))];
  lines.push(line('reward', cell(format(T.RewardedExpD, entry.experience), reward, true)));
  lines.push(line('reward', cell(format(T.MonsterKillCountD, entry.score), reward, true)));
  lines.push(line('reward', cell(format(T.PlayersKillCountD, entry.money), reward, true)));
  return { kind: 'chaos-castle-result', lines };
}

/** CSDevilSquareMatch::CollectMatchResult: the ranking, the hero's own row in orange and again under the table. */
export function devilSquareResult(heroName: string, myRank: number, entries: RankEntry[]): MatchResult {
  const white = 'rgb(255, 255, 255)', green = 'rgb(0, 255, 0)', mine = 'rgb(200, 120, 0)', others = 'rgb(255, 255, 0)';
  const lines = [line('message', cell(T.Congratulations, white)), line('message', cell(format(T.SYourBraveryIsProvenInDevilSquare, heroName), white))];
  lines.push(line('header', cell(T.Rank, green), cell(T.Point, green), cell(T.EXP, green), cell(T.Reward, green)));
  const row = (role: string, rank: number, entry: RankEntry, color: string) => line(role,
    cell(format('%2d', rank), color), cell(entry.name, color), cell(format('%10lu', entry.score), color),
    cell(format('%6lu', entry.experience), color), cell(format('%6lu', entry.money), color));
  entries.forEach((entry, i) => lines.push(row('row', i + 1, entry, i === myRank - 1 ? mine : others)));
  if (myRank > 0 && myRank <= entries.length) {
    lines.push(line('my-info', cell(T.MyInfo, mine)));
    lines.push(row('my-row', myRank, entries[myRank - 1], mine));
  }
  return { kind: 'devil-square-rank', lines };
}

// ---------- the event state ----------

/** The time window's lines (CBloodCastle / CChaosCastleTime::SyncView). */
export interface TimerView {
  kind: 'bloodCastle' | 'chaosCastle';
  kills: string;
  time: string;
  imminent: boolean;
}

export interface EventActions {
  /** The server map the hero is on. */
  mapNumber: () => number | null;
  /** The walkability flags of the loaded map (TerrainFlag per cell). */
  flags: () => Uint16Array | null;
  /** The hero's name (the Devil Square ranking's line). */
  heroName: string;
  hero: () => EventHero;
  /** Whether the hero stands in a safe zone. */
  safeZone: () => boolean;
  /** An OK message box (CreateOkMessageBox). */
  ok: (text: string) => void;
  /** A question with OK and Cancel; never blocks a packet handler. */
  confirm: (lines: string[]) => Promise<boolean>;
  /** A system line (the desktop's system log). */
  message: (text: string) => void;
  /** Opens an entry window (C3 30 4 / 6). */
  openEntry: (kind: 'bloodCastle' | 'devilSquare') => void;
  /** Hides the entry window and the other windows (HideAll / Hide on an entry result). */
  closeWindows: () => void;
  /** Shows a result box. */
  showResult: (result: MatchResult) => void;
  play: (id: string, loop?: boolean) => void;
  stop: (id: string) => void;
}

const TERRAIN_SIZE = 256;
const NO_GROUND = 0x08;
const MAX_KILL_MONSTER = 65535;
const MAX_BLOOD_CASTLE_MEN = 10, MAX_CHAOS_CASTLE_MEN = 70;

const u16 = (bytes: Uint8Array, at: number) => bytes[at] | (bytes[at + 1] << 8);
const u32 = (bytes: Uint8Array, at: number) => (bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24)) >>> 0;
const text = (bytes: Uint8Array, at: number, length: number) => {
  const raw = bytes.subarray(at, at + length);
  const end = raw.indexOf(0);
  return new TextDecoder().decode(end < 0 ? raw : raw.subarray(0, end));
};

/** The match of the event map the hero is on (CSBaseMatch and its event's subclass). */
interface MatchState {
  kind: EventKind;
  /** The state + 1 the server last sent (m_byMatchType), 0 for none. */
  type: number;
  timeMax: number; time: number; maxKill: number; kill: number;
  countdownType: number; countdownStart: number;
  /** Chaos Castle: the ring of walls about to fall (m_byCurrCastleLevel). */
  castleLevel: number;
}

const newMatch = (kind: EventKind): MatchState => ({
  kind, type: 0, timeMax: -1, time: -1, maxKill: -1, kill: -1, countdownType: Match.NONE, countdownStart: 0, castleLevel: 255,
});

// CNewChaosCastleSystem: the walls of each ring (x, y, width, height), falling when the kills get low.
const CHAOS_CASTLE_WALLS: Record<number, { level: number; next: number; areas: number[][] }> = {
  8: { level: 0, next: 1, areas: [[23, 75, 22, 2], [43, 77, 2, 32], [23, 107, 20, 2], [23, 77, 2, 30]] },
  9: { level: 3, next: 4, areas: [[25, 77, 18, 2], [41, 79, 2, 28], [25, 105, 16, 2], [25, 79, 2, 26]] },
  10: { level: 6, next: 7, areas: [[27, 79, 14, 2], [39, 81, 2, 24], [27, 103, 12, 2], [27, 81, 2, 22]] },
};

export class EventMatches {
  match: MatchState | null = null;
  /** The last result shown, for tests and the console. */
  lastResult: MatchResult | null = null;
  /** The bag slot of the item used to ask for an opening state (SetStandbyItemKey), with its level. */
  private standby: { slot: number; level: number } | null = null;
  private mapOfMatch: number | null = null;

  constructor(private readonly connection: Connection, private readonly actions: EventActions) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  /** matchEvent::CreateEventMatch on each map change: a fresh match on an event map, none elsewhere. */
  private syncMap(): void {
    const map = this.actions.mapNumber();
    if (map === this.mapOfMatch) return;
    this.mapOfMatch = map;
    const kind = eventOfMap(map);
    if (this.match?.kind === 'bloodCastle') this.actions.stop('SOUND_BLOODCASTLE');
    if (this.match?.kind === 'chaosCastle') { this.actions.stop('SOUND_CHAOSCASTLE'); this.actions.stop('SOUND_CHAOS_ENVIR'); }
    this.match = kind ? newMatch(kind) : null;
  }

  handle(bytes: Uint8Array): void {
    this.syncMap();
    const c1 = bytes[0] === 0xc1, c3 = bytes[0] === 0xc3;
    const code = bytes[2];
    if ((c1 || c3) && code === 0x30 && bytes.length >= 4) {
      if (bytes[3] === 4) this.actions.openEntry('devilSquare');
      else if (bytes[3] === 6) this.actions.openEntry('bloodCastle');
      return;
    }
    if (!c1) return;
    switch (code) {
      case 0x90: if (bytes.length >= 4) this.devilSquareEnterResult(bytes[3]); break;
      case 0x9a: if (bytes.length >= 4) this.bloodCastleEnterResult(bytes[3]); break;
      case 0xaf: if (bytes[3] === 0x01 && bytes.length >= 5) this.chaosCastleEnterResult(bytes[4]); break;
      case 0x91: if (bytes.length >= 7) this.openingState(bytes[3], bytes[4], bytes[5], bytes[6]); break;
      case 0x92: if (bytes.length >= 4) this.startCountdown((bytes[3] + 1) & 0xff); break;
      case 0x93: if (bytes.length >= 5) this.score(bytes); break;
      case 0x9b: if (bytes.length >= 13) this.gameState(bytes); break;
      case 0x46: if (bytes.length >= 7) this.terrain(bytes); break;
      case 0xf3: if (bytes[3] === 0x40 && bytes.length >= 7 && bytes[4] === 1) this.actions.ok(eventNpcLine(bytes[5])); break;
      default: break;
    }
  }

  // ReceiveMoveToDevilSquareResult
  private devilSquareEnterResult(result: number): void {
    this.actions.closeWindows();
    const texts: Record<number, string> = {
      1: T.BringTheDevilSInvitationToEnter, 2: T.YouVeComeTooLateToEnterTheDevilSquare, 3: T.YouReUnderestimatingYourselfChooseAnotherSquare,
      4: T.IfYouWishToStayAliveChooseAnotherSquare, 5: T.DevilSquareIsFull, 6: format(T.KillersAreRestrictedToEnterS, T.DevilSquare),
    };
    if (texts[result]) this.actions.ok(texts[result]);
  }

  // ReceiveMoveToEventMatchResult
  private bloodCastleEnterResult(result: number): void {
    this.actions.closeWindows();
    const texts: Record<number, string> = {
      1: T.TheLevelOfTheCloakOfInvisibilityIsIncorrect, 2: format(T.TheTimeToEnterSHasPassed, T.BloodCastle),
      3: T.YouReUnderestimatingYourselfChooseAnotherSquare, 4: T.IfYouWishToStayAliveChooseAnotherSquare,
      5: format(T.TheMaximumCapacityOfSHasBeenReachedTheMaxNumberAllowedIsD, T.BloodCastle, MAX_BLOOD_CASTLE_MEN),
      6: format(T.YouAreNotAllowedToEnterMoreThanDTimesInOneDay, 6), 7: format(T.KillersAreRestrictedToEnterS, T.BloodCastle),
      8: format(T.TheTimeToEnterSHasPassed, T.ChaosCastle),
      9: format(T.TheMaximumCapacityOfSHasBeenReachedTheMaxNumberAllowedIsD, T.ChaosCastle, MAX_CHAOS_CASTLE_MEN),
    };
    if (texts[result]) this.actions.ok(texts[result]);
  }

  // ReceiveMoveToEventMatchResult2
  private chaosCastleEnterResult(result: number): void {
    this.actions.closeWindows();
    const texts: Record<number, string> = {
      1: T.TheLevelOfTheCloakOfInvisibilityIsIncorrect, 2: format(T.TheTimeToEnterSHasPassed, T.ChaosCastle),
      3: T.YouReUnderestimatingYourselfChooseAnotherSquare, 4: T.IfYouWishToStayAliveChooseAnotherSquare,
      5: format(T.TheMaximumCapacityOfSHasBeenReachedTheMaxNumberAllowedIsD, T.ChaosCastle, MAX_CHAOS_CASTLE_MEN),
      6: format(T.YouAreNotAllowedToEnterMoreThanDTimesInOneDay, 6), 7: T.YouAreShortOfZen, 8: format(T.KillersAreRestrictedToEnterS, T.ChaosCastle),
    };
    if (texts[result]) this.actions.ok(texts[result]);
  }

  // ReceiveEventZoneOpenTime: the event's opening state after using a ticket; for Chaos Castle a
  // question whose OK enters with the armour used.
  private openingState(type: number, high: number, middle: number, low: number): void {
    if (type === 1) {
      this.actions.ok(high === 0 ? T.YouCanEnterDevilSquareNow : format(T.DevilSquareWillOpenInDMinutes, high));
    } else if (type === 2) {
      this.actions.ok(high === 0 ? format(T.YouCanEnterSNow, T.BloodCastle) : format(T.AfterDMinutesYouMayEnterS, high, T.BloodCastle));
    } else if (type === 4) {
      const minutes = low | (high << 8);
      const lines = minutes === 0
        ? [format(T.YouCanEnterSNow, T.ChaosCastle), format(T.InSCurrentlyDDEntered, T.ChaosCastle, middle, 100)]
        : [format(T.WhenD, Math.floor(minutes / 60)) + format(T.AfterDMinutesYouMayEnterS, minutes % 60, T.ChaosCastle)];
      void this.actions.confirm(lines).then((accepted) => {
        const standby = this.standby;
        if (accepted && standby) this.connection.send('ClientToServer', 'ChaosCastleEnterRequest', { CastleLevel: standby.level, TicketItemInventoryIndex: standby.slot });
      });
    } else if (type === 5) {
      this.actions.ok(high === 0 ? format(T.YouCanEnterSNow, 'Illusion Temple') : format(T.AfterDMinutesYouMayEnterS, high, 'Illusion Temple'));
    }
  }

  // CSBaseMatch::StartMatchCountDown: only on an event map (there is no match elsewhere).
  startCountdown(type: number, now = performance.now()): void {
    if (!this.match) return;
    this.match.countdownType = type;
    this.match.countdownStart = now;
  }

  /** CSBaseMatch::CountdownText: the countdown line for 30 seconds after the state message. */
  countdown(now = performance.now()): string {
    this.syncMap();
    const match = this.match;
    if (!match || match.countdownType <= Match.NONE || match.countdownType >= Match.END) return '';
    const elapsed = Math.floor((now - match.countdownStart) / 1000);
    if (elapsed >= COUNTDOWN_MS / 1000) { match.countdownType = Match.NONE; return ''; }
    return countdownText(match.countdownType, COUNTDOWN_MS / 1000 - elapsed);
  }

  // ReceiveDevilSquareRank → matchEvent::SetMatchResult: the match of the map decides (count 255 Blood
  // Castle, 254 Chaos Castle, else the Devil Square ranking).
  private score(bytes: Uint8Array): void {
    const match = this.match;
    if (!match) return;
    const count = bytes[4];
    const entry = (at: number): RankEntry => ({ name: text(bytes, at, 10), score: u32(bytes, at + 12), experience: u32(bytes, at + 16), money: u32(bytes, at + 20) });
    let result: MatchResult | null = null;
    if (match.kind === 'bloodCastle' && count === 255 && bytes.length >= 29) result = bloodCastleResult(bytes[3] !== 0, entry(5));
    else if (match.kind === 'chaosCastle' && count === 254 && bytes.length >= 29) result = chaosCastleResult(bytes[3] !== 0, entry(5));
    else if (match.kind === 'devilSquare' && count < 200) {
      const entries: RankEntry[] = [];
      for (let i = 0; i < count && 5 + (i + 1) * 24 <= bytes.length; i++) entries.push(entry(5 + i * 24));
      result = devilSquareResult(this.actions.heroName, bytes[3], entries);
    }
    if (!result) return;
    this.lastResult = result;
    this.actions.showResult(result);
  }

  // SetMatchGameCommand of the map's match.
  private gameState(bytes: Uint8Array): void {
    const match = this.match;
    if (!match) return;
    const state = bytes[3], remain = u16(bytes, 4), maxKill = u16(bytes, 6), kill = u16(bytes, 8);
    const setInfo = () => Object.assign(match, { type: state + 1, timeMax: 15 * 60, time: remain, maxKill, kill });
    const clear = () => Object.assign(match, { type: 0, timeMax: -1, time: -1, maxKill: -1, kill: -1 });
    if (match.kind === 'bloodCastle') {
      if (state === 0) { this.actions.play('SOUND_BLOODCASTLE', true); setInfo(); }
      else if (state === 1 || state === 4) setInfo();
      else if (state === 2) { clear(); this.actions.stop('SOUND_BLOODCASTLE'); }
    } else if (match.kind === 'chaosCastle') {
      if (state === 5) {
        match.castleLevel = 255;
        this.actions.stop('SOUND_CHAOS_ENVIR');
        this.actions.play('SOUND_CHAOSCASTLE', true);
      } else if (state === 6) setInfo();
      else if (state === 7) {
        clear();
        this.actions.stop('SOUND_CHAOSCASTLE');
        this.actions.play('SOUND_CHAOS_ENVIR', true);
        this.actions.play('SOUND_CHAOS_END');
      } else if (CHAOS_CASTLE_WALLS[state]) {
        const walls = CHAOS_CASTLE_WALLS[state];
        if (match.castleLevel === walls.level) {
          match.castleLevel = walls.next;
          for (const [x, y, width, height] of walls.areas) this.changeTerrain(x, y, width, height, NO_GROUND, true);
          this.actions.play('SOUND_CHAOS_FALLING_STONE');
        }
      }
    }
  }

  // ReceiveSetAttribute: areas (type 0) or single cells (type 1) gain or lose an attribute.
  private terrain(bytes: Uint8Array): void {
    const type = bytes[3], attribute = bytes[4], remove = bytes[5] !== 0, count = bytes[6];
    if (type === 0) {
      for (let k = 0; k < count && 7 + k * 4 + 3 < bytes.length; k++) {
        const at = 7 + k * 4;
        const x = bytes[at], y = bytes[at + 1];
        this.changeTerrain(x, y, bytes[at + 2] - x + 1, bytes[at + 3] - y + 1, attribute, !remove);
      }
    } else {
      for (let i = 0; i < count && 7 + i * 2 + 1 < bytes.length; i++) this.changeTerrain(bytes[7 + i * 2], bytes[8 + i * 2], 1, 1, attribute, !remove);
    }
  }

  /** AddTerrainAttributeRange / SubTerrainAttribute on the loaded map's flags. */
  changeTerrain(x: number, y: number, width: number, height: number, attribute: number, add: boolean): void {
    const flags = this.actions.flags();
    if (!flags) return;
    for (let j = 0; j < height; j++) {
      for (let i = 0; i < width; i++) {
        const cx = x + i, cy = y + j;
        if (cx < 0 || cy < 0 || cx >= TERRAIN_SIZE || cy >= TERRAIN_SIZE) continue;
        const index = cy * TERRAIN_SIZE + cx;
        flags[index] = add ? flags[index] | attribute : flags[index] & ~attribute;
      }
    }
  }

  /** RenderMatchTimes + the time window's SyncView: null while it is hidden. */
  timer(now = performance.now()): TimerView | null {
    this.syncMap();
    const match = this.match;
    if (!match || match.time <= 0) return null;
    const minutes = Math.floor(match.time / 60);
    const time = ` ${format('%.2d:%.2d:%.2d', minutes, match.time % 60, Math.floor(now) % 60)}`;
    if (match.kind === 'bloodCastle' && [1, 2, 5].includes(match.type)) {
      const kills = match.maxKill === MAX_KILL_MONSTER ? '' : format(match.type === 5 ? T.MagicSkeletonDD : T.MonsterDD, match.kill, match.maxKill);
      return { kind: 'bloodCastle', kills, time, imminent: minutes < 5 };
    }
    if (match.kind === 'chaosCastle' && [6, 7, 8].includes(match.type)) {
      // The ring of walls that falls next, by the kills left.
      if (match.kill <= 46 && match.kill > 40) match.castleLevel = 0;
      else if (match.kill <= 36 && match.kill > 30) match.castleLevel = 3;
      else if (match.kill <= 26 && match.kill > 20) match.castleLevel = 6;
      const kills = match.maxKill === MAX_KILL_MONSTER ? '' : format(T.CharacterDD, match.kill, match.maxKill);
      return { kind: 'chaosCastle', kills, time, imminent: minutes < 5 };
    }
    return null;
  }

  // ---------- the tickets ----------

  /**
   * CInventoryActionController's event items, right-clicked in the bag: true when the item is one
   * (the Armor of Guardsman, the Devil's Invitation, the Invisibility Cloak).
   */
  useTicket(slot: number, item: Item): boolean {
    const send = (eventType: number, level: number) => this.connection.send('ClientToServer', 'MiniGameOpeningStateRequest', { EventType: eventType, EventLevel: level });
    if (item.group === 13 && item.number === 29) {
      // ITEM_ARMOR_OF_GUARDSMAN
      if (this.actions.mapNumber() === UNITED_MARKET_PLACE) { this.actions.ok(`${T.YouCannotEnterChaosCastle} ${T.FromTheMarketInLorencia}`); return true; }
      if (!this.actions.safeZone()) { this.actions.ok(T.YouCanOnlyUseThisInASafeZone); return true; }
      send(4, item.level);
      this.standby = { slot, level: item.level };
      return true;
    }
    if (item.group === 14 && item.number === 19) {
      // ITEM_DEVILS_INVITATION
      send(1, item.level);
      return true;
    }
    if (item.group === 13 && item.number === 18) {
      // ITEM_INVISIBILITY_CLOAK
      if (item.level === 0) this.actions.message(T.IncorrectItem);
      else send(2, item.level - 1);
      return true;
    }
    return false;
  }

  /** The entry window's enabled button: BloodCastleEnterRequest (castle 1-8) / DevilSquareEnterRequest (square 0-6), the ticket found by the server. */
  enter(kind: 'bloodCastle' | 'devilSquare', active: number): void {
    if (kind === 'bloodCastle') this.connection.send('ClientToServer', 'BloodCastleEnterRequest', { CastleLevel: active + 1, TicketItemInventoryIndex: 0xff });
    else this.connection.send('ClientToServer', 'DevilSquareEnterRequest', { SquareLevel: active, TicketItemInventoryIndex: 0xff });
  }
}

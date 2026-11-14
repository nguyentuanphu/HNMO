// The Crywolf Fortress event as the desktop keeps it (World/GameMaps/GMCrywolf1st: CheckCryWolf1stMVP,
// AdvanceNoticeTexts, Set_BossMonster, Check_AltarState; UI/Events/CryWolf: the HUD's SyncResult and
// SyncHud; ZzzInterface's altar talk; the C1 BD handlers of WSclient.cpp).
//
//   BD 00  the occupation (peace, occupied, war) and the event state: the HUD shows from Ready to the end
//          of the cycle; another occupation loads TerrainLight1/2.jpg and EncTerrain351/352.att
//   BD 02  the statue's shield and the five altars (contract use << 4 | contracts left)
//   BD 03  an altar contract's result: refused (level / try again) or the guardian question
//   BD 04  the time left (the clock counts the minute down in thirds)
//   BD 05  Balgass's health and the dark elves left
//   BD 07 / 08  the hero's rank and experience, the five heroes: the result after the end
//   BD 0C  a ballista's shot (an effect only; the web game has none yet)
// An elf clicking an altar during Ready or Start asks to contract it (CrywolfContractRequest) instead
// of talking to it. No DOM here: the HUD is ui/fieldEventWindows.ts.

import { format } from './textFormat.js';
import type { Connection } from '../net/connection.js';

export const CRYWOLF_MAP = 34;
export const Occupation = { PEACE: 0, OCCUPIED: 1, WAR: 2 } as const;
export const CrywolfState = { NONE: 0, NOTIFY_1: 1, NOTIFY_2: 2, READY: 3, START: 4, END: 5, END_CYCLE: 6 } as const;
const ALTAR_CONTRACTED = 1;
const ALTARS = [205, 209] as const; // Wolf Altar 1-5

export const CrywolfText = {
  Rank: 'Rank', Character: 'Character', Class: 'Class', Score: 'Score',
  MonsterStrengthDecreased10: 'Monster strength decreased 10%%.',
  _5IncreaseInCastleAndArenaInvitationCombineRate: '5%% increase in castle and arena invitation combine rate.',
  AllNPCsInCrywolfHaveBeenDeleted: 'All NPCs in Crywolf have been deleted.',
  DarkElfD12: 'Dark Elf (%d/12)', Balgass: 'Balgass',
  DisqualifiedForTheContractRequirement: 'Disqualified for the contract requirement.',
  OnlyLevelAbove350IsAllowedToMakeAContract: 'Only level above 350 is allowed to make a contract.',
  PleaseTryAgainInAWhile: 'Please try again in a while.',
  YouHaveBeenRegisteredToBeAGuardianToProtectTheWolf: 'You have been registered to be a guardian to protect the wolf.',
  YourRoleAsAGuardianWillBeCancelledWhenYouWarp: 'Your role as a guardian will be cancelled when you warp.',
  ContractCanTBeMadeWhenYouAreOnAMount: "Contract can't be made when you are on a mount.",
  ContractCanBeMadeForDTimes: 'Contract can be made for %d times.',
  WouldYouLikeToProceedWithTheContract: 'Would you like to proceed with the contract?',
  WeNeedAGuardianToProtectTheWolf: 'We need a guardian to protect the wolf.',
  FurtherContractCanTBeDoneSinceTheAltarHasBeenDestroyed: "Further contract can't be done since the altar has been destroyed.",
  ContractIsOngoingThereforeDualCompactIsNotPossible: 'Contract is ongoing therefore dual compact is not possible.',
} as const;

// The ready-state notice board: four pages of four lines from the page before the first.
const NOTICE_LINES = [
  'You have been registered to be a guardian to protect the wolf.', 'Your role as a guardian will be cancelled when you warp.',
  'You have been disqualified to be a guardian.', "Contract can't be made when you are on a mount.",
  '< Mission Point : 1. Defend the Wolf statue >', 'Make a contract with the altar to protect the wolf statue!',
  'Only the Elf can be a guardian to give power to the Wolf statue!', 'You have to protect elves when the contract is being made!',
  '< Mission Point : 2. Defeat Balgass >', 'Fortress of Crywolf will not be safe unless Balgass is defeated!',
  'Balgass can only show up for 5 minutes around the Fortress of Crywolf!', 'Defeat Balgass within the given time!',
  '<Success reparation >', format('10%% monster strength decrease (maintain during the event)'),
  format('5%% decrease in castle and arena invitation combine rate'), 'Above reparation is valid till the next Crywolf battle.',
  '<Failure penalty>', 'Delete all the NPC within Crywolf', '', 'Above penalty is valid till the next Crywolf battle.',
];

/** NoticeText: line 0-3 of page -1 (before the first turn) to 3. */
export function noticeText(page: number, line: number): string {
  if (page < -1 || page >= 4 || line < 0 || line >= 4) return '';
  return NOTICE_LINES[(page + 1) * 4 + line] ?? '';
}

// GetCharacterClassText by the server's class number (CharacterClassNumber).
const CLASS_NAMES: string[][] = [
  ['Dark Wizard', 'Soul Master', 'Grand Master'], ['Dark Knight', 'Blade Knight', 'Blade Master'], ['Elf', 'Muse Elf', 'High Elf'],
  ['Magic Gladiator', 'Magic Gladiator', 'Dual Master'], ['Dark Lord', 'Dark Lord', 'Lord Emperor'],
  ['Summoner', 'Bloody Summoner', 'Dimension Master'], ['Rage Fighter', 'Rage Fighter', 'Fist Master'],
];
export function classText(serverClass: number): string {
  const base = Math.floor(serverClass / 4);
  const step = serverClass % 4 === 0 ? 0 : serverClass % 4 === 2 ? 1 : 2;
  return CLASS_NAMES[base]?.[step] ?? '';
}

/** One image of the HUD (an altar, an experience digit): CryWolfImageEntry. */
export interface CrywolfImage { shown: boolean; src: string; rect: string }
export type Digits = string[] & { size: number };

/** CryWolfRmlModel without the text sizes: what the HUD shows this frame. */
export interface CrywolfView {
  result_visible: boolean; banner_left: number; banner_src: string; banner_opacity: number; rank_label_left: number;
  rank_details_visible: boolean; rank_letter_src: string; exp_digits: CrywolfImage[];
  hud_visible: boolean; altars: CrywolfImage[]; dark_elf_icon_src: string; dark_elf_text: string;
  balgass_visible: boolean; balgass_text: string; balgass_bar_width: number; balgass_bar_rect: string;
  minute_pad: boolean; minute_digits: Digits; second_pad: boolean; second_digits: Digits; timer_urgent: boolean;
  statue_bar_left: number; statue_bar_width: number; statue_bar_rect: string;
  notices: { text: string; heading: boolean }[];
}

const image = (file: string) => `../../../${file}`;
const rect = (x: number, y: number, w: number, h: number) => `${x} ${y} ${w} ${h}`;
function digitCells(value: number): Digits {
  const cells = [...String(Math.max(0, value))].map((c) => rect(Number(c) * 16, 0, 16, 16));
  return Object.assign(cells, { size: cells.length });
}

export interface CrywolfActions {
  mapNumber: () => number | null;
  hero: () => { classIndex: number; level: number; mounted: boolean };
  ok: (lines: string[]) => Promise<boolean>;
  ask: (lines: string[]) => Promise<boolean>;
  /** The hero's contract pose (SetAction PLAYER_HEALING_FEMALE1 and SendRequestAction AT_HEALING1). */
  contractPose: () => void;
  /** Loads the terrain light and attributes of the occupation (OpenTerrainLight / OpenTerrainAttribute). */
  applyOccupation: (occupation: number) => void;
  play: (id: string) => void;
}

const FRAME_MS = 40; // the HUD's animations count frames (the original's 25 a second)
const TOTAL_DELAY = 400;

export class Crywolf {
  occupation = 0;
  state = 0;
  statueHp = 0;
  altars = [2, 2, 2, 2, 2];
  /** CCryWolf shown (Show(INTERFACE_CRYWOLF) from Ready on); hidden again on another map. */
  shown = false;
  // the result
  private sucOrFail = -1;
  private viewSucOrFail = -1;
  private delay = 1;
  private addNum = 10;
  private delayAddInter = 390;
  rank = 0;
  experience = 0;
  readonly heroes: ({ name: string; serverClass: number; score: number } | null)[] = [null, null, null, null, null];
  // the battle panel
  private viewBalgass = false;
  private decoInsert = 0;
  darkElves = 0;
  balgassHp = 100;
  private nextNotice = -1;
  private noticeTime = 0;
  // the clock (CCryWolf::SetTime)
  private minute = 0;
  private second = 0;
  private syncTime = 0;
  private countTime = 0;
  private timeStarted = false;
  private mapOfState: number | null = null;
  private frameTime = 0;
  private view: CrywolfView = Crywolf.emptyView();
  /** The result's ranking box (GenericConfirmDialog), handed to the HUD when the banner leaves. */
  onResultBox: ((lines: string[]) => void) | null = null;

  constructor(private readonly connection: Connection, private readonly actions: CrywolfActions) {
    connection.onPacket((bytes) => this.handle(bytes));
  }

  static emptyView(): CrywolfView {
    return {
      result_visible: false, banner_left: 0, banner_src: '', banner_opacity: 1, rank_label_left: 0, rank_details_visible: false, rank_letter_src: '',
      exp_digits: [], hud_visible: false, altars: [], dark_elf_icon_src: '', dark_elf_text: '', balgass_visible: false, balgass_text: '',
      balgass_bar_width: 0, balgass_bar_rect: rect(0, 0, 0, 8), minute_pad: true, minute_digits: digitCells(0), second_pad: true,
      second_digits: digitCells(0), timer_urgent: false, statue_bar_left: 0, statue_bar_width: 0, statue_bar_rect: rect(0, 0, 0, 29), notices: [],
    };
  }

  get onMap(): boolean { return this.actions.mapNumber() === CRYWOLF_MAP; }

  /** Get_State_Only_Elf: the ready and start states. */
  get battle(): boolean { return this.state === CrywolfState.START || this.state === CrywolfState.READY; }

  /** The music rule's state (M34CryWolf1st::ChangeBackGroundMusic). */
  get musicState(): 'none' | 'notify2' | 'ready' | 'start' | 'end' {
    switch (this.state) {
      case CrywolfState.NOTIFY_2: return 'notify2';
      case CrywolfState.READY: return 'ready';
      case CrywolfState.START: return 'start';
      case CrywolfState.END: return 'end';
      default: return 'none';
    }
  }

  /** CryWolfMVPInit on entering a map, and the info request on entering Crywolf. */
  private syncMap(): void {
    const map = this.actions.mapNumber();
    if (map === this.mapOfState) return;
    this.mapOfState = map;
    Object.assign(this, { occupation: 0, state: 0, statueHp: 0, darkElves: 0, balgassHp: 100, viewBalgass: false, decoInsert: 0, nextNotice: -1, delay: 1, addNum: 10, shown: false });
    this.minute = 0;
    this.second = 0;
    this.timeStarted = false;
    this.countTime = 0;
    if (map === CRYWOLF_MAP) this.connection.send('ClientToServer', 'CrywolfInfoRequest', {});
  }

  handle(bytes: Uint8Array): void {
    this.syncMap();
    if (bytes[0] !== 0xc1 || bytes[2] !== 0xbd || bytes.length < 5) return;
    const u32 = (at: number) => (bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24)) >>> 0;
    switch (bytes[3]) {
      case 0x00: if (bytes.length >= 6) this.info(bytes[4], bytes[5]); break;
      case 0x02: if (bytes.length >= 13) { this.statueHp = u32(4); this.altars = [bytes[8], bytes[9], bytes[10], bytes[11], bytes[12]]; } break;
      case 0x03: if (bytes.length >= 8) this.contractResult(bytes[4], bytes[5], (bytes[6] << 8) | bytes[7]); break;
      case 0x04: if (bytes.length >= 6) this.setTime(bytes[5]); break;
      case 0x05: if (bytes.length >= 9) this.bossInfo(u32(4), bytes[8]); break;
      case 0x07: if (bytes.length >= 12) { this.rank = bytes[4]; this.experience = u32(8) | 0; } break;
      case 0x08: this.heroList(bytes); break;
      default: break;
    }
  }

  // CheckCryWolf1stMVP
  private info(occupation: number, state: number): void {
    if (this.occupation === occupation && this.state === state) return;
    const previous = this.state;
    this.state = state;
    if (state >= CrywolfState.READY && state < CrywolfState.END_CYCLE) this.shown = true;
    if (state === CrywolfState.START) this.syncTime = performance.now();
    if (state !== CrywolfState.START) this.viewBalgass = false;
    if (state === CrywolfState.END) {
      this.heroes.fill(null);
      if (occupation !== Occupation.WAR) {
        this.sucOrFail = 1;
        if (occupation === Occupation.PEACE) { this.addNum = 11; this.viewSucOrFail = 1; } else this.viewSucOrFail = -1;
      }
      // ChangeBackGroundMusic's end: the success or failure sound.
      if (previous !== CrywolfState.END && this.onMap) this.actions.play(this.addNum === 10 ? 'SOUND_CRY1ST_FAILED' : 'SOUND_CRY1ST_SUCCESS');
    }
    if (this.occupation === occupation) return;
    this.occupation = occupation;
    this.actions.applyOccupation(occupation);
  }

  // ReceiveCrywolfAltarContract
  private contractResult(result: number, altarState: number, key: number): void {
    const hero = this.actions.hero();
    if (result === 0) {
      void this.actions.ok(hero.level < 260
        ? [CrywolfText.DisqualifiedForTheContractRequirement, CrywolfText.OnlyLevelAbove350IsAllowedToMakeAContract]
        : [CrywolfText.PleaseTryAgainInAWhile]);
    } else if (result === 1) {
      // The original wired this box's OK to the contract request again.
      void this.actions.ok([CrywolfText.YouHaveBeenRegisteredToBeAGuardianToProtectTheWolf, CrywolfText.YourRoleAsAGuardianWillBeCancelledWhenYouWarp]).then((accepted) => {
        if (!accepted) return;
        if (this.actions.hero().mounted) void this.actions.ok([CrywolfText.ContractCanTBeMadeWhenYouAreOnAMount]);
        else this.connection.send('ClientToServer', 'CrywolfContractRequest', { StatueId: key });
      });
      const altar = key - 316;
      if (this.battle && this.onMap && altar >= 1 && altar <= 5) this.altars[altar - 1] = altarState;
      this.actions.contractPose();
    }
  }

  /** The altar talk of MOVEMENT_TALK: true when the click was the elf's contract (no TalkToNpcRequest). */
  talk(npc: { id: number; monsterNumber: number | null }): boolean {
    if (!this.battle || !this.onMap) return false;
    const number = npc.monsterNumber ?? -1;
    if (number < ALTARS[0] || number > ALTARS[1]) return false;
    const altar = number - ALTARS[0];
    const isElf = this.actions.hero().classIndex === 2;
    if (!isElf) return false;
    const contracted = (this.altars[altar] >> 4) === ALTAR_CONTRACTED;
    const left = this.altars[altar] & 0x0f;
    if (contracted) { void this.actions.ok([CrywolfText.ContractIsOngoingThereforeDualCompactIsNotPossible]); return true; }
    if (left <= 0) { void this.actions.ok([CrywolfText.FurtherContractCanTBeDoneSinceTheAltarHasBeenDestroyed]); return true; }
    void this.actions.ask([format(CrywolfText.ContractCanBeMadeForDTimes, left), CrywolfText.WouldYouLikeToProceedWithTheContract]).then((accepted) => {
      if (!accepted) { void this.actions.ok([CrywolfText.WeNeedAGuardianToProtectTheWolf]); return; }
      if (this.actions.hero().mounted) void this.actions.ok([CrywolfText.ContractCanTBeMadeWhenYouAreOnAMount]);
      else this.connection.send('ClientToServer', 'CrywolfContractRequest', { StatueId: npc.id });
    });
    return true;
  }

  // CCryWolf::SetTime (UI::CryWolf::SetCountdown): a new minute restarts at 60 s, then 40 and 20.
  private setTime(minute: number): void {
    if (this.minute !== minute) { this.second = 60_000; this.countTime = 1; this.timeStarted = true; }
    else if (this.countTime === 1) { this.second = 40_000; this.countTime++; }
    else if (this.countTime === 2) { this.second = 20_000; this.countTime = 0; }
    this.minute = minute;
  }

  // Set_BossMonster
  private bossInfo(hp: number, darkElves: number): void {
    if (!this.battle || !this.onMap) return;
    this.darkElves = darkElves;
    if (hp > 0 && !this.viewBalgass) this.viewBalgass = true;
    else if (hp <= 0 && this.viewBalgass) this.viewBalgass = false;
    this.balgassHp = hp;
  }

  // ReceiveCrywolfHeroList: Set_WorldRank of each.
  private heroList(bytes: Uint8Array): void {
    const count = bytes[4];
    for (let i = 0; i < count && 5 + (i + 1) * 20 <= bytes.length; i++) {
      const at = 5 + i * 20;
      const raw = bytes.subarray(at + 1, at + 11);
      const end = raw.indexOf(0);
      const name = new TextDecoder().decode(end < 0 ? raw : raw.subarray(0, end));
      const score = (bytes[at + 12] | (bytes[at + 13] << 8) | (bytes[at + 14] << 16) | (bytes[at + 15] << 24)) | 0;
      const rank = bytes[at];
      if (rank < 5) this.heroes[rank] = { name, serverClass: bytes[at + 16], score };
    }
  }

  /** The HUD of this frame (CCryWolf::SyncView); the animations step 25 times a second. */
  frame(now = performance.now()): CrywolfView {
    this.syncMap();
    if (!this.shown || !this.onMap) { this.frameTime = now; return (this.view = Crywolf.emptyView()); }
    if (now - this.frameTime < FRAME_MS && this.view.hud_visible === this.battle) {
      this.syncHudClock(this.view, now);
      return this.view;
    }
    // One step for each 40 ms gone by (the original counted its frames), a second's worth at most.
    const steps = Math.min(25, Math.max(1, Math.floor((now - this.frameTime) / FRAME_MS)));
    this.frameTime = now;
    let view = this.view;
    for (let i = 0; i < steps; i++) {
      view = Crywolf.emptyView();
      this.syncResult(view);
      this.syncHud(view, now);
    }
    this.view = view;
    return view;
  }

  // SyncResult: the banner slides in, holds 400 frames, slides out, then the ranking box; the rank label
  // slides in, then the rank letter and the experience.
  private syncResult(view: CrywolfView): void {
    if (this.sucOrFail === 1) {
      this.delay++;
      if (this.delay >= TOTAL_DELAY) { this.delay = 0; this.sucOrFail = 0; }
    }
    if (this.sucOrFail < 0) return;
    const aa = (this.delay * 2) % 140;
    const alpha = aa > 70 ? 1 - (aa - 70) * 0.01 : 0.3 + aa * 0.01;
    this.delayAddInter = this.delayAddInter <= 0 ? 0 : this.delayAddInter - 15;
    view.result_visible = true;
    view.banner_src = image(this.addNum === 11 ? 'icon_success.tga' : 'icon_failure.tga');
    view.banner_opacity = Math.min(1, Math.max(0, alpha));
    if (this.delay * 15 > 479) {
      view.banner_left = 150;
    } else if (this.sucOrFail === 0) {
      view.banner_left = 150 + this.delay * 15;
      this.delay++;
      if (this.delay * 15 > 479) {
        this.delay = 0;
        this.sucOrFail = -1;
        this.delayAddInter = 390;
        this.onResultBox?.(this.resultLines());
      }
    } else {
      view.banner_left = -329 + this.delay * 15;
    }
    view.rank_label_left = 250 + this.delayAddInter;
    view.rank_details_visible = this.delayAddInter === 0;
    if (!view.rank_details_visible) return;
    const letters = ['icon_Rank_D.tga', 'icon_Rank_C.tga', 'icon_Rank_B.tga', 'icon_Rank_A.tga', 'icon_Rank_S.tga'];
    view.rank_letter_src = this.rank < 5 ? image(letters[this.rank]) : '';
    const digits = this.experience < 0 ? Array(9).fill(0) : String(this.experience % 1_000_000_000).padStart(9, '0').split('').map(Number);
    view.exp_digits = digits.map((d) => ({ shown: true, src: image(`icon_Rank_${d}.tga`), rect: rect(0, 0, 15, 19) }));
  }

  /** The ranking box's lines (the original's GenericDialogConfig at the banner's end). */
  resultLines(): string[] {
    const lines = [`${CrywolfText.Rank}    ${CrywolfText.Character}    ${CrywolfText.Class}    ${CrywolfText.Score}`];
    this.heroes.forEach((hero, i) => { if (hero) lines.push(`${i + 1}      ${hero.name}      ${classText(hero.serverClass)}      ${hero.score}`); });
    lines.push('    ', '    ', '    ', '    ');
    if (this.viewSucOrFail === 1) lines.push(format(CrywolfText.MonsterStrengthDecreased10), format(CrywolfText._5IncreaseInCastleAndArenaInvitationCombineRate));
    else lines.push(CrywolfText.AllNPCsInCrywolfHaveBeenDeleted);
    return lines;
  }

  // SyncHud: the battle panel of the ready and start states.
  private syncHud(view: CrywolfView, now: number): void {
    if (!this.battle) return;
    view.hud_visible = true;
    view.altars = this.altars.map((value) => {
      const use = (value & 0xf0) >> 4, state = value & 0x0f;
      let file: string | null = null;
      if (use === ALTAR_CONTRACTED) file = state === 1 ? 'in_main_number1_1.tga' : state === 2 ? 'in_main_number2_1.tga' : 'in_main_number0_2.tga';
      else if (state === 1) file = 'in_main_number1.tga';
      else if (state === 2) file = 'in_main_number2.tga';
      return { shown: file !== null, src: file ? image(file) : '', rect: rect(0, 0, 12, 12) };
    });
    view.dark_elf_icon_src = image(this.darkElves === 0 ? 'in_main_icon_dl2.tga' : 'in_main_icon_dl1.tga');
    view.dark_elf_text = format(CrywolfText.DarkElfD12, this.darkElves);
    // Balgass shows 21 frames after he appears, and leaves at once.
    if (this.viewBalgass) {
      if (this.decoInsert < 21) this.decoInsert++;
      else {
        view.balgass_visible = true;
        view.balgass_text = CrywolfText.Balgass;
        view.balgass_bar_width = (68 / 100) * this.balgassHp;
        view.balgass_bar_rect = rect(0, 0, (67 / 100) * this.balgassHp, 8);
      }
    } else if (this.decoInsert > 0) this.decoInsert--;
    this.syncHudClock(view, now);
    const hidden = 100 - this.statueHp;
    const nx = (89 / 100) * hidden;
    view.statue_bar_left = 548 + nx;
    view.statue_bar_width = 89 - nx;
    view.statue_bar_rect = rect((88 / 100) * hidden, 0, (88 / 100) * (100 - hidden), 29);
    // AdvanceNoticeTexts: the ready state's board, a page every ten seconds.
    if (this.state === CrywolfState.READY) {
      if (now - this.noticeTime >= 10_000) {
        if (this.noticeTime !== 0) { this.nextNotice++; if (this.nextNotice >= 4) this.nextNotice = 0; }
        this.noticeTime = now;
      }
      view.notices = [0, 1, 2, 3].map((line) => ({ text: noticeText(this.nextNotice, line), heading: line === 0 }));
    }
  }

  private syncHudClock(view: CrywolfView, now: number): void {
    if (!view.hud_visible) return;
    if (this.timeStarted && this.state === CrywolfState.START) {
      view.timer_urgent = this.viewBalgass;
      this.second -= now - this.syncTime;
      this.syncTime = now;
      const seconds = Math.floor(Math.max(this.second, 0) / 1000);
      view.minute_pad = this.minute < 10; view.minute_digits = digitCells(this.minute);
      view.second_pad = seconds < 10; view.second_digits = digitCells(seconds);
      if (this.minute <= 0 && this.second <= 0) { this.timeStarted = false; this.second = 0; this.countTime = 0; }
    } else {
      this.syncTime = now;
      view.timer_urgent = false;
      view.minute_pad = true; view.minute_digits = digitCells(0);
      view.second_pad = true; view.second_digits = digitCells(0);
    }
  }
}

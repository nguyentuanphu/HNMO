// The castle siege of the Valley of Loren as the desktop keeps it (GameLogic/Events: SenatusInfo,
// SiegeRegistration; UI/Combat: GuardWindow, GuardGuildLists, CastleWindow, SiegeWarfare, SiegeWarBase
// and its commander / soldier / observer; UI/NPCs: UIGateKeeper, GatemanWindow; UI/Events: GateSwitchWindow,
// CatapultWindow; GameLogic/NPCs/npcGateSwitch; World/GameMaps/GMBattleCastle; the C1 B2-BB handlers of
// WSclient.cpp).
//
//   C3 30 0C / 0D  the senior's window / the guardsman's (asks for the status first)
//   B2 00          the siege status: the guardsman's window opens with it
//   B2 01-04       registration, giving up, the registration state, the guild marks handed in
//   B2 05-07, B3   the senior's gates and statues: buying, repairing, upgrading, the lists
//   B2 08-10, 1A   the tax rates and the castle's money; the rates the NPCs charge
//   B2 11-13       the gate switch's window, opening and closing a gate (and its cells)
//   B2 14-16, 20   the crown switches and the crown (progress boxes)
//   B2 17-19, 1E   the battle starts or ends, a guild at the crown, the hero's side, the time left
//   B2 1D, B6, BB  a commander's orders and where the guild's members and NPCs are
//   B4, B5         the guilds that announced the siege / that take part
//   B7 00-02       the catapult's window and its shots
//   B9 01-05       a life stone being built, the castle owner's guild mark, the gatekeeper and its hunting zone
// No DOM here: the windows are ui/castleSiegeWindows.ts.

import { read } from '../net/packets.js';
import { format, gold } from './textFormat.js';
import type { Connection } from '../net/connection.js';

export const BATTLE_CASTLE_MAP = 30;
export const SiegePhase = {
  NONE: -1, IDLE_1: 0, REGSIEGE: 1, IDLE_2: 2, REGMARK: 3, IDLE_3: 4, NOTIFY: 5, READYSIEGE: 6, STARTSIEGE: 7, ENDSIEGE: 8, ENDCYCLE: 9,
} as const;
export const GATE_NPC = 277, STATUE_NPC = 283;
const NPC_WINDOW_SENIOR = 0x0c, NPC_WINDOW_GUARDSMAN = 0x0d;
const DECLARE_LEVEL = 200;
const MAX_GUILDS = 100;
/** The gate cells of GMBattleCastle (g_byGateLocation): a gate's 4 cells in the row past it. */
const GATE_LOCATIONS = [[67, 114], [93, 114], [119, 114], [81, 161], [107, 161], [93, 204]];
const FIRST_CROWN_SWITCH = 217;

export const SiegeText = {
  CastleInformationFailed: 'Castle information failed', UnusualCastleInformation: 'Unusual castle information',
  CastleGuildIsDisappeared: 'Castle guild is disappeared', FailedToRegisterForCastleSiege: 'Failed to register for Castle Siege',
  CastleSiegeRegistrationIsSuccessful: 'Castle Siege registration is successful', AlreadyRegisteredInCastleSiege: 'Already registered in Castle Siege.',
  YouBelongToTheGuildOfTheDefendingTeam: 'You belong to the guild of the defending team.', IncorrectGuild: 'Incorrect guild.',
  GuildMasterSLevelIsInsufficient: "Guild master's level is insufficient.", NoAffiliatedGuild: 'No affiliated guild.',
  ItSNotARegistrationPeriodForCastleSiege: "It's not a registration period for Castle Siege.", NumberOfGuildMembersIsLacking: 'Number of guild members is lacking.',
  SurrenderingCastleSiegeHasFailed: 'Surrendering Castle Siege has failed.', SurrenderingCastleSiegeIsSuccessful: 'Surrendering Castle Siege is successful.',
  ThisGuildIsNotRegisteredInCastleSiege: 'This guild is not registered in Castle Siege.',
  ItSNotASurrenderingPeriodForCastleSiege: "It's not a surrendering period for Castle Siege.", RegistrationOfSignHasFailed: 'Registration of sign has failed.',
  ThisGuildHasNotParticipatedInCastleSiege: 'This guild has not participated in Castle Siege.', IncorrectItemWasRegistered: 'Incorrect item was registered.',
  FailedToPurchase: 'Failed to purchase.', NoAuthorization: 'No authorization', PurchasingCostIsInsufficient: 'Purchasing cost is insufficient.',
  AlreadyExists: 'Already exists.', JewelIsLacking: 'Jewel is lacking.', IncorrectType: 'Incorrect type.', IncorrectRequestedValue: 'Incorrect requested value.',
  NPCDoesNotExist: 'NPC does not exist.', AcquiringTaxRateInformationHasFailed: 'Acquiring tax rate information has failed',
  ChangingTaxRateInformationHasFailed: 'Changing tax rate information has failed', WithdrawalFailed: 'Withdrawal failed',
  RequestHasFailed: 'The request has failed.', HasNotBeenConfirmedYet: 'Has not been confirmed yet.',
  CrownSwitchHasBeenReleased: 'Crown Switch has been released!', CrownSwitchHasBeenActivated: 'Crown Switch has been activated!',
  CharacterSIs: 'Character %s is', CharacterIs: 'Character is', AlreadyPressingS: 'already pressing %s',
  SAccumulatedHourDseconds: '%s (Accumulated hour : %dseconds)', OfficialSealRegistrationWillStart: 'Official seal registration will start',
  OfficialSealRegistrationIsFailed: 'Official seal registration is failed', OfficialSealRegistrationIsSuccessful: 'Official seal registration is successful',
  AnotherCharacterIsRegisteringTheOfficialSeal: 'Another character is registering the official seal',
  OtherSiegeTeamIsRunningTheCrownSwitch: 'Other siege team is running the crown switch.',
  ShieldOfTheCrownHasBeenRemoved: 'Shield of the crown has been removed', ShieldOfTheCrownHasBeenActivated: 'Shield of the crown has been activated',
  SAllianceIsTryingToRegisterTheOfficialSealNow: '%s alliance is trying to register the official seal now',
  SGuildHasRegisteredTheOfficialSealSuccessfully: '%s guild has registered the official seal successfully',
} as const;

export interface SiegeDate { year: number; month: number; day: number; hour: number; minute: number }
export interface Declaration { name: string; markCount: number; gaveUp: boolean; order: number }
export interface SiegeGuild { name: string; side: number; involved: boolean; score: number }
/** PMSG_NPCDBLIST: a gate or statue of the senior's lists. */
export interface DefenseNpc { number: number; index: number; defenseLevel: number; recoverLevel: number; maxHp: number; hp: number; live: boolean }
export interface GuildCommand { team: number; x: number; y: number; command: number; lifetime: number }

const emptyNpc = (): DefenseNpc => ({ number: 0, index: 0, defenseLevel: 0, recoverLevel: 0, maxHp: 0, hp: 0, live: false });

// CSenatusInfo's levels.
const GATE_HP = [1_900_000, 2_500_000, 3_500_000, 5_200_000], GATE_DEFENSE = [100, 180, 300, 520];
const STATUE_HP = [1_500_000, 2_200_000, 3_400_000, 5_000_000], STATUE_DEFENSE = [80, 180, 340, 550], STATUE_RECOVER = [0, 1, 2, 3];
export const MAX_LEVEL = 3, MAX_TAX_RATE = 3;
export const NpcUpgrade = { DEFENSE: 1, RECOVER: 2, HP: 3 } as const;

/** CSenatusInfo: the senior's numbers. */
export const senatus = {
  repairCost(npc: DefenseNpc): number {
    if (npc.number === GATE_NPC) return (npc.maxHp - npc.hp) * 5 + npc.defenseLevel * 1_000_000;
    if (npc.number === STATUE_NPC) return (npc.maxHp - npc.hp) * 3 + npc.defenseLevel * 1_000_000;
    return -1;
  },
  hp(type: number, level: number): number { const table = type === GATE_NPC ? GATE_HP : STATUE_HP; return table[Math.min(level, 3)]; },
  hpLevel(npc: DefenseNpc): number {
    const table = npc.number === GATE_NPC ? GATE_HP : npc.number === STATUE_NPC ? STATUE_HP : null;
    if (!table) return -1;
    if (npc.maxHp <= table[0]) return 0;
    if (npc.maxHp <= table[1]) return 1;
    if (npc.maxHp <= table[2]) return 2;
    return 3;
  },
  nextAddHp(npc: DefenseNpc): number {
    const table = npc.number === GATE_NPC ? GATE_HP : STATUE_HP;
    const level = table.indexOf(npc.maxHp);
    return level >= 0 && level < 3 ? table[level + 1] - table[level] : 0;
  },
  defense(type: number, level: number): number { return (type === GATE_NPC ? GATE_DEFENSE : STATUE_DEFENSE)[Math.min(Math.max(level, 0), 3)]; },
  nextAddDefense(npc: DefenseNpc): number {
    const table = npc.number === GATE_NPC ? GATE_DEFENSE : STATUE_DEFENSE;
    return npc.defenseLevel >= 0 && npc.defenseLevel < 3 ? table[npc.defenseLevel + 1] - table[npc.defenseLevel] : 0;
  },
  recover(type: number, level: number): number { return type === GATE_NPC ? 0 : STATUE_RECOVER[Math.min(Math.max(level, 0), 3)]; },
  recoverLevel(npc: DefenseNpc): number { return npc.number === GATE_NPC ? 0 : npc.recoverLevel; },
  nextAddRecover(npc: DefenseNpc): number {
    if (npc.number === GATE_NPC) return 0;
    return npc.recoverLevel >= 0 && npc.recoverLevel < 3 ? STATUE_RECOVER[npc.recoverLevel + 1] - STATUE_RECOVER[npc.recoverLevel] : 0;
  },
};

export interface SiegeActions {
  mapNumber: () => number | null;
  /** The system log (UI::Chat::PostSystem). */
  message: (text: string) => void;
  /** The golden notice of the screen (UI::Notices::Create). */
  notice: (text: string) => void;
  ok: (lines: string[]) => void;
  /** A crown or seal progress box (CProgressMsgBox), and PopAllMessageBoxes. */
  progress: (lines: string[], durationMs: number) => void;
  clearProgress: () => void;
  /** Where an object in view stands, its name and monster number. */
  object: (id: number) => { cell: { x: number; y: number }; name: string; monsterNumber: number | null } | null;
  /** AddTerrainAttributeRange: TW_NOMOVE on (add) or off. */
  setNoMove: (x: number, y: number, width: number, height: number, add: boolean) => void;
  /** A gate opens or closes (its animation and SOUND_BC_GATE_OPEN). */
  gateMoved: (id: number, open: boolean) => void;
  /** A catapult fires (its action 1, SOUND_BC_CATAPULT_ATTACK) at a cell; id null for the shot falling near the hero. */
  catapultFired: (id: number | null, type: number, x: number, y: number) => void;
  /** SetBattleCastleStart on the siege map: EncTerrain312.att and TerrainLight2.jpg, or back. */
  battleTerrain: (started: boolean) => void;
  /** The windows opened by a packet. */
  open: (window: 'guard' | 'senior' | 'gatekeeper' | 'gateSwitch' | 'catapult') => void;
  /** The castle owner's guild mark (64 cells), painted on the castle's flag (CreateCastleMark). */
  ownerMark: (cells: number[]) => void;
  /** The NPC tax rate (UI::Shop::SetNpcTaxRate). */
  taxRate: (type: number, rate: number) => void;
}

const text = (bytes: Uint8Array, at: number, length: number): string => {
  const raw = bytes.subarray(at, at + length);
  const end = raw.indexOf(0);
  return new TextDecoder().decode(end < 0 ? raw : raw.subarray(0, end));
};

export class CastleSiege {
  // the guardsman (CGuardWindow::SetData, CSiegeRegistration, GuardGuildLists)
  phase: number = SiegePhase.NONE;
  ownerGuild = '';
  ownerGuildMaster = '';
  registrationStart: SiegeDate = { year: 0, month: 0, day: 0, hour: 0, minute: 0 };
  registrationEnd: SiegeDate = { year: 0, month: 0, day: 0, hour: 0, minute: 0 };
  battleStart: SiegeDate = { year: 0, month: 0, day: 0, hour: 0, minute: 0 };
  secondsRemaining = 0;
  registered = false;
  markCount = 0;
  declarations: Declaration[] = [];
  selectedDeclaration = '';
  siegeGuilds: SiegeGuild[] = [];
  selectedSiegeGuild = '';
  showScore = false;
  // the senior (CSenatusInfo)
  readonly gates: DefenseNpc[] = Array.from({ length: 6 }, emptyNpc);
  readonly statues: DefenseNpc[] = Array.from({ length: 4 }, emptyNpc);
  currentGate = 0;
  currentStatue = 0;
  chaosTaxRate = 0;
  normalTaxRate = 0;
  realChaosTaxRate = 0;
  realStoreTaxRate = 0;
  castleMoney = 0;
  // the gatekeeper (CUIGateKeeper)
  gatekeeper = { type: 0, public: false, fee: 0, viewFee: 0, unit: 0, max: 0 };
  // the gate switch (npcGateSwitch)
  gateSwitch = { key: 0, opened: false };
  readonly openGates = new Set<number>();
  // the catapult (CCatapultWindow)
  catapult = { key: 0, type: 0, area: -1, fireLocked: true };
  // the battle (battleCastle, CSiegeWarfare, CSiegeWarBase)
  battleStarted = false;
  /** The hero's battle formation (SettingBattleFormation): 0 none, 1 defense, 2-4 attack. */
  formation = 0;
  private hour = 0;
  private minute = 0;
  private second = 0;
  private syncTime = 0;
  readonly commands: (GuildCommand | null)[] = Array.from({ length: 7 }, () => null);
  memberLocations: { type: number; x: number; y: number }[] = [];
  readonly switches: { state: number; side: number; guild: string; user: string }[] = [
    { state: 0, side: 0, guild: '', user: '' }, { state: 0, side: 0, guild: '', user: '' }];
  crownShielded = true;
  ownerMarkCells: number[] | null = null;
  /** The life stones being built: object id → build time (0 to 5). */
  readonly buildTimes = new Map<number, number>();

  constructor(private readonly connection: Connection, private readonly actions: SiegeActions) {
    connection.onPacket((bytes) => { try { this.handle(bytes); } catch (error) { console.warn('Castle siege packet', error); } });
  }

  get onMap(): boolean { return this.actions.mapNumber() === BATTLE_CASTLE_MAP; }

  handle(bytes: Uint8Array): void {
    const head = bytes[0];
    if (head === 0xc3 && bytes[2] === 0x30 && bytes.length >= 4) {
      if (bytes[3] === NPC_WINDOW_SENIOR) this.actions.open('senior');
      else if (bytes[3] === NPC_WINDOW_GUARDSMAN) this.connection.send('ClientToServer', 'CastleSiegeStatusRequest', {});
      return;
    }
    if (head === 0xc2) {
      const code = bytes[3];
      if (code === 0xb3) this.npcList(bytes);
      else if (code === 0xb4) this.declareList(bytes);
      else if (code === 0xb5) this.guildList(bytes);
      else if (code === 0xb6) this.memberPositions(bytes);
      else if (code === 0xbb) this.npcPositions(bytes);
      return;
    }
    if (head !== 0xc1 || bytes.length < 5) return;
    const code = bytes[2], sub = bytes[3];
    if (code === 0xb2) this.b2(sub, bytes);
    else if (code === 0xb7) this.b7(sub, bytes);
    else if (code === 0xb9) this.b9(sub, bytes);
  }

  private r<T extends object>(name: string, bytes: Uint8Array): T { return read<T>('ServerToClient', name, bytes); }

  private b2(sub: number, bytes: Uint8Array): void {
    const m = this.actions.message;
    const result = bytes[4];
    const say = (texts: Record<number, string>) => { const t = texts[result]; if (t) m(t); };
    switch (sub) {
      case 0x00: this.status(bytes); break;
      case 0x01:
        if (result === 1) this.registered = true;
        say({ 0: SiegeText.FailedToRegisterForCastleSiege, 1: SiegeText.CastleSiegeRegistrationIsSuccessful, 2: SiegeText.AlreadyRegisteredInCastleSiege,
          3: SiegeText.YouBelongToTheGuildOfTheDefendingTeam, 4: SiegeText.IncorrectGuild, 5: SiegeText.GuildMasterSLevelIsInsufficient,
          6: SiegeText.NoAffiliatedGuild, 7: SiegeText.ItSNotARegistrationPeriodForCastleSiege, 8: SiegeText.NumberOfGuildMembersIsLacking });
        break;
      case 0x02:
        if (result === 1) {
          this.connection.send('ClientToServer', 'CastleSiegeRegistrationStateRequest', {});
          this.connection.send('ClientToServer', 'CastleSiegeRegisteredGuildsListRequest', {});
          this.registered = false;
        }
        say({ 0: SiegeText.SurrenderingCastleSiegeHasFailed, 1: SiegeText.SurrenderingCastleSiegeIsSuccessful, 2: SiegeText.ThisGuildIsNotRegisteredInCastleSiege,
          3: SiegeText.ItSNotASurrenderingPeriodForCastleSiege });
        break;
      case 0x03: {
        const p = this.r<{ Result: number; GuildMarkCount: number; IsGivingUp: boolean }>('CastleSiegeRegistrationStateResponse', bytes);
        if (p.Result === 1) { this.registered = !p.IsGivingUp; this.markCount = p.GuildMarkCount; } else this.registered = false;
        break;
      }
      case 0x04: {
        const p = this.r<{ Result: number; GuildMarkCount: number }>('CastleSiegeMarkRegistrationResponse', bytes);
        if (p.Result === 1) this.markCount = p.GuildMarkCount;
        else say({ 0: SiegeText.RegistrationOfSignHasFailed, 2: SiegeText.ThisGuildHasNotParticipatedInCastleSiege, 3: SiegeText.IncorrectItemWasRegistered });
        break;
      }
      case 0x05: {
        const p = this.r<{ Result: number; NpcNumber: number; NpcIndex: number }>('CastleSiegeDefenseBuyResponse', bytes);
        if (p.Result === 1) this.buyNpc(p.NpcNumber, p.NpcIndex);
        else say({ 0: SiegeText.FailedToPurchase, 2: SiegeText.NoAuthorization, 3: SiegeText.PurchasingCostIsInsufficient, 4: SiegeText.AlreadyExists });
        break;
      }
      case 0x06: {
        const p = this.r<{ Result: number; NpcNumber: number; NpcIndex: number; CurrentHp: number; MaxHp: number }>('CastleSiegeDefenseRepairResponse', bytes);
        const npc = this.npcOf(p.NpcNumber, p.NpcIndex);
        if (p.Result === 1 && npc) { npc.hp = p.CurrentHp; npc.maxHp = p.MaxHp; }
        else say({ 0: SiegeText.FailedToPurchase, 2: SiegeText.NoAuthorization, 3: SiegeText.PurchasingCostIsInsufficient });
        break;
      }
      case 0x07: {
        const p = this.r<{ Result: number; NpcNumber: number; NpcIndex: number; NpcUpgradeType: number; NpcUpgradeValue: number }>('CastleSiegeDefenseUpgradeResponse', bytes);
        const npc = this.npcOf(p.NpcNumber, p.NpcIndex);
        if (p.Result === 1 && npc) {
          if (p.NpcUpgradeType === 1) npc.defenseLevel = p.NpcUpgradeValue;
          else if (p.NpcUpgradeType === 2) npc.recoverLevel = p.NpcUpgradeValue;
          else if (p.NpcUpgradeType === 3) npc.hp = npc.maxHp = p.NpcUpgradeValue;
        } else {
          say({ 0: SiegeText.FailedToPurchase, 2: SiegeText.NoAuthorization, 3: SiegeText.PurchasingCostIsInsufficient, 4: SiegeText.JewelIsLacking,
            5: SiegeText.IncorrectType, 6: SiegeText.IncorrectRequestedValue, 7: SiegeText.NPCDoesNotExist });
        }
        break;
      }
      case 0x08: {
        const p = this.r<{ Result: number; TaxRateChaosMachine: number; TaxRateNormal: number; Treasury: number }>('CastleSiegeTaxInfoResponse', bytes);
        if (p.Result === 1) {
          this.chaosTaxRate = this.realChaosTaxRate = p.TaxRateChaosMachine;
          this.normalTaxRate = this.realStoreTaxRate = p.TaxRateNormal;
          this.castleMoney = p.Treasury;
        } else say({ 0: SiegeText.AcquiringTaxRateInformationHasFailed, 2: SiegeText.NoAuthorization });
        break;
      }
      case 0x09: {
        const p = this.r<{ Result: number; TaxType: number; TaxValue: number }>('CastleSiegeTaxChangeResponse', bytes);
        if (p.Result === 1) {
          if (p.TaxType === 3) this.gatekeeper.fee = p.TaxValue;
          else if (p.TaxType === 1) this.chaosTaxRate = this.realChaosTaxRate = p.TaxValue;
          else if (p.TaxType === 2) this.normalTaxRate = this.realStoreTaxRate = p.TaxValue;
        } else say({ 0: SiegeText.ChangingTaxRateInformationHasFailed, 2: SiegeText.NoAuthorization });
        break;
      }
      case 0x10: {
        const p = this.r<{ Result: number; Money: number }>('CastleSiegeTributeWithdrawResponse', bytes);
        if (p.Result === 1) this.castleMoney = p.Money;
        else say({ 0: SiegeText.WithdrawalFailed, 2: SiegeText.NoAuthorization });
        break;
      }
      case 0x11: {
        const p = this.r<{ Result: number; GateIndex: number }>('CastleSiegeGateInterfaceResponse', bytes);
        if (p.Result === 1) { this.gateSwitch = { key: p.GateIndex, opened: this.openGates.has(p.GateIndex & 0x7fff) }; this.actions.open('gateSwitch'); }
        break;
      }
      case 0x12: {
        const p = this.r<{ Result: number; IsOpen: boolean; GateIndex: number }>('CastleSiegeGateOperateResponse', bytes);
        if (p.Result === 1) this.gateState(p.GateIndex, p.IsOpen, true);
        break;
      }
      case 0x13: {
        const p = this.r<{ IsOpen: boolean; GateIndex: number }>('CastleSiegeGateStateNotification', bytes);
        this.gateState(p.GateIndex, p.IsOpen, false);
        break;
      }
      case 0x14: this.crownSwitch(bytes); break;
      case 0x15: {
        const p = this.r<{ State: number; AccumulatedTimeMs: number }>('CastleSiegeCrownAccessState', bytes);
        this.actions.clearProgress();
        const seconds = Math.min(59, Math.floor(p.AccumulatedTimeMs / 1000));
        if (p.State === 0) this.actions.progress([format(SiegeText.SAccumulatedHourDseconds, SiegeText.OfficialSealRegistrationWillStart, seconds)], 60_000 - p.AccumulatedTimeMs);
        else if (p.State === 1) this.actions.progress([SiegeText.OfficialSealRegistrationIsSuccessful], 3000);
        else if (p.State === 2) this.actions.progress([format(SiegeText.SAccumulatedHourDseconds, SiegeText.OfficialSealRegistrationIsFailed, seconds)], 3000);
        else if (p.State === 3) this.actions.progress([SiegeText.AnotherCharacterIsRegisteringTheOfficialSeal], 3000);
        else if (p.State === 4) this.actions.progress([SiegeText.OtherSiegeTeamIsRunningTheCrownSwitch], 3000);
        break;
      }
      case 0x16: {
        const state = bytes[4];
        if (state === 0) { this.actions.progress([SiegeText.ShieldOfTheCrownHasBeenRemoved], 3000); this.crownShielded = false; }
        else if (state === 1) { this.actions.progress([SiegeText.ShieldOfTheCrownHasBeenActivated], 3000); this.crownShielded = true; }
        else if (state === 2) this.actions.progress([SiegeText.OfficialSealRegistrationIsSuccessful], 3000);
        break;
      }
      case 0x17: this.setBattleStarted(bytes[4] !== 0); break;
      case 0x18: {
        const p = this.r<{ State: number; GuildName: string }>('CastleSiegeBattleProcess', bytes);
        if (p.State === 0) this.actions.notice(format(SiegeText.SAllianceIsTryingToRegisterTheOfficialSealNow, p.GuildName));
        else if (p.State === 1) this.actions.notice(format(SiegeText.SGuildHasRegisteredTheOfficialSealSuccessfully, p.GuildName));
        break;
      }
      case 0x19: this.formation = bytes[4] <= 4 ? bytes[4] : 0; break;
      case 0x1a: { const p = this.r<{ TaxType: number; TaxRate: number }>('CastleSiegeTaxRateNotification', bytes); this.actions.taxRate(p.TaxType, p.TaxRate); break; }
      case 0x1d: {
        const p = this.r<{ Team: number; PositionX: number; PositionY: number; Command: number }>('CastleSiegeGuildCommand', bytes);
        if (p.Team < 7) this.commands[p.Team] = { team: p.Team, x: p.PositionX, y: p.PositionY, command: p.Command, lifetime: 100 };
        break;
      }
      case 0x1e: this.setTime(bytes[4], bytes[5] ?? 0); break;
      case 0x1f: {
        const p = this.r<{ Result: number; IsPublic: boolean }>('CastleSiegeHuntingZoneEntranceSettingResponse', bytes);
        if (p.Result === 1) this.gatekeeper.public = p.IsPublic;
        else this.actions.ok([p.Result === 2 ? SiegeText.NoAuthorization : SiegeText.RequestHasFailed]);
        break;
      }
      case 0x20: {
        const p = this.r<{ SwitchIndex: number; IsOccupied: boolean; JoinSide: number; GuildName: string; UserName: string }>('CastleSiegeSwitchInfo', bytes);
        const at = p.SwitchIndex === FIRST_CROWN_SWITCH ? 0 : 1;
        this.switches[at] = { state: p.IsOccupied ? 1 : 0, side: p.JoinSide, guild: p.GuildName, user: p.UserName };
        break;
      }
      default: break;
    }
  }

  // ReceiveBCStatus
  private status(bytes: Uint8Array): void {
    const p = this.r<{
      Result: number; State: number; StartYear: number; StartMonth: number; StartDay: number; StartHour: number; StartMinute: number;
      EndYear: number; EndMonth: number; EndDay: number; EndHour: number; EndMinute: number; SiegeStartYear: number; SiegeStartMonth: number;
      SiegeStartDay: number; SiegeStartHour: number; SiegeStartMinute: number; GuildName: string; GuildMasterName: string; RemainingTime: number;
    }>('CastleSiegeStatusResponse', bytes);
    if (p.Result === 1 || p.Result === 2) {
      this.phase = p.State > 127 ? p.State - 256 : p.State;
      this.ownerGuild = p.GuildName;
      this.ownerGuildMaster = p.GuildMasterName;
      this.registrationStart = { year: p.StartYear, month: p.StartMonth, day: p.StartDay, hour: p.StartHour, minute: p.StartMinute };
      this.registrationEnd = { year: p.EndYear, month: p.EndMonth, day: p.EndDay, hour: p.EndHour, minute: p.EndMinute };
      this.battleStart = { year: p.SiegeStartYear, month: p.SiegeStartMonth, day: p.SiegeStartDay, hour: p.SiegeStartHour, minute: p.SiegeStartMinute };
      this.secondsRemaining = p.RemainingTime;
      this.actions.open('guard');
    } else {
      const texts: Record<number, string> = { 0: SiegeText.CastleInformationFailed, 3: SiegeText.UnusualCastleInformation, 4: SiegeText.CastleGuildIsDisappeared };
      if (texts[p.Result]) this.actions.message(texts[p.Result]);
    }
  }

  /** The guard window's openingProcess: the guild's registration state. */
  openingGuard(): void { this.connection.send('ClientToServer', 'CastleSiegeRegistrationStateRequest', {}); }

  /** The senior's OpeningProcess: the first gate and statue, the lists and the taxes. */
  openingSenior(): void {
    this.currentGate = 0;
    this.currentStatue = 0;
    this.connection.send('ClientToServer', 'CastleSiegeGateListRequest', {});
    this.connection.send('ClientToServer', 'CastleSiegeStatueListRequest', {});
    this.connection.send('ClientToServer', 'CastleSiegeTaxInfoRequest', {});
  }

  /** The guard window's list tab asks for its list. */
  requestList(): void {
    if (this.phase === SiegePhase.REGSIEGE || this.phase === SiegePhase.REGMARK) this.connection.send('ClientToServer', 'CastleSiegeRegisteredGuildsListRequest', {});
    else if (this.phase === SiegePhase.NOTIFY || this.phase === SiegePhase.READYSIEGE) this.connection.send('ClientToServer', 'CastleOwnerListRequest', {});
  }

  /** GuardGuildLists::Clear on closing the guard window. */
  clearLists(): void {
    this.declarations = []; this.selectedDeclaration = '';
    this.siegeGuilds = []; this.selectedSiegeGuild = ''; this.showScore = false;
  }

  /** CSiegeRegistration::IsSufficientDeclareLevel. */
  static canDeclare(isMaster: boolean, level: number): boolean { return isMaster && level >= DECLARE_LEVEL; }

  private npcOf(number: number, index: number): DefenseNpc | null {
    if (number === GATE_NPC) return this.gates[index - 1] ?? null;
    if (number === STATUE_NPC) return this.statues[index - 1] ?? null;
    return null;
  }

  // CSenatusInfo::BuyNewNPC
  private buyNpc(number: number, index: number): void {
    const npc = this.npcOf(number, index);
    if (!npc) return;
    const hp = number === GATE_NPC ? GATE_HP[0] : STATUE_HP[0];
    Object.assign(npc, { number, index, live: true, defenseLevel: 0, recoverLevel: 0, hp, maxHp: hp });
  }

  // ReceiveBCNPCList
  private npcList(bytes: Uint8Array): void {
    const p = this.r<{ Result: number; NpcList: { NpcNumber: number; NpcIndex: number; DefenseUpgradeLevel: number; RegenerationLevel: number; MaxHp: number; CurrentHp: number; IsAlive: boolean }[] }>('CastleSiegeNpcList', bytes);
    if (p.Result === 0) { this.actions.message(SiegeText.RequestHasFailed); return; }
    if (p.Result === 2) { this.actions.message(SiegeText.NoAuthorization); return; }
    for (const n of p.NpcList ?? []) {
      const npc = this.npcOf(n.NpcNumber, n.NpcIndex);
      if (npc) Object.assign(npc, { number: n.NpcNumber, index: n.NpcIndex, defenseLevel: n.DefenseUpgradeLevel, recoverLevel: n.RegenerationLevel, maxHp: n.MaxHp, hp: n.CurrentHp, live: n.IsAlive });
    }
  }

  // ReceiveBCDeclareGuildList → ReplaceDeclarations: the list sorted by marks, the first one chosen.
  private declareList(bytes: Uint8Array): void {
    const p = this.r<{ Result: number; Guilds: { GuildName: string; GuildMarkCount: number; IsGivingUp: boolean; SequenceNumber: number }[] }>('CastleSiegeRegisteredGuildList', bytes);
    if (p.Result !== 1) { if (p.Result === 0) this.actions.message(SiegeText.RequestHasFailed); return; }
    this.declarations = (p.Guilds ?? []).filter((g) => g.GuildName).slice(-MAX_GUILDS)
      .map((g) => ({ name: g.GuildName, markCount: g.GuildMarkCount, gaveUp: g.IsGivingUp, order: g.SequenceNumber }));
    this.declarations.sort((a, b) => b.markCount - a.markCount);
    this.selectedDeclaration = this.declarations[0]?.name ?? '';
  }

  // ReceiveBCGuildList → ReplaceAttackingGuilds.
  private guildList(bytes: Uint8Array): void {
    const p = this.r<{ Result: number; Guilds: { Side: number; IsInvolved: boolean; GuildName: string; Score: number }[] }>('CastleSiegeGuildList', bytes);
    if (p.Result !== 1) {
      if (p.Result === 0) this.actions.message(SiegeText.RequestHasFailed);
      else this.actions.message(SiegeText.HasNotBeenConfirmedYet);
      return;
    }
    this.siegeGuilds = (p.Guilds ?? []).filter((g) => g.GuildName).slice(-MAX_GUILDS)
      .map((g) => ({ name: g.GuildName, side: g.Side, involved: g.IsInvolved, score: g.Score }));
    this.selectedSiegeGuild = this.siegeGuilds[0]?.name ?? '';
    this.showScore = false;
  }

  private memberPositions(bytes: Uint8Array): void {
    const p = this.r<{ Players: { PositionX: number; PositionY: number }[] }>('CastleSiegeMiniMapPlayerPositions', bytes);
    this.memberLocations = (p.Players ?? []).map((pl) => ({ type: 0, x: pl.PositionX, y: pl.PositionY }));
  }

  private npcPositions(bytes: Uint8Array): void {
    const p = this.r<{ Npcs: { NpcType: number; PositionX: number; PositionY: number }[] }>('CastleSiegeMiniMapNpcPositions', bytes);
    for (const npc of p.Npcs ?? []) this.memberLocations.push({ type: npc.NpcType + 1, x: npc.PositionX, y: npc.PositionY });
  }

  // npcGateSwitch::ProcessState / ReceiveGateCurrentState: the gate's cells and its look.
  private gateState(id: number, open: boolean, fromOperator: boolean): void {
    const key = id & 0x7fff;
    const gate = this.actions.object(key);
    if (fromOperator && gate && gate.monsterNumber !== GATE_NPC) return;
    if (open) this.openGates.add(key); else this.openGates.delete(key);
    if (!gate) return;
    this.actions.gateMoved(key, open);
    // SetCastleGate_Attribute: the four cells past the gate.
    for (const [x, y] of GATE_LOCATIONS) {
      if (gate.cell.x === x && gate.cell.y === y) { this.actions.setNoMove(x - 1, y + 1, 4, 1, !open); break; }
    }
  }

  // ReceiveCrownSwitchState
  private crownSwitch(bytes: Uint8Array): void {
    const p = this.r<{ SwitchIndex: number; PlayerIndex: number; State: number }>('CastleSiegeCrownSwitchState', bytes);
    if (p.State === 0) {
      this.switches[p.SwitchIndex === FIRST_CROWN_SWITCH ? 0 : 1] = { state: 0, side: 0, guild: '', user: '' };
      this.actions.progress([SiegeText.CrownSwitchHasBeenReleased], 3000);
    } else if (p.State === 1) {
      this.actions.progress([SiegeText.CrownSwitchHasBeenActivated], 3000);
    } else if (p.State === 2) {
      const other = this.actions.object(p.PlayerIndex & 0x7fff);
      const owner = this.actions.object(p.SwitchIndex & 0x7fff);
      this.actions.progress([other?.name ? format(SiegeText.CharacterSIs, other.name) : SiegeText.CharacterIs, format(SiegeText.AlreadyPressingS, owner?.name ?? '')], 3000);
    }
  }

  // battleCastle::SetBattleCastleStart, with the battle skills (SetBattleSkillsActive).
  setBattleStarted(started: boolean): void {
    this.battleStarted = started;
    if (this.onMap) this.actions.battleTerrain(started);
  }

  // CSiegeWarfare::SetTime and its Update: the minute counted down.
  private setTime(hour: number, minute: number): void {
    this.hour = hour; this.minute = minute; this.second = 60_000; this.syncTime = performance.now();
  }

  /** The siege's hour and minute left (CSiegeWarfare::Update while the battle runs). */
  time(now = performance.now()): { hour: number; minute: number } {
    if (this.onMap && this.battleStarted) {
      this.second -= now - this.syncTime;
      if (this.second <= 0) {
        if (this.minute <= 0) {
          if (this.hour <= 0) { this.second = 0; this.minute = 0; this.hour = 0; } else { this.hour--; this.minute += 60; }
        } else { this.minute--; this.second += 60_000; }
      }
    }
    this.syncTime = now;
    return { hour: this.hour, minute: this.minute };
  }

  private b7(sub: number, bytes: Uint8Array): void {
    if (sub === 0x00) {
      const p = this.r<{ Result: number; MachineType: number; NpcIndex: number }>('CastleSiegeMachineInterface', bytes);
      if (p.Result === 1) { this.catapult = { key: p.NpcIndex, type: p.MachineType, area: -1, fireLocked: true }; this.actions.open('catapult'); }
      else if (p.Result === 0) this.actions.message('ReceiveCatapultState');
    } else if (sub === 0x01) {
      const p = this.r<{ Result: number; NpcIndex: number; MachineType: number; TargetX: number; TargetY: number }>('CastleSiegeMachineUseResult', bytes);
      if (p.Result) this.actions.catapultFired(p.NpcIndex & 0x7fff, p.MachineType, p.TargetX, p.TargetY);
      else this.actions.message('ReceiveCatapultFire');
    } else if (sub === 0x02) {
      const p = this.r<{ MachineType: number; TargetX: number; TargetY: number }>('CastleSiegeMachineRegionNotify', bytes);
      this.actions.catapultFired(null, p.MachineType, p.TargetX, p.TargetY);
    }
  }

  private b9(sub: number, bytes: Uint8Array): void {
    if (sub === 0x01) {
      const p = this.r<{ NpcIndex: number; BuildTime: number }>('CastleSiegeLifeStoneBuildTime', bytes);
      this.buildTimes.set(p.NpcIndex & 0x7fff, p.BuildTime);
    } else if (sub === 0x02) {
      // ReceiveCastleGuildMark: 32 bytes of two 4-bit cells each.
      const cells: number[] = [];
      for (let i = 0; i < 64; i++) cells.push(i % 2 === 0 ? (bytes[4 + (i >> 1)] >> 4) & 0x0f : bytes[4 + (i >> 1)] & 0x0f);
      this.ownerMarkCells = cells;
      this.actions.ownerMark(cells);
    } else if (sub === 0x03) {
      const p = this.r<{ Result: number; IsEnabled: boolean; CurrentPrice: number; MaxPrice: number; UnitPrice: number }>('CastleSiegeHuntingZoneGuardInfo', bytes);
      if (p.Result === 0) { this.actions.ok([SiegeText.RequestHasFailed]); return; }
      this.gatekeeper = { type: p.Result, public: p.IsEnabled, fee: p.CurrentPrice, viewFee: p.CurrentPrice, unit: p.UnitPrice, max: p.MaxPrice };
      this.actions.open('gatekeeper');
    } else if (sub === 0x05) {
      if (bytes[4] === 0) this.actions.ok([SiegeText.RequestHasFailed]);
    }
  }

  /** On entering the siege map (battleCastle::Init): the owner's mark. */
  enteredMap(): void {
    this.connection.send('ClientToServer', 'GuildLogoOfCastleOwnerRequest', {});
    if (this.battleStarted) this.actions.battleTerrain(true);
  }

  // ---- the requests ----

  register(): void { this.connection.send('ClientToServer', 'CastleSiegeRegistrationRequest', {}); }
  giveUp(): void { this.connection.send('ClientToServer', 'CastleSiegeUnregisterRequest', { IsGivingUp: true }); }
  registerMark(gridIndex: number): void { this.connection.send('ClientToServer', 'CastleSiegeMarkRegistration', { ItemIndex: gridIndex }); }

  /** DoGateRepairAction / DoStatueRepairAction: buy a dead one, repair a live one. */
  repair(npc: DefenseNpc, fallbackNumber: number, fallbackIndex: number): void {
    const values = { NpcNumber: npc.number || fallbackNumber, NpcIndex: npc.index || fallbackIndex };
    this.connection.send('ClientToServer', npc.live ? 'CastleSiegeDefenseRepairRequest' : 'CastleSiegeDefenseBuyRequest', values);
  }

  upgrade(npc: DefenseNpc, type: number): void {
    const value = type === NpcUpgrade.HP ? senatus.hp(npc.number, senatus.hpLevel(npc) + 1)
      : type === NpcUpgrade.DEFENSE ? npc.defenseLevel + 1 : senatus.recoverLevel(npc) + 1;
    this.connection.send('ClientToServer', 'CastleSiegeDefenseUpgradeRequest', { NpcNumber: npc.number, NpcIndex: npc.index, NpcUpgradeType: type, NpcUpgradeValue: value });
  }

  /** PlusChaosTaxRate / PlusNormalTaxRate. */
  changeTax(chaos: boolean, by: number): void {
    if (chaos) this.chaosTaxRate = Math.min(MAX_TAX_RATE, Math.max(0, this.chaosTaxRate + by));
    else this.normalTaxRate = Math.min(MAX_TAX_RATE, Math.max(0, this.normalTaxRate + by));
  }

  applyTax(): void {
    this.connection.send('ClientToServer', 'CastleSiegeTaxChangeRequest', { TaxType: 1, TaxValue: this.chaosTaxRate });
    this.connection.send('ClientToServer', 'CastleSiegeTaxChangeRequest', { TaxType: 2, TaxValue: this.normalTaxRate });
  }

  withdraw(amount: number): void { this.connection.send('ClientToServer', 'CastleSiegeTaxMoneyWithdraw', { Amount: amount }); }

  /** npcGateSwitch::SendToggleGate. */
  toggleGate(): void {
    this.connection.send('ClientToServer', 'ToggleCastleGateRequest', { IsOpen: !this.gateSwitch.opened, GateId: this.gateSwitch.key });
    this.gateSwitch.key = 0;
  }

  /** CUIGateKeeper's requests. */
  togglePublic(): void { this.connection.send('ClientToServer', 'CastleSiegeHuntingZoneEntranceSetting', { IsPublic: !this.gatekeeper.public }); }
  sendFee(): void { this.connection.send('ClientToServer', 'CastleSiegeTaxChangeRequest', { TaxType: 3, TaxValue: this.gatekeeper.viewFee }); }
  feeUp(): void { this.gatekeeper.viewFee = Math.min(this.gatekeeper.max, this.gatekeeper.viewFee + this.gatekeeper.unit); }
  feeDown(): void { this.gatekeeper.viewFee = Math.max(0, this.gatekeeper.viewFee - this.gatekeeper.unit); }
  enterHuntingZone(): void { this.connection.send('ClientToServer', 'CastleSiegeHuntingZoneEnterRequest', { Money: this.gatekeeper.viewFee }); }

  fire(): void {
    if (this.catapult.fireLocked || this.catapult.area < 0) return;
    this.connection.send('ClientToServer', 'FireCatapultRequest', { CatapultId: this.catapult.key, TargetAreaIndex: this.catapult.area + 1 });
  }

  command(team: number, x: number, y: number, command: number): void {
    this.connection.send('ClientToServer', 'CastleGuildCommand', { Team: team, PositionX: x, PositionY: y, Command: command });
  }
}

export { format, gold };
